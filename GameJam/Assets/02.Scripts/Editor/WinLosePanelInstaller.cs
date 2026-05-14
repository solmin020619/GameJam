#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AScene_FightUI 에 빈 VictoryPanel + DefeatPanel 생성.
/// 사용자가 직접 안에 UI 배치 (아이콘 / 이름 / 버튼 등).
/// 둘 다 SetActive(false) 시작 — VictoryScreenController 가 결과에 따라 하나만 켬.
/// </summary>
public static class WinLosePanelInstaller
{
    const string FightUIPath = "Assets/01.Scenes/AScene_FightUI.unity";

    [MenuItem("TFM/Install Victory + Defeat Panels in AScene_FightUI", priority = -36)]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(FightUIPath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"[TFM] {FightUIPath} 열기 실패"); return; }

        // Canvas 찾기 — 가장 큰 거
        Canvas canvas = null;
        float bestArea = 0f;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var rt = c.transform as RectTransform;
            if (rt == null) continue;
            float a = Mathf.Abs(rt.rect.width * rt.rect.height);
            if (a > bestArea) { bestArea = a; canvas = c; }
        }
        if (canvas == null) { Debug.LogError("[TFM] Canvas 없음"); return; }

        // 기존 같은 이름 GO 있으면 제거 (재실행 안전)
        foreach (var t in canvas.GetComponentsInChildren<RectTransform>(true))
        {
            if (t == null) continue;
            if (t.gameObject.name == "VictoryPanel" || t.gameObject.name == "DefeatPanel")
            {
                Debug.Log($"[WinLose] 기존 '{t.gameObject.name}' 제거");
                Object.DestroyImmediate(t.gameObject);
            }
        }

        var winPanel  = CreatePanel("VictoryPanel", canvas.transform, new Color(0.05f, 0.05f, 0.08f, 0.92f));
        var losePanel = CreatePanel("DefeatPanel",  canvas.transform, new Color(0.08f, 0.03f, 0.03f, 0.92f));

        // 시작 시 둘 다 비활성
        winPanel.SetActive(false);
        losePanel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[WinLose] VictoryPanel + DefeatPanel 생성 완료. 둘 다 SetActive(false) 시작.");
        EditorUtility.DisplayDialog("승리/패배 패널 생성 완료",
            "'VictoryPanel' (승리) + 'DefeatPanel' (패배) 생성 완료.\n둘 다 시작 시 비활성.\n\n" +
            "Hierarchy 에서 각 패널을 SetActive(true) 한 후 안에 자유롭게 UI 배치하세요:\n" +
            "  - 트로피/아이콘 이미지\n" +
            "  - 결과 텍스트\n" +
            "  - 점수\n" +
            "  - 진행 버튼 (이름은 'WinButton' / 'LoseButton' 권장)\n\n" +
            "배치 끝나면 다시 SetActive(false) 로 끄고, 알려주면 인터랙션 wire 추가.",
            "OK");

        Selection.activeGameObject = winPanel;
    }

    static GameObject CreatePanel(string name, Transform parent, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;

        // 캔버스 전체 stretch
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        var img = go.GetComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true; // 뒤 클릭 막음

        var cg = go.GetComponent<CanvasGroup>();
        cg.interactable = true;
        cg.blocksRaycasts = true;

        // 항상 최상단으로 (다른 UI 위에 표시)
        rt.SetAsLastSibling();

        Debug.Log($"[WinLose] '{name}' 패널 생성 — 전체 stretch, bg color {bgColor}");
        return go;
    }
}
#endif
