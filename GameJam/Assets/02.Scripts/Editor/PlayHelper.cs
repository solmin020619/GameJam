#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayHelper
{
    const string HScenePath = "Assets/01.Scenes/HScene.unity";
    const string AScenePath = "Assets/01.Scenes/AScene.unity";

    [MenuItem("TFM/▶ Play From HScene (BanPick → Battle)", priority = -100)]
    public static void PlayFromHScene() => PlayScene(HScenePath);

    [MenuItem("TFM/▶ Play From AScene (Main Menu)", priority = -99)]
    public static void PlayFromAScene() => PlayScene(AScenePath);

    static void PlayScene(string path)
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[PlayHelper] {path} 열기 실패");
            return;
        }
        EditorApplication.isPlaying = true;
    }
}
#endif
