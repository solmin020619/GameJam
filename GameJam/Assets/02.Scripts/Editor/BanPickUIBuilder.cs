#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public static class BanPickUIBuilder
{
    const string ConfigPath = "Assets/06.ScriptableObjects/BanPickConfig.asset";

    [MenuItem("TFM/Build BanPick UI in Current Scene")]
    public static void Build()
    {
        var config = AssetDatabase.LoadAssetAtPath<BanPickConfig>(ConfigPath);
        if (config == null)
        {
            EditorUtility.DisplayDialog("BanPickConfig 없음",
                "먼저 'TFM/Generate Champions From SPUM Units' 메뉴를 눌러 챔프와 Config를 생성하세요.",
                "OK");
            return;
        }

        var existing = GameObject.Find("BanPickRoot");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("이미 BanPickRoot 있음",
                "기존 BanPickRoot 를 삭제하고 새로 만들까요?", "예", "아니오")) return;
            Object.DestroyImmediate(existing);
        }

        EnsureEventSystem();
        var canvas = EnsureCanvas();

        var root = new GameObject("BanPickRoot");
        var managerGO = new GameObject("BanPickManager");
        managerGO.transform.SetParent(root.transform, false);
        var manager = managerGO.AddComponent<BanPickManager>();
        var ai = managerGO.AddComponent<BanPickAI>();

        var bgGO = CreatePanel(canvas.transform, "BG", new Color(0.08f, 0.09f, 0.14f, 1f));
        StretchFull(bgGO.GetComponent<RectTransform>());

        var hud = CreateHUD(canvas.transform);
        var cardRow = CreateCardRow(canvas.transform, config, manager, out var cards);
        var allySlot = CreateTeamPanel(canvas.transform, TeamSide.Ally, config);
        var enemySlot = CreateTeamPanel(canvas.transform, TeamSide.Enemy, config);

        hud.allySlotUI = allySlot;
        hud.enemySlotUI = enemySlot;

        manager.config = config;
        manager.hud = hud;
        manager.cards = cards;
        manager.allySlots = allySlot;
        manager.enemySlots = enemySlot;
        manager.ai = ai;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        Debug.Log("[TFM] BanPick UI built in current scene.");
    }

    // ============ helpers ============
    static void EnsureEventSystem()
    {
        var existing = Object.FindFirstObjectByType<EventSystem>();
        GameObject esGO;
        if (existing != null)
        {
            esGO = existing.gameObject;
        }
        else
        {
            esGO = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
        }

#if ENABLE_INPUT_SYSTEM
        var legacy = esGO.GetComponent<StandaloneInputModule>();
        if (legacy != null) Object.DestroyImmediate(legacy);
        var newModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (newModuleType != null && esGO.GetComponent(newModuleType) == null)
            esGO.AddComponent(newModuleType);
#else
        if (esGO.GetComponent<StandaloneInputModule>() == null)
            esGO.AddComponent<StandaloneInputModule>();
#endif
    }

    static Canvas EnsureCanvas()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null) return canvas;

        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    // ============ HUD ============
    static BanPickHUD CreateHUD(Transform parent)
    {
        var go = new GameObject("HUD", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -20);
        rt.sizeDelta = new Vector2(800, 160);

        var hud = go.AddComponent<BanPickHUD>();
        hud.phaseLabel = CreateText(go.transform, "PhaseLabel", "BAN PHASE", 56, new Vector2(0, -30), new Vector2(800, 80));
        hud.phaseLabel.alignment = TextAlignmentOptions.Center;
        hud.turnLabel = CreateText(go.transform, "TurnLabel", "YOUR TURN · BAN", 28, new Vector2(0, -90), new Vector2(800, 36));
        hud.turnLabel.alignment = TextAlignmentOptions.Center;

        var timerGO = new GameObject("TimerFill", typeof(RectTransform));
        timerGO.transform.SetParent(go.transform, false);
        var trt = (RectTransform)timerGO.transform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(-380, -50);
        trt.sizeDelta = new Vector2(80, 80);
        var timerImg = timerGO.AddComponent<Image>();
        timerImg.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        timerImg.type = Image.Type.Filled;
        timerImg.fillMethod = Image.FillMethod.Radial360;
        timerImg.fillAmount = 1f;
        timerImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        hud.timerFill = timerImg;

        hud.timerLabel = CreateText(timerGO.transform, "TimerLabel", "20", 36, Vector2.zero, new Vector2(80, 80));
        hud.timerLabel.alignment = TextAlignmentOptions.Center;
        hud.timerLabel.color = Color.black;

        // Done overlay
        var doneGO = new GameObject("DoneOverlay", typeof(RectTransform));
        doneGO.transform.SetParent(parent, false);
        var drt = (RectTransform)doneGO.transform;
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
        drt.offsetMin = drt.offsetMax = Vector2.zero;
        var doneBg = doneGO.AddComponent<Image>();
        doneBg.color = new Color(0, 0, 0, 0.7f);
        var doneLabel = CreateText(doneGO.transform, "DoneLabel", "READY!", 140, Vector2.zero, new Vector2(800, 200));
        doneLabel.alignment = TextAlignmentOptions.Center;
        doneLabel.color = new Color(0.4f, 1f, 0.5f);
        hud.doneOverlay = doneGO;
        hud.doneLabel = doneLabel;
        doneGO.SetActive(false);

        return hud;
    }

    // ============ Card Row ============
    static GameObject CreateCardRow(Transform parent, BanPickConfig config, BanPickManager mgr, out ChampionCardUI[] cards)
    {
        int n = config.championPool.Count;
        int cols = Mathf.Min(6, n);
        int rows = Mathf.CeilToInt(n / (float)cols);

        var go = new GameObject("CardGrid", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 0);

        var grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(150, 200);
        grid.spacing = new Vector2(12, 12);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;
        grid.childAlignment = TextAnchor.MiddleCenter;
        rt.sizeDelta = new Vector2(cols * 162, rows * 212);

        cards = new ChampionCardUI[n];
        for (int i = 0; i < n; i++)
        {
            cards[i] = CreateCard(go.transform, i);
        }
        return go;
    }

    static ChampionCardUI CreateCard(Transform parent, int idx)
    {
        var go = new GameObject($"Card_{idx:D2}", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(150, 200);

        var bgImg = go.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.18f, 0.25f, 1f);

        var card = go.AddComponent<ChampionCardUI>();
        var cg = go.AddComponent<CanvasGroup>();

        // Portrait
        var portraitGO = new GameObject("Portrait", typeof(RectTransform));
        portraitGO.transform.SetParent(go.transform, false);
        var prt = (RectTransform)portraitGO.transform;
        prt.anchorMin = new Vector2(0.5f, 1f);
        prt.anchorMax = new Vector2(0.5f, 1f);
        prt.pivot = new Vector2(0.5f, 1f);
        prt.anchoredPosition = new Vector2(0, -10);
        prt.sizeDelta = new Vector2(120, 120);
        var portrait = portraitGO.AddComponent<Image>();
        portrait.color = Color.white;
        card.portrait = portrait;
        card.background = bgImg;

        // Name
        card.nameLabel = CreateText(go.transform, "Name", "Name", 18, new Vector2(0, -135), new Vector2(140, 24));
        card.nameLabel.alignment = TextAlignmentOptions.Center;

        // Role
        card.roleLabel = CreateText(go.transform, "Role", "Role", 14, new Vector2(0, -160), new Vector2(140, 20));
        card.roleLabel.alignment = TextAlignmentOptions.Center;
        card.roleLabel.color = new Color(0.7f, 0.8f, 1f);

        // Banned overlay
        var banned = new GameObject("BannedOverlay", typeof(RectTransform));
        banned.transform.SetParent(go.transform, false);
        var brt = (RectTransform)banned.transform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        var banImg = banned.AddComponent<Image>();
        banImg.color = new Color(1f, 0.1f, 0.1f, 0.35f);
        var banX = CreateText(banned.transform, "X", "BAN", 32, Vector2.zero, new Vector2(150, 80));
        banX.alignment = TextAlignmentOptions.Center;
        banX.color = Color.white;
        card.bannedOverlay = banned;
        banned.SetActive(false);

        // Picked overlay
        var picked = new GameObject("PickedOverlay", typeof(RectTransform));
        picked.transform.SetParent(go.transform, false);
        var prt2 = (RectTransform)picked.transform;
        prt2.anchorMin = Vector2.zero; prt2.anchorMax = Vector2.one;
        prt2.offsetMin = prt2.offsetMax = Vector2.zero;
        var pickImg = picked.AddComponent<Image>();
        pickImg.color = new Color(0.3f, 1f, 0.4f, 0.25f);
        card.pickedOverlay = picked;
        picked.SetActive(false);

        // Hover frame
        var hover = new GameObject("HoverFrame", typeof(RectTransform));
        hover.transform.SetParent(go.transform, false);
        var hrt = (RectTransform)hover.transform;
        hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
        hrt.offsetMin = new Vector2(-4, -4); hrt.offsetMax = new Vector2(4, 4);
        var hovImg = hover.AddComponent<Image>();
        hovImg.color = new Color(1f, 0.9f, 0.4f, 0.85f);
        card.hoverFrame = hover;
        hover.SetActive(false);

        return card;
    }

    // ============ Team Slot Panel ============
    static TeamSlotUI CreateTeamPanel(Transform parent, TeamSide side, BanPickConfig config)
    {
        bool ally = side == TeamSide.Ally;
        var go = new GameObject(ally ? "AllyPanel" : "EnemyPanel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(ally ? 0f : 1f, 0f);
        rt.anchorMax = new Vector2(ally ? 0f : 1f, 1f);
        rt.pivot = new Vector2(ally ? 0f : 1f, 0.5f);
        rt.anchoredPosition = new Vector2(ally ? 20 : -20, 0);
        rt.sizeDelta = new Vector2(240, 0);

        var slot = go.AddComponent<TeamSlotUI>();
        slot.side = side;
        slot.teamLabel = CreateText(go.transform, "TeamLabel", ally ? "YOUR TEAM" : "ENEMY TEAM", 28,
            new Vector2(0, -50), new Vector2(220, 36));
        slot.teamLabel.alignment = TextAlignmentOptions.Center;

        // Ban row label + slots
        CreateText(go.transform, "BanLabel", "BANS", 18, new Vector2(0, -100), new Vector2(220, 24))
            .alignment = TextAlignmentOptions.Center;
        slot.banSlots = new Image[config.bansPerTeam];
        for (int i = 0; i < config.bansPerTeam; i++)
        {
            slot.banSlots[i] = CreateSlot(go.transform, $"Ban_{i}",
                new Vector2(-60 + i * 70, -150), 60);
        }

        CreateText(go.transform, "PickLabel", "PICKS", 18, new Vector2(0, -210), new Vector2(220, 24))
            .alignment = TextAlignmentOptions.Center;
        slot.pickSlots = new Image[config.picksPerTeam];
        for (int i = 0; i < config.picksPerTeam; i++)
        {
            slot.pickSlots[i] = CreateSlot(go.transform, $"Pick_{i}",
                new Vector2(0, -270 - i * 90), 80);
        }
        return slot;
    }

    static Image CreateSlot(Transform parent, string name, Vector2 pos, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        var img = go.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.15f);
        return img;
    }

    // ============ Generic helpers ============
    static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize,
                                      Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.enableAutoSizing = false;
        return tmp;
    }
}
#endif
