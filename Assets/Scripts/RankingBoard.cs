using System;
using UnityEngine;
using UnityEngine.UI;

// 랭킹 패널 (TOP 10 + 내 순위). 메인 화면과 결과 화면에서 같이 쓴다.
public static class RankingBoard
{
    public static void Build(Transform parent, Vector2 anchor, Vector2 pos, float width)
    {
        var s = Art.Ui("panel_ranking");
        var panel = UIKit.Img(parent, s, anchor, pos, UIKit.Fit(s, width));
        // 패널 그림 안쪽 남색 영역 (전체 랭킹만 — 친구·주간 랭킹 탭 없음)
        var inner = UIKit.Rect(panel.transform, "Rows", Vector2.zero, Vector2.zero, Vector2.zero);
        inner.anchorMin = new Vector2(0.10f, 0.12f);
        inner.anchorMax = new Vector2(0.90f, 0.615f);
        inner.offsetMin = inner.offsetMax = Vector2.zero;
        var status = UIKit.Label(inner, "불러오는 중...", 32, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 100));
        Load(inner, status);
    }

    static async void Load(RectTransform inner, Text status)
    {
        try
        {
            var top = await Db.Top(10);
            long myRank = Session.LoggedIn ? await Db.MyRank() : 0;
            if (!inner) return; // 기다리는 사이 창이 닫힘

            status.text = top.Count == 0 ? "아직 기록이 없습니다.\n첫 번째 기록의 주인공이 되어보세요!" : "";
            float h = inner.rect.height / 12f;
            var head = Line(inner, 0, h);
            UIKit.Img(head, null, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.3f)).rectTransform.anchorMax = Vector2.one;
            Row(head, UIKit.Gold, "순위", "학과", "이름", "점수");
            int rank = 0;
            for (int i = 0; i < top.Count; i++)
            {
                var e = top[i];
                if (i == 0 || e.Score != top[i - 1].Score) rank = i + 1; // 동점은 같은 순위
                var line = Line(inner, i + 1, h);
                Deco(line, rank, h);
                var c = e.Id == Session.Id && rank != 1 ? UIKit.Gold : Color.white; // 1위 줄은 금색 바탕이라 흰 글자
                Row(line, c, rank <= 3 ? "" : rank.ToString(), e.Dept, Db.Mask(e.Name), e.Score.ToString("N0"));
            }
            if (Session.LoggedIn)
            {
                string me = myRank > 0 ? $"내 순위  {myRank}위  ·  {Session.Best:N0}점" : "아직 내 기록이 없습니다";
                Cell(Line(inner, 11, h), me, 0, 1, TextAnchor.MiddleCenter, UIKit.Gold, 30);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(e);
            if (status) status.text = Db.Friendly(e);
        }
    }

    // 줄 배경(1위 금색, 나머지 파랑) + 왼쪽 순위 배지. 1~3위 배지엔 숫자가 그려져 있고 4위부터는 빈 배지에 글자를 얹는다.
    static void Deco(RectTransform line, int rank, float h)
    {
        var bs = Art.Ui(rank == 1 ? "rank_bar_gold" : "rank_bar_blue");
        var bar = UIKit.Img(line, bs, Vector2.zero, Vector2.zero, Vector2.zero);
        bar.preserveAspect = false;
        bar.type = Image.Type.Sliced;
        bar.pixelsPerUnitMultiplier = bs.rect.height / (h * 0.88f); // 끝부분을 줄 높이에 맞춰 축소
        bar.rectTransform.anchorMin = new Vector2(0.05f, 0.06f);
        bar.rectTransform.anchorMax = new Vector2(1f, 0.94f);

        var ks = Art.Ui(rank <= 3 ? "rank_" + rank : "rank_n");
        var badge = UIKit.Img(line, ks, new Vector2(0.06f, 0.5f), Vector2.zero, ks.rect.size * h / 85f).rectTransform;
        badge.pivot = new Vector2(0.5f, rank == 1 ? 0.45f : 0.5f); // 1위는 왕관만큼 위로 솟게
    }

    static void Row(RectTransform line, Color c, string rank, string dept, string name, string score)
    {
        Cell(line, rank, 0f, 0.12f, TextAnchor.MiddleCenter, c, 30);
        Cell(line, dept, 0.14f, 0.56f, TextAnchor.MiddleLeft, c, 28);
        Cell(line, name, 0.56f, 0.74f, TextAnchor.MiddleLeft, c, 30);
        Cell(line, score, 0.74f, 0.95f, TextAnchor.MiddleRight, c, 30);
    }

    static RectTransform Line(RectTransform inner, int i, float h)
    {
        var rt = UIKit.Rect(inner, "Row" + i, new Vector2(0.5f, 1), new Vector2(0, -i * h), new Vector2(0, h));
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(0, h);
        return rt;
    }

    static Text Cell(RectTransform line, string s, float x0, float x1, TextAnchor align, Color c, int size)
    {
        var t = UIKit.Label(line, s, size, Vector2.zero, Vector2.zero, Vector2.zero, align, c);
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true; // 긴 학과명은 글자를 줄여서 칸에 맞춘다
        t.resizeTextMinSize = 14;
        t.resizeTextMaxSize = size;
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(x0, 0);
        rt.anchorMax = new Vector2(x1, 1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return t;
    }
}
