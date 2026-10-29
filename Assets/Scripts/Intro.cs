using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

// 게임 시작 컷신 (만화 연출). 첫 페이지에 컷 1~4가 차례로 들어오고 컷마다 안쪽 카드가 얹힌다 → 페이지를 넘겨 컷 5 →
// 암전했다가 빠르게 밝아지며 게임 시작. ESC = 건너뛰기. 그림: Resources/Art/intro (SCH > Bake Art)
public class Intro : MonoBehaviour
{
    const float Gap = 18, Top = 72; // 컷 사이 간격 / 위쪽 띠(ESC 안내)
    static readonly Vector2 Mid = new(0.5f, 0.5f);
    static readonly Color PageColor = new(0.035f, 0.07f, 0.13f);

    class Panel { public RectTransform rt, art; public CanvasGroup cg; public Image flash, shade; public Vector2 home; }

    Action begin;
    RectTransform root, page; // page: 지금 보이는 페이지 (넘김·흔들림으로 움직임)
    Image black;
    bool ending;
    Vector2 offset;
    float shakeT, shakeAmp;

    // begin: 화면이 완전히 어두워졌을 때 한 번 불린다 (판 시작). 그 뒤 빠르게 밝아지며 컷신이 사라진다
    public static void Play(Action begin)
    {
        var c = UIKit.Canvas(100);
        c.name = "Intro";
        c.gameObject.AddComponent<Intro>().begin = begin;
    }

    void Start()
    {
        root = (RectTransform)transform;
        UIKit.Dim(root, 1).color = PageColor; // 페이지 바탕 (뒤의 게임 화면·클릭을 가림)

        // 첫 페이지: 2×2 컷 (전체 구성 레퍼런스 배치)
        var page1 = Page();
        float w = (1920 - Gap * 3) / 2, h = (1080 - Top - Gap * 2) / 2;
        var p = new[]
        {
            MakePanel(page1, new Rect(Gap, Top, w, h), "cut_1", "1. 학생 성적 공개"),
            MakePanel(page1, new Rect(Gap * 2 + w, Top, w, h), "cut_2", "2. 교수 반응"),
            MakePanel(page1, new Rect(Gap, Top + h + Gap, w, h), "cut_3", "3. 학생의 거부"),
            // 달리는 학생이 오른쪽에 있어서, 확대해 오른쪽 끝에 맞추고 카드 자리를 만든다
            MakePanel(page1, new Rect(Gap * 2 + w, Top + h + Gap, w, h), "cut_4", "4. 학생 도망", 1.3f, new Vector2(1, 0.57f)),
        };
        // 안쪽 카드: 컷 좌상단 기준 중심, 그림 높이, 기울기 (말풍선·인물을 가리지 않는 오른쪽 자리)
        var cards = new[]
        {
            Bare(p[0], "card_1", new Vector2(746, 243), 462),
            Framed(p[1], "card_2", new Vector2(748, 243), 440, -2),
            Framed(p[2], "card_3", new Vector2(727, 250), 350, 5),
            Framed(p[3], "card_4", new Vector2(793, 243), 387, -4),
        };

        // 둘째 페이지: 컷 5 한 장 가득
        var page2 = Page();
        var p5 = MakePanel(page2, new Rect(Gap, Top, 1920 - Gap * 2, 1080 - Top - Gap), "cut_5", "5. 교수의 추격 명령");
        // 카드 5는 컷 그림과 같은 크기로 겹쳐 얹는다 (구울 때 컷에서 지운 자리 그대로). 피벗 = 카드 중심
        var center5 = new Vector2(836 / 1024f, 1 - 325 / 572f);
        var lines = UIKit.Img(p5.art, Art.Intro("lines"), center5, Vector2.zero, new Vector2(1300, 1300), Color.clear);
        lines.rectTransform.pivot = Mid;
        var card5 = UIKit.Fill(p5.art, "Card");
        card5.pivot = center5; // 크기 0으로 늘린 사각형이라 피벗을 바꿔도 위치는 그대로
        card5.gameObject.AddComponent<Image>().sprite = Art.Intro("card_5");
        card5.GetComponent<Image>().raycastTarget = false;
        card5.gameObject.AddComponent<CanvasGroup>().alpha = 0;
        page2.gameObject.SetActive(false);

        // 오른쪽 위: [ESC] 누르면 스킵
        var tr = new Vector2(1, 1);
        UIKit.Label(root, "누르면 스킵", 28, tr, new Vector2(-28, -14), new Vector2(200, 44), TextAnchor.MiddleRight, outline: false);
        var key = UIKit.Img(root, null, tr, new Vector2(-196, -15), new Vector2(84, 42));
        UIKit.Fill(key.transform, "In", 3).gameObject.AddComponent<Image>().color = PageColor;
        UIKit.Label(key.transform, "ESC", 24, Mid, Vector2.zero, new Vector2(84, 42), outline: false);

        black = UIKit.Dim(root, 1);
        black.color = Color.black;
        black.raycastTarget = false;
        page = page1;
        StartCoroutine(Sequence(p, cards, page2, p5, card5, lines));
    }

    IEnumerator Sequence(Panel[] p, RectTransform[] cards, RectTransform page2, Panel p5, RectTransform card5, Image lines)
    {
        yield return Tween(0.35f, t => black.color = new Color(0, 0, 0, 1 - t));

        var from = new[] { Vector2.left, Vector2.right, Vector2.left, Vector2.right };
        for (int i = 0; i < 4; i++)
        {
            Focus(p, i);
            yield return Enter(p[i], from[i]);
            yield return Wait(0.7f);
            yield return i switch
            {
                0 => ShowCard(p[0], cards[0], 0, "hit", 8),        // 성적표 A+ '쾅'
                1 => ShowCard(p[1], cards[1], 260, "hit", 5),      // 뿌듯한 교수 얼굴이 옆에서 쓱
                2 => ShowCard(p[2], cards[2], 0, "slam", 16),      // "싫어요!" 크게 흔들림
                _ => ShowCard(p[3], cards[3], 320, "dash", 8),     // 돌아보는 얼굴이 휙
            };
            yield return Wait(1.1f);
        }

        // 한 페이지 전체를 잠깐 보여 주고 넘긴다 (왼쪽으로 밀려나며 사라짐)
        Focus(p, -1);
        yield return Wait(1.0f);
        var cg1 = page.GetComponent<CanvasGroup>();
        Sfx.Play("throw", 0.8f);
        yield return Tween(0.35f, t => { offset = new Vector2(-320 * t * t, 0); cg1.alpha = 1 - t; });
        page.gameObject.SetActive(false);
        page = page2;
        page.gameObject.SetActive(true);
        offset = Vector2.zero;

        yield return Enter(p5, Vector2.right);
        yield return Wait(0.8f);
        // 놀란 학생 카드: 집중선 + 호루라기 + 쾅
        StartCoroutine(Lines(lines));
        Sfx.Play("whistle");
        yield return ShowCard(p5, card5, 0, "slam", 22);
        yield return Wait(1.7f);
        yield return End(0.55f);
    }

    void Update()
    {
        if (!ending && Input.GetKeyDown(KeyCode.Escape))
        {
            StopAllCoroutines();
            StartCoroutine(End(0.25f));
        }
        if (!page) return;
        shakeT -= Dt;
        page.anchoredPosition = offset + (shakeT > 0 ? Random.insideUnitCircle * shakeAmp * (shakeT / 0.3f) : Vector2.zero);
    }

    // 암전 → begin(판 시작) → 빠르게 밝아지며 컷신이 사라진다
    IEnumerator End(float dur)
    {
        ending = true;
        float a0 = black.color.a;
        yield return Tween(dur, t => black.color = new Color(0, 0, 0, Mathf.Lerp(a0, 1, t)));
        foreach (Transform c in root)
            if (c != black.transform) Destroy(c.gameObject);
        page = null;
        begin();
        yield return Wait(0.15f);
        yield return Tween(0.25f, t => black.color = new Color(0, 0, 0, 1 - t));
        Destroy(gameObject);
    }

    // ---------- 연출 ----------

    // 컷 등장: 옆에서 밀려 들어와 자리 잡으며 하얗게 번쩍. 그림은 그 뒤로 아주 천천히 다가온다
    IEnumerator Enter(Panel p, Vector2 dir)
    {
        Sfx.Play("throw", 0.5f);
        StartCoroutine(Tween(9f, t => p.art.localScale = Vector3.one * (1 + 0.06f * t)));
        StartCoroutine(Flash(p.flash, 0.8f, 0.35f));
        yield return Tween(0.38f, t =>
        {
            p.cg.alpha = Mathf.Clamp01(t * 3);
            p.rt.anchoredPosition = p.home + dir * 110 * (1 - OutBack(t));
            p.rt.localScale = Vector3.one * Mathf.Lerp(1.05f, 1, OutCubic(t));
        });
    }

    // 안쪽 카드 등장. slide = 0 이면 크게 날아와 '쾅' (커졌다 줄며 기울기가 풀림), 아니면 오른쪽에서 미끄러져 들어온다
    IEnumerator ShowCard(Panel p, RectTransform card, float slide, string sfx, float shake)
    {
        var cg = card.GetComponent<CanvasGroup>();
        float rot = card.localEulerAngles.z;
        var home = card.anchoredPosition;
        Sfx.Play(sfx);
        yield return Tween(slide > 0 ? 0.3f : 0.22f, t =>
        {
            float e = OutBack(t);
            cg.alpha = Mathf.Clamp01(t * 4);
            if (slide > 0) card.anchoredPosition = home + Vector2.right * slide * (1 - e);
            else
            {
                card.localScale = Vector3.one * Mathf.LerpUnclamped(1.8f, 1, e);
                card.localEulerAngles = new Vector3(0, 0, rot + 14 * (1 - e));
            }
        });
        Shake(shake);
        StartCoroutine(Flash(p.flash, 0.55f, 0.2f));
    }

    // 집중선: 확 퍼졌다가 남아서 조금씩 다른 각도로 떨린다
    IEnumerator Lines(Image img)
    {
        var rt = img.rectTransform;
        StartCoroutine(Tween(0.18f, t =>
        {
            img.color = new Color(1, 1, 1, 0.9f * t);
            rt.localScale = Vector3.one * Mathf.Lerp(1.3f, 1, OutCubic(t));
        }));
        while (true)
        {
            rt.localEulerAngles = new Vector3(0, 0, Random.Range(-3f, 3f));
            yield return Wait(0.06f);
        }
    }

    // 지금 컷으로 시선을 모은다: 앞의 컷은 어둡게. i = -1 이면 모두 밝게 (페이지 전체 보기)
    void Focus(Panel[] ps, int i)
    {
        for (int j = 0; j < ps.Length; j++)
        {
            var s = ps[j].shade;
            float to = j < i ? 0.7f : 0f; // 리니어 색공간이라 이 정도여야 눈에 띄게 어두워진다
            StartCoroutine(Tween(0.4f, t => s.color = new Color(0, 0, 0, Mathf.Lerp(s.color.a, to, t))));
        }
    }

    void Shake(float amp) { shakeAmp = amp; shakeT = 0.3f; }

    IEnumerator Flash(Image img, float from, float dur) => Tween(dur, t => img.color = new Color(1, 1, 1, from * (1 - t)));

    // ---------- 페이지 · 컷 · 카드 만들기 ----------

    RectTransform Page()
    {
        var pg = UIKit.Rect(root, "Page", Mid, Vector2.zero, new Vector2(1920, 1080));
        pg.gameObject.AddComponent<CanvasGroup>();
        return pg;
    }

    // 컷 하나 (r = 페이지 좌상단 기준 픽셀). 그림은 흰 테두리 안을 꽉 채우고 넘치는 부분은 잘린다.
    // zoom = 꽉 채운 크기에서 더 확대, focus = 컷 가운데에 오게 할 그림 위의 점 (비율, 좌상단 기준. 끝을 넘으면 끝에 맞춤)
    Panel MakePanel(RectTransform pg, Rect r, string sprite, string caption, float zoom = 1, Vector2? focus = null)
    {
        var p = new Panel { home = new Vector2(r.x + r.width / 2 - 960, 540 - r.y - r.height / 2) };
        p.rt = UIKit.Rect(pg, caption, Mid, p.home, r.size);
        p.cg = p.rt.gameObject.AddComponent<CanvasGroup>();
        p.cg.alpha = 0;
        Overlay(p.rt, "Border", 0).color = new Color(0.96f, 0.96f, 0.94f);
        var view = UIKit.Fill(p.rt, "View", 5);
        view.gameObject.AddComponent<RectMask2D>();

        var s = Art.Intro(sprite);
        var vs = r.size - Vector2.one * 10;
        var size = s.rect.size * (Mathf.Max(vs.x / s.rect.width, vs.y / s.rect.height) * zoom);
        var f = focus ?? Mid;
        Vector2 off = new((0.5f - f.x) * size.x, (f.y - 0.5f) * size.y), slack = (size - vs) / 2;
        off = new Vector2(Mathf.Clamp(off.x, -slack.x, slack.x), Mathf.Clamp(off.y, -slack.y, slack.y));
        p.art = UIKit.Img(view, s, Mid, off, size).rectTransform;

        p.flash = Overlay(p.rt, "Flash", 5);
        p.flash.color = Color.clear;
        p.shade = Overlay(p.rt, "Shade", 0);
        p.shade.color = Color.clear;
        return p;
    }

    static Image Overlay(Transform parent, string name, float pad)
    {
        var img = UIKit.Fill(parent, name, pad).gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    // 테두리 없이 그림 자체가 카드인 것 (인트로 1: 기울어진 노란 카드)
    RectTransform Bare(Panel p, string sprite, Vector2 c, float h)
    {
        var s = Art.Intro(sprite);
        var card = Card(p, c, new Vector2(h * s.rect.width / s.rect.height, h), 0);
        UIKit.Img(card, s, Mid, Vector2.zero, card.sizeDelta);
        return card;
    }

    // 흰 테두리 + 검은 선 + 그림자를 두른 카드
    RectTransform Framed(Panel p, string sprite, Vector2 c, float h, float rot)
    {
        var s = Art.Intro(sprite);
        var size = new Vector2(h * s.rect.width / s.rect.height, h);
        var card = Card(p, c, size + Vector2.one * 16, rot);
        UIKit.Img(card, null, Mid, new Vector2(10, -12), size + Vector2.one * 16, new Color(0, 0, 0, 0.5f));
        UIKit.Img(card, null, Mid, Vector2.zero, size + Vector2.one * 16, new Color(0.05f, 0.07f, 0.12f));
        UIKit.Img(card, null, Mid, Vector2.zero, size + Vector2.one * 10, Color.white);
        UIKit.Img(card, s, Mid, Vector2.zero, size);
        return card;
    }

    // c = 컷 좌상단 기준 중심. 번쩍임·어둡게 막은 카드보다 위에 오게 다시 맨 뒤로
    RectTransform Card(Panel p, Vector2 c, Vector2 size, float rot)
    {
        var half = p.rt.sizeDelta / 2;
        var card = UIKit.Rect(p.rt, "Card", Mid, new Vector2(c.x - half.x, half.y - c.y), size);
        card.localEulerAngles = new Vector3(0, 0, rot);
        card.gameObject.AddComponent<CanvasGroup>().alpha = 0;
        p.flash.transform.SetAsLastSibling();
        p.shade.transform.SetAsLastSibling();
        return card;
    }

    // ---------- 시간 ----------

    static float Dt => Mathf.Min(Time.unscaledDeltaTime, 0.05f); // 씬 로딩 직후 큰 한 프레임에 연출이 건너뛰지 않게

    static IEnumerator Tween(float dur, Action<float> f)
    {
        for (float t = 0; t < 1; t += Dt / dur)
        {
            f(t);
            yield return null;
        }
        f(1);
    }

    static WaitForSecondsRealtime Wait(float s) => new(s);
    static float OutCubic(float t) => 1 - (1 - t) * (1 - t) * (1 - t);
    static float OutBack(float t) { const float c = 1.70158f; t -= 1; return 1 + (c + 1) * t * t * t + c * t * t; }
}
