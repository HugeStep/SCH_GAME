using UnityEngine;

public enum EnemyType { Chaser, Charger, Ranged, Engineer, Pilot, Drone }

// 적: 추적형(교수님·대학원생), 돌진형(미식축구부), 원거리형(화학과), 로봇팔(기계공학과), 드론 조종사(드론학과)와 드론
public class Enemy : MonoBehaviour
{
    EnemyType type;
    int hp, score, facing = 1;
    int state; // 돌진형: 0 접근 1 준비 2 돌진 3 숨고르기 / 원거리형·드론: 0 이동 1 쏘기 / 대학원생: 0 접근 1 공격 2 내려찍은 뒤 / 기계과: 0 접근 1 공격 / 조종사: 0 첫 드론 전 1 이후
    float speed, height = 1.9f, stateT, hurtT, flashT, alpha, attackCd;
    Vector3 dashDir;
    bool dead, thrown, melee, touch, professor;
    Enemy partner; // 조종사 ↔ 드론
    SpriteAnim anim;
    Sprite[] idle, walk, run, attack, hit, die;

    public EnemyType Type => type;

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
            case EnemyType.Engineer: sheet = "mech_eng_base"; hp = 3; speed = 1.9f; score = 180; break;
            case EnemyType.Pilot: sheet = "drone_pilot"; hp = 3; speed = 0f; score = 300; height = 1.5f; break;
            case EnemyType.Drone: sheet = "drone_unit"; hp = 1; speed = 3.2f; score = 120; break;
            default:
                // 교수님은 한 번에 한 명만 (이미 있으면 대학원생)
                professor = Random.value < 0.5f && !GameManager.I.Enemies.Exists(e => e.professor && !e.dead);
                sheet = professor ? "professor_base" : "grad_student_base"; hp = 2; score = 100;
                speed = professor ? Player.WalkSpeed + 0.3f : 2.4f; // 교수님은 주인공이 걸을 때보다 조금 빠르고 뛸 때보다 느리다
                break;
        }
        if (professor) // 교수님은 늘 뒤(화면 왼쪽 밖)에서 쫓아온다
            transform.position = new Vector3(GameManager.I.CamLeft - 1f, Random.Range(GameManager.BandMin, GameManager.BandMax));
        melee = sheet == "grad_student_base"; // 몸통 접촉 대신 논문 내려찍기로만 피해
        touch = !melee && t <= EnemyType.Ranged; // 몸이 닿으면 피해 (기계과·조종사·드론은 공격으로만)
        speed *= 1f + GameManager.I.Elapsed / 240f; // 시간이 갈수록 빨라짐 (끝날 때 1.5배)
        // 드론·조종사 시트는 동작 이름이 달라서 비슷한 것으로 대신한다
        idle = Art.Frames(sheet, "idle") ?? Art.Frames(sheet, "hover") ?? Art.Frames(sheet, "operate_1");
        walk = Art.Frames(sheet, "walk") ?? Art.Frames(sheet, "move") ?? idle;
        run = Art.Frames(sheet, "run") ?? walk;
        attack = Art.Frames(sheet, "attack") ?? Art.Frames(sheet, "shoot");
        hit = Art.Frames(sheet, "hit") ?? Art.Frames(sheet, "knockback");
        die = Art.Frames(sheet, "knockdown") ?? Art.Frames(sheet, "trip_fall") ?? Art.Frames(sheet, "hit_spin");
        attackCd = Random.Range(0.8f, 1.6f);
        anim.Sr.color = Color.clear;
        anim.Play(run, 12, true);
    }

    // 몸통 사각형에 점이 들어오는지 (종이공 판정)
    public bool Contains(Vector3 p)
    {
        var f = transform.position;
        if (type == EnemyType.Drone) return !dead && Mathf.Abs(p.x - f.x) < 0.5f && Mathf.Abs(p.y - f.y) < 0.35f; // 드론은 가운데 기준
        return !dead && Mathf.Abs(p.x - f.x) < 0.45f && p.y > f.y + 0.1f && p.y < f.y + height;
    }

    public void Damage(int dmg, Vector3 dir)
    {
        if (dead) return;
        hp -= dmg;
        flashT = 0.1f;
        Sfx.Play("hit", 0.6f);
        if (melee) state = 0; // 맞으면 내려찍기가 끊긴다
        // 돌진 중·로봇팔 휘두르는 중·의자에 앉은 조종사는 경직 없음
        bool steady = (type == EnemyType.Charger && state == 2) || (type == EnemyType.Engineer && state == 1) || type == EnemyType.Pilot;
        if (!steady)
        {
            hurtT = 0.18f;
            transform.position += new Vector3(Mathf.Sign(dir.x) * 0.15f, 0);
        }
        if (hp > 0) return;
        Sfx.Play("kill");
        Die();
        GameManager.I.AddKill(score);
        if (type == EnemyType.Pilot && partner) partner.Die(); // 조종사가 쓰러지면 드론도 추락 (점수는 조종사 몫만)
    }

    void Die()
    {
        if (dead) return;
        dead = true;
        stateT = 0;
        anim.Play(die, 12, false, true);
        GameManager.I.Enemies.Remove(this);
    }

    void Update()
    {
        var gm = GameManager.I;
        float dt = Time.deltaTime;

        if (dead) // 쓰러진 뒤 서서히 사라짐
        {
            stateT += dt;
            anim.Sr.color = new Color(1, 1, 1, Mathf.Clamp01(1.6f - stateT));
            if (type == EnemyType.Drone) transform.position += Vector3.down * (3f * dt); // 빙글 돌며 추락
            if (type == EnemyType.Pilot) transform.rotation = Quaternion.Euler(0, 0, facing * Mathf.Min(80f, stateT * 300f)); // 의자째 뒤로 넘어감
            if (stateT > 1.6f) Destroy(gameObject);
            return;
        }
        alpha = Mathf.Min(1f, alpha + dt * 3f); // 등장할 때 서서히 나타남
        flashT -= dt;
        anim.Sr.color = flashT > 0 ? new Color(1f, 0.4f, 0.4f, alpha) : new Color(1, 1, 1, alpha);

        if (!gm.Running) { anim.Play(idle, 6, true); return; }
        // 교수님은 떨어져도 사라지지 않고 화면 바로 뒤에 붙어 걸어온다 → 주인공이 걸으면 왼쪽 끝에 살짝씩 보인다
        if (professor && transform.position.x < gm.CamLeft - 1.5f) transform.position = new Vector3(gm.CamLeft - 1.5f, transform.position.y);
        if (transform.position.x < gm.CamLeft - 6f) { gm.Enemies.Remove(this); Destroy(gameObject); return; } // 한참 뒤처짐

        var to = gm.Player.transform.position - transform.position;
        if (hurtT > 0) { hurtT -= dt; anim.Play(hit, 14, false); }
        else if (type == EnemyType.Charger) Charge(to, dt);
        else if (type == EnemyType.Ranged) Shoot(to, dt);
        else if (type == EnemyType.Engineer) Arm(to, dt);
        else if (type == EnemyType.Pilot) Pilot(to, dt);
        else if (type == EnemyType.Drone) Fly(to, dt);
        else if (melee) Slam(to, dt);
        else Move(to, speed, professor ? walk : run);
        anim.Sr.flipX = facing < 0;
        if (type == EnemyType.Drone) { anim.Sr.sortingOrder = 3000; return; } // 하늘에 떠 있으니 땅 위 캐릭터들보다 앞에
        if (type != EnemyType.Pilot) Separate(gm);

        var p = transform.position;
        p.y = Mathf.Clamp(p.y, -7f, GameManager.BandMax);
        transform.position = p;
        anim.Sr.sortingOrder = GameManager.Order(p.y);

        // 같은 깊이에서 몸이 겹치면 주인공 피격
        to = gm.Player.transform.position - p;
        if (touch && Mathf.Abs(to.x) < 0.7f && Mathf.Abs(to.y) < 0.35f) gm.Player.Damage(p);
    }

    // 기계공학과: 주인공 2칸 앞까지 다가가 쪼그려 앉고, 태블릿으로 부른 로봇팔로 앞쪽을 넓게 휩쓴다.
    // 쪼그리는 동작이 예고, 휘두르는 동안은 맞아도 멈추지 않으니 피해야 한다
    void Arm(Vector3 to, float dt)
    {
        if (state == 1)
        {
            anim.Play(attack, 8, false);
            float ahead = to.x * facing;
            if (!thrown && anim.Frame >= 3 && anim.Frame <= 4 && ahead > 0.2f && ahead < 2.8f && Mathf.Abs(to.y) < 0.45f)
            {
                thrown = true;
                GameManager.I.Player.Damage(transform.position);
            }
            if (anim.Done) { state = 0; attackCd = Random.Range(1.5f, 2.2f); }
            return;
        }

        attackCd -= dt;
        var spot = to - new Vector3(Mathf.Sign(to.x) * 2f, 0); // 주인공 2칸 앞 자리
        if (spot.sqrMagnitude > 0.09f) { Move(spot, speed, walk); return; }
        Face(to);
        if (attackCd > 0) { anim.Play(idle, 6, true); return; }
        state = 1;
        thrown = false;
        anim.Play(attack, 8, false, true);
        Sfx.Play("robot", 0.6f);
    }

    // 드론학과: 캠핑 의자에 앉아 움직이지 않고 드론을 띄운다. 드론이 격추되면 잠시 뒤 새로 띄우니 조종사를 잡아야 끝난다
    void Pilot(Vector3 to, float dt)
    {
        Face(to);
        anim.Play(idle, 10, true);
        if (partner && !partner.dead) { stateT = 0; return; }
        stateT += dt;
        if (stateT < (state == 0 ? 0.6f : 3.5f)) return;
        state = 1;
        stateT = 0;
        partner = Create(EnemyType.Drone, transform.position + new Vector3(0.5f * facing, 1.6f));
        partner.partner = this;
        SpriteAnim.Fx(Art.Frames("fx_common", "landing_ring"), transform.position + new Vector3(0.5f * facing, 0.2f), 0.6f); // 이륙 바람
    }

    // 드론: 주인공의 옆 위 하늘에 떠서 따라다니며 에너지탄을 쏜다. 조종사가 없어지면 추락
    void Fly(Vector3 to, float dt)
    {
        var gm = GameManager.I;
        if (!partner || partner.dead) { Die(); return; }
        if (state == 1)
        {
            Face(to);
            anim.Play(attack, 12, false);
            if (!thrown && anim.Frame >= 2)
            {
                thrown = true;
                var muzzle = transform.position + new Vector3(0.45f * facing, -0.1f);
                Projectile.Energy(muzzle, (gm.Player.transform.position + Vector3.up - muzzle).normalized);
                Sfx.Play("zap", 0.6f);
            }
            if (anim.Done) { state = 0; attackCd = Random.Range(2.2f, 3f); }
            return;
        }

        stateT += dt;
        var spot = to + new Vector3(-Mathf.Sign(to.x) * 3.2f, 2.4f + Mathf.Sin(stateT * 3f) * 0.2f); // 지금 있는 쪽 위
        if (spot.sqrMagnitude > 0.04f) Move(spot, speed, walk);
        else { Face(to); anim.Play(idle, 12, true); }

        attackCd -= dt;
        float x = transform.position.x;
        if (attackCd <= 0 && x > gm.CamLeft + 0.5f && x < gm.CamRight - 0.5f)
        {
            state = 1;
            thrown = false;
            Face(to);
            anim.Play(attack, 12, false, true);
        }
    }

    // 대학원생: 붙으면 논문 더미를 머리 위로 들었다가(예고) 앞으로 내려찍는다. 내려찍는 프레임에 앞에 있으면 피격
    void Slam(Vector3 to, float dt)
    {
        if (state >= 1) // 2 = 내려찍는 소리를 낸 뒤
        {
            anim.Play(attack, 12, false);
            if (state == 1 && anim.Frame >= 4) { state = 2; Sfx.Play("slam", 0.7f); }
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
                    Sfx.Play("dash");
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
            if (o == this || o.type == EnemyType.Drone) continue;
            var d = transform.position - o.transform.position;
            d.y *= 2f;
            float m = d.sqrMagnitude;
            if (m > 0.0001f && m < 0.36f) transform.position += d.normalized * ((0.6f - Mathf.Sqrt(m)) * 0.5f);
        }
    }
}
