using System;
using UnityEngine;
using UnityEngine.UI;

// 랭킹 패널 (한 페이지 5명 + 좌우 넘기기 + 내 순위). 메인 화면과 결과 화면에서 같이 쓴다.
public static class RankingBoard
{
    const int PerPage = 5;

    // atMe: 내 순위가 있는 페이지부터 보여준다 (결과 화면)
    public static void Build(Transform parent, Vector2 anchor, Vector2 pos, float width, bool atMe = false)
    {
        var s = Art.Ui("panel_ranking");
        var panel = UIKit.Img(parent, s, anchor, pos, UIKit.Fit(s, width));
        // 패널 그림 안쪽 남색 영역 (전체 랭킹만 — 친구·주간 랭킹 탭 없음)
        var inner = UIKit.Rect(panel.transform, "Rows", Vector2.zero, Vector2.zero, Vector2.zero);
        inner.anchorMin = new Vector2(0.10f, 0.12f);
        inner.anchorMax = new Vector2(0.90f, 0.615f);
        inner.offsetMin = inner.offsetMax = Vector2.zero;
        var status = UIKit.Label(inner, "불러오는 중...", 32, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 100));
        Load(inner, status, atMe);
    }

    static async void Load(RectTransform inner, Text status, bool atMe)
    {
        try
        {
            long total = await Db.Count();
            long myRank = Session.LoggedIn ? await Db.MyRank() : 0;
            var page = atMe && myRank > 0 ? await Db.At(Session.Best, myRank - 1, PerPage) : await Db.First(PerPage);
            if (!inner) return; // 기다리는 사이 창이 닫힘

            status.text = total == 0 ? "아직 기록이 없습니다.\n첫 번째 기록의 주인공이 되어보세요!" : "";
            float h = inner.rect.height / (PerPage + 2); // 머리줄 + 5명 + 아래 줄(넘기기·내 순위)
            int font = Mathf.RoundToInt(h * 0.48f);

            var head = Line(inner, 0, h);
            UIKit.Img(head, null, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.3f)).rectTransform.anchorMax = Vector2.one;
            Row(head, UIKit.Gold, Mathf.RoundToInt(h * 0.4f), "순위", "학과", "이름", "점수");
            var rows = UIKit.Fill(inner, "Page");

            var foot = Line(inner, PerPage + 1, h);
            if (Session.LoggedIn)
            {
                string me = myRank > 0 ? $"내 순위  {myRank}위  ·  {Session.Best:N0}점" : "아직 내 기록이 없습니다";
                Cell(foot, me, 0.12f, 0.88f, TextAnchor.MiddleCenter, UIKit.Gold, Mathf.RoundToInt(h * 0.42f));
            }
            bool busy = false;
            Button prev = null, next = null;
            prev = Arrow(foot, "rank_prev", 0, h, () => Go(-1));
            next = Arrow(foot, "rank_next", 1, h, () => Go(1));

            void Show(Db.Page p)
            {
                page = p;
                foreach (Transform c in rows) UnityEngine.Object.Destroy(c.gameObject);
                long rank = 0;
                for (int i = 0; i < p.List.Count; i++)
                {
                    var e = p.List[i];
                    rank = i == 0 ? p.FirstRank : e.Score == p.List[i - 1].Score ? rank : p.Start + i + 1; // 동점은 같은 순위
                    var line = Line(rows, i + 1, h);
                    Deco(line, rank, h);
                    var c = e.Id == Session.Id && rank != 1 ? UIKit.Gold : Color.white; // 1위 줄은 금색 바탕이라 흰 글자
                    Row(line, c, font, rank <= 3 ? "" : rank.ToString(), e.Dept, Db.Mask(e.Name), e.Score.ToString("N0"));
                }
                prev.gameObject.SetActive(p.Start > 0); // 넘길 페이지가 없으면 화살표를 숨긴다
                next.gameObject.SetActive(p.Start + p.List.Count < total);
            }

            async void Go(int dir)
            {
                if (busy) return;
                busy = true;
                try
                {
                    var p = dir > 0 ? await Db.Next(page, PerPage) : await Db.Prev(page, PerPage);
                    if (inner && p.List.Count > 0) Show(p);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e);
                    if (status) status.text = Db.Friendly(e);
                }
                busy = false;
            }

            Show(page);
        }
        catch (Exception e)
        {
            Debug.LogWarning(e);
            if (status) status.text = Db.Friendly(e);
        }
    }

    // 아래 줄 양 끝의 넘기기 화살표 (side 0 = 왼쪽, 1 = 오른쪽)
    static Button Arrow(RectTransform foot, string sprite, float side, float h, UnityEngine.Events.UnityAction onClick)
    {
        var s = Art.Ui(sprite);
        return UIKit.Btn(foot, s, null, new Vector2(side, 0.5f), Vector2.zero, s.rect.size * (h * 0.9f / s.rect.height), onClick);
    }

    // 줄 배경(1위 금색, 나머지 파랑) + 왼쪽 순위 배지. 1~3위 배지엔 숫자가 그려져 있고 4위부터는 빈 배지에 글자를 얹는다.
    static void Deco(RectTransform line, long rank, float h)
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

    static void Row(RectTransform line, Color c, int size, string rank, string dept, string name, string score)
    {
        Cell(line, rank, 0f, 0.12f, TextAnchor.MiddleCenter, c, size);
        Cell(line, dept, 0.14f, 0.54f, TextAnchor.MiddleLeft, c, size - 2);
        Cell(line, name, 0.56f, 0.74f, TextAnchor.MiddleLeft, c, size);
        Cell(line, score, 0.74f, 0.95f, TextAnchor.MiddleRight, c, size);
    }

    static RectTransform Line(RectTransform parent, int i, float h)
    {
        var rt = UIKit.Rect(parent, "Row" + i, new Vector2(0.5f, 1), new Vector2(0, -i * h), new Vector2(0, h));
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
