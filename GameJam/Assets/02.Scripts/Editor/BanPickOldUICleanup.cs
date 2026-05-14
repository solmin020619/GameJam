#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// AScene_BanPick 에 남아있는 옛 BanPickUIBuilder 자동 생성물 정리.
/// 새 디자인 UI 는 건드리지 않고, 옛 builder 가 만든 이름의 오브젝트만 제거.
/// </summary>
public static class BanPickOldUICleanup
{
    const string AScenePath = "Assets/01.Scenes/AScene_BanPick.unity";
    const string OldScenePath = "Assets/01.Scenes/BanPick.unity";

    // 옛 BanPickUIBuilder 가 만드는 루트/주요 GO 이름들 (중첩된 곳에 있어도 매칭)
    static readonly string[] OldNames = {
        "BanPickRoot",
        "BanPickRoot (1)",
        "BanPickManager",
        "DoneOverlay",
        "HUD",
        "CardGrid",
        "AllyPanel",
        "EnemyPanel",
        "BG",
    };

    // Card_00, Card_01, ..., Card_99 패턴
    static readonly Regex OldCardPattern = new Regex(@"^Card_\d{1,3}( \(\d+\))?$");

    [MenuItem("TFM/Clear Old BanPick UI (AScene_BanPick)", priority = -42)]
    public static void CleanAScene() => Clean(AScenePath);

    [MenuItem("TFM/Clear Old BanPick UI (BanPick - 옛 씬)", priority = -41)]
    public static void CleanOldScene() => Clean(OldScenePath);

    static void Clean(string scenePath)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[TFM] {scenePath} 열기 실패");
            return;
        }

        int removed = 0;
        var toDelete = new List<GameObject>();
        var nameSet = new HashSet<string>(OldNames);

        // 0) InGame FightUI 관련 컴포넌트 — 밴픽 씬에 있으면 안 됨 (Additive 로 AScene_FightUI 로드 트리거)
        foreach (var ctrl in Object.FindObjectsByType<FightUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { Debug.Log($"[TFM] 밴픽 씬에 잘못 들어있는 FightUIController '{ctrl.gameObject.name}' 제거"); toDelete.Add(ctrl.gameObject); }
        foreach (var bm in Object.FindObjectsByType<BattleManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { Debug.Log($"[TFM] 밴픽 씬에 잘못 들어있는 BattleManager '{bm.gameObject.name}' 제거"); toDelete.Add(bm.gameObject); }
        foreach (var c in Object.FindObjectsByType<KillLogUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { toDelete.Add(c.gameObject); }
        foreach (var c in Object.FindObjectsByType<KillScoreUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { toDelete.Add(c.gameObject); }
        foreach (var c in Object.FindObjectsByType<DamageVignette>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { toDelete.Add(c.gameObject); }

        // 1) 모든 ChampionCardUI 컴포넌트 — 옛 BanPickUIBuilder 만 부착함 (새 디자인은 PickCardUI 사용)
        //    이 컴포넌트 가진 GO 들 = 옛 카드들 → 통째로 삭제
        foreach (var cu in Object.FindObjectsByType<ChampionCardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Debug.Log($"[TFM] 옛 ChampionCardUI 카드 제거 '{cu.gameObject.name}'");
            toDelete.Add(cu.gameObject);
        }

        // 2) 옛 BanPickHUD / TeamSlotUI / BanPickManager / BanPickAI 컴포넌트들 — 새 와이어러가 다시 만들 거라
        foreach (var c in Object.FindObjectsByType<BanPickHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { toDelete.Add(c.gameObject); Debug.Log($"[TFM] 옛 BanPickHUD '{c.gameObject.name}' 제거"); }
        foreach (var c in Object.FindObjectsByType<TeamSlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { toDelete.Add(c.gameObject); Debug.Log($"[TFM] 옛 TeamSlotUI '{c.gameObject.name}' 제거"); }
        foreach (var c in Object.FindObjectsByType<BanPickManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { toDelete.Add(c.gameObject); Debug.Log($"[TFM] 옛 BanPickManager '{c.gameObject.name}' 제거"); }
        foreach (var c in Object.FindObjectsByType<BanPickAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { toDelete.Add(c.gameObject); Debug.Log($"[TFM] 옛 BanPickAI '{c.gameObject.name}' 제거"); }

        // 3) 이름 기반 - 씬 전체 트랜스폼 훑어서 옛 이름 매칭 GO 제거
        //    nested 된 거 (Canvas 자식 아닌 곳에 있어도) 모두 잡음
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                string n = t.gameObject.name;
                if (nameSet.Contains(n) || OldCardPattern.IsMatch(n))
                {
                    if (!toDelete.Contains(t.gameObject)) toDelete.Add(t.gameObject);
                }
            }
        }

        foreach (var go in toDelete)
        {
            if (go == null) continue;  // 부모 GO 와 함께 이미 삭제됨
            Debug.Log($"[TFM] 옛 BanPick UI 제거: '{go.name}'");
            Object.DestroyImmediate(go);
            removed++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[TFM] '{scenePath}' 정리 완료 — {removed} 개 제거");

        if (removed == 0)
            EditorUtility.DisplayDialog("BanPick 정리",
                "옛 BanPickUIBuilder 자동 생성물이 발견되지 않았습니다.\n" +
                "혹시 다른 이름으로 남아있다면 Hierarchy 에서 직접 삭제 필요.",
                "OK");
        else
            EditorUtility.DisplayDialog("BanPick 정리",
                $"{removed} 개 옛 UI 오브젝트 제거 완료.\n이제 자유롭게 새 UI 배치 가능.",
                "OK");
    }
}
#endif
