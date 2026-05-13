#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 현재 씬에 킬로그 UI 자동 추가.
/// Canvas 상단 중앙에 VerticalLayoutGroup 컨테이너 + KillLogUI 컴포넌트.
/// 새 엔트리는 코드에서 SetAsFirstSibling 으로 위에 추가됨.
/// </summary>
public static class KillLogInstaller
{
    const string SwordPath = "Assets/04.Images/UI/sword_icon.png";

    [MenuItem("TFM/Add Kill Log to Current Scene")]
    public static void Add()
    {
        // 검 sprite 임포트 설정 강제 (있을 때만)
        var importer = AssetImporter.GetAtPath(SwordPath) as TextureImporter;
        if (importer != null)
        {
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (Mathf.Abs(importer.spritePixelsPerUnit - 100f) > 0.1f) { importer.spritePixelsPerUnit = 100f; dirty = true; }
            if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; dirty = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (dirty) importer.SaveAndReimport();
        }
        var existing = GameObject.Find("KillLogRoot");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("KillLogRoot 이미 있음",
                "기존 KillLogRoot 를 삭제하고 새로 만들까요?", "예", "아니오")) return;
            Object.DestroyImmediate(existing);
        }

        // Canvas 확보 (없으면 생성)
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // EventSystem 확보
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            var newModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (newModuleType != null) es.AddComponent(newModuleType);
            else es.AddComponent<StandaloneInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        // KillLogRoot — 상단 중앙
        var root = new GameObject("KillLogRoot");
        root.transform.SetParent(canvas.transform, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 1f);
        rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.anchoredPosition = new Vector2(0, -30);  // 상단에서 30px 아래
        rootRt.sizeDelta = new Vector2(400, 400);       // 충분히 넓게

        // Container (VerticalLayoutGroup — 새 엔트리 위, 기존 아래)
        var container = new GameObject("KillLogContainer");
        container.transform.SetParent(root.transform, false);
        var contRt = container.AddComponent<RectTransform>();
        contRt.anchorMin = new Vector2(0.5f, 1f);
        contRt.anchorMax = new Vector2(0.5f, 1f);
        contRt.pivot = new Vector2(0.5f, 1f);
        contRt.anchoredPosition = Vector2.zero;
        contRt.sizeDelta = new Vector2(400, 0);

        var vlg = container.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 4f;

        var fitter = container.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // KillLogUI 컴포넌트 (root 에)
        var killLog = root.AddComponent<KillLogUI>();
        killLog.entryContainer = container.transform;
        killLog.swordSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SwordPath);

        // ============ KillScoreUI — 상단 가운데 [N] ⚔ [N] ============
        var oldScore = GameObject.Find("KillScoreRoot");
        if (oldScore != null) Object.DestroyImmediate(oldScore);

        var scoreRoot = new GameObject("KillScoreRoot");
        scoreRoot.transform.SetParent(canvas.transform, false);
        var scoreRt = scoreRoot.AddComponent<RectTransform>();
        scoreRt.anchorMin = new Vector2(0.5f, 1f);
        scoreRt.anchorMax = new Vector2(0.5f, 1f);
        scoreRt.pivot = new Vector2(0.5f, 1f);
        scoreRt.anchoredPosition = new Vector2(0, -10);
        scoreRt.sizeDelta = new Vector2(200, 50);

        // Team0 (왼쪽, 파랑)
        var t0 = MakeScoreText(scoreRoot.transform, "Team0Kills", "0",
            new Vector2(-50, 0), new Color(0.4f, 0.7f, 1f));
        // 검 아이콘 (작게)
        var swordGo = new GameObject("ScoreSword");
        swordGo.transform.SetParent(scoreRoot.transform, false);
        var swordRt = swordGo.AddComponent<RectTransform>();
        swordRt.sizeDelta = new Vector2(32, 32);
        swordRt.anchoredPosition = Vector2.zero;
        var swordImg = swordGo.AddComponent<UnityEngine.UI.Image>();
        swordImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SwordPath);
        swordImg.preserveAspect = true;
        // Team1 (오른쪽, 빨강)
        var t1 = MakeScoreText(scoreRoot.transform, "Team1Kills", "0",
            new Vector2(50, 0), new Color(1f, 0.4f, 0.4f));

        var killScore = scoreRoot.AddComponent<KillScoreUI>();
        killScore.team0Text = t0;
        killScore.team1Text = t1;

        // ============ DamageVignette — 화면 전체 빨간 비네트 ============
        var oldVignette = GameObject.Find("DamageVignette");
        if (oldVignette != null) Object.DestroyImmediate(oldVignette);

        var vignetteGo = new GameObject("DamageVignette");
        vignetteGo.transform.SetParent(canvas.transform, false);
        var vRt = vignetteGo.AddComponent<RectTransform>();
        vRt.anchorMin = Vector2.zero;
        vRt.anchorMax = Vector2.one;
        vRt.offsetMin = Vector2.zero;
        vRt.offsetMax = Vector2.zero;
        vignetteGo.AddComponent<DamageVignette>();
        // 비네트는 다른 UI 위에 (단, 마우스 클릭 안 막게 raycastTarget = false)
        vignetteGo.transform.SetAsLastSibling();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[TFM] KillLog + KillScore + DamageVignette UI added.");
    }

    static TMPro.TextMeshProUGUI MakeScoreText(Transform parent, string name, string text,
                                               Vector2 anchoredPos, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60, 40);
        rt.anchoredPosition = anchoredPos;
        var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 36;
        tmp.color = color;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.raycastTarget = false;
        return tmp;
    }
}
#endif
