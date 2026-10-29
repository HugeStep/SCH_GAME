using UnityEngine;

// 주인공: WASD 걷기 + Shift 달리기(스태미나 소모), 마우스 조준 + 왼쪽 클릭으로 종이공 투척
public class Player : MonoBehaviour
{
    public const int MaxHp = 3;
    public const float WalkSpeed = 3f, RunSpeed = 5.4f;
    const float FireCooldown = 0.5f, ThrowTime = 0.2f, InvulTime = 1.0f;
    const float RunDrain = 2.5f, Regen = 4f; // 가득 찬 스태미나를 다 쓰는 / 다시 채우는 데 걸리는 초 (서 있으면 2배로 참)
    public const float HalfWidth = 0.4f, Height = 1.9f;

    public int Hp = MaxHp;
    public float Stamina { get; private set; } = 1f; // 0~1
    public bool Tired { get; private set; } // 다 쓰면 30% 찰 때까지 못 뜀
    SpriteAnim anim;
    Sprite[] idle, walk, run, hit, fall, throwStand, throwRun;
    float fireT, throwT, hurtT, invulT;
    int facing = 1;
    bool moving;

    public static Player Create(Vector3 pos)
    {
        var go = new GameObject("Player");
        go.transform.position = pos;
        go.AddComponent<SpriteAnim>();
        return go.AddComponent<Player>();
    }

    void Awake()
    {
        anim = GetComponent<SpriteAnim>();
        idle = Art.Frames("player_base", "idle");
        walk = Art.Frames("player_base", "walk");
        run = Art.Frames("player_base", "run");
        hit = Art.Frames("player_base", "hit");
        fall = Art.Frames("player_base", "trip_fall");
        throwStand = Art.Frames("player_throw_stand", "throw_stand")[3..]; // 줍는 동작은 빼고 와인드업부터
        throwRun = Art.Frames("player_throw_run", "throw_run")[3..];
        anim.Play(idle, 6, true);
    }

    // 몸통 사각형에 점이 들어오는지 (투사체 판정)
    public bool Contains(Vector3 p)
    {
        var f = transform.position;
        return Mathf.Abs(p.x - f.x) < HalfWidth && p.y > f.y + 0.1f && p.y < f.y + Height;
    }

    void Update()
    {
        var gm = GameManager.I;
        if (!gm.Running)
        {
            anim.Sr.enabled = true;
            if (Hp <= 0) anim.Play(fall, 10, false);
            else anim.Play(idle, 6, true);
            return;
        }
        float dt = Time.deltaTime;

        // 이동
        var input = new Vector2(Key(KeyCode.D) - Key(KeyCode.A), Key(KeyCode.W) - Key(KeyCode.S));
        if (input.sqrMagnitude > 1) input.Normalize();
        moving = input.sqrMagnitude > 0.01f;
        bool running = moving && !Tired && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        Stamina = Mathf.Clamp01(Stamina + (running ? -dt / RunDrain : dt / Regen * (moving ? 1f : 2f)));
        if (Stamina <= 0) Tired = true;
        else if (Tired && Stamina >= 0.3f) Tired = false;
        if (hurtT > 0) { hurtT -= dt; input *= 0.3f; }
        var p = transform.position + (Vector3)(input * ((running ? RunSpeed : WalkSpeed) * dt));
        p.x = Mathf.Clamp(p.x, gm.CamLeft + 0.6f, gm.CamRight - 0.6f);
        p.y = Mathf.Clamp(p.y, GameManager.BandMin, GameManager.BandMax);
        transform.position = p;

        // 조준 방향으로 몸을 돌린다
        var mouse = gm.Cam.ScreenToWorldPoint(Input.mousePosition);
        mouse.z = 0;
        facing = mouse.x >= p.x ? 1 : -1;
        anim.Sr.flipX = facing < 0;

        fireT -= dt;
        throwT -= dt;
        invulT -= dt;
        if (Input.GetMouseButton(0) && fireT <= 0) Fire(mouse);

        if (hurtT > 0) anim.Play(hit, 14, false);
        else if (throwT > 0) anim.Play(moving ? throwRun : throwStand, 25, false);
        else if (running) anim.Play(run, 14, true);
        else if (moving) anim.Play(walk, 10, true);
        else anim.Play(idle, 6, true);

        anim.Sr.enabled = invulT <= 0 || (int)(invulT * 16) % 2 == 0; // 무적 중 깜빡임
        anim.Sr.sortingOrder = GameManager.Order(p.y);
    }

    static int Key(KeyCode k) => Input.GetKey(k) ? 1 : 0;

    void Fire(Vector3 target)
    {
        fireT = FireCooldown;
        throwT = ThrowTime;
        anim.Play(moving ? throwRun : throwStand, 25, false, true);
        var hand = transform.position + new Vector3(0.35f * facing, 1.3f);
        var dir = target - hand;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.right * facing;
        Projectile.Ball(hand, dir.normalized);
        Sfx.Play("throw", 0.35f);
    }

    public void Damage(Vector3 from)
    {
        if (invulT > 0 || Hp <= 0 || !GameManager.I.Running) return;
        Hp--;
        invulT = InvulTime;
        hurtT = 0.25f;
        anim.Play(hit, 14, false, true);
        var p = transform.position;
        p.x += Mathf.Sign(p.x - from.x) * 0.5f; // 살짝 밀려남
        transform.position = p;
        SpriteAnim.Fx(Art.Frames("fx_common", "hit_spark"), p + Vector3.up, 0.9f);
        Sfx.Play("hurt");
        GameManager.I.OnPlayerHit();
    }
}
