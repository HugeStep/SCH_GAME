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
    const int HitPenalty = 100;
    const float ComboTime = 3f; // 이 시간 안에 다음 적을 잡으면 연속 처치
    const int MaxChain = 4;     // 배율 ×1 → ×1.5 → ×2 → ×2.5 → ×3 (최대)

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
    int killScore, chain;
    float startX, spawnT, chainT;

    // 카메라 연출. camX = 따라가는 기준 위치(흔들림·반동 제외), trauma = 흔들림 세기 0~1(제곱으로 적용),
    // kick = 투척 반동, lead = 달릴 때 앞쪽 여유, zoom/zoomPunch = 확대량, focus = 끝날 때 주인공 쪽으로 다가가는 정도
    const float DeadZone = 0.3f; // 주인공이 가운데에서 이만큼 벗어나야 따라간다 (잔걸음에 화면이 흔들리지 않게)
    float camX, trauma, lead, zoom, zoomPunch, focus, profCueT;
    Vector2 kick;
    bool over;
    Text warnText;

    // 설정 화면의 화면 흔들림 켜기/끄기 (저장됨)
    public static bool ShakeOn { get => PlayerPrefs.GetInt("shake", 1) == 1; set => PlayerPrefs.SetInt("shake", value ? 1 : 0); }

    public float CamLeft => camX - Cam.orthographicSize * Cam.aspect;
    public float CamRight => camX + Cam.orthographicSize * Cam.aspect;
    public int Meters => Mathf.Max(0, Mathf.FloorToInt((Player.transform.position.x - startX) * MetersPerUnit));
    public int Score => CalcScore(Meters, killScore, Hits);

    // 최종 점수 = 이동 거리 × 10 + 처치 점수(연속 처치 배율·고양이 보너스 포함) − 피격 × 100
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
    Text timeText, scoreText, comboText, statText, distText, zoneText, centerText;

    void Awake()
    {
        I = this;
        Time.timeScale = 1f;
    }

    void Start()
    {
        // 모든 판이 랭킹에 등록되도록 로그인 없이는 시작하지 않는다 (에디터에서 Game 씬을 바로 실행한 경우)
        if (!Session.LoggedIn) { enabled = false; SceneManager.LoadScene("Login"); return; }

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
        chainT -= Time.deltaTime;
        Spawn();
        if (Player.Hp <= 0) End(true);
        else if (Elapsed >= Duration) End(false);
    }

    void LateUpdate()
    {
        UpdateCamera(Time.unscaledDeltaTime); // 히트스톱 중에도 흔들림·줌은 움직인다
        UpdateCampus();
        RefreshHud();
    }

    void UpdateCamera(float dt)
    {
        // 오른쪽으로만 따라간다 (지나온 곳으로는 돌아가지 않음).
        // 평소엔 주인공을 가운데에 두고(뒤에서 오는 교수님이 보이게), 오른쪽으로 달릴 땐 앞쪽을 더 보여준다
        lead = Mathf.MoveTowards(lead, Running && Player.Sprinting ? 1.2f : 0f, 1.5f * dt);
        float target = Player.transform.position.x + lead;
        if (target > camX + DeadZone) camX = Mathf.Lerp(camX, target - DeadZone, 8f * dt);

        // 줌: 처치 순간 살짝 당겼다 풀리고, 마지막 10초엔 조금씩 다가가고, 끝나면 주인공 쪽으로 확대
        zoomPunch *= Mathf.Exp(-10f * dt);
        float want = over ? (Player.Hp <= 0 ? 0.8f : 0.4f) : Running ? 0.3f * Mathf.InverseLerp(Duration - 10f, Duration, Elapsed) : 0f;
        zoom = Mathf.Lerp(zoom, want, 4f * dt);
        focus = Mathf.MoveTowards(focus, over ? 1f : 0f, dt / 0.6f);
        Cam.orthographicSize = 5f - zoom - zoomPunch;

        // 교수님이 나타나면 카메라를 잠깐 뒤로 당겨 보여준다
        profCueT = Mathf.Max(0f, profCueT - dt);
        float back = -0.6f * Mathf.Sin(Mathf.PI * profCueT / 1.2f);

        var c = Vector2.Lerp(new Vector2(camX + back, 0f), (Vector2)Player.transform.position + Vector2.up, focus * 0.5f);
        c.x = Mathf.Clamp(c.x, camX - 1.5f, camX + 1.5f); // 배경은 CamLeft/CamRight ±2 까지만 깔려 있다
        float ymax = 5f - Cam.orthographicSize; // 배경(높이 10) 위아래 끝이 보이지 않게
        c.y = Mathf.Clamp(c.y, -ymax, ymax);

        // 흔들림: Perlin 노이즈로 부드럽게, 세기는 trauma² (작은 충격은 아주 약하게)
        trauma = Mathf.Max(0f, trauma - 1.6f * dt);
        kick = Vector2.Lerp(kick, Vector2.zero, 18f * dt);
        if (ShakeOn)
        {
            float s = trauma * trauma, n = Time.unscaledTime * 22f;
            c += kick + new Vector2(0.3f * s * (Mathf.PerlinNoise(n, 0.3f) * 2 - 1), 0.15f * s * (Mathf.PerlinNoise(0.7f, n) * 2 - 1));
        }
        Cam.transform.position = new Vector3(c.x, c.y, -10);
    }

    // 투척 반동: 던진 반대쪽으로 카메라를 살짝 밀었다가 되돌린다
    public void Kick(Vector2 dir) => kick -= dir * 0.1f;

    public void OnProfessor() => profCueT = 1.2f;

    float Multiplier => 1f + chain * 0.5f;

    // at = 점수를 띄울 월드 위치 (적 머리 위)
    public void AddKill(int score, Vector3 at)
    {
        chain = chainT > 0 ? Mathf.Min(chain + 1, MaxChain) : 0;
        chainT = ComboTime;
        int pts = Mathf.RoundToInt(score * Multiplier);
        Kills++;
        killScore += pts;
        StartCoroutine(Popup(chain > 0 ? $"+{pts}  ×{Multiplier:0.#}" : $"+{pts}", at, chain > 0));
        StartCoroutine(HitStop(0.05f));
        zoomPunch = 0.25f;
        trauma = Mathf.Min(1f, trauma + 0.3f);
    }

    // 고양이 보너스: 처치 점수에 더한다 (연속 처치 배율은 붙지 않음)
    public void AddBonus(int pts, Vector3 at)
    {
        killScore += pts;
        StartCoroutine(Popup($"+{pts}  냥!", at, true));
    }

    // 처치·피격 순간 아주 짧게 멈춰서 타격감을 준다
    IEnumerator HitStop(float time)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(time);
        Time.timeScale = 1f;
    }

    // 쓰러질 때: 느려졌다가 원래 속도로 돌아온다 (결과 화면이 뜨는 1.5초 안에 끝남)
    IEnumerator SlowMo()
    {
        for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 1.2f)
        {
            Time.timeScale = Mathf.Lerp(0.25f, 1f, t * t);
            yield return null;
        }
        Time.timeScale = 1f;
    }

    // 처치 점수가 떠오르며 사라진다 (big = 크게 금색으로)
    IEnumerator Popup(string s, Vector3 world, bool big)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(hud, Cam.WorldToScreenPoint(world), null, out var pos);
        var t = UIKit.Label(hud, s, big ? 44 : 36, new Vector2(0.5f, 0.5f), pos, new Vector2(400, 60), color: big ? UIKit.Gold : Color.white);
        var cg = t.gameObject.AddComponent<CanvasGroup>(); // 외곽선까지 같이 흐려지도록
        Destroy(t.gameObject, 0.8f); // 도중에 판이 끝나 코루틴이 멈춰도 지워진다
        for (float k = 0; k < 1 && t; k += Time.deltaTime / 0.8f)
        {
            t.rectTransform.anchoredPosition = pos + Vector2.up * (70f * k);
            cg.alpha = 1 - k * k;
            yield return null;
        }
    }

    public void OnPlayerHit()
    {
        Hits++;
        trauma = Mathf.Min(1f, trauma + 0.7f);
        StartCoroutine(HitStop(0.08f));
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
        // 새 학과가 차례로 합류: 0초 교수님·대학원생 → 10초 미식축구부 → 30초 화학과·기계공학과 → 50초 드론학과 → 70초 컴퓨터공학과
        var type = t < 10 ? EnemyType.Chaser
                 : t < 30 ? (r < 0.7f ? EnemyType.Chaser : EnemyType.Charger)
                 : t < 50 ? (r < 0.45f ? EnemyType.Chaser : r < 0.65f ? EnemyType.Charger : r < 0.85f ? EnemyType.Ranged : EnemyType.Engineer)
                 : t < 70 ? (r < 0.35f ? EnemyType.Chaser : r < 0.53f ? EnemyType.Charger : r < 0.7f ? EnemyType.Ranged : r < 0.87f ? EnemyType.Engineer : EnemyType.Pilot)
                 : r < 0.3f ? EnemyType.Chaser : r < 0.45f ? EnemyType.Charger : r < 0.58f ? EnemyType.Ranged : r < 0.72f ? EnemyType.Engineer : r < 0.85f ? EnemyType.Pilot : EnemyType.Lobber;
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
        comboText = UIKit.Label(hud, "", 34, tr, new Vector2(-40, -192), new Vector2(500, 46), TextAnchor.MiddleRight, UIKit.Gold);

        centerText = UIKit.Label(hud, "", 120, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 160), color: UIKit.Gold);
        // 화면 뒤(왼쪽 밖)에 교수님이 있으면 왼쪽 끝에 깜빡이는 경고
        warnText = UIKit.Label(hud, "!", 110, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 140), color: new Color(1f, 0.3f, 0.25f));
    }

    void RefreshHud()
    {
        if (!hud || !Player) return;
        float left = Mathf.Max(0f, Duration - Elapsed);
        int sec = Mathf.CeilToInt(left);
        timeText.text = $"{sec / 60}:{sec % 60:00}";
        timeText.color = left <= 10f ? new Color(1f, 0.35f, 0.3f) : Color.white;
        scoreText.text = $"SCORE  {Score:N0}";
        comboText.text = chainT > 0 && chain > 0 ? $"{chain + 1} 연속 처치  ×{Multiplier:0.#}" : "";
        statText.text = $"처치  {Kills}\n피격  {Hits}";
        distText.text = $"{Meters:N0} m";

        int hp = Mathf.Clamp(Player.Hp, 0, Player.MaxHp);
        if (hp != shownHp) { shownHp = hp; hpIcon.sprite = Art.Ui("hp_" + Mathf.CeilToInt(hp * 5f / Player.MaxHp)); } // 커피 그림은 0~5단계
        staminaFill.anchorMax = new Vector2(Player.Stamina, 1);
        staminaImg.color = Player.Tired ? new Color(1f, 0.35f, 0.3f) : new Color(0.45f, 0.9f, 0.4f); // 지치면 빨강

        float progress = Mathf.Clamp01((Player.transform.position.x - startX) / lapLength); // START → GOAL (캠퍼스 한 바퀴)
        progressFill.anchorMax = new Vector2(Mathf.Lerp(0.118f, 0.89f, progress), 0.53f);

        var prof = Running ? Enemies.Find(e => e.IsProfessor && e.transform.position.x < CamLeft - 0.3f) : null;
        warnText.enabled = prof && (int)(Time.unscaledTime * 4) % 2 == 0;
        if (prof)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hud, Cam.WorldToScreenPoint(prof.transform.position + Vector3.up), null, out var pos);
            warnText.rectTransform.anchoredPosition = new Vector2(hud.rect.xMin + 50, pos.y);
        }
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
        over = true;
        StopAllCoroutines();
        Time.timeScale = 1f; // 히트스톱 도중에 끝났을 때
        if (died) StartCoroutine(SlowMo());
        if (fade) Destroy(fade.gameObject); // 암전 도중에 끝났을 때
        int score = Score, meters = Meters, kills = Kills, hits = Hits, kill = killScore;
        centerText.text = died ? "GAME OVER" : "TIME UP!";
        Sfx.Play(died ? "gameover" : "whistle");
        var save = Db.SaveResult(score, meters, kills, hits); // 결과 화면이 뜨기 전에 저장을 시작해서 랭킹에 바로 반영

        await Task.Delay(1500);
        if (!this) return;
        centerText.text = "";
        var (bestText, rankText, dim) = BuildResult(score, meters, kills, hits, kill);
        try
        {
            bool best = await save;
            long rank = await Db.MyRank();
            if (!this) return;
            bestText.text = best ? "★ 최고 기록 갱신! ★" : $"내 최고 기록  {Session.Best:N0}점";
            rankText.text = rank > 0 ? $"현재 순위  <color=#FFD54A>#{rank}</color>" : "0점은 랭킹에 오르지 않습니다";
        }
        catch (Exception e)
        {
            Debug.LogWarning(e);
            if (rankText) rankText.text = Db.Friendly(e);
        }
        // 오른쪽: 랭킹 (저장이 끝난 뒤에 불러와서 방금 기록이 반영된다)
        // 두 프레임의 네모 틀 위·아래 끝이 같은 높이에 오도록 크기·위치를 맞춘 값 (패널 그림 비율 기준)
        if (dim) RankingBoard.Build(dim.transform, new Vector2(0.5f, 0.5f), new Vector2(RankX, 47), 888, atMe: true);
    }

    const float OverX = -448, RankX = 425; // 결과 화면 왼쪽(게임오버)·오른쪽(랭킹) 패널 가로 위치

    // 왼쪽: 이번 판 점수
    // 프레임 그림(panel_gameover)에 'GAME OVER'·'최종 점수'·항목 이름이 그려져 있어서 숫자만 얹는다
    (Text best, Text rank, Image dim) BuildResult(int score, int meters, int kills, int hits, int killScore)
    {
        var mid = new Vector2(0.5f, 0.5f);
        var dim = UIKit.Dim(hud, 0.6f);
        dim.raycastTarget = true;
        var frame = Art.Ui("panel_gameover");
        var panel = UIKit.Img(dim.transform, frame, mid, new Vector2(OverX, -27), UIKit.Fit(frame, 860)).transform;

        // (fx, fy) = 그림 좌상단 기준 비율 좌표
        Text At(string s, int size, float fx, float fy, float w, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null)
        {
            var t = UIKit.Label(panel, s, size, new Vector2(fx, 1 - fy), Vector2.zero, new Vector2(w, size * 1.4f), align, color);
            t.rectTransform.pivot = mid;
            return t;
        }

        At(score.ToString("N0"), 90, 0.5f, 0.345f, 800, color: UIKit.Gold);
        (string value, string pts)[] rows =
        {
            ($"{meters:N0} m", $"+{meters * 10:N0}"),
            ($"{kills}", $"+{killScore:N0}"),
            ($"{hits}", hits > 0 ? $"-{hits * HitPenalty:N0}" : "0"),
        };
        float[] rowY = { 0.494f, 0.575f, 0.656f }; // 그림 속 이동 거리 / 적 처치 / 피격 횟수 줄
        for (int i = 0; i < rows.Length; i++)
        {
            At(rows[i].value, 38, 0.55f, rowY[i], 240, TextAnchor.MiddleRight);
            At(rows[i].pts, 30, 0.74f, rowY[i], 200, TextAnchor.MiddleRight, new Color(0.75f, 0.85f, 1f));
        }
        // 항목 상자 아래 ~ 남색 틀 안쪽 끝(0.72 ~ 0.83) 사이에 두 줄
        var best = At("", 26, 0.5f, 0.752f, 800, color: UIKit.Gold);
        var rank = At("순위 확인 중...", 28, 0.5f, 0.802f, 800);

        // 버튼은 각 패널 아래 가운데
        var retry = Art.Ui("btn_retry");
        UIKit.Btn(dim.transform, retry, Art.Ui("btn_retry_pressed"), new Vector2(0.5f, 0), new Vector2(OverX, 30), UIKit.Fit(retry, 470),
            () => SceneManager.LoadScene("Game"));
        var menu = Art.Ui("btn_menu");
        UIKit.Btn(dim.transform, menu, Art.Ui("btn_menu_pressed"), new Vector2(0.5f, 0), new Vector2(RankX, 30), UIKit.Fit(menu, 470),
            () => SceneManager.LoadScene("Main"));
        return (best, rank, dim);
    }
}
