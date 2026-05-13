#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

/// <summary>
/// AScene_FightUI 에 팀 이름 라벨 (OUR TEAM / ENEMY TEAM) 두 개 추가.
/// 한 번 클릭 → 씬에 GO 추가 + 저장. 그 다음은 사용자가 씬에서 드래그로 위치 조절.
/// 그리고 KScene 의 FightUIController 인스펙터에 자동 wire (allyLabelTmp, enemyLabelTmp).
/// </summary>
public static class TeamLabelInstaller
{
    const string FightUIScenePath = "Assets/01.Scenes/AScene_FightUI.unity";

    [MenuItem("TFM/Add Team Labels to FightUI Scene")]
    public static void Add()
    {
        // 현재 씬 백업 후 AScene_FightUI 열기
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(FightUIScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[TFM] {FightUIScenePath} 열기 실패");
            return;
        }

        // Canvas 찾기
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[TFM] AScene_FightUI 에 Canvas 없음");
            return;
        }

        // 기존 라벨 있으면 삭제
        var oldAlly = GameObject.Find("AllyTeamLabel");
        if (oldAlly != null) Object.DestroyImmediate(oldAlly);
        var oldEnemy = GameObject.Find("EnemyTeamLabel");
        if (oldEnemy != null) Object.DestroyImmediate(oldEnemy);

        // 새로 생성 (화면 중앙 상단에 일단 배치, 사용자가 씬에서 위치 조절)
        var allyLabel = CreateLabel(canvas.transform, "AllyTeamLabel", "OUR TEAM",
                                    new Vector2(-300, 250), TextAlignmentOptions.Right);
        var enemyLabel = CreateLabel(canvas.transform, "EnemyTeamLabel", "ENEMY TEAM",
                                     new Vector2(300, 250), TextAlignmentOptions.Left);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = allyLabel.gameObject;
        Debug.Log("[TFM] AllyTeamLabel / EnemyTeamLabel 추가 완료 — Scene 뷰에서 드래그로 위치 조절 가능. " +
                  "그 다음 InGame 씬의 FightUIController 인스펙터에 두 GO 끌어넣기.");
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
                                       Vector2 anchoredPos, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(360, 60);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 32;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = align;
        tmp.enableAutoSizing = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }
}
#endif
