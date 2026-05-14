#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AScene_FightUI 의 Canvas 양 끝에 팀 로고 Image GO 생성.
/// 왼쪽 = Shingu_logo, 오른쪽 = YONSEI.
/// 위치는 적당한 기본값으로 두고, 이후 user 가 수동 조정.
/// </summary>
public static class TeamLogoInstaller
{
    const string FightUIPath = "Assets/01.Scenes/AScene_FightUI.unity";
    const string ShinguLogoPath = "Assets/04.Images/Team logo/Shingu_logo.png";
    const string YonseiLogoPath = "Assets/04.Images/Team logo/YONSEI.png";

    [MenuItem("TFM/Install Team Logos in AScene_FightUI", priority = -37)]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(FightUIPath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"[TFM] {FightUIPath} 열기 실패"); return; }

        var shinguSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShinguLogoPath);
        var yonseiSprite = AssetDatabase.LoadAssetAtPath<Sprite>(YonseiLogoPath);

        if (shinguSprite == null) Debug.LogWarning($"[TeamLogo] {ShinguLogoPath} 로드 실패 — Sprite 가 아닐 수 있음");
        if (yonseiSprite == null) Debug.LogWarning($"[TeamLogo] {YonseiLogoPath} 로드 실패");

        // Canvas 찾기 — 가장 큰 Canvas (보통 메인)
        Canvas canvas = null;
        float bestArea = 0f;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var rt = c.transform as RectTransform;
            if (rt == null) continue;
            float a = Mathf.Abs(rt.rect.width * rt.rect.height);
            if (a > bestArea) { bestArea = a; canvas = c; }
        }
        if (canvas == null) { Debug.LogError("[TeamLogo] Canvas 없음"); return; }

        // 기존 같은 이름 GO 있으면 먼저 제거 (재실행 안전)
        foreach (var t in canvas.GetComponentsInChildren<RectTransform>(true))
        {
            if (t == null) continue;
            if (t.gameObject.name == "TeamLogo_Ally" || t.gameObject.name == "TeamLogo_Enemy")
            {
                Debug.Log($"[TeamLogo] 기존 '{t.gameObject.name}' 제거 (재생성)");
                Object.DestroyImmediate(t.gameObject);
            }
        }

        var canvasRt = canvas.transform as RectTransform;
        Vector2 canvasSize = canvasRt.rect.size;

        // 로고 사이즈 — 캔버스 높이의 약 8%
        float logoSize = canvasSize.y * 0.08f;
        if (logoSize < 60f) logoSize = 60f;
        if (logoSize > 140f) logoSize = 140f;

        // 좌측 끝 — anchor (0, 1), pivot (0, 1), 화면 좌상단으로부터 약간 안쪽
        var ally  = CreateLogo("TeamLogo_Ally",  canvas.transform, shinguSprite,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(40f, -10f),  // 좌상단 (40, -10)
            new Vector2(logoSize, logoSize));

        // 우측 끝 — anchor (1, 1), pivot (1, 1)
        var enemy = CreateLogo("TeamLogo_Enemy", canvas.transform, yonseiSprite,
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-40f, -10f), // 우상단 (-40, -10)
            new Vector2(logoSize, logoSize));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[TeamLogo] 완료 — 'TeamLogo_Ally' (Shingu) + 'TeamLogo_Enemy' (Yonsei) 생성. 크기 {logoSize:F0}px");
        EditorUtility.DisplayDialog("팀 로고 생성 완료",
            $"'TeamLogo_Ally' (신구 로고, 좌상단) + 'TeamLogo_Enemy' (연세 로고, 우상단) 생성 완료.\n" +
            $"기본 크기 {logoSize:F0}px.\n" +
            "Hierarchy 에서 선택해서 위치/크기 수동 조정하세요.",
            "OK");

        Selection.activeGameObject = ally;
    }

    static GameObject CreateLogo(string name, Transform parent, Sprite sprite,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;  // pivot 도 anchor 와 같게
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        img.preserveAspect = true;
        img.raycastTarget = false;
        return go;
    }
}
#endif
