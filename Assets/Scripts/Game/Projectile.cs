using UnityEngine;

// 주인공의 종이공(관통) / 화학과의 플라스크 / 드론의 에너지탄 / 컴퓨터공학과의 맥북·키보드(포물선)
public class Projectile : MonoBehaviour
{
    const float LobTime = 1.1f, LobPeak = 2.2f, HandH = 1.4f; // 포물선: 날아가는 시간, 가운데에서 더 솟는 높이, 손 높이

    Vector3 vel, from; // 포물선이면 vel·from 은 바닥 위(그림자) 기준
    float life, fxScale = 1f;
    bool hostile; // true = 적이 던진 것 (주인공을 맞힘)
    SpriteAnim anim;
    Sprite[] endFx; // 적 투사체가 터질 때
    string endSfx;
    SpriteRenderer shadow; // 포물선일 때만
    static Sprite shadowSprite;
    readonly System.Collections.Generic.HashSet<Enemy> pierced = new(); // 이미 맞힌 적

    public static void Ball(Vector3 pos, Vector3 dir)
    {
        var p = Make("PaperBall", pos, dir * 15f, 0.85f, false);
        p.anim.Sr.sprite = Art.Ui("paper_ball");
        p.transform.localScale = Vector3.one * 0.75f;
    }

    public static void Flask(Vector3 pos, Vector3 dir)
    {
        var p = Make("Flask", pos, dir * 6.5f, 3f, true);
        p.anim.Play(Art.Frames("flask", "projectile"), 14, true);
        p.anim.Sr.flipX = dir.x < 0;
        p.endFx = Art.Frames("flask", "explode");
        p.endSfx = "glass";
    }

    public static void Energy(Vector3 pos, Vector3 dir)
    {
        var p = Make("Energy", pos, dir * 7.5f, 3f, true);
        p.anim.Sr.sprite = Art.Ui("energy_ball");
        p.anim.Sr.flipX = dir.x < 0; // 꼬리가 왼쪽에 그려져 있음
        p.transform.localScale = Vector3.one * 0.7f;
        p.endFx = Art.Frames("fx_common", "electric_shock");
        p.fxScale = 0.6f;
    }

    // 맥북·키보드: 던진 발밑(from)에서 목표 발밑(to)까지 바닥 위치는 곧게, 높이는 손에서 솟았다가 떨어진다.
    // 떨어질 자리에 그림자가 커지며 미리 보이고, 바닥에 부딪혀 부서질 때 그 자리에 있으면 피격
    public static void Lob(Vector3 from, Vector3 to, bool macbook)
    {
        string sheet = macbook ? "macbook" : "keyboard";
        var p = Make(sheet, from, (to - from) / LobTime, LobTime, true);
        p.from = from;
        p.anim.Play(Art.Frames(sheet, "fly"), 8, true);
        p.anim.Sr.flipX = to.x < from.x;
        p.endFx = Art.Frames(sheet, "break");
        p.endSfx = macbook ? "glass" : "slam";
        p.shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
        p.shadow.transform.SetParent(p.transform);
        p.shadow.sprite = Shadow();
        p.shadow.sortingOrder = -1000; // 배경 바로 위, 캐릭터들 아래
        p.Arc(0f);
    }

    // 바닥 그림자: 가운데가 진한 타원 (폭 1유닛)
    static Sprite Shadow()
    {
        if (shadowSprite) return shadowSprite;
        var t = new Texture2D(64, 32);
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 64; x++)
        {
            float dx = (x - 31.5f) / 32f, dy = (y - 15.5f) / 16f;
            t.SetPixel(x, y, new Color(0, 0, 0, 0.6f * Mathf.Clamp01((1 - dx * dx - dy * dy) * 3f)));
        }
        t.Apply();
        return shadowSprite = Sprite.Create(t, new Rect(0, 0, 64, 32), new Vector2(0.5f, 0.5f), 64);
    }

    static Projectile Make(string name, Vector3 pos, Vector3 vel, float life, bool hostile)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var p = go.AddComponent<Projectile>();
        p.anim = go.AddComponent<SpriteAnim>();
        p.anim.Sr.sortingOrder = 5000;
        p.vel = vel;
        p.life = life;
        p.hostile = hostile;
        return p;
    }

    void Update()
    {
        var gm = GameManager.I;
        if (!gm.Running) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        if (shadow) { Arc(dt); return; }
        transform.position += vel * dt;
        var pos = transform.position;
        if (!hostile) transform.Rotate(0, 0, -720 * dt * Mathf.Sign(vel.x));

        life -= dt;
        if (life <= 0) { End(); return; }

        if (hostile)
        {
            if (gm.Player.Contains(pos)) { gm.Player.Damage(pos); End(); }
            return;
        }
        // 종이공은 관통한다: 지나가는 길의 적을 한 번씩 모두 맞힌다
        foreach (var e in gm.Enemies.ToArray()) // 맞아 죽으면 목록에서 빠지므로 복사본으로 돈다
        {
            if (!e.Contains(pos) || !pierced.Add(e)) continue;
            e.Damage(1, vel);
            SpriteAnim.Fx(Art.Frames("fx_common", "hit_spark"), pos, 0.7f, 24f);
        }
    }

    // 포물선 비행: 한 바퀴 돌아 바로 선 채로 그림자 위에 떨어져 부서진다
    void Arc(float dt)
    {
        life -= dt;
        float t = LobTime - Mathf.Max(life, 0f), s = t / LobTime;
        var g = from + vel * t;
        transform.SetPositionAndRotation(g + Vector3.up * (HandH * (1 - s) + 4f * LobPeak * s * (1 - s)), Quaternion.Euler(0, 0, -360f * s * Mathf.Sign(vel.x)));
        shadow.transform.SetPositionAndRotation(g, Quaternion.identity);
        shadow.transform.localScale = Vector3.one * (0.4f + 0.6f * s);
        anim.Sr.sortingOrder = GameManager.Order(g.y);
        if (life > 0) return;

        var gm = GameManager.I;
        var d = gm.Player.transform.position - g;
        if (Mathf.Abs(d.x) < 0.8f && Mathf.Abs(d.y) < 0.4f) gm.Player.Damage(g);
        SpriteAnim.Fx(endFx, g, 1.4f, 10f);
        Sfx.Play(endSfx, 0.7f);
        Destroy(gameObject);
    }

    void End()
    {
        if (hostile) SpriteAnim.Fx(endFx, transform.position, fxScale, 14f);
        if (endSfx != null) Sfx.Play(endSfx, 0.7f);
        Destroy(gameObject);
    }
}
