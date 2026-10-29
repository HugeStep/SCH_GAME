using System.Collections.Generic;
using UnityEngine;

// Resources/Art 에 구워둔 이미지(SCH > Bake Art)에서 스프라이트를 꺼내 쓴다.
public static class Art
{
    [System.Serializable] class Db { public Sheet[] sheets; }
    [System.Serializable] class Sheet { public string key; public int w, h; public Anim[] anims; }
    [System.Serializable] class Anim { public string name; public int[] rects, feet; public float ppu; } // x,y,w,h 반복 (원본 좌상단 기준) / feet: 프레임별 발 x (그림이 한쪽으로 넓은 프레임용, 없으면 가운데) / ppu: 시트 안에서 이 줄만 크기가 다르게 그려졌을 때 (없으면 시트 값)

    // 시트마다 그림 크기가 달라서, 화면에서 캐릭터 키가 비슷해지도록 맞춘 픽셀/유닛 값 (튜닝용)
    static readonly Dictionary<string, float> Ppu = new()
    {
        { "player_base", 117 }, { "player_throw_stand", 283 }, { "player_throw_run", 224 },
        { "professor_base", 111 }, { "grad_student_base", 142 }, { "football_base", 133 }, { "chemistry_base", 149 },
        { "mech_eng_base", 130 }, { "drone_pilot", 240 }, { "drone_unit", 245 },
        { "computer_base", 74 }, { "computer_throw", 146 },
        { "flask", 400 }, { "fx_common", 300 }, { "macbook", 185 }, { "keyboard", 185 },
    };

    static Db db;
    static readonly Dictionary<string, Sprite[]> cache = new();

    // 시트의 애니메이션 프레임들. 캐릭터는 발밑(하단 중앙), 이펙트는 가운데가 기준점. 없는 애니메이션이면 null.
    public static Sprite[] Frames(string sheet, string anim)
    {
        string id = sheet + "/" + anim;
        if (cache.TryGetValue(id, out var frames)) return frames;

        db ??= JsonUtility.FromJson<Db>(Resources.Load<TextAsset>("Art/frames").text);
        var s = System.Array.Find(db.sheets, x => x.key == sheet);
        var a = System.Array.Find(s.anims, x => x.name == anim);
        if (a == null) return cache[id] = null;

        var tex = Resources.Load<Texture2D>("Art/chr/" + sheet);
        float k = tex.width / (float)s.w; // 임포트 시 축소됐을 경우 대비
        bool center = sheet is "flask" or "fx_common" or "drone_unit" or "macbook" or "keyboard";
        frames = new Sprite[a.rects.Length / 4];
        for (int i = 0; i < frames.Length; i++)
        {
            int x = a.rects[i * 4], y = a.rects[i * 4 + 1], w = a.rects[i * 4 + 2], h = a.rects[i * 4 + 3];
            var pivot = center ? new Vector2(0.5f, 0.5f) : new Vector2(a.feet is { Length: > 0 } ? (a.feet[i] - x) / (float)w : 0.5f, 0f);
            frames[i] = Sprite.Create(tex, new Rect(x * k, (s.h - y - h) * k, w * k, h * k), pivot, (a.ppu > 0 ? a.ppu : Ppu[sheet]) * k);
        }
        return cache[id] = frames;
    }

    public static Sprite Ui(string name) => Resources.Load<Sprite>("Art/ui/" + name);
    public static Sprite Bg(int i) => Bg("bg_" + i);
    public static Sprite Bg(string name) => Resources.Load<Sprite>("Art/bg/" + name);
    public static Sprite Intro(string name) => Resources.Load<Sprite>("Art/intro/" + name);
}
