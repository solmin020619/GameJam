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
        Debug.Log("[TFM] MainMenuController 추가 완료. Start → BanPick / Exit → 종료 자동 와이어링.");
    }

    [MenuItem("TFM/Add All Scenes To Build Settings")]
    public static void AddScenesToBuild()
    {
        // 게임 흐름: Lobby(메인) → AScene_Wait(대기) → BanPick(밴픽) → InGame(전투) + AScene_FightUI(전투 UI 오버레이)
        string[] scenes = {
            "Assets/01.Scenes/AScene.unity",
            "Assets/01.Scenes/Lobby.unity",
            "Assets/01.Scenes/AScene_Wait.unity",
            "Assets/01.Scenes/BanPick.unity",
            "Assets/01.Scenes/InGame.unity",
            "Assets/01.Scenes/AScene_FightUI.unity",
            "Assets/01.Scenes/Tourment.unity",
            "Assets/01.Scenes/Base.unity",
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
        Debug.Log($"[TFM] Build Settings 에 {list.Count} 씬 등록.");
    }
}
#endif
