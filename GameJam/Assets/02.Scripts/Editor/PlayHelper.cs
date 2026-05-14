#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayHelper
{
    const string LobbyPath        = "Assets/01.Scenes/Lobby.unity";
    const string AScene_BanPick   = "Assets/01.Scenes/AScene_BanPick.unity";
    const string InGamePath       = "Assets/01.Scenes/InGame.unity";
    const string OldBanPickPath   = "Assets/01.Scenes/BanPick.unity";

    [MenuItem("TFM/▶ Play From Lobby (메인 흐름)", priority = -100)]
    public static void PlayFromLobby() => PlayScene(LobbyPath);

    [MenuItem("TFM/▶ Play From AScene_BanPick (밴픽만 테스트)", priority = -99)]
    public static void PlayFromAScene_BanPick() => PlayScene(AScene_BanPick);

    [MenuItem("TFM/▶ Play From InGame (전투만 테스트)", priority = -98)]
    public static void PlayFromInGame() => PlayScene(InGamePath);

    [MenuItem("TFM/▶ Play From Old BanPick (옛 BanPickUIBuilder)", priority = -97)]
    public static void PlayFromOldBanPick() => PlayScene(OldBanPickPath);

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
