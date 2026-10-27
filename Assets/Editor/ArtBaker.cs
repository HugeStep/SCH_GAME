using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 원본 에셋 폴더의 이미지를 게임용으로 가공해 Assets/Resources/Art 에 저장한다.
// (마젠타 배경 제거, UI 시트 잘라내기, 배경 복사) 메뉴: SCH > Bake Art
public static class ArtBaker
{
    const string Src = @"C:\Users\cscsw\OneDrive - sch.ac.kr\게임경진대회 소스\";
    const string Dst = "Assets/Resources/Art/";

    // 진행 순서대로 이어 붙일 캠퍼스 배경 (bg_1 ~ bg_8)
    public static readonly string[] Backgrounds =
    {
        "1. 앙뜨레프레너관.png", "2. 유니토피아 좌측면.png", "2. 유니토피아 정면.png", "3. 유니토 앞 건물.png",
        "4. 공학관 20.9.png", "5. 향1-도서관.png", "6. 도서관 옆길 내리막.png", "7. 대학본관.png",
    };

    [System.Serializable] class Db { public Sheet[] sheets; }
    [System.Serializable] class Sheet { public string key, file; public bool chroma, jpeg; }

    [MenuItem("SCH/Bake Art")]
    public static void Bake()
    {
        foreach (var d in new[] { "chr", "ui", "bg" }) Directory.CreateDirectory(Dst + d);

        // 1) 캐릭터·이펙트 시트: 마젠타 배경 제거 (프레임 좌표는 frames.json)
        var db = JsonUtility.FromJson<Db>(File.ReadAllText(Dst + "frames.json"));
        Texture2D throwRun = null;
        foreach (var s in db.sheets)
        {
            var t = Load(s.file);
            if (s.chroma) KeyMagenta(t, s.jpeg);
            Save(t, "chr/" + s.key);
            if (s.key == "player_throw_run") throwRun = t;
        }
        Save(Crop(throwRun, 1585, 810, 60, 60), "ui/paper_ball"); // 던지는 프레임 속 종이공

        // 2) 메인 메뉴 버튼 (위: 게임시작/설정, 아래: 도감/종료 — 아래 라벨 알약은 제외)
        var menu = Load("ui/buttons/ui_btn_main_menu_sheet.png");
        Save(Crop(menu, 15, 140, 437, 195), "ui/btn_start");
        Save(Crop(menu, 455, 140, 450, 195), "ui/btn_start_pressed");
        Save(Crop(menu, 910, 500, 435, 195), "ui/btn_exit");
        Save(Crop(menu, 1345, 500, 425, 195), "ui/btn_exit_pressed");
        FillRows(menu, 205, 545, 147, 100, 353);           // '도감' 글자 지우기 → 글자는 코드에서 얹음
        Save(Crop(menu, 15, 500, 445, 195), "ui/btn_blue_book");
        CoverIcon(menu, 82, 562, 88, 74);                   // 책 아이콘 지우기
        Save(Crop(menu, 15, 500, 445, 195), "ui/btn_blue_blank");

        // 3) 결과 화면 버튼
        var result = Load("ui/buttons/ui_btn_result_sheet.png");
        Save(Crop(result, 30, 160, 740, 290, 60), "ui/btn_retry");
        Save(Crop(result, 780, 160, 750, 300, 60), "ui/btn_retry_pressed");
        Save(Crop(result, 30, 570, 740, 268, 60), "ui/btn_menu");
        Save(Crop(result, 780, 550, 750, 300, 60), "ui/btn_menu_pressed");

        // 4) HUD: 커피 체력(5칸) / 진행 바 / 트로피 배너
        var cup = Load("ui/hud/ui_hud_hp_coffee_sheet.png");
        Save(Crop(cup, 55, 35, 260, 450, 70), "ui/hp_5");
        int[] cupX = { 65, 370, 665, 952, 1238 };
        for (int i = 0; i < 5; i++) Save(Crop(cup, cupX[i], 568, 240, 358, 70), "ui/hp_" + (4 - i));

        var bar = Load("ui/hud/ui_hud_progress_bar.png");
        Recolor(bar, 240, 345, 1360, 66, new Vector2Int(1700, 377)); // 그려진 노란 채움 → 빈 트랙
        Save(Crop(bar, 0, 240, bar.width, 240, 0, false), "ui/progress_bar");

        var banner = Load("ui/panels/ui_banner_trophy.png");
        Save(Crop(banner, 60, 170, 1660, 520, 100), "ui/banner_trophy");
        Save(Crop(banner, 100, 190, 580, 480, 100), "ui/trophy");

        var panel = Load("ui/panels/ui_panel_ranking.png");
        RemoveCheckerboard(panel);
        Save(Crop(panel, 0, 0, panel.width, panel.height), "ui/panel_ranking");

        Save(Crop(Load("ui/title/ui_title_logo.png"), 0, 0, 2695, 1696, 10), "ui/logo");

        // 5) 배경 (불투명, 그대로 복사)
        File.Copy(Src + "ui/main_menu/bg_main_menu_campus_gate.png", Dst + "ui/menu_bg.png", true);
        for (int i = 0; i < Backgrounds.Length; i++)
            File.Copy(Src + "배경/" + Backgrounds[i], Dst + "bg/bg_" + (i + 1) + ".png", true);

        AssetDatabase.Refresh();
        Debug.Log("[ArtBaker] 완료");
    }

    // Resources/Art 아래 이미지는 모두 스프라이트로, 시트 좌표가 어긋나지 않게 원본 크기 유지
    class Importer : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Dst)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 4096;
        }
    }

    // ---------- 이미지 처리 ----------

    static Texture2D Load(string rel)
    {
        var raw = new Texture2D(2, 2);
        raw.LoadImage(File.ReadAllBytes(Src + rel));
        var t = new Texture2D(raw.width, raw.height, TextureFormat.RGBA32, false);
        t.SetPixels32(raw.GetPixels32());
        t.Apply();
        Object.DestroyImmediate(raw);
        return t;
    }

    static void Save(Texture2D t, string name) => File.WriteAllBytes(Dst + name + ".png", t.EncodeToPNG());

    // (x, y)는 원본 이미지 좌상단 기준 픽셀 좌표. alphaFloor 미만 알파는 버리고(희미한 글로우/노이즈), 남은 그림에 맞춰 자른다.
    static Texture2D Crop(Texture2D src, int x, int y, int w, int h, byte alphaFloor = 0, bool trim = true)
    {
        var p = src.GetPixels32();
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int yy = y; yy < y + h; yy++)
        for (int xx = x; xx < x + w; xx++)
        {
            var c = p[(src.height - 1 - yy) * src.width + xx];
            if (c.a <= alphaFloor) continue;
            minX = Mathf.Min(minX, xx); maxX = Mathf.Max(maxX, xx);
            minY = Mathf.Min(minY, yy); maxY = Mathf.Max(maxY, yy);
        }
        if (!trim || maxX < 0) { minX = x; minY = y; maxX = x + w - 1; maxY = y + h - 1; }

        int ow = maxX - minX + 1, oh = maxY - minY + 1;
        var o = new Color32[ow * oh];
        for (int yy = 0; yy < oh; yy++)
        for (int xx = 0; xx < ow; xx++)
        {
            var c = p[(src.height - 1 - (maxY - yy)) * src.width + minX + xx];
            if (c.a <= alphaFloor) c = new Color32(0, 0, 0, 0);
            o[yy * ow + xx] = c;
        }
        var t = new Texture2D(ow, oh, TextureFormat.RGBA32, false);
        t.SetPixels32(o);
        t.Apply();
        return t;
    }

    // 마젠타(≈#F80AF9) 배경을 투명으로. 경계 픽셀은 섞인 배경색을 빼서 원래 색을 복원한다.
    static void KeyMagenta(Texture2D t, bool jpeg)
    {
        var p = t.GetPixels32();
        for (int i = 0; i < p.Length; i++)
        {
            var c = p[i];
            int m = Mathf.Min(c.r, c.b) - c.g;              // 마젠타 정도
            float a = 1f - Mathf.Clamp01((m - 40) / 110f);  // m<=40 불투명, m>=150 투명
            if (a <= 0.05f) { p[i] = new Color32(0, 0, 0, 0); continue; }
            if (a < 1f)
            {
                float k = 1f - a;
                c.r = (byte)Mathf.Clamp((c.r - k * 248f) / a, 0, 255);
                c.g = (byte)Mathf.Clamp((c.g - k * 10f) / a, 0, 255);
                c.b = (byte)Mathf.Clamp((c.b - k * 249f) / a, 0, 255);
            }
            c.a = (byte)(a * 255);
            p[i] = c;
        }
        if (jpeg) Erode(p, t.width, t.height); // JPEG 경계 노이즈 1px 정리
        t.SetPixels32(p);
        t.Apply();
    }

    static void Erode(Color32[] p, int w, int h)
    {
        var a = new byte[p.Length];
        for (int i = 0; i < p.Length; i++) a[i] = p[i].a;
        for (int y = 1; y < h - 1; y++)
        for (int x = 1; x < w - 1; x++)
        {
            int i = y * w + x;
            p[i].a = (byte)Mathf.Min(a[i], Mathf.Min(Mathf.Min(a[i - 1], a[i + 1]), Mathf.Min(a[i - w], a[i + w])));
        }
    }

    // 회색 체크무늬(가짜 투명)를 테두리부터 채우기로 지운다.
    static void RemoveCheckerboard(Texture2D t)
    {
        var p = t.GetPixels32();
        int w = t.width, h = t.height;
        var seen = new bool[p.Length];
        var q = new Queue<int>();
        for (int x = 0; x < w; x++) { q.Enqueue(x); q.Enqueue((h - 1) * w + x); }
        for (int y = 0; y < h; y++) { q.Enqueue(y * w); q.Enqueue(y * w + w - 1); }
        while (q.Count > 0)
        {
            int i = q.Dequeue();
            if (seen[i]) continue;
            seen[i] = true;
            var c = p[i];
            int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            if (max - min > 24 || min < 185) continue; // 밝은 무채색만 배경으로 본다
            p[i] = new Color32(0, 0, 0, 0);
            int x = i % w, y = i / w;
            if (x > 0) q.Enqueue(i - 1);
            if (x < w - 1) q.Enqueue(i + 1);
            if (y > 0) q.Enqueue(i - w);
            if (y < h - 1) q.Enqueue(i + w);
        }
        t.SetPixels32(p);
        t.Apply();
    }

    // 사각형(좌상단 기준) 안을 각 행마다 refX 위치의 색으로 덮는다 — 버튼 글자 지우기용
    static void FillRows(Texture2D t, int x, int y, int w, int h, int refX)
    {
        for (int yy = y; yy < y + h; yy++)
        {
            int row = t.height - 1 - yy;
            var c = t.GetPixel(refX, row);
            for (int xx = x; xx < x + w; xx++) t.SetPixel(xx, row, c);
        }
        t.Apply();
    }

    // 사각형 안의 밝은 픽셀(아이콘)을 주변 어두운 바탕의 평균색으로 덮는다
    static void CoverIcon(Texture2D t, int x, int y, int w, int h)
    {
        Color sum = Color.clear; int n = 0;
        for (int yy = y; yy < y + h; yy++)
        for (int xx = x; xx < x + w; xx++)
        {
            var c = t.GetPixel(xx, t.height - 1 - yy);
            if (c.r + c.g + c.b < 0.85f && c.b > c.r) { sum += c; n++; }
        }
        var fill = n > 0 ? sum / n : new Color(0.08f, 0.17f, 0.4f, 1f);
        fill.a = 1f;
        for (int yy = y; yy < y + h; yy++)
        for (int xx = x; xx < x + w; xx++)
        {
            var c = t.GetPixel(xx, t.height - 1 - yy);
            if (c.r + c.g + c.b > 0.85f) t.SetPixel(xx, t.height - 1 - yy, fill);
        }
        t.Apply();
    }

    // 사각형 안의 노란/주황 계열 픽셀을 refPx 위치 색으로 바꾼다 — 진행 바의 채움 지우기용
    static void Recolor(Texture2D t, int x, int y, int w, int h, Vector2Int refPx)
    {
        var fill = t.GetPixel(refPx.x, t.height - 1 - refPx.y);
        for (int yy = y; yy < y + h; yy++)
        for (int xx = x; xx < x + w; xx++)
        {
            var c = t.GetPixel(xx, t.height - 1 - yy);
            if (c.a > 0.5f && c.r > 0.47f && c.r > c.b + 0.16f) t.SetPixel(xx, t.height - 1 - yy, fill);
        }
        t.Apply();
    }
}
