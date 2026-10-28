using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 메인 화면: 게임 시작 / 규칙 / 랭킹 / 종료 + 내 정보
public class MainMenuScreen : MonoBehaviour
{
    static readonly Vector2 Mid = new(0.5f, 0.5f);
    RectTransform root;
    GameObject popup;
    Text info;

    void Start()
    {
        if (!Session.LoggedIn) { SceneManager.LoadScene("Login"); return; }

        root = UIKit.Canvas();
        UIKit.Background(root, Art.Ui("menu_bg"));
        var logo = Art.Ui("logo");
        UIKit.Img(root, logo, new Vector2(0, 1), new Vector2(40, -20), UIKit.Fit(logo, 620));

        // 오른쪽 버튼 열
        var right = new Vector2(1, 0.5f);
        var start = Art.Ui("btn_start");
        UIKit.Btn(root, start, Art.Ui("btn_start_pressed"), right, new Vector2(-60, 200), UIKit.Fit(start, 600),
            () => SceneManager.LoadScene("Game"));
        var rules = BlueButton("btn_blue_book", "규칙", new Vector2(-70, 30), ShowRules);
        var rank = BlueButton("btn_blue_blank", "랭킹", new Vector2(-70, -130), ShowRanking);
        var trophy = Art.Ui("trophy");
        var size = rank.GetComponent<RectTransform>().sizeDelta;
        UIKit.Img(rank.transform, trophy, Mid, new Vector2(-size.x * 0.245f, 2), UIKit.Fit(trophy, size.x * 0.24f));
        var exit = Art.Ui("btn_exit");
        UIKit.Btn(root, exit, Art.Ui("btn_exit_pressed"), right, new Vector2(-70, -290), UIKit.Fit(exit, 520), Quit);

        // 오른쪽 위: 내 정보 배너
        var banner = Art.Ui("banner_trophy");
        var bannerSize = UIKit.Fit(banner, 640);
        var b = UIKit.Img(root, banner, new Vector2(1, 1), new Vector2(-30, -20), bannerSize);
        info = UIKit.Label(b.transform, "", 28, Mid, new Vector2(bannerSize.x * 0.11f, 4), new Vector2(bannerSize.x * 0.62f, 120));
        UIKit.TextBtn(root, "로그아웃", 24, new Vector2(1, 1), new Vector2(-40, -225), new Vector2(160, 48), new Color(0.1f, 0.2f, 0.45f, 0.9f),
            () => { Session.Clear(); SceneManager.LoadScene("Login"); });
        RefreshInfo();

        // 왼쪽 아래: 주인공 + 말풍선
        var hero = Art.Frames("player_base", "emote")[5];
        UIKit.Img(root, hero, new Vector2(0, 0), new Vector2(50, 30), UIKit.Fit(hero, 190));
        var bubble = UIKit.Panel(root, new Vector2(0, 0), new Vector2(240, 60), new Vector2(760, 120)).transform;
        UIKit.Label(bubble, $"{Session.Name}님, 2분 안에 캠퍼스를 가로질러\n<color=#FFD54A>최대한 멀리</color> 달려보세요!", 30, Mid, Vector2.zero, new Vector2(720, 110));
    }

    // 파란 버튼(그림의 글자를 지운 것) 위에 글자를 얹는다
    Button BlueButton(string sprite, string text, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var s = Art.Ui(sprite);
        var size = UIKit.Fit(s, 520);
        var b = UIKit.Btn(root, s, null, new Vector2(1, 0.5f), pos, size, onClick);
        UIKit.Label(b.transform, text, 50, Mid, new Vector2(size.x * 0.08f, 4), new Vector2(240, 80));
        return b;
    }

    async void RefreshInfo()
    {
        info.text = $"{Session.Dept}\n{Session.Name}  |  최고 {Session.Best:N0}점";
        if (Session.Best <= 0) return;
        try
        {
            long rank = await Db.MyRank();
            if (info) info.text = $"{Session.Dept}\n{Session.Name}  |  최고 {Session.Best:N0}점 · <color=#FFD54A>{rank}위</color>";
        }
        catch (Exception e) { Debug.LogWarning(e); }
    }

    void Update()
    {
        if (popup && Input.GetKeyDown(KeyCode.Escape)) Destroy(popup);
    }

    RectTransform OpenPopup()
    {
        if (popup) Destroy(popup);
        var dim = UIKit.Dim(root, 0.65f);
        dim.raycastTarget = true;
        popup = dim.gameObject;
        return dim.rectTransform;
    }

    void ShowRules()
    {
        var p = OpenPopup();
        var panel = UIKit.Panel(p, Mid, new Vector2(0, 20), new Vector2(1320, 860)).transform;
        UIKit.Label(panel, "게임 규칙", 56, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(800, 70), color: UIKit.Gold);
        UIKit.Label(panel,
            "<color=#FFD54A>목표</color>   2분 동안 캠퍼스를 <b>오른쪽으로</b> 최대한 멀리 달리세요!\n" +
            "<color=#FFD54A>이동</color>   W A S D  (8방향) · Shift 누르면 달리기 (스태미나 소모)\n" +
            "<color=#FFD54A>공격</color>   마우스로 조준하고 왼쪽 클릭 — 1초에 한 번\n" +
            "<color=#FFD54A>체력</color>   아이스커피 5잔 · 다 비우면 그 자리에서 게임 종료\n" +
            "<color=#FFD54A>점수</color>   이동 거리(m) × 10  +  처치 점수  −  피격 1회당 200",
            34, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(1200, 300), TextAnchor.UpperLeft);

        (string sheet, string name, string desc)[] enemies =
        {
            ("professor_base", "교수님", "추적형 · +100"),
            ("grad_student_base", "대학원생", "근접형 · +100"),
            ("football_base", "미식축구부", "돌진형 · +150"),
            ("chemistry_base", "화학과", "원거리형 · +200"),
        };
        for (int i = 0; i < enemies.Length; i++)
        {
            float x = -450 + i * 300;
            var s = Art.Frames(enemies[i].sheet, "idle")[0];
            UIKit.Img(panel, s, new Vector2(0.5f, 0), new Vector2(x, 190), new Vector2(150, 190));
            UIKit.Label(panel, enemies[i].name, 32, new Vector2(0.5f, 0), new Vector2(x, 140), new Vector2(280, 44), color: UIKit.Gold);
            UIKit.Label(panel, enemies[i].desc, 26, new Vector2(0.5f, 0), new Vector2(x, 100), new Vector2(280, 40));
        }
        UIKit.TextBtn(panel, "닫기", 32, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(220, 64), new Color(1f, 0.72f, 0.1f), () => Destroy(popup));
    }

    void ShowRanking()
    {
        var p = OpenPopup();
        RankingBoard.Build(p, Mid, new Vector2(0, 40), 1150);
        UIKit.TextBtn(p, "닫기", 32, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(220, 64), new Color(1f, 0.72f, 0.1f), () => Destroy(popup));
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
