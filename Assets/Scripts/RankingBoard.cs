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
        // 패널 그림 안쪽 남색 영역
        var inner = UIKit.Rect(panel.transform, "Rows", Vector2.zero, Vector2.zero, Vector2.zero);
        inner.anchorMin = new Vector2(0.10f, 0.13f);
        inner.anchorMax = new Vector2(0.90f, 0.61f);
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
            Row(inner, 0, h, UIKit.Gold, "순위", "학과", "이름", "점수");
            int rank = 0;
            for (int i = 0; i < top.Count; i++)
            {
                var e = top[i];
                if (i == 0 || e.Score != top[i - 1].Score) rank = i + 1; // 동점은 같은 순위
                var c = e.Id == Session.Id ? UIKit.Gold : Color.white;
                Row(inner, i + 1, h, c, rank.ToString(), e.Dept, Db.Mask(e.Name), e.Score.ToString("N0"));
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

    static void Row(RectTransform inner, int i, float h, Color c, string rank, string dept, string name, string score)
    {
        var line = Line(inner, i, h);
        Cell(line, rank, 0f, 0.12f, TextAnchor.MiddleCenter, c, 30);
        Cell(line, dept, 0.14f, 0.56f, TextAnchor.MiddleLeft, c, 28);
        Cell(line, name, 0.56f, 0.74f, TextAnchor.MiddleLeft, c, 30);
        Cell(line, score, 0.74f, 0.98f, TextAnchor.MiddleRight, c, 30);
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
