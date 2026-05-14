using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 모든 Button onClick 에 UI 클릭 사운드 자동 연결.
/// RuntimeInitializeOnLoadMethod 로 게임 시작 시 + 매 씬 로드 시 모든 Button 에 hook.
/// </summary>
public static class UIClickSound
{
    static AudioClip _clip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        _clip = Resources.Load<AudioClip>("UI_Click");
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        WireAllButtons();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => WireAllButtons();

    static void WireAllButtons()
    {
        if (_clip == null) return;
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // 중복 hook 방지 — 이미 hook 됐는지 marker 컴포넌트로 체크
            if (b.gameObject.GetComponent<_UIClickMarker>() != null) continue;
            b.gameObject.AddComponent<_UIClickMarker>();
            b.onClick.AddListener(PlayClick);
        }
    }

    public static void PlayClick()
    {
        if (_clip == null) return;
        var cam = Camera.main;
        var pos = cam != null ? cam.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(_clip, pos, VolumeSettings.SfxVolume);
    }
}

// 빈 marker — Button 에 hook 됐는지 표시
class _UIClickMarker : MonoBehaviour { }
