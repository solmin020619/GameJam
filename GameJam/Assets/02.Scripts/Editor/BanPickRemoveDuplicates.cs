#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Canvas 자식 중 같은 이름 중복 GameObject 제거 — 첫 번째만 keep, 나머지 삭제.
/// OLD BanPickUIBuilder 가 두 번 실행되어 모든 UI 가 통째로 중복된 경우 해결.
/// </summary>
public static class BanPickRemoveDuplicates
{
    const string AScenePath = "Assets/01.Scenes/AScene_BanPick.unity";

    [MenuItem("TFM/Remove Duplicate Canvas Children (Keep First)", priority = -38)]
    public static void Remove()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(AScenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"[TFM] {AScenePath} 열기 실패"); return; }

        int totalDeleted = 0;

        // 모든 Canvas 찾기 — 같은 이름 그룹마다 자식 많은 거 keep, 적은 거 삭제
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // 이름별 그룹화
            var groups = new Dictionary<string, List<Transform>>();
            foreach (Transform t in canvas.transform)
            {
                string n = t.gameObject.name;
                if (!groups.ContainsKey(n)) groups[n] = new List<Transform>();
                groups[n].Add(t);
            }

            var toDelete = new List<GameObject>();
            foreach (var kv in groups)
            {
                if (kv.Value.Count <= 1) continue;  // 중복 아님
                // 각 GO 의 자식 + 손자 + ... 전체 개수 계산. 많은 거가 더 "완전" 한 것
                Transform best = null;
                int bestCount = -1;
                foreach (var t in kv.Value)
                {
                    int count = t.GetComponentsInChildren<Transform>(true).Length;
                    if (count > bestCount) { bestCount = count; best = t; }
                }
                // best 만 keep, 나머지 삭제
                foreach (var t in kv.Value)
                {
                    if (t == best) continue;
                    int n = t.GetComponentsInChildren<Transform>(true).Length;
                    Debug.Log($"[RemoveDup] '{kv.Key}' 중복 제거 — keep:{bestCount}자식, 삭제:{n}자식 (이번 GO)");
                    toDelete.Add(t.gameObject);
                }
            }

            foreach (var go in toDelete)
            {
                Object.DestroyImmediate(go);
                totalDeleted++;
            }
        }

        // Scene root 도 같은 식 — 자식 많은 거 keep (예: BanPickRoot vs BanPickRoot (1))
        var rootGroups = new Dictionary<string, List<GameObject>>();
        foreach (var root in scene.GetRootGameObjects())
        {
            string baseName = System.Text.RegularExpressions.Regex.Replace(root.name, @" \(\d+\)$", "").Trim();
            if (!rootGroups.ContainsKey(baseName)) rootGroups[baseName] = new List<GameObject>();
            rootGroups[baseName].Add(root);
        }
        var rootToDelete = new List<GameObject>();
        foreach (var kv in rootGroups)
        {
            if (kv.Value.Count <= 1) continue;
            GameObject best = null;
            int bestCount = -1;
            foreach (var go in kv.Value)
            {
                int count = go.GetComponentsInChildren<Transform>(true).Length;
                if (count > bestCount) { bestCount = count; best = go; }
            }
            foreach (var go in kv.Value)
            {
                if (go == best) continue;
                Debug.Log($"[RemoveDup] 루트 '{kv.Key}' 중복 제거 — keep:{bestCount}자식, 삭제:'{go.name}'");
                rootToDelete.Add(go);
            }
        }
        foreach (var go in rootToDelete)
        {
            Object.DestroyImmediate(go);
            totalDeleted++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[RemoveDup] 완료 — 총 {totalDeleted} 개 중복 GO 삭제");
        EditorUtility.DisplayDialog("중복 제거 완료",
            $"{totalDeleted} 개 중복 GameObject 삭제 완료.\n" +
            "이제 'TFM > Setup AScene_BanPick Card Wiring' 다시 실행하면 됩니다.",
            "OK");
    }
}
#endif
