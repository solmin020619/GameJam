#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AScene_BanPick 의 Character_stat 템플릿 1 개를 6 개로 복제 + 좌/우 배치.
/// 좌 3 (PickCard_Ally_0/1/2) + 우 3 (PickCard_Enemy_0/1/2).
/// 이후 TFM > Setup AScene_BanPick Card Wiring 으로 PickCardUI wire.
/// </summary>
public static class BanPickSidePanelDuplicator
{
    const string AScenePath = "Assets/01.Scenes/AScene_BanPick.unity";

    [MenuItem("TFM/Duplicate Character_stat → 6 Side Panels (Ally x3 + Enemy x3)", priority = -39)]
    public static void Duplicate()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(AScenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"[TFM] {AScenePath} 열기 실패"); return; }

        // 1) 템플릿 찾기 — 이름이 "Character_stat" 인 GO. 자식 중 Attack_Text 가 있어야 진짜 PickCard 템플릿
        GameObject template = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Character_stat") continue;
                if (HasChildNamed(t, "Attack_Text") || HasChildNamed(t, "HP_Text"))
                {
                    template = t.gameObject;
                    break;
                }
            }
            if (template != null) break;
        }

        if (template == null)
        {
            EditorUtility.DisplayDialog("템플릿 없음",
                "'Character_stat' GameObject 가 없습니다 (또는 Attack_Text/HP_Text 자식이 없음).\n" +
                "Hierarchy 에서 Character_stat 가 있는지 확인하세요.",
                "OK");
            return;
        }

        var templateRt = template.GetComponent<RectTransform>();
        if (templateRt == null)
        {
            EditorUtility.DisplayDialog("RectTransform 없음", "Character_stat 에 RectTransform 이 없습니다.", "OK");
            return;
        }

        // Canvas 찾기 — 템플릿이 속한 Canvas
        var canvas = template.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("[TFM] Canvas 없음"); return; }

        var canvasRt = canvas.transform as RectTransform;
        Vector2 canvasSize = canvasRt.rect.size;

        // 템플릿 사이즈 — 복제할 패널 크기
        Vector2 panelSize = templateRt.rect.size;
        Debug.Log($"[SidePanel] 템플릿 size: {panelSize}, canvas size: {canvasSize}");

        // 2) 기존에 PickCard_Ally_0 등 이미 있으면 먼저 제거 (재실행 시 누적 방지)
        var existingNames = new HashSet<string> {
            "PickCard_Ally_0", "PickCard_Ally_1", "PickCard_Ally_2",
            "PickCard_Enemy_0", "PickCard_Enemy_1", "PickCard_Enemy_2"
        };
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t.gameObject == template) continue;
                if (existingNames.Contains(t.name))
                {
                    Debug.Log($"[SidePanel] 기존 '{t.name}' 제거 (재배치)");
                    Object.DestroyImmediate(t.gameObject);
                }
            }
        }

        // 3) 배치 계산 — 좌/우 가장자리에서 안쪽으로 margin, 세로 3 등분
        float margin = 40f;
        float leftX  = -canvasSize.x * 0.5f + panelSize.x * 0.5f + margin;
        float rightX =  canvasSize.x * 0.5f - panelSize.x * 0.5f - margin;

        // Y 위치 — 캔버스 상단에서 시작, 위→아래 3 개
        float topY    = canvasSize.y * 0.5f - panelSize.y * 0.5f - 80f;  // 상단 80px 여유
        float spacing = panelSize.y + 20f;
        float[] ys = { topY, topY - spacing, topY - spacing * 2f };

        // 4) 좌 3 + 우 3 복제
        var created = new List<GameObject>();
        for (int i = 0; i < 3; i++)
        {
            var ally  = DuplicateAndPlace(template, canvas.transform, $"PickCard_Ally_{i}",  new Vector2(leftX,  ys[i]));
            var enemy = DuplicateAndPlace(template, canvas.transform, $"PickCard_Enemy_{i}", new Vector2(rightX, ys[i]));
            created.Add(ally);
            created.Add(enemy);
        }

        // 5) 원본 Character_stat 삭제 — 가운데 시각 잡음 제거 (복제본 6 개로 충분)
        Debug.Log($"[SidePanel] 원본 Character_stat 삭제 — 가운데 겹침 제거");
        Object.DestroyImmediate(template);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[SidePanel] 완료 — 6 개 사이드 패널 생성:");
        foreach (var go in created) Debug.Log($"  '{go.name}' at {go.GetComponent<RectTransform>().anchoredPosition}");

        EditorUtility.DisplayDialog("사이드 패널 생성 완료",
            "6 개 PickCard (좌 3 + 우 3) 생성 완료.\n\n" +
            "다음 단계:\n" +
            "1. (선택) 원본 Character_stat 는 Hierarchy 에서 삭제 또는 위치 조정\n" +
            "2. 'TFM > Setup AScene_BanPick Card Wiring' 실행하여 PickCardUI wire",
            "OK");
    }

    static GameObject DuplicateAndPlace(GameObject template, Transform canvasRoot, string newName, Vector2 anchoredPos)
    {
        var clone = Object.Instantiate(template, canvasRoot, false);
        clone.name = newName;
        var rt = clone.GetComponent<RectTransform>();

        // Anchor 를 캔버스 중앙 기준으로 — 절대 위치 계산이 쉬워짐
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        return clone;
    }

    static bool HasChildNamed(Transform parent, string name)
    {
        foreach (var t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return true;
        return false;
    }
}
#endif
