#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayHelper
{
    const string BanPickPath = "Assets/01.Scenes/BanPick.unity";
    const string AScenePath  = "Assets/01.Scenes/AScene.unity";
    const string LobbyPath   = "Assets/01.Scenes/Lobby.unity";

    [MenuItem("TFM/▶ Play From BanPick (BanPick → InGame)", priority = -100)]
    public static void PlayFromBanPick() => PlayScene(BanPickPath);

    [MenuItem("TFM/▶ Play From AScene (Main Menu)", priority = -99)]
    public static void PlayFromAScene() => PlayScene(AScenePath);

    [MenuItem("TFM/▶ Play From Lobby", priority = -98)]
    public static void PlayFromLobby() => PlayScene(LobbyPath);

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
