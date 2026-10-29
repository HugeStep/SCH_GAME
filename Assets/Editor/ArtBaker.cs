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

    // 진행 순서대로 이어 붙일 캠퍼스 배경. bg_1 ~ bg_4 는 한 장짜리 파노라마를 건물 사이 x 좌표에서 잘라 쓴다
    const string Panorama = "1-4. 캠퍼스 파노라마.png";
    static readonly int[] PanoramaCuts = { 0, 395, 905, 1500, 2000 };
    // bg_5 (화면 전환 전 마지막 구역)
    static readonly string[] Backgrounds = { "4. 공학관 20.9.png" };
    // 화면 전환 뒤: 끝없이 반복되는 기본 도로 배경(road) + 한 장 걸러 하나씩 얹는 건물(bld_1 ~, 순서는 GameManager.Buildings 와 같음).
    // 건물 원본은 검은 배경. cropH = 위에서부터 남길 높이(오른쪽 아래 워터마크 별 잘라냄), fade = 그림 좌우 끝에서 잘린 나무를 흐리게
    const string Road = "8. 기본 도로 배경.png";
    static readonly (string file, int cropH, bool fade)[] Buildings =
    {
        ("1. 대학본관.png", 300, false), ("2. A01.png", 455, false), ("3. B27.png", 467, true), ("4. SCHU 가림막.png", 450, false),
    };

    [System.Serializable] class Db { public Sheet[] sheets; }
    [System.Serializable] class Sheet { public string key, file; public bool chroma, jpeg, white; public Anim[] anims; }
    [System.Serializable] class Anim { public string name; public int[] rects; }

    [MenuItem("SCH/Bake Art")]
    public static void Bake()
    {
        foreach (var d in new[] { "chr", "ui", "bg" }) Directory.CreateDirectory(Dst + d);

        // 1) 캐릭터·이펙트 시트: 마젠타 배경 제거 (프레임 좌표는 frames.json)
        var db = JsonUtility.FromJson<Db>(File.ReadAllText(Dst + "frames.json"));
        Texture2D throwRun = null, drone = null;
        foreach (var s in db.sheets)
        {
            var t = Load(s.file);
            if (s.chroma) KeyMagenta(t, s.jpeg);
            if (s.white) KeyWhite(t);
            if (s.key == "computer_base") ClearFaint(t, 40); // webp 원본의 희미한 알파 얼룩
            // (computer_throw 원본은 받은 시트에서 프레임끼리 겹치지 않게 다시 배치해 둔 것)
            if (s.key == "mech_eng_base") Untangle(t, System.Array.Find(s.anims, a => a.name == "attack").rects); // 로봇팔 프레임끼리 겹침
            Save(t, "chr/" + s.key);
            if (s.key == "player_throw_run") throwRun = t;
            if (s.key == "drone_unit") drone = t;
        }
        Save(Crop(throwRun, 1585, 810, 60, 60), "ui/paper_ball"); // 던지는 프레임 속 종이공
        Save(Crop(drone, 874, 1364, 56, 54, 60), "ui/energy_ball"); // 발사 프레임 옆 에너지탄

        // 2) 메인 메뉴 버튼 (위: 게임시작/설정, 아래: 도감/종료 — 아래 라벨 알약은 제외)
        var menu = Load("ui/buttons/ui_btn_main_menu_sheet.png");
        Save(Crop(menu, 15, 140, 437, 195), "ui/btn_start");
        Save(Crop(menu, 455, 140, 450, 195), "ui/btn_start_pressed");
        Save(Crop(menu, 910, 500, 435, 195), "ui/btn_exit");
        Save(Crop(menu, 1345, 500, 425, 195), "ui/btn_exit_pressed");
        Save(Crop(menu, 910, 140, 435, 195), "ui/btn_settings");
        Save(Crop(menu, 1345, 140, 425, 195), "ui/btn_settings_pressed");
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
        // 랭킹 요소 시트(투명 배경): 1~3위 배지, 4위 이하용 빈 배지, 줄 배경
        var rk = Load("ui/panels/ui_ranking_elements.png");
        Save(Crop(rk, 902, 402, 118, 120, 60), "ui/rank_1");
        Save(Crop(rk, 1027, 415, 118, 105, 60), "ui/rank_2");
        Save(Crop(rk, 1152, 415, 118, 105, 60), "ui/rank_3");
        FillRows(rk, 1309, 453, 42, 44, 1305);           // '4' 글자 지우기 → 번호는 코드에서 얹음
        Save(Crop(rk, 1278, 425, 102, 95, 60), "ui/rank_n");
        FillRows(rk, 995, 548, 477, 154, 1060);          // 1·2위 줄의 아바타·이름·점수 지우기 (배지에 가려지는 왼쪽 끝은 잘라냄)
        Save(Crop(rk, 995, 548, 515, 79, 60), "ui/rank_bar_gold");
        Save(Crop(rk, 995, 629, 515, 71, 60), "ui/rank_bar_blue");
        Save(Crop(rk, 465, 925, 63, 75, 60), "ui/rank_prev");
        Save(Crop(rk, 545, 925, 63, 75, 60), "ui/rank_next");

        var over = Load("ui/panels/ui_panel_gameover.png"); // 체크무늬 배경은 미리 지워 둔 것. 결과 화면이 그림 좌표 비율로 글자를 얹으니 자르지 않음
        Save(Crop(over, 0, 0, over.width, over.height, 0, false), "ui/panel_gameover");

        Save(Crop(Load("ui/title/ui_title_logo.png"), 0, 0, 2695, 1696, 10), "ui/logo");

        // 5) 배경 (불투명, 그대로 복사)
        File.Copy(Src + "ui/main_menu/bg_main_menu_campus_gate.png", Dst + "ui/menu_bg.png", true);
        var pano = Load("배경/" + Panorama);
        int n = PanoramaCuts.Length - 1;
        for (int i = 0; i < n; i++)
            Save(Crop(pano, PanoramaCuts[i], 0, PanoramaCuts[i + 1] - PanoramaCuts[i], pano.height, 0, false), "bg/bg_" + (i + 1));
        for (int i = 0; i < Backgrounds.Length; i++)
            File.Copy(Src + "배경/" + Backgrounds[i], Dst + "bg/bg_" + (n + i + 1) + ".png", true);
        BakeBuildings();

        BakeIntro();
        AssetDatabase.Refresh();
        Debug.Log("[ArtBaker] 완료");
    }

    public static void BakeBuildings()
    {
        File.Copy(Src + "배경/" + Road, Dst + "bg/road.png", true);
        for (int i = 0; i < Buildings.Length; i++)
        {
            var (file, cropH, fade) = Buildings[i];
            var t = Load("배경/건물/" + file);
            KeyBlack(t);
            t = Crop(t, 0, 0, t.width, cropH);
            if (fade) FadeSides(t, 90);
            Save(t, "bg/bld_" + (i + 1));
        }
    }

    // 게임 시작 컷신 (Intro.cs): 컷 1~5 + 컷마다 얹히는 안쪽 카드
    public static void BakeIntro()
    {
        Directory.CreateDirectory(Dst + "intro");
        const string In = "플레이 인트로/";
        for (int i = 1; i <= 4; i++) File.Copy(Src + In + $"인트로 {i}.jpg", Dst + $"intro/cut_{i}.jpg", true);
        Save(Crop(Load(In + "인트로 1 안쪽카드.png"), 0, 0, 1024, 572, 8), "intro/card_1"); // 투명 배경 → 카드 그림만
        Save(GlowCard(Load(In + "인트로 2 안쪽 카드 .png"), 600, new Vector2(810, 230)), "intro/card_2");
        Save(Crop(Load(In + "인트로 3 안쪽카드.jpg"), 160, 0, 575, 572, 0, false), "intro/card_3"); // 얼굴 + 내민 손
        Save(Crop(Load(In + "인트로 4 안쪽 카드.jpg"), 560, 0, 355, 572, 0, false), "intro/card_4"); // 얼굴 + 땀방울
        var card5 = Load(In + "인트로 5 안쪽카드.png");
        Save(card5, "intro/card_5"); // 컷 5와 같은 크기·위치 그대로 겹쳐 얹는다
        Save(RemoveCard(Load(In + "인트로 5.png"), card5), "intro/cut_5");
        Save(FocusLines(1024), "intro/lines");
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
            if (assetPath.StartsWith(Dst + "intro/")) ti.textureCompression = TextureImporterCompression.Uncompressed; // 화면 가득 확대되는 그림이라 압축 얼룩 방지
            if (assetPath.Contains("rank_bar")) ti.spriteBorder = new Vector4(12, 0, 44, 0); // 줄 길이에 맞춰 늘릴 때 오른쪽 뾰족한 끝은 유지
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

    // 한 줄에 놓인 프레임 사각형끼리 겹칠 때, 이웃 프레임에서 삐져 들어온 그림을 지운다.
    // 이어진 덩어리마다 중심 x가 들어 있는 프레임이 주인이고, 주인 아닌 프레임 사각형 안에 있는 픽셀은 투명으로.
    static void Untangle(Texture2D t, int[] rects)
    {
        var p = t.GetPixels32();
        int w = t.width, h = t.height, n = rects.Length / 4;
        int Idx(int x, int y) => (h - 1 - y) * w + x; // (x, y)는 좌상단 기준
        int FrameAt(int x, int y, int skip)
        {
            for (int f = 0; f < n; f++)
                if (f != skip && x >= rects[f * 4] && x < rects[f * 4] + rects[f * 4 + 2] && y >= rects[f * 4 + 1] && y < rects[f * 4 + 1] + rects[f * 4 + 3]) return f;
            return -1;
        }
        int y0 = rects[1], y1 = rects[1] + rects[3], midY = (y0 + y1) / 2;
        var seen = new bool[p.Length];
        var blob = new List<(int x, int y)>();
        var stack = new Stack<(int x, int y)>();
        for (int sy = y0; sy < y1; sy++)
        for (int sx = 0; sx < w; sx++)
        {
            if (seen[Idx(sx, sy)] || p[Idx(sx, sy)].a == 0) continue;
            blob.Clear();
            int minX = sx, maxX = sx;
            seen[Idx(sx, sy)] = true;
            stack.Push((sx, sy));
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                blob.Add((x, y));
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= w || ny < y0 || ny >= y1 || seen[Idx(nx, ny)] || p[Idx(nx, ny)].a == 0) continue;
                    seen[Idx(nx, ny)] = true;
                    stack.Push((nx, ny));
                }
            }
            int owner = FrameAt((minX + maxX) / 2, midY, -1); // 쓰지 않는 그림이면 -1 → 어느 프레임에도 남기지 않음
            foreach (var (x, y) in blob)
                if (FrameAt(x, y, owner) >= 0) p[Idx(x, y)] = new Color32(0, 0, 0, 0);
        }
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

    static void ClearFaint(Texture2D t, byte floor)
    {
        var p = t.GetPixels32();
        for (int i = 0; i < p.Length; i++) if (p[i].a <= floor) p[i] = new Color32(0, 0, 0, 0);
        t.SetPixels32(p);
        t.Apply();
    }

    // 배경을 테두리부터 채우기로 지운다. key 가 배경 픽셀이면 바꿀 색을, 그림이면 null 을 돌려준다 (그림에서 멈춤).
    static void FloodKey(Texture2D t, System.Func<Color32, Color32?> key)
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
            var c = key(p[i]);
            if (c == null) continue;
            p[i] = c.Value;
            int x = i % w, y = i / w;
            if (x > 0) q.Enqueue(i - 1);
            if (x < w - 1) q.Enqueue(i + 1);
            if (y > 0) q.Enqueue(i - w);
            if (y < h - 1) q.Enqueue(i + w);
        }
        t.SetPixels32(p);
        t.Apply();
    }

    // 흰 배경을 지운다 (흰 티셔츠는 윤곽선에 막혀 남는다).
    // 밝을수록 투명하게, 경계 픽셀은 섞인 흰색을 빼서 원래 색을 복원한다.
    static void KeyWhite(Texture2D t) => FloodKey(t, c =>
    {
        int min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        if (min < 200) return null; // 윤곽선에서 멈춘다
        float a = Mathf.Clamp01((250 - min) / 50f);
        if (a <= 0.05f) return new Color32(0, 0, 0, 0);
        float k = (1f - a) * 255f;
        return new Color32((byte)Mathf.Clamp((c.r - k) / a, 0, 255), (byte)Mathf.Clamp((c.g - k) / a, 0, 255), (byte)Mathf.Clamp((c.b - k) / a, 0, 255), (byte)(a * 255));
    });

    // 검은 배경을 지운다 (건물 그림). 안쪽의 어두운 창문·그늘은 밝은 부분에 막혀 남고, 경계의 어두운 픽셀은 반투명으로.
    static void KeyBlack(Texture2D t) => FloodKey(t, c =>
    {
        int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (max > 36) return null;
        float a = Mathf.Clamp01((max - 12) / 24f);
        return a <= 0.05f ? new Color32(0, 0, 0, 0) : new Color32(c.r, c.g, c.b, (byte)(a * 255));
    });

    // 회색 체크무늬(가짜 투명)를 지운다. 밝은 무채색만 배경으로 본다
    static void RemoveCheckerboard(Texture2D t) => FloodKey(t, c =>
    {
        int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        return max - min > 24 || min < 185 ? null : new Color32(0, 0, 0, 0);
    });

    // 그림 좌우 끝 px 픽셀을 점점 투명하게 (가장자리에서 잘린 나무가 뚝 끊겨 보이지 않게)
    static void FadeSides(Texture2D t, int px)
    {
        var p = t.GetPixels32();
        int w = t.width;
        for (int i = 0; i < p.Length; i++) p[i].a = (byte)(p[i].a * Mathf.Clamp01(Mathf.Min(i % w, w - 1 - i % w) / (float)px));
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

    // 투명 배경 인물 그림의 x0 ~ 오른쪽 끝을 따뜻한 햇살 배경 위에 얹은 카드 (center = 빛 중심, 좌상단 기준)
    static Texture2D GlowCard(Texture2D src, int x0, Vector2 center)
    {
        int sw = src.width, w = sw - x0, h = src.height;
        var p = src.GetPixels32();
        var o = new Color32[w * h];
        Color inner = new(1f, 0.97f, 0.86f), outer = new(0.97f, 0.74f, 0.33f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var d = new Vector2(x0 + x - center.x, y - center.y);
            float r = Mathf.Clamp01(d.magnitude / 560f);
            var bg = Color.Lerp(inner, outer, r * r);
            float ray = Mathf.SmoothStep(0, 1, (Mathf.Sin(Mathf.Atan2(d.y, d.x) * 22f) - 0.2f) / 0.4f); // 22줄 햇살
            bg = Color.Lerp(bg, Color.white, 0.45f * ray * (1 - r));
            Color c = p[(h - 1 - y) * sw + x0 + x];
            var m = Color.Lerp(bg, c, c.a);
            m.a = 1;
            o[(h - 1 - y) * w + x] = m;
        }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels32(o);
        t.Apply();
        return t;
    }

    // 컷 그림에 이미 그려진 카드(card 의 불투명 부분, 같은 크기·위치)를 지운다.
    // 카드 자리는 1/4 크기에서 주변 색으로 메우고, x 620 부터 오른쪽은 통째로 흐리고 어둡게 해서 메운 자국이 안 보이게 한다.
    static Texture2D RemoveCard(Texture2D cut, Texture2D card)
    {
        const int S = 4;
        int w = cut.width, h = cut.height, sw = w / S, sh = h / S;
        var p = cut.GetPixels32();
        var c = card.GetPixels32();
        var small = new Color[sw * sh];
        var hole = new bool[sw * sh];
        for (int y = 0; y < sh; y++)
        for (int x = 0; x < sw; x++)
        {
            Color sum = Color.clear;
            for (int yy = 0; yy < S; yy++)
            for (int xx = 0; xx < S; xx++)
            {
                int i = (y * S + yy) * w + x * S + xx;
                if (c[i].a > 8) hole[y * sw + x] = true;
                sum += p[i];
            }
            small[y * sw + x] = sum / (S * S);
        }

        // 카드 가장자리 번짐까지 지우게 한 칸 넓힌 구멍
        var mask = new Color[sw * sh];
        var known = new bool[sw * sh];
        for (int y = 0; y < sh; y++)
        for (int x = 0; x < sw; x++)
        {
            bool near = false;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < sw && ny >= 0 && ny < sh && hole[ny * sw + nx]) near = true;
            }
            mask[y * sw + x] = near ? Color.white : Color.clear;
            known[y * sw + x] = !near;
        }
        // 바깥부터 한 겹씩 이웃 평균으로 메운다
        for (bool left = true; left;)
        {
            left = false;
            var next = (bool[])known.Clone();
            for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
            {
                if (known[y * sw + x]) continue;
                Color sum = Color.clear; int k = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && nx < sw && ny >= 0 && ny < sh && known[ny * sw + nx]) { sum += small[ny * sw + nx]; k++; }
                }
                if (k > 0) { small[y * sw + x] = sum / k; next[y * sw + x] = true; }
                else left = true;
            }
            known = next;
        }
        for (int i = 0; i < 3; i++) BoxBlur(small, sw, sh, 5);
        BoxBlur(mask, sw, sh, 1);

        var dark = new Color(0.5f, 0.55f, 0.68f);
        var o = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = (x + 0.5f) / S - 0.5f, v = (y + 0.5f) / S - 0.5f;
            float k = Mathf.Max(Mathf.SmoothStep(0, 1, (x - 620) / 100f), Bilinear(mask, sw, sh, u, v).r);
            var m = Color.Lerp(p[y * w + x], Bilinear(small, sw, sh, u, v) * dark, k);
            m.a = 1;
            o[y * w + x] = m;
        }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels32(o);
        t.Apply();
        return t;
    }

    // 만화 집중선 (흰 선, 투명 배경): 가운데는 비우고 바깥으로 갈수록 굵어진다. 왼쪽(인물 쪽)은 비워 둔다.
    static Texture2D FocusLines(int size)
    {
        const int N = 110;
        var rnd = new System.Random(3);
        var ang = new float[N]; var wid = new float[N]; var from = new float[N];
        for (int n = 0; n < N;)
        {
            float a = (float)rnd.NextDouble() * 360f;
            if (Mathf.Abs(Mathf.DeltaAngle(a, 180)) < 55) continue;
            ang[n] = a;
            wid[n] = (0.35f + 0.9f * (float)rnd.NextDouble()) * Mathf.Deg2Rad; // 반폭 (각도)
            from[n++] = 0.55f + 0.3f * (float)rnd.NextDouble();               // 선이 시작하는 반지름
        }
        float c = size / 2f;
        var o = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - c) / c, dy = (y + 0.5f - c) / c, r = Mathf.Sqrt(dx * dx + dy * dy);
            if (r < 0.55f || r >= 1f) continue;
            float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, best = 0;
            for (int i = 0; i < N; i++)
            {
                if (r <= from[i]) continue;
                float half = wid[i] * Mathf.Clamp01((r - from[i]) / 0.25f) * r * c;      // 픽셀 반폭 (뾰족하게 시작)
                float dist = Mathf.Abs(Mathf.DeltaAngle(a, ang[i])) * Mathf.Deg2Rad * r * c;
                best = Mathf.Max(best, Mathf.Clamp01(half - dist + 0.5f));
            }
            best *= Mathf.Clamp01((1f - r) / 0.08f); // 네모 끝이 안 보이게
            o[y * size + x] = new Color32(255, 255, 255, (byte)(best * 255));
        }
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.SetPixels32(o);
        t.Apply();
        return t;
    }

    // 가로 → 세로 상자 흐림 (가장자리는 끝 칸 반복)
    static void BoxBlur(Color[] a, int w, int h, int r)
    {
        var t = new Color[a.Length];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Color s = Color.clear;
            for (int k = -r; k <= r; k++) s += a[y * w + Mathf.Clamp(x + k, 0, w - 1)];
            t[y * w + x] = s / (2 * r + 1);
        }
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Color s = Color.clear;
            for (int k = -r; k <= r; k++) s += t[Mathf.Clamp(y + k, 0, h - 1) * w + x];
            a[y * w + x] = s / (2 * r + 1);
        }
    }

    static Color Bilinear(Color[] a, int w, int h, float u, float v)
    {
        u = Mathf.Clamp(u, 0, w - 1); v = Mathf.Clamp(v, 0, h - 1);
        int x = Mathf.Min((int)u, w - 2), y = Mathf.Min((int)v, h - 2);
        float fx = u - x, fy = v - y;
        return Color.Lerp(Color.Lerp(a[y * w + x], a[y * w + x + 1], fx), Color.Lerp(a[(y + 1) * w + x], a[(y + 1) * w + x + 1], fx), fy);
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
