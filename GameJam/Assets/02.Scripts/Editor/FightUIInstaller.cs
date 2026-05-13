#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FightUIInstaller
{
    const string FightUIScenePath = "Assets/01.Scenes/AScene_FightUI.unity";

    [MenuItem("TFM/Install Fight UI (KScene)")]
    public static void Install()
    {
        // 현재 씬에 FightUIController 자동 추가
        var existing = Object.FindFirstObjectByType<FightUIController>();
        if (existing != null)
        {
            Debug.Log($"[TFM] FightUIController 이미 있음 ({existing.gameObject.name})");
            Selection.activeGameObject = existing.gameObject;
        }
        else
        {
            var go = new GameObject("FightUIController");
            go.AddComponent<FightUIController>();
            Selection.activeGameObject = go;
            Debug.Log("[TFM] FightUIController 추가 완료. Play 시 AScene_FightUI 자동 로드.");
        }

        // Build Settings 에 AScene_FightUI 자동 추가
        var current = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool found = false;
        foreach (var s in current) if (s.path == FightUIScenePath) { found = true; break; }
        if (!found)
        {
            current.Add(new EditorBuildSettingsScene(FightUIScenePath, true));
            EditorBuildSettings.scenes = current.ToArray();
            Debug.Log($"[TFM] Build Settings 에 {FightUIScenePath} 추가됨");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
#endif
