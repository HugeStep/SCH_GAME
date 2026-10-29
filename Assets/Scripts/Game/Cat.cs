using UnityEngine;

// 적을 쓰러뜨리면 가끔 나오는 고양이(보너스 점수)와 아이스커피(목숨 +1, 목숨이 줄었을 때만 나옴).
// 주인공이 닿으면 먹고, 그냥 두면 잠시 뒤 사라진다
public class Cat : MonoBehaviour
{
    const float DropChance = 0.1f, CoffeeChance = 0.08f, Life = 7f;
    const int Bonus = 300; // 가장 비싼 적(드론학과)만큼. 돌아가서 줍느라 놓치는 이동 점수(초당 150~270)보다 조금 크게

    // cat.png: 정면·비스듬·옆 3포즈 × 윗줄 눈 뜸 / 아랫줄 눈 감음.
    // 포즈별 (몸 왼쪽 x, 발밑 y) 뜬눈·감은눈 / 몸 반폭 — 반짝이는 빼고 잰 원본 픽셀 좌표 (눈 깜빡일 때 몸이 안 흔들리게 맞춤)
    static readonly int[,] Body = { { 35, 131, 35, 305 }, { 291, 133, 286, 308 }, { 534, 132, 531, 305 } };
    static readonly int[] HalfW = { 52, 66, 94 };
    const int CellW = 256, RowSplit = 162;
    static Sprite[][] poses;
    static Sprite cup;

    SpriteAnim anim;
    float t;
    bool coffee;

    public static void MaybeDrop(Vector3 pos)
    {
        var gm = GameManager.I;
        float r = Random.value;
        bool coffee = r >= DropChance && r < DropChance + CoffeeChance && gm.Player.Hp < Player.MaxHp;
        if (r >= DropChance && !coffee) return;
        pos.x = Mathf.Clamp(pos.x, gm.CamLeft + 0.8f, gm.CamRight - 0.8f); // 화면 밖에서 쓰러져도 주울 수 있게
        pos.y = Mathf.Clamp(pos.y, GameManager.BandMin, GameManager.BandMax); // 하늘의 드론이면 땅으로
        var go = new GameObject(coffee ? "Coffee" : "Cat");
        go.transform.position = pos;
        var c = go.AddComponent<Cat>();
        c.coffee = coffee;
        c.anim = go.AddComponent<SpriteAnim>();
        if (coffee)
        {
            var s = Art.Ui("hp_5"); // HUD 의 가득 찬 커피 그림을 땅 위에 0.9 높이로 세운다
            if (!cup) cup = Sprite.Create(s.texture, s.rect, new Vector2(0.5f, 0f), s.rect.height / 0.9f);
            c.anim.Play(new[] { cup }, 1f, true);
        }
        else
        {
            poses ??= Load();
            c.anim.Play(poses[Random.Range(0, poses.Length)], 3f, true);
            c.anim.Sr.flipX = Random.value < 0.5f;
        }
        c.anim.Sr.sortingOrder = GameManager.Order(pos.y);
    }

    static Sprite[][] Load()
    {
        var tex = Resources.Load<Texture2D>("Art/chr/cat");
        var r = new Sprite[HalfW.Length][];
        for (int i = 0; i < r.Length; i++)
        {
            Sprite Cut(int row)
            {
                int top = row == 0 ? 0 : RowSplit, bottom = row == 0 ? RowSplit : tex.height, x0 = i * CellW;
                int left = Body[i, row * 2], foot = Body[i, row * 2 + 1];
                var pivot = new Vector2((left + HalfW[i] - x0) / (float)CellW, (bottom - foot) / (float)(bottom - top));
                return Sprite.Create(tex, new Rect(x0, tex.height - bottom, CellW, bottom - top), pivot, 140);
            }
            var open = Cut(0);
            r[i] = new[] { open, open, open, open, Cut(1) }; // 가끔 눈을 끔뻑
        }
        return r;
    }

    void Update()
    {
        var gm = GameManager.I;
        t += Time.deltaTime;
        transform.localScale = Vector3.one * Mathf.Min(1f, t * 6f); // 퐁 하고 나타남
        if (t > Life || transform.position.x < gm.CamLeft - 1f) { Destroy(gameObject); return; }
        anim.Sr.enabled = t < Life - 2f || (int)(t * 8) % 2 == 0; // 사라지기 전 깜빡임
        if (!gm.Running) return;

        var to = gm.Player.transform.position - transform.position;
        if (Mathf.Abs(to.x) > 0.8f || Mathf.Abs(to.y) > 0.45f) return;
        if (!coffee) gm.AddBonus(Bonus, transform.position + Vector3.up * 1.1f);
        else if (gm.Player.Hp < Player.MaxHp) gm.AddLife(transform.position + Vector3.up * 1.1f);
        else return; // 목숨이 가득이면 그대로 둔다
        Sfx.Play("click");
        Destroy(gameObject);
    }
}
