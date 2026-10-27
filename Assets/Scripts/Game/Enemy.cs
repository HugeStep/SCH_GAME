using UnityEngine;

public enum EnemyType { Chaser, Charger, Ranged }

// 적 3종: 추적형(교수님·대학원생), 돌진형(미식축구부), 원거리형(화학과)
public class Enemy : MonoBehaviour
{
    EnemyType type;
    int hp, score, facing = 1;
    int state; // 돌진형: 0 접근 1 준비 2 돌진 3 숨고르기 / 원거리형: 0 이동 1 던지기 / 대학원생: 0 추적 1 내려찍기
    float speed, height = 1.9f, stateT, hurtT, flashT, alpha, attackCd;
    Vector3 dashDir;
    bool dead, thrown, melee;
    SpriteAnim anim;
    Sprite[] idle, walk, run, attack, hit, die;

    public static Enemy Create(EnemyType type, Vector3 pos)
    {
        var go = new GameObject(type.ToString());
        go.transform.position = pos;
        go.AddComponent<SpriteAnim>();
        var e = go.AddComponent<Enemy>();
        e.Init(type);
        GameManager.I.Enemies.Add(e);
        return e;
    }

    void Init(EnemyType t)
    {
        type = t;
        anim = GetComponent<SpriteAnim>();
        string sheet;
        switch (t)
        {
            case EnemyType.Charger: sheet = "football_base"; hp = 3; speed = 1.8f; score = 150; height = 2.2f; break;
            case EnemyType.Ranged: sheet = "chemistry_base"; hp = 2; speed = 2.2f; score = 200; break;
            default: sheet = Random.value < 0.5f ? "professor_base" : "grad_student_base"; hp = 2; speed = 2.4f; score = 100; break;
        }
        melee = sheet == "grad_student_base"; // 몸통 접촉 대신 논문 내려찍기로만 피해
        speed *= 1f + GameManager.I.Elapsed / 240f; // 시간이 갈수록 빨라짐 (끝날 때 1.5배)
        idle = Art.Frames(sheet, "idle");
        walk = Art.Frames(sheet, "walk");
        run = Art.Frames(sheet, "run");
        attack = Art.Frames(sheet, "attack");
        hit = Art.Frames(sheet, "hit") ?? Art.Frames(sheet, "knockback");
        die = Art.Frames(sheet, "knockdown") ?? Art.Frames(sheet, "trip_fall");
        attackCd = Random.Range(0.8f, 1.6f);
        anim.Sr.color = Color.clear;
        anim.Play(run, 12, true);
    }

    // 몸통 사각형에 점이 들어오는지 (종이공 판정)
    public bool Contains(Vector3 p)
    {
        var f = transform.position;
        return !dead && Mathf.Abs(p.x - f.x) < 0.45f && p.y > f.y + 0.1f && p.y < f.y + height;
    }

    public void Damage(int dmg, Vector3 dir)
    {
        if (dead) return;
        hp -= dmg;
        flashT = 0.1f;
        if (melee) state = 0; // 맞으면 내려찍기가 끊긴다
        if (!(type == EnemyType.Charger && state == 2)) // 돌진 중엔 경직 없음
        {
            hurtT = 0.18f;
            transform.position += new Vector3(Mathf.Sign(dir.x) * 0.15f, 0);
        }
        if (hp > 0) return;
        dead = true;
        stateT = 0;
        anim.Play(die, 12, false, true);
        GameManager.I.Enemies.Remove(this);
        GameManager.I.AddKill(score);
    }

    void Update()
    {
        var gm = GameManager.I;
        float dt = Time.deltaTime;

        if (dead) // 쓰러진 뒤 서서히 사라짐
        {
            stateT += dt;
            anim.Sr.color = new Color(1, 1, 1, Mathf.Clamp01(1.6f - stateT));
            if (stateT > 1.6f) Destroy(gameObject);
            return;
        }
        alpha = Mathf.Min(1f, alpha + dt * 3f); // 등장할 때 서서히 나타남
        flashT -= dt;
        anim.Sr.color = flashT > 0 ? new Color(1f, 0.4f, 0.4f, alpha) : new Color(1, 1, 1, alpha);

        if (!gm.Running) { anim.Play(idle, 6, true); return; }
        if (transform.position.x < gm.CamLeft - 6f) { gm.Enemies.Remove(this); Destroy(gameObject); return; } // 한참 뒤처짐

        var to = gm.Player.transform.position - transform.position;
        if (hurtT > 0) { hurtT -= dt; anim.Play(hit, 14, false); }
        else if (type == EnemyType.Charger) Charge(to, dt);
        else if (type == EnemyType.Ranged) Shoot(to, dt);
        else if (melee) Slam(to, dt);
        else Move(to, speed, run);
        Separate(gm);

        var p = transform.position;
        p.y = Mathf.Clamp(p.y, -7f, GameManager.BandMax);
        transform.position = p;
        anim.Sr.flipX = facing < 0;
        anim.Sr.sortingOrder = GameManager.Order(p.y);

        // 같은 깊이에서 몸이 겹치면 주인공 피격
        to = gm.Player.transform.position - p;
        if (!melee && Mathf.Abs(to.x) < 0.7f && Mathf.Abs(to.y) < 0.35f) gm.Player.Damage(p);
    }

    // 대학원생: 붙으면 논문 더미를 머리 위로 들었다가(예고) 앞으로 내려찍는다. 내려찍는 프레임에 앞에 있으면 피격
    void Slam(Vector3 to, float dt)
    {
        if (state == 1)
        {
            anim.Play(attack, 12, false);
            float ahead = to.x * facing;
            if (!thrown && anim.Frame >= 4 && anim.Frame <= 5 && ahead > -0.2f && ahead < 1.4f && Mathf.Abs(to.y) < 0.4f)
            {
                thrown = true;
                GameManager.I.Player.Damage(transform.position);
            }
            if (anim.Done) { state = 0; attackCd = Random.Range(0.8f, 1.3f); }
            return;
        }

        attackCd -= dt;
        if (Mathf.Abs(to.x) > 1.1f || Mathf.Abs(to.y) > 0.3f) { Move(to, speed, run); return; }
        Face(to);
        if (attackCd > 0) { anim.Play(idle, 6, true); return; }
        state = 1;
        thrown = false;
        anim.Play(attack, 12, false, true);
    }

    void Move(Vector3 dir, float spd, Sprite[] frames)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.position += dir.normalized * (spd * Time.deltaTime);
        if (Mathf.Abs(dir.x) > 0.05f) facing = dir.x > 0 ? 1 : -1;
        anim.Play(frames, 12, true);
    }

    void Face(Vector3 to) => facing = to.x >= 0 ? 1 : -1;

    // 돌진형: 다가가다 → 멈춰서 빨갛게 깜빡(예고) → 직선으로 빠르게 돌진 → 숨 고르기
    void Charge(Vector3 to, float dt)
    {
        stateT += dt;
        switch (state)
        {
            case 0:
                Move(to, speed, walk);
                if (stateT > 1.2f && to.magnitude < 7f) { state = 1; stateT = 0; }
                break;
            case 1:
                Face(to);
                anim.Play(idle, 8, true);
                if ((int)(stateT * 12) % 2 == 0) flashT = 0.04f;
                if (stateT > 0.55f)
                {
                    state = 2;
                    stateT = 0;
                    dashDir = to.normalized;
                    SpriteAnim.Fx(Art.Frames("fx_common", "dust_puff"), transform.position + Vector3.up * 0.3f, 0.8f, 18f, dashDir.x > 0);
                }
                break;
            case 2:
                Move(dashDir, 10f, attack);
                if (stateT > 0.6f) { state = 3; stateT = 0; }
                break;
            default:
                anim.Play(idle, 6, true);
                if (stateT > 0.7f) { state = 0; stateT = 0; }
                break;
        }
    }

    // 원거리형: 거리를 4.5~7 정도로 유지하며 주인공을 향해 플라스크를 던진다
    void Shoot(Vector3 to, float dt)
    {
        var gm = GameManager.I;
        if (state == 1)
        {
            Face(to);
            anim.Play(attack, 10, false);
            if (!thrown && anim.Frame >= 3)
            {
                thrown = true;
                var hand = transform.position + new Vector3(0.4f * facing, 1.4f);
                var target = gm.Player.transform.position + Vector3.up;
                Projectile.Flask(hand, (target - hand).normalized);
            }
            if (anim.Done) { state = 0; attackCd = Random.Range(2f, 2.8f); }
            return;
        }

        float dist = to.magnitude;
        var move = new Vector3(dist > 7f ? to.x : dist < 4.5f ? -to.x : 0, to.y * 0.8f);
        if (move.sqrMagnitude > 0.04f) Move(move, speed, walk);
        else { Face(to); anim.Play(idle, 6, true); }

        attackCd -= dt;
        float x = transform.position.x;
        if (attackCd <= 0 && dist < 10f && x > gm.CamLeft + 0.5f && x < gm.CamRight - 0.5f)
        {
            state = 1;
            thrown = false;
            Face(to);
            anim.Play(attack, 10, false, true);
        }
    }

    // 적끼리 너무 겹치지 않게 살짝 밀어낸다 (적 수가 수십 명이라 O(n²)로 충분)
    void Separate(GameManager gm)
    {
        foreach (var o in gm.Enemies)
        {
            if (o == this) continue;
            var d = transform.position - o.transform.position;
            d.y *= 2f;
            float m = d.sqrMagnitude;
            if (m > 0.0001f && m < 0.36f) transform.position += d.normalized * ((0.6f - Mathf.Sqrt(m)) * 0.5f);
        }
    }
}
