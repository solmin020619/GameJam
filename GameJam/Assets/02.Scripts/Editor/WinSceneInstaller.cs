#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AScene_Win — Firecracker_N Image 들에 FireworkSpriteAnimator 자동 부착.
/// 펄스 + 색 변경 으로 폭죽 터지는 느낌.
/// </summary>
public static class WinSceneInstaller
{
    const string WinScenePath = "Assets/01.Scenes/AScene_Win.unity";

    [MenuItem("TFM/Setup AScene_Win Firework Animation", priority = -35)]
    public static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(WinScenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"[TFM] {WinScenePath} 열기 실패"); return; }

        int added = 0, skipped = 0;
        foreach (var img in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string n = img.gameObject.name;
            if (!n.StartsWith("Firecracker")) continue;

            if (img.GetComponent<FireworkSpriteAnimator>() != null)
            {
                skipped++;
                continue;
            }

            // 각 Firecracker 마다 약간 다른 burstInterval 로 동기 안 되게
            var anim = img.gameObject.AddComponent<FireworkSpriteAnimator>();
            anim.burstInterval = Random.Range(1.0f, 1.6f);
            anim.maxScale = Random.Range(1.2f, 1.7f);
            added++;
            Debug.Log($"[WinScene] '{n}' FireworkSpriteAnimator 부착 (interval:{anim.burstInterval:F2}s)");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[WinScene] 완료 — 새로 부착 {added}, 이미 있음 {skipped}");
        EditorUtility.DisplayDialog("Win 씬 폭죽 셋업 완료",
            $"Firecracker GO 들에 FireworkSpriteAnimator {added} 개 부착 (이미 있음 {skipped}).\n" +
            "Play 하면 폭죽들이 펄스/색변화로 터지는 효과.",
            "OK");
    }
}
#endif
