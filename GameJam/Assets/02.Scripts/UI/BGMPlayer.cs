using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 따라 BGM 자동 재생. DontDestroyOnLoad 싱글톤.
/// Resources/BGM/ 의 mp3 자동 로드 또는 인스펙터 직접 wire.
/// </summary>
public class BGMPlayer : MonoBehaviour
{
    public static BGMPlayer Instance;

    [Header("Clip (또는 Resources 자동)")]
    public AudioClip lobbyBgm;
    public AudioClip waitBgm;      // AScene_Wait 전용 (사용자가 별도 BGM 줄 예정)
    public AudioClip fightBgm;
    public AudioClip banPickBgm;

    AudioSource _src;
    string _currentScene = "";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _src = gameObject.AddComponent<AudioSource>();
        _src.loop = true;
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;
        // 첫 진입 시 PlayerPrefs 의 0 값을 그대로 안 쓰게 fallback (사용자가 슬라이더 한 번 안 만지면 PlayerPrefs 가 0 일 수도)
        if (VolumeSettings.BgmVolume < 0.05f) VolumeSettings.BgmVolume = 0.6f;
        _src.volume = VolumeSettings.BgmVolume;

        // Resources 자동 로드 — OnSceneLoaded 전에
        if (lobbyBgm == null) lobbyBgm = Resources.Load<AudioClip>("BGM/Lobby_bgm") ?? Resources.Load<AudioClip>("BGM/LobbyBGM") ?? Resources.Load<AudioClip>("BGM/MainBGM");
        if (waitBgm == null)  waitBgm  = Resources.Load<AudioClip>("BGM/Wait_bgm") ?? Resources.Load<AudioClip>("BGM/WaitBgm") ?? Resources.Load<AudioClip>("BGM/Wait_Bgm") ?? Resources.Load<AudioClip>("BGM/SelectBGM");
        if (fightBgm == null) fightBgm = Resources.Load<AudioClip>("BGM/Fight_Bgm") ?? Resources.Load<AudioClip>("BGM/FightBGM");
        if (banPickBgm == null) banPickBgm = Resources.Load<AudioClip>("BGM/ban_bgm") ?? Resources.Load<AudioClip>("BGM/SelectBGM");

        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (scene.name == _currentScene) return;
        _currentScene = scene.name;

        AudioClip clip = null;
        if (scene.name == "AScene_Wait")
            clip = waitBgm != null ? waitBgm : lobbyBgm; // wait 전용 BGM 없으면 lobby 로 fallback
        else if (scene.name == "Lobby" || scene.name == "AScene")
            clip = lobbyBgm;
        else if (scene.name == "InGame")
            clip = fightBgm;
        else if (scene.name == "BanPick" || scene.name == "AScene_BanPick")
            clip = banPickBgm;

        Debug.Log($"[BGM] 씬 '{scene.name}' 로드 — clip:{(clip != null ? clip.name : "null")}, lobbyBgm:{(lobbyBgm != null ? "ok" : "null")}, fightBgm:{(fightBgm != null ? "ok" : "null")}, vol:{VolumeSettings.BgmVolume:F2}");

        if (clip == null) { _src.Stop(); return; }
        if (_src.clip == clip && _src.isPlaying) return;
        _src.clip = clip;
        _src.volume = VolumeSettings.BgmVolume;
        _src.Play();
        Debug.Log($"[BGM] '{clip.name}' 재생 시작 (volume {_src.volume:F2})");
    }

    public void RefreshVolume()
    {
        if (_src != null) _src.volume = VolumeSettings.BgmVolume;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("BGMPlayer");
        var p = go.AddComponent<BGMPlayer>();
        // Resources 에서 자동 로드. case-sensitive 라 여러 이름 시도
        if (p.lobbyBgm == null)
            p.lobbyBgm = Resources.Load<AudioClip>("BGM/Lobby_bgm") ?? Resources.Load<AudioClip>("BGM/LobbyBGM") ?? Resources.Load<AudioClip>("BGM/MainBGM");
        if (p.fightBgm == null)
            p.fightBgm = Resources.Load<AudioClip>("BGM/Fight_Bgm") ?? Resources.Load<AudioClip>("BGM/FightBGM");
        if (p.banPickBgm == null)
            p.banPickBgm = Resources.Load<AudioClip>("BGM/ban_bgm") ?? Resources.Load<AudioClip>("BGM/SelectBGM");
        Debug.Log($"[BGM] AutoSpawn — lobbyBgm:{(p.lobbyBgm != null ? "ok" : "null")}, fightBgm:{(p.fightBgm != null ? "ok" : "null")}, banPickBgm:{(p.banPickBgm != null ? "ok" : "null")}");
    }
}

/// <summary>
/// 배경음 / 효과음 볼륨 — static + PlayerPrefs 영속화.
/// </summary>
public static class VolumeSettings
{
    const string KeyBgm = "tfm.vol.bgm";
    const string KeySfx = "tfm.vol.sfx";

    static float _bgm = -1f, _sfx = -1f;

    public static float BgmVolume
    {
        get { if (_bgm < 0f) _bgm = PlayerPrefs.GetFloat(KeyBgm, 0.6f); return _bgm; }
        set { _bgm = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KeyBgm, _bgm); if (BGMPlayer.Instance != null) BGMPlayer.Instance.RefreshVolume(); }
    }

    public static float SfxVolume
    {
        get { if (_sfx < 0f) _sfx = PlayerPrefs.GetFloat(KeySfx, 0.9f); return _sfx; }
        set { _sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KeySfx, _sfx); AudioListener.volume = 1f; /* SFX 는 PlayClipAtPoint 에 사용 */ }
    }
}
