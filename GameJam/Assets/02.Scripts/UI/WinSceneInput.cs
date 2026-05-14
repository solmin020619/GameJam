using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// AScene_Win (시상식) 에서 Space / Enter / Esc 누르면 처음 (Lobby) 로 돌아감.
/// AScene_Win 씬 로드 시 자동으로 spawn (UI 없음).
/// </summary>
public class WinSceneInput : MonoBehaviour
{
    public string returnSceneName = "Lobby";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawnHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (scene.name != "AScene_Win") return;

        // 이미 있으면 중복 spawn 방지
        if (Object.FindFirstObjectByType<WinSceneInput>() != null) return;

        var go = new GameObject("WinSceneInput");
        go.AddComponent<WinSceneInput>();
        Debug.Log("[WinSceneInput] AScene_Win 진입 — Space/Enter/Esc 로 Lobby 복귀");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)
         || Input.GetKeyDown(KeyCode.Return)
         || Input.GetKeyDown(KeyCode.KeypadEnter)
         || Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("[WinSceneInput] 처음으로 복귀");
            Time.timeScale = 1f;
            MatchResult.Clear();
            PickResult.Clear();
            SceneManager.LoadScene(returnSceneName);
        }
    }
}
