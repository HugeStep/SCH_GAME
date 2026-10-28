using System.Collections.Generic;
using UnityEngine;

// 효과음: Resources/Audio/sfx_<이름>. 씬에 AudioListener가 없어서 리스너도 여기서 하나 만들어 계속 유지한다
public static class Sfx
{
    static AudioSource src;
    static readonly Dictionary<string, AudioClip> clips = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        var go = new GameObject("Audio");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<AudioListener>();
        src = go.AddComponent<AudioSource>();
    }

    // 설정 화면의 켜기/끄기 (저장됨)
    public static bool On { get => PlayerPrefs.GetInt("sfx", 1) == 1; set => PlayerPrefs.SetInt("sfx", value ? 1 : 0); }
    public static bool BgmOn { get => PlayerPrefs.GetInt("bgm", 1) == 1; set => PlayerPrefs.SetInt("bgm", value ? 1 : 0); }

    public static void Play(string name, float volume = 1f)
    {
        if (!On) return;
        if (!clips.TryGetValue(name, out var clip)) clips[name] = clip = Resources.Load<AudioClip>("Audio/sfx_" + name);
        if (clip) src.PlayOneShot(clip, volume);
    }
}
