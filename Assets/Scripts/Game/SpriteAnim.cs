using UnityEngine;

// 프레임 배열을 순서대로 넘기는 단순한 애니메이터
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnim : MonoBehaviour
{
    public SpriteRenderer Sr { get; private set; }
    public bool destroyOnEnd;
    Sprite[] frames;
    float fps, t;
    bool loop;

    public int Frame => frames == null ? 0 : loop ? (int)(t * fps) % frames.Length : Mathf.Min((int)(t * fps), frames.Length - 1);
    public bool Done => !loop && frames != null && t * fps >= frames.Length;

    void Awake() => Sr = GetComponent<SpriteRenderer>();

    public void Play(Sprite[] f, float fps, bool loop, bool restart = false)
    {
        if (f == null || (f == frames && !restart)) return;
        frames = f;
        this.fps = fps;
        this.loop = loop;
        t = 0;
        Sr.sprite = f[0];
    }

    // 스쿼시 & 스트레치: 크기 1에서 (x, y)만큼 찌그러졌다가 빠르게 되돌아온다 (판정은 그대로, 그림만)
    Vector2 squash;
    public void Squash(float x, float y) => squash = new Vector2(x, y);

    void Update()
    {
        if (squash != Vector2.zero)
        {
            squash = squash.sqrMagnitude < 0.0001f ? Vector2.zero : squash * Mathf.Exp(-14f * Time.deltaTime);
            transform.localScale = new Vector3(1 + squash.x, 1 + squash.y, 1);
        }
        if (frames == null) return;
        t += Time.deltaTime;
        if (Done && destroyOnEnd) { Destroy(gameObject); return; }
        Sr.sprite = frames[Frame];
    }

    // 한 번 재생하고 사라지는 이펙트
    public static void Fx(Sprite[] f, Vector3 pos, float scale = 1f, float fps = 20f, bool flip = false)
    {
        var go = new GameObject("FX");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        var a = go.AddComponent<SpriteAnim>();
        a.Sr.sortingOrder = 4000;
        a.Sr.flipX = flip;
        a.destroyOnEnd = true;
        a.Play(f, fps, false);
    }
}
