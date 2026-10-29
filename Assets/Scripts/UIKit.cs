using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 코드로 uGUI 화면을 조립하는 도우미. 위치는 1920x1080 기준 픽셀.
// anchor 는 부모 안의 기준점이자 자신의 피벗 (예: (0,1)=좌상단, (0.5,0.5)=가운데).
public static class UIKit
{
    static Font font, fontRegular;
    // 한글 폰트: Paperlogy (Resources/Fonts). 굵기는 파일로 고르고 fontStyle 은 Normal 로 둔다.
    public static Font Font => font ??= Resources.Load<Font>("Fonts/Paperlogy-7Bold");
    public static Font FontRegular => fontRegular ??= Resources.Load<Font>("Fonts/Paperlogy-4Regular");

    public static readonly Color Navy = new Color(0.06f, 0.13f, 0.32f, 1f); // 불투명이어야 Outline(금색 테두리)이 안쪽에 비치지 않음
    public static readonly Color Gold = new Color(1f, 0.78f, 0.15f);

    public static RectTransform Canvas(int order = 0)
    {
        if (Object.FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var cs = go.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        return (RectTransform)go.transform;
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    // 부모를 꽉 채우는 사각형 (pad 만큼 안쪽 여백)
    public static RectTransform Fill(Transform parent, string name, float pad = 0)
    {
        var rt = Rect(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
        return rt;
    }

    // 스프라이트 비율을 유지한 크기
    public static Vector2 Fit(Sprite s, float width) => new Vector2(width, width * s.rect.height / s.rect.width);

    public static Image Img(Transform parent, Sprite s, Vector2 anchor, Vector2 pos, Vector2 size, Color? color = null)
    {
        var img = Rect(parent, s ? s.name : "Box", anchor, pos, size).gameObject.AddComponent<Image>();
        img.sprite = s;
        img.color = color ?? Color.white;
        img.preserveAspect = s != null;
        img.raycastTarget = false;
        return img;
    }

    // 화면 전체 배경 그림
    public static Image Background(Transform parent, Sprite s)
    {
        var img = Fill(parent, "Background").gameObject.AddComponent<Image>();
        img.sprite = s;
        img.raycastTarget = false;
        return img;
    }

    // 화면 전체를 덮는 반투명 막 (팝업 뒤 클릭 막기)
    public static Image Dim(Transform parent, float alpha = 0.6f)
    {
        var img = Fill(parent, "Dim").gameObject.AddComponent<Image>();
        img.color = new Color(0, 0, 0, alpha);
        return img;
    }

    // 금색 테두리가 있는 남색 패널
    public static Image Panel(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var img = Img(parent, null, anchor, pos, size, Navy);
        img.raycastTarget = true;
        var o = img.gameObject.AddComponent<Outline>();
        o.effectColor = Gold;
        o.effectDistance = new Vector2(5, 5);
        return img;
    }

    public static Text Label(Transform parent, string s, int size, Vector2 anchor, Vector2 pos, Vector2 box,
        TextAnchor align = TextAnchor.MiddleCenter, Color? color = null, bool outline = true)
    {
        var t = Rect(parent, "Text", anchor, pos, box).gameObject.AddComponent<Text>();
        t.font = Font;
        t.fontSize = size;
        t.text = s;
        t.alignment = align;
        t.color = color ?? Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (outline)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.03f, 0.07f, 0.2f, 0.95f);
            o.effectDistance = new Vector2(2, -2);
        }
        return t;
    }

    // pressed 스프라이트가 있으면 눌림 이미지로 교체, 없으면 색 변화
    public static Button Btn(Transform parent, Sprite normal, Sprite pressed, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick, Color? color = null)
    {
        var img = Img(parent, normal, anchor, pos, size, color);
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        if (pressed)
        {
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState { pressedSprite = pressed, highlightedSprite = normal, selectedSprite = normal };
        }
        else
        {
            var cb = b.colors;
            cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            b.colors = cb;
        }
        b.onClick.AddListener(() => Sfx.Play("click"));
        b.onClick.AddListener(onClick);
        return b;
    }

    // 글자만 있는 단색 버튼
    public static Button TextBtn(Transform parent, string label, int fontSize, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, UnityAction onClick)
    {
        var b = Btn(parent, null, null, anchor, pos, size, onClick, bg);
        Label(b.transform, label, fontSize, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        return b;
    }

    public static InputField Field(Transform parent, string placeholder, Vector2 anchor, Vector2 pos, Vector2 size,
        InputField.ContentType type, int limit)
    {
        var bg = Img(parent, null, anchor, pos, size, new Color(1, 1, 1, 0.96f));
        bg.raycastTarget = true;
        var f = bg.gameObject.AddComponent<InputField>();

        var ph = Label(bg.transform, placeholder, 30, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, new Color(0.5f, 0.53f, 0.6f), false);
        ph.font = FontRegular;
        var tx = Label(bg.transform, "", 32, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, new Color(0.08f, 0.1f, 0.2f), false);
        tx.supportRichText = false;
        foreach (var t in new[] { ph, tx })
        {
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(22, 4);
            rt.offsetMax = new Vector2(-22, -4);
        }
        f.textComponent = tx;
        f.placeholder = ph;
        f.contentType = type;
        f.characterLimit = limit;
        return f;
    }
}
