using UnityEngine;

// 주인공의 종이공(관통) / 화학과의 플라스크 / 드론의 에너지탄
public class Projectile : MonoBehaviour
{
    Vector3 vel;
    float life, fxScale = 1f;
    bool hostile; // true = 적이 던진 것 (주인공을 맞힘)
    SpriteAnim anim;
    Sprite[] endFx; // 적 투사체가 터질 때
    string endSfx;
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

    void End()
    {
        if (hostile) SpriteAnim.Fx(endFx, transform.position, fxScale, 14f);
        if (endSfx != null) Sfx.Play(endSfx, 0.7f);
        Destroy(gameObject);
    }
}
