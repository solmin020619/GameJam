using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// AScene 의 Start / Exit 버튼에 자동 와이어링.
/// Awake 시 이름으로 Button 찾아서 onClick 등록. Inspector 작업 0.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Scene Names")]
    public string hSceneName = "HScene";   // Start → 이 씬 로드

    void Awake()
    {
        var startBtn = FindButton("Start");
        var exitBtn = FindButton("Exit");

        if (startBtn != null)
        {
            startBtn.onClick.RemoveAllListeners();
            startBtn.onClick.AddListener(OnStartClicked);
        }
        if (exitBtn != null)
        {
            exitBtn.onClick.RemoveAllListeners();
            exitBtn.onClick.AddListener(OnExitClicked);
        }
    }

    public void OnStartClicked()
    {
        SceneManager.LoadScene(hSceneName);
    }

    public void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static Button FindButton(string name)
    {
        var all = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in all)
            if (b.gameObject.name == name) return b;
        return null;
    }
}
