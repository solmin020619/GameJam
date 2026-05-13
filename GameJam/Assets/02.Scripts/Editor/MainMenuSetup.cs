#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MainMenuSetup
{
    [MenuItem("TFM/Setup Main Menu Buttons (AScene)")]
    public static void Setup()
    {
        // 기존 MainMenuController 있는지
        var existing = Object.FindFirstObjectByType<MainMenuController>();
        if (existing != null)
        {
            Debug.Log($"[TFM] MainMenuController 이미 있음 ({existing.gameObject.name}). 그대로 사용.");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        var go = new GameObject("MainMenuController");
        go.AddComponent<MainMenuController>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = go;
        Debug.Log("[TFM] MainMenuController 추가 완료. Start → HScene / Exit → 종료 자동 와이어링.");
    }

    [MenuItem("TFM/Add All Scenes To Build Settings")]
    public static void AddScenesToBuild()
    {
        string[] scenes = {
            "Assets/01.Scenes/AScene.unity",
            "Assets/01.Scenes/HScene.unity",
            "Assets/01.Scenes/KScene.unity"
        };

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        foreach (var s in scenes)
        {
            if (System.IO.File.Exists(s))
                list.Add(new EditorBuildSettingsScene(s, true));
            else
                Debug.LogWarning($"[TFM] 씬 파일 없음: {s}");
        }

        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[TFM] Build Settings 에 {list.Count} 씬 등록 (순서: AScene → HScene → KScene).");
    }
}
#endif
