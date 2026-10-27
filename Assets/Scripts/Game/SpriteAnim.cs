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

    void Update()
    {
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
