using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 로그인 화면: 학과 · 학번 · 이름 입력 → Firestore 확인 후 메인 화면으로
public class LoginScreen : MonoBehaviour
{
    InputField dept, id, studentName;
    Text msg;
    bool busy;

    void Start()
    {
        var root = UIKit.Canvas();
        var mid = new Vector2(0.5f, 0.5f);
        UIKit.Background(root, Art.Ui("menu_bg"));
        UIKit.Dim(root, 0.35f).raycastTarget = false;

        var logo = Art.Ui("logo");
        UIKit.Img(root, logo, new Vector2(0.5f, 1f), new Vector2(0, -30), UIKit.Fit(logo, 560));

        var panel = UIKit.Panel(root, mid, new Vector2(0, -150), new Vector2(760, 560)).transform;
        UIKit.Label(panel, "학생 로그인", 44, new Vector2(0.5f, 1f), new Vector2(0, -28), new Vector2(700, 60), color: UIKit.Gold);

        dept = Row(panel, 0, "학과", "예) 컴퓨터소프트웨어공학과", InputField.ContentType.Standard, 20);
        id = Row(panel, 1, "학번", "숫자만 입력", InputField.ContentType.IntegerNumber, 12);
        studentName = Row(panel, 2, "이름", "이름", InputField.ContentType.Standard, 10);

        UIKit.TextBtn(panel, "로그인", 40, new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(380, 84), new Color(1f, 0.72f, 0.1f), Submit);
        msg = UIKit.Label(panel, "", 26, new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(720, 40), color: new Color(1f, 0.85f, 0.4f));

        UIKit.Label(root, "학과 · 학번 · 이름으로 로그인하면 점수가 랭킹에 기록됩니다", 26, new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(1400, 40));
        dept.Select();
    }

    static InputField Row(Transform panel, int i, string label, string placeholder, InputField.ContentType type, int limit)
    {
        float y = -110 - i * 88; // 마지막 칸 아래 끝(패널 위에서 356)이 로그인 버튼(아래에서 154)과 겹치지 않게
        UIKit.Label(panel, label, 34, new Vector2(0f, 1f), new Vector2(40, y), new Vector2(120, 70), TextAnchor.MiddleLeft);
        return UIKit.Field(panel, placeholder, new Vector2(0f, 1f), new Vector2(170, y), new Vector2(550, 70), type, limit);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            var fields = new[] { dept, id, studentName };
            int cur = Array.FindIndex(fields, f => f.isFocused);
            fields[(cur + (Input.GetKey(KeyCode.LeftShift) ? 2 : 1)) % 3].Select();
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Submit();
    }

    async void Submit()
    {
        if (busy) return;
        string d = dept.text.Trim(), s = id.text.Trim(), n = studentName.text.Trim();
        if (d.Length == 0) { msg.text = "학과를 입력해 주세요."; dept.Select(); return; }
        if (!Regex.IsMatch(s, @"^\d{4,12}$")) { msg.text = "학번은 숫자 4~12자리로 입력해 주세요."; id.Select(); return; }
        if (n.Length == 0) { msg.text = "이름을 입력해 주세요."; studentName.Select(); return; }

        busy = true;
        msg.text = "접속 중...";
        try
        {
            await Db.Login(d, s, n);
            SceneManager.LoadScene("Main");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (this) msg.text = Db.Friendly(e);
        }
        busy = false;
    }
}
