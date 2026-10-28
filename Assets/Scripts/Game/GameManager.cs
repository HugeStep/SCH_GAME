using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

// 한 판(2분)의 진행: 카메라 · 캠퍼스 배경 · 적 생성 · 점수 · HUD · 결과 화면
public class GameManager : MonoBehaviour
{
    public const float Duration = 120f;
    public const float BandMin = -4.6f, BandMax = -0.6f; // 걸어다닐 수 있는 바닥 높이 범위 (발 위치 y)
    const float MetersPerUnit = 5f;
    const int HitPenalty = 200;

    // 배경 bg_1 ~ bg_8 순서의 구역 이름 (ArtBaker 의 배경 순서와 같음)
    static readonly string[] Zones = { "앙뜨레프레너관", "유니토피아", "유니토피아 광장", "유니토 앞", "공학관", "향설1관 · 도서관", "도서관 옆길", "대학본관" };

    public static GameManager I { get; private set; }
    public Camera Cam { get; private set; }
    public Player Player { get; private set; }
    public readonly List<Enemy> Enemies = new();
    public bool Running { get; private set; }
    public float Elapsed { get; private set; }
    public int Kills { get; private set; }
    public int Hits { get; private set; }
    int killScore;
    float startX, spawnT, shakeT;

    public float CamLeft => Cam.transform.position.x - Cam.orthographicSize * Cam.aspect;
    public float CamRight => Cam.transform.position.x + Cam.orthographicSize * Cam.aspect;
    public int Meters => Mathf.Max(0, Mathf.FloorToInt((Player.transform.position.x - startX) * MetersPerUnit));
    public int Score => CalcScore(Meters, killScore, Hits);

    // 최종 점수 = 이동 거리 × 10 + 처치 점수 − 피격 × 200
    public static int CalcScore(int meters, int killScore, int hits) => Mathf.Max(0, meters * 10 + killScore - hits * HitPenalty);
    // 화면 아래쪽(앞)에 있을수록 앞에 그린다
    public static int Order(float y) => Mathf.RoundToInt(-y * 100);

    readonly List<(GameObject go, float x0, float x1, int zone)> segs = new();
    float nextX, lapLength, warpX = float.MaxValue; // warpX: 5→6 구역 경계
    int nextSeg, zone = -1, shownHp = -1;
    Image fade;

    RectTransform hud;
    Image hpIcon;
    RectTransform progressFill, staminaFill;
    Image staminaImg;
    Text timeText, scoreText, statText, distText, zoneText, centerText;

    void Awake()
    {
        I = this;
        Time.timeScale = 1f;
    }

    void Start()
    {
        Cam = Camera.main;
        Cam.orthographic = true;
        Cam.orthographicSize = 5;
        Cam.transform.position = new Vector3(0, 0, -10);

        // 배경음악 (AudioListener는 Sfx가 만든다)
        var bgm = gameObject.AddComponent<AudioSource>();
        bgm.clip = Resources.Load<AudioClip>("Audio/bgm_game");
        bgm.loop = true;
        bgm.volume = 0.5f;
        bgm.mute = !Sfx.BgmOn;
        bgm.Play();

        for (int i = 1; i <= Zones.Length; i++) lapLength += Width(Art.Bg(i));
        nextX = CamLeft - 1f;
        UpdateCampus();

        Player = Player.Create(new Vector3(0, -2.6f, 0));
        startX = Player.transform.position.x;
        BuildHud();
        Running = true;
        StartCoroutine(Toast("START!", 1.2f));
    }

    void Update()
    {
        if (!Running) return;
        Elapsed += Time.deltaTime;
        Spawn();
        if (Player.Hp <= 0) End(true);
        else if (Elapsed >= Duration) End(false);
    }

    void LateUpdate()
    {
        // 카메라는 오른쪽으로만 따라간다 (지나온 곳으로는 돌아가지 않음)
        var p = Cam.transform.position;
        float target = Player.transform.position.x; // 주인공을 화면 가운데에
        if (target > p.x) p.x = Mathf.Lerp(p.x, target, 8f * Time.deltaTime);
        shakeT -= Time.deltaTime;
        p.y = shakeT > 0 ? Random.Range(-0.08f, 0.08f) : 0f;
        Cam.transform.position = p;
        UpdateCampus();
        RefreshHud();
    }

    public void AddKill(int score)
    {
        Kills++;
        killScore += score;
    }

    public void OnPlayerHit()
    {
        Hits++;
        shakeT = 0.25f;
    }

    // ---------- 적 생성 ----------

    void Spawn()
    {
        float t = Elapsed;
        spawnT -= Time.deltaTime;
        if (spawnT > 0 || Enemies.Count >= 10 + (int)(t / 5)) return;

        // 생성 간격: 0~10초 1.3초 → 40초 0.8초 → 90초 0.5초 → 마지막 30초 0.35초
        spawnT = t < 10 ? 1.3f : t < 40 ? Mathf.Lerp(1.1f, 0.8f, (t - 10) / 30) : t < 90 ? Mathf.Lerp(0.8f, 0.5f, (t - 40) / 50) : 0.35f;
        float r = Random.value;
        // 새 학과가 차례로 합류: 0초 교수님·대학원생 → 10초 미식축구부 → 30초 화학과·기계공학과 → 50초 드론학과
        var type = t < 10 ? EnemyType.Chaser
                 : t < 30 ? (r < 0.7f ? EnemyType.Chaser : EnemyType.Charger)
                 : t < 50 ? (r < 0.45f ? EnemyType.Chaser : r < 0.65f ? EnemyType.Charger : r < 0.85f ? EnemyType.Ranged : EnemyType.Engineer)
                 : r < 0.35f ? EnemyType.Chaser : r < 0.53f ? EnemyType.Charger : r < 0.7f ? EnemyType.Ranged : r < 0.87f ? EnemyType.Engineer : EnemyType.Pilot;
        if (type == EnemyType.Pilot && Enemies.Exists(e => e.Type == EnemyType.Pilot)) type = EnemyType.Chaser; // 드론은 한 번에 한 대만
        // 조종사는 앉아서 움직이지 않으니 화면 오른쪽 안에 자리 잡는다
        Enemy.Create(type, type == EnemyType.Pilot ? new Vector3(CamRight - 1.2f, Random.Range(BandMin, BandMax)) : SpawnPoint());
    }

    // 화면 밖 사방(오른쪽·왼쪽·뒤쪽 건물 앞·화면 아래)에서 나타난다. 주인공 바로 옆은 피한다.
    Vector3 SpawnPoint()
    {
        var pl = Player.transform.position;
        for (int i = 0; ; i++)
        {
            float r = Random.value, y = Random.Range(BandMin, BandMax);
            var p = r < 0.45f ? new Vector3(CamRight + 1f, y)
                  : r < 0.65f ? new Vector3(CamLeft - 1f, y)
                  : new Vector3(Random.Range(CamLeft + 1f, CamRight - 1f), r < 0.85f ? BandMax : -6.8f);
            if (i >= 5 || (p - pl).sqrMagnitude > 16f) return p;
        }
    }

    // ---------- 캠퍼스 배경 (오른쪽으로 계속 이어 붙임) ----------

    static float Width(Sprite s) => s.bounds.size.x * 10f / s.bounds.size.y; // 높이 10(화면 높이)에 맞춘 폭

    void UpdateCampus()
    {
        while (nextX < CamRight + 2f)
        {
            int z = nextSeg % Zones.Length;
            var s = Art.Bg(z + 1);
            var go = new GameObject("BG " + Zones[z]);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = -32000;
            float w = Width(s);
            go.transform.localScale = Vector3.one * (10f / s.bounds.size.y);
            go.transform.position = new Vector3(nextX + w / 2, 0, 0);
            segs.Add((go, nextX, nextX + w, z));
            if (z == 5) warpX = nextX;
            nextX += w - 0.02f; // 이음매 틈 방지
            nextSeg++;
        }
        for (int i = segs.Count - 1; i >= 0; i--)
            if (segs[i].x1 < CamLeft - 2f) { Destroy(segs[i].go); segs.RemoveAt(i); }
        if (Running && CamRight + 1.5f > warpX) { StartCoroutine(Warp(warpX)); warpX = float.MaxValue; }

        if (!Player) return;
        float px = Player.transform.position.x;
        foreach (var s in segs)
            if (px >= s.x0 && px < s.x1 && s.zone != zone)
            {
                zone = s.zone;
                if (zoneText) zoneText.text = Zones[zone];
            }
    }

    // 5→6 구역은 이어지지 않고 다른 맵으로 넘어가듯 빠르게 암전했다가 밝아진다.
    // 어두운 동안 배경만 당겨서 새 구역이 화면 왼쪽 끝부터 시작되게 한다 (주인공·적은 그대로).
    IEnumerator Warp(float seam)
    {
        fade = UIKit.Dim(hud, 0f);
        fade.raycastTarget = false;
        for (float t = 0; t < 1; t += Time.deltaTime / 0.12f) { fade.color = new Color(0, 0, 0, t); yield return null; }
        fade.color = Color.black;

        float dx = seam - CamLeft;
        for (int i = segs.Count - 1; i >= 0; i--)
        {
            var s = segs[i];
            if (s.x0 < seam - 0.01f) { Destroy(s.go); segs.RemoveAt(i); continue; }
            s.go.transform.position += Vector3.left * dx;
            segs[i] = (s.go, s.x0 - dx, s.x1 - dx, s.zone);
        }
        nextX -= dx;
        yield return new WaitForSeconds(0.06f);

        for (float t = 1; t > 0; t -= Time.deltaTime / 0.22f) { fade.color = new Color(0, 0, 0, t); yield return null; }
        Destroy(fade.gameObject);
    }

    // ---------- HUD ----------

    void BuildHud()
    {
        hud = UIKit.Canvas();
        Vector2 tl = new(0, 1), tr = new(1, 1), top = new(0.5f, 1);

        var cup = Art.Ui("hp_5");
        hpIcon = UIKit.Img(hud, cup, tl, new Vector2(30, -16), UIKit.Fit(cup, 92));
        statText = UIKit.Label(hud, "", 30, tl, new Vector2(140, -40), new Vector2(240, 100), TextAnchor.UpperLeft);
        // 스태미나 바 (Shift 달리기)
        UIKit.Label(hud, "STAMINA", 22, tl, new Vector2(30, -150), new Vector2(160, 28), TextAnchor.MiddleLeft, UIKit.Gold);
        var track = UIKit.Img(hud, null, tl, new Vector2(30, -180), new Vector2(260, 16), new Color(0, 0, 0, 0.55f));
        staminaImg = UIKit.Img(track.transform, null, Vector2.zero, Vector2.zero, Vector2.zero);
        staminaFill = staminaImg.rectTransform;
        staminaFill.anchorMin = Vector2.zero;
        staminaFill.offsetMin = staminaFill.offsetMax = Vector2.zero;

        var barSprite = Art.Ui("progress_bar");
        var bar = UIKit.Img(hud, barSprite, top, new Vector2(0, -6), UIKit.Fit(barSprite, 900));
        progressFill = UIKit.Img(bar.transform, null, Vector2.zero, Vector2.zero, Vector2.zero, new Color(1f, 0.82f, 0.12f)).rectTransform;
        progressFill.anchorMin = new Vector2(0.118f, 0.33f); // 바 그림 안쪽 트랙 영역
        progressFill.anchorMax = new Vector2(0.118f, 0.53f);
        progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
        distText = UIKit.Label(hud, "0 m", 38, top, new Vector2(-140, -100), new Vector2(240, 50), TextAnchor.MiddleRight);
        zoneText = UIKit.Label(hud, zone >= 0 ? Zones[zone] : "", 30, top, new Vector2(200, -100), new Vector2(360, 50), TextAnchor.MiddleLeft, UIKit.Gold);

        UIKit.Label(hud, "TIME", 28, tr, new Vector2(-40, -18), new Vector2(240, 36), TextAnchor.MiddleRight, UIKit.Gold);
        timeText = UIKit.Label(hud, "2:00", 72, tr, new Vector2(-40, -50), new Vector2(260, 84), TextAnchor.MiddleRight);
        scoreText = UIKit.Label(hud, "", 40, tr, new Vector2(-40, -138), new Vector2(500, 52), TextAnchor.MiddleRight);

        centerText = UIKit.Label(hud, "", 120, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 160), color: UIKit.Gold);
    }

    void RefreshHud()
    {
        if (!hud || !Player) return;
        float left = Mathf.Max(0f, Duration - Elapsed);
        int sec = Mathf.CeilToInt(left);
        timeText.text = $"{sec / 60}:{sec % 60:00}";
        timeText.color = left <= 10f ? new Color(1f, 0.35f, 0.3f) : Color.white;
        scoreText.text = $"SCORE  {Score:N0}";
        statText.text = $"처치  {Kills}\n피격  {Hits}";
        distText.text = $"{Meters:N0} m";

        int hp = Mathf.Clamp(Player.Hp, 0, Player.MaxHp);
        if (hp != shownHp) { shownHp = hp; hpIcon.sprite = Art.Ui("hp_" + hp); }
        staminaFill.anchorMax = new Vector2(Player.Stamina, 1);
        staminaImg.color = Player.Tired ? new Color(1f, 0.35f, 0.3f) : new Color(0.45f, 0.9f, 0.4f); // 지치면 빨강

        float progress = Mathf.Clamp01((Player.transform.position.x - startX) / lapLength); // START → GOAL (캠퍼스 한 바퀴)
        progressFill.anchorMax = new Vector2(Mathf.Lerp(0.118f, 0.89f, progress), 0.53f);
    }

    IEnumerator Toast(string s, float time)
    {
        centerText.text = s;
        yield return new WaitForSeconds(time);
        if (Running) centerText.text = "";
    }

    // ---------- 종료 · 결과 ----------

    async void End(bool died)
    {
        Running = false;
        StopAllCoroutines();
        if (fade) Destroy(fade.gameObject); // 암전 도중에 끝났을 때
        int score = Score, meters = Meters, kills = Kills, hits = Hits, kill = killScore;
        centerText.text = died ? "GAME OVER" : "TIME UP!";
        Sfx.Play(died ? "gameover" : "whistle");
        var save = Session.LoggedIn ? Db.SaveResult(score, meters, kills, hits) : null;

        await Task.Delay(1500);
        if (!this) return;
        centerText.text = "";
        var (bestText, rankText, dim) = BuildResult(died, score, meters, kills, hits, kill);
        if (save == null) rankText.text = "게스트 플레이 — 기록이 저장되지 않습니다";
        else try
        {
            bool best = await save;
            long rank = await Db.MyRank();
            if (!this) return;
            bestText.text = best ? "★ 최고 기록 갱신! ★" : $"내 최고 기록  {Session.Best:N0}점";
            rankText.text = $"현재 순위  <color=#FFD54A>#{rank}</color>";
        }
        catch (Exception e)
        {
            Debug.LogWarning(e);
            if (rankText) rankText.text = Db.Friendly(e);
        }
        // 오른쪽: 랭킹 (저장이 끝난 뒤에 불러와서 방금 기록이 반영된다)
        if (dim) RankingBoard.Build(dim.transform, new Vector2(0.5f, 0.5f), new Vector2(490, 70), 920);
    }

    // 왼쪽: 이번 판 점수
    (Text best, Text rank, Image dim) BuildResult(bool died, int score, int meters, int kills, int hits, int killScore)
    {
        var mid = new Vector2(0.5f, 0.5f);
        var dim = UIKit.Dim(hud, 0.6f);
        dim.raycastTarget = true;
        var panel = UIKit.Panel(dim.transform, mid, new Vector2(-470, 70), new Vector2(860, 760)).transform;

        UIKit.Label(panel, died ? "GAME OVER" : "TIME UP!", 72, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(860, 90), color: UIKit.Gold);
        UIKit.Label(panel, "최종 점수", 34, new Vector2(0.5f, 1), new Vector2(0, -122), new Vector2(860, 44));
        UIKit.Label(panel, score.ToString("N0"), 100, new Vector2(0.5f, 1), new Vector2(0, -166), new Vector2(860, 120), color: UIKit.Gold);

        (string label, string value, string pts)[] rows =
        {
            ("이동 거리", $"{meters:N0} m", $"+{meters * 10:N0}"),
            ("적 처치", $"{kills}", $"+{killScore:N0}"),
            ("피격 횟수", $"{hits}", hits > 0 ? $"-{hits * HitPenalty:N0}" : "0"),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            float y = -310 - i * 62;
            UIKit.Label(panel, rows[i].label, 36, new Vector2(0, 1), new Vector2(110, y), new Vector2(260, 56), TextAnchor.MiddleLeft);
            UIKit.Label(panel, rows[i].value, 40, new Vector2(0, 1), new Vector2(380, y), new Vector2(200, 56), TextAnchor.MiddleRight);
            UIKit.Label(panel, rows[i].pts, 30, new Vector2(1, 1), new Vector2(-110, y), new Vector2(200, 56), TextAnchor.MiddleRight, new Color(0.75f, 0.85f, 1f));
        }
        var best = UIKit.Label(panel, "", 36, new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(860, 50), color: UIKit.Gold);
        var rank = UIKit.Label(panel, "순위 확인 중...", 44, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(860, 60));

        var retry = Art.Ui("btn_retry");
        UIKit.Btn(dim.transform, retry, Art.Ui("btn_retry_pressed"), new Vector2(0.5f, 0), new Vector2(-270, 30), UIKit.Fit(retry, 470),
            () => SceneManager.LoadScene("Game"));
        var menu = Art.Ui("btn_menu");
        UIKit.Btn(dim.transform, menu, Art.Ui("btn_menu_pressed"), new Vector2(0.5f, 0), new Vector2(270, 30), UIKit.Fit(menu, 470),
            () => SceneManager.LoadScene("Main"));
        return (best, rank, dim);
    }
}
