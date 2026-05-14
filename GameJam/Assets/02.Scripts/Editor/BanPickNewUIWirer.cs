#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// AScene_BanPick 의 새 디자인 (ashyun1 배치) 자동 wire.
/// 사용자가 6 개 카드 (좌 3 / 우 3) + 좌우 밴 슬롯을 배치한 상태에서 실행.
///
/// 자동 인식 규칙:
/// - "카드" 후보 = Image 가 있고, 자식 중 TMP 가 6 개 이상 (이름 1 + 스탯 6) 인 GO
/// - X 좌표 기준 좌/우 그룹 → 좌=Ally, 우=Enemy. 각 그룹 안에서 Y 내림차순 → pick 0/1/2
/// - 각 카드 안의 TMP 들도 Y 내림차순으로 정렬해서 nameLabel + 스탯 6개 wire
/// - "흰 박스" portrait = 카드 안 Image 중 가장 큰 흰색
/// - "스킬 설명" / "궁극기 설명" 버튼 = Button 컴포넌트 중 Y 가장 아래 두 개
/// - 밴 슬롯 = 카드 그룹 위쪽에 있는 작은 박스 (좌/우 각 1개)
/// </summary>
public static class BanPickNewUIWirer
{
    const string AScenePath = "Assets/01.Scenes/AScene_BanPick.unity";

    [MenuItem("TFM/Setup AScene_BanPick Card Wiring", priority = -40)]
    public static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(AScenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"[TFM] {AScenePath} 열기 실패"); return; }

        // Canvas / EventSystem 보강
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("[TFM] Canvas 없음 — AScene_BanPick 에 Canvas 가 있어야 함"); return; }
        EnsureEventSystem();

        // 밴픽 씬에 잘못 들어있는 InGame 전용 컴포넌트 자동 제거
        // (FightUIController 가 있으면 Play 시 AScene_FightUI 가 Additive 로 올라와서 인게임 UI 가 겹쳐 보임)
        int inGameRemoved = 0;
        foreach (var c in Object.FindObjectsByType<FightUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { Debug.Log($"[BanPickWirer] FightUIController '{c.gameObject.name}' 제거 (InGame 전용)"); Object.DestroyImmediate(c.gameObject); inGameRemoved++; }
        foreach (var c in Object.FindObjectsByType<BattleManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { Debug.Log($"[BanPickWirer] BattleManager '{c.gameObject.name}' 제거 (InGame 전용)"); Object.DestroyImmediate(c.gameObject); inGameRemoved++; }
        foreach (var c in Object.FindObjectsByType<KillLogUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { Object.DestroyImmediate(c.gameObject); inGameRemoved++; }
        foreach (var c in Object.FindObjectsByType<KillScoreUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { Object.DestroyImmediate(c.gameObject); inGameRemoved++; }
        foreach (var c in Object.FindObjectsByType<DamageVignette>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { Object.DestroyImmediate(c.gameObject); inGameRemoved++; }
        if (inGameRemoved > 0) Debug.Log($"[BanPickWirer] InGame 잔여 컴포넌트 {inGameRemoved} 개 제거");

        // 1) 카드 후보 수집 — 우선 명시적 이름 (PickCard_Ally_N / PickCard_Enemy_N) 찾고, 없으면 TMP 개수 heuristic
        var allRects = canvas.GetComponentsInChildren<RectTransform>(true);
        var cardCandidates = new List<RectTransform>();
        var diagSamples = new List<(RectTransform rt, int tmpCount, int textCount)>();

        // 1a) 명시적 PickCard_Ally_N / PickCard_Enemy_N 우선
        var allyByName = new RectTransform[3];
        var enemyByName = new RectTransform[3];
        bool foundByName = false;
        foreach (var rt in allRects)
        {
            string n = rt.gameObject.name;
            if (n.StartsWith("PickCard_Ally_") && int.TryParse(n.Substring("PickCard_Ally_".Length), out int ai) && ai >= 0 && ai < 3)
            { allyByName[ai] = rt; foundByName = true; }
            else if (n.StartsWith("PickCard_Enemy_") && int.TryParse(n.Substring("PickCard_Enemy_".Length), out int ei) && ei >= 0 && ei < 3)
            { enemyByName[ei] = rt; foundByName = true; }
        }

        if (foundByName)
        {
            for (int i = 0; i < 3; i++) if (allyByName[i] != null)  cardCandidates.Add(allyByName[i]);
            for (int i = 0; i < 3; i++) if (enemyByName[i] != null) cardCandidates.Add(enemyByName[i]);
            Debug.Log($"[BanPickWirer] 명시적 이름 검색: Ally {allyByName.Count(x => x != null)} / Enemy {enemyByName.Count(x => x != null)}");
        }

        // 1b) Heuristic fallback — TMP 카운트
        if (cardCandidates.Count < 6)
        {
            foreach (var rt in allRects)
            {
                int tmpCount = rt.GetComponentsInChildren<TextMeshProUGUI>(true).Length;
                int textCount = rt.GetComponentsInChildren<Text>(true).Length;
                int total = tmpCount + textCount;
                if (total >= 3) diagSamples.Add((rt, tmpCount, textCount));
                if (total >= 5 && total <= 15 && !cardCandidates.Contains(rt))
                    cardCandidates.Add(rt);
            }

            // 중첩 제거 — INNER 만 남김
            var filtered = new List<RectTransform>();
            foreach (var c in cardCandidates)
            {
                bool hasDescendantIn = false;
                foreach (var other in cardCandidates)
                {
                    if (other == c) continue;
                    if (IsAncestor(c, other)) { hasDescendantIn = true; break; }
                }
                if (!hasDescendantIn) filtered.Add(c);
            }
            cardCandidates = filtered;
        }

        Debug.Log($"[BanPickWirer] 카드 후보 {cardCandidates.Count} 개 발견 (foundByName={foundByName})");

        // 진단 로그 — 카드 후보가 6 미만이면 텍스트 가진 모든 RT 상세 출력
        if (cardCandidates.Count < 6)
        {
            Debug.Log("[BanPickWirer] === 진단: 텍스트 3개 이상 있는 RectTransform 목록 ===");
            foreach (var s in diagSamples.OrderByDescending(x => x.tmpCount + x.textCount).Take(30))
            {
                string path = GetPath(s.rt);
                Debug.Log($"  '{path}' — TMP:{s.tmpCount}, legacy Text:{s.textCount}");
            }

            EditorUtility.DisplayDialog("카드 부족",
                $"카드 후보를 {cardCandidates.Count} 개 만 찾았습니다 (6 개 필요).\n\n" +
                "Console 에 텍스트 가진 모든 GameObject 목록을 출력했습니다.\n" +
                "거기서 카드 GO 의 자식 텍스트 개수를 확인하고 알려주세요.",
                "OK");
            return;
        }

        // 2) X 위치 기준 좌/우 그룹 (Ally / Enemy)
        cardCandidates.Sort((a, b) => GetWorldX(a).CompareTo(GetWorldX(b)));
        var allyCards  = cardCandidates.Take(cardCandidates.Count / 2).ToList();
        var enemyCards = cardCandidates.Skip(cardCandidates.Count / 2).ToList();
        // 각 그룹 안에서 Y 내림차순 (위→아래)
        allyCards.Sort((a, b)  => GetWorldY(b).CompareTo(GetWorldY(a)));
        enemyCards.Sort((a, b) => GetWorldY(b).CompareTo(GetWorldY(a)));

        // 3 개씩만 사용 (혹시 더 찾았으면 잘라냄)
        if (allyCards.Count > 3) allyCards = allyCards.Take(3).ToList();
        if (enemyCards.Count > 3) enemyCards = enemyCards.Take(3).ToList();

        // 3) 각 카드에 PickCardUI 부착 + 자식 wire
        int wired = 0;
        foreach (var c in allyCards) { WireCard(c, isAlly: true); wired++; }
        foreach (var c in enemyCards) { WireCard(c, isAlly: false); wired++; }

        // 4) 밴 슬롯 자동 추측 — "밴" 라벨 옆에 공간적으로 가까운 Image (PickCardUI 자식 제외)
        // PickCardUI 의 모든 자식 transform 을 exclude set 에 넣어 둠
        var excludePickTransforms = new HashSet<Transform>();
        foreach (var c in allyCards)
            foreach (var t in c.GetComponentsInChildren<Transform>(true)) excludePickTransforms.Add(t);
        foreach (var c in enemyCards)
            foreach (var t in c.GetComponentsInChildren<Transform>(true)) excludePickTransforms.Add(t);

        var banAlly = FindBanSlot(canvas, leftSide: true, excludePickTransforms);
        var banEnemy = FindBanSlot(canvas, leftSide: false, excludePickTransforms);
        if (banAlly != null) excludePickTransforms.Add(banAlly.transform);
        if (banEnemy != null) excludePickTransforms.Add(banEnemy.transform);

        // 5) TeamSlotUI 생성/wire — Ally / Enemy 각 GO 따로 만들고 PickCardUI[] / banSlots wire
        var slotRoot = new GameObject("TeamSlotRoot");
        slotRoot.transform.SetParent(canvas.transform, false);

        var allySlot = AttachTeamSlotUI(slotRoot, "AllyTeamSlot", TeamSide.Ally, allyCards, banAlly);
        var enemySlot = AttachTeamSlotUI(slotRoot, "EnemyTeamSlot", TeamSide.Enemy, enemyCards, banEnemy);

        // 6) BanPickManager 생성/wire
        var mgr = Object.FindFirstObjectByType<BanPickManager>();
        if (mgr == null)
        {
            var mgrGo = new GameObject("BanPickManager");
            mgr = mgrGo.AddComponent<BanPickManager>();
            mgrGo.AddComponent<BanPickAI>();
        }
        mgr.allySlots = allySlot;
        mgr.enemySlots = enemySlot;
        // 강제 — config 를 BanPickConfig.asset 으로 다시 wire (잘못된 config 가 wire 되어있을 수 있어 항상 덮어씀)
        var loadedConfig = AssetDatabase.LoadAssetAtPath<BanPickConfig>("Assets/06.ScriptableObjects/BanPickConfig.asset");
        if (loadedConfig != null) mgr.config = loadedConfig;
        else if (mgr.config == null) Debug.LogError("[BanPickWirer] BanPickConfig.asset 못 찾음 — TFM > Rebuild Champion Pool 먼저 실행");
        mgr.autoLoadBattleSceneOnDone = true;
        mgr.battleSceneName = "InGame";
        if (mgr.banSfx == null)
            mgr.banSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/05.Sounds/SFX/ban.mp3");
        if (mgr.pickSfx == null)
            mgr.pickSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/05.Sounds/SFX/pick.mp3");
        if (mgr.banPickBgm == null)
            mgr.banPickBgm = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/05.Sounds/BGM/ban_bgm.mp3");

        // PickResultBridge
        if (Object.FindFirstObjectByType<PickResultBridge>() == null)
        {
            var bridgeGo = new GameObject("PickResultBridge");
            bridgeGo.AddComponent<PickResultBridge>();
        }

        // HUD 가 필요해서 BanPickManager 가 작동함 — Top 의 phase/turn/timer 텍스트 wire
        WireHud(mgr, canvas);

        // 카드 그리드 (가운데 9개) 도 wire — 픽 선택 가능하게
        WireCardGrid(mgr, canvas, excludePickTransforms);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[BanPickWirer] 완료 — 카드 {wired} 개 wire, 밴슬롯 ally:{banAlly != null} enemy:{banEnemy != null}");
        EditorUtility.DisplayDialog("Wire 완료",
            $"카드 {wired} 개 ({allyCards.Count} ally + {enemyCards.Count} enemy) 자동 wire 완료.\n" +
            "Hierarchy 의 카드 GO 들 인스펙터에서 PickCardUI 의 wire 가 맞는지 확인하세요.",
            "OK");
    }

    static TeamSlotUI AttachTeamSlotUI(GameObject root, string name, TeamSide side, List<RectTransform> cards, Image banImage)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var slot = go.AddComponent<TeamSlotUI>();
        slot.side = side;

        // PickCardUI 배열
        slot.pickCards = new PickCardUI[cards.Count];
        for (int i = 0; i < cards.Count; i++)
        {
            slot.pickCards[i] = cards[i].GetComponent<PickCardUI>();
        }
        // 밴 슬롯
        if (banImage != null) slot.banSlots = new Image[] { banImage };
        else slot.banSlots = new Image[0];

        // pickSlots 는 비워둠 (PickCardUI 가 우선)
        slot.pickSlots = new Image[0];

        return slot;
    }

    static void WireCard(RectTransform card, bool isAlly)
    {
        var pc = card.GetComponent<PickCardUI>();
        if (pc == null) pc = card.gameObject.AddComponent<PickCardUI>();
        pc.isAlly = isAlly;

        // 1순위 — 명시적 자식 이름 (Character_stat 디자인) 매핑
        // Attack_Text → atkText, HP_Text → hpText, Armor_Text → defText 등
        TextMeshProUGUI byName(string n)
        {
            foreach (var t in card.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t.gameObject.name == n) return t;
                // 자식의 자식이 TMP 면 그 자식도 매칭 ("Attack_Text" GO 안에 들어있는 TMP)
                if (t.transform.parent != null && t.transform.parent.gameObject.name == n) return t;
            }
            return null;
        }

        pc.atkText       = byName("Attack_Text");
        pc.hpText        = byName("HP_Text");
        pc.defText       = byName("Armor_Text");
        pc.rangeText     = byName("Attack_range_Text") ?? byName("AttackRange_Text") ?? byName("Range_Text");
        pc.moveSpeedText = byName("Speed_Text") ?? byName("MoveSpeed_Text");
        pc.atkSpeedText  = byName("AttackSpeed_Text") ?? byName("Atk_Speed_Text");

        // 이름 라벨 — "캐릭터 이름" / "Name" / "ChampionName" 등
        pc.nameLabel = byName("캐릭터 이름") ?? byName("Name") ?? byName("ChampionName") ?? byName("Name_Text");

        // 2순위 — 명시적 이름 못 찾으면 Y 정렬 heuristic
        int filledCount = (pc.atkText != null ? 1 : 0) + (pc.hpText != null ? 1 : 0)
                        + (pc.defText != null ? 1 : 0) + (pc.rangeText != null ? 1 : 0)
                        + (pc.moveSpeedText != null ? 1 : 0) + (pc.atkSpeedText != null ? 1 : 0);
        Debug.Log($"[WireCard] '{card.name}' 명시적 이름 매핑 결과: 스탯 {filledCount}/6, name:{pc.nameLabel != null}");

        if (filledCount < 6)
        {
            // Y 내림차순 fallback
            var tmps = new List<TextMeshProUGUI>();
            foreach (var t in card.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (IsDescendantWithin(t.transform, card, 6)) tmps.Add(t);
            tmps.Sort((a, b) => GetWorldY(b.rectTransform).CompareTo(GetWorldY(a.rectTransform)));

            if (pc.nameLabel == null && tmps.Count > 0) pc.nameLabel = tmps[0];
            if (pc.atkText == null && tmps.Count > 1) pc.atkText       = tmps[1];
            if (pc.hpText == null && tmps.Count > 2) pc.hpText         = tmps[2];
            if (pc.defText == null && tmps.Count > 3) pc.defText       = tmps[3];
            if (pc.rangeText == null && tmps.Count > 4) pc.rangeText   = tmps[4];
            if (pc.moveSpeedText == null && tmps.Count > 5) pc.moveSpeedText = tmps[5];
            if (pc.atkSpeedText == null && tmps.Count > 6) pc.atkSpeedText  = tmps[6];
        }

        // 포트레이트 = 카드 안 Image 중 가장 큰 흰색 (혹은 가장 큰 거)
        Image portrait = null;
        float maxArea = 0f;
        foreach (var img in card.GetComponentsInChildren<Image>(true))
        {
            if (img.gameObject == card.gameObject) continue;  // 카드 자체 배경 제외
            if (img.GetComponent<Button>() != null) continue; // 버튼 제외
            var rt = img.rectTransform;
            float area = Mathf.Abs(rt.sizeDelta.x * rt.sizeDelta.y);
            // 흰 박스 우선 + 가장 큰 거
            bool isWhite = img.color.r > 0.9f && img.color.g > 0.9f && img.color.b > 0.9f;
            if (isWhite && area > maxArea) { portrait = img; maxArea = area; }
        }
        // 흰 박스 못 찾으면 그냥 가장 큰 Image
        if (portrait == null)
        {
            maxArea = 0f;
            foreach (var img in card.GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject == card.gameObject) continue;
                if (img.GetComponent<Button>() != null) continue;
                var rt = img.rectTransform;
                float area = Mathf.Abs(rt.sizeDelta.x * rt.sizeDelta.y);
                if (area > maxArea) { portrait = img; maxArea = area; }
            }
        }
        pc.portrait = portrait;

        // 버튼 — Y 가장 아래 두 개 = skillDescButton, ultDescButton
        var buttons = new List<Button>(card.GetComponentsInChildren<Button>(true));
        buttons.Sort((a, b) => GetWorldY(a.GetComponent<RectTransform>()).CompareTo(GetWorldY(b.GetComponent<RectTransform>())));
        if (buttons.Count >= 1) pc.skillDescButton = buttons[buttons.Count - 2 < 0 ? 0 : buttons.Count - 2];
        if (buttons.Count >= 2) pc.ultDescButton = buttons[buttons.Count - 1];
        // 둘이 겹치면 정정
        if (buttons.Count >= 2)
        {
            pc.skillDescButton = buttons[buttons.Count - 2];
            pc.ultDescButton   = buttons[buttons.Count - 1];
        }
    }

    static void WireHud(BanPickManager mgr, Canvas canvas)
    {
        // "밴 단계" / "내 차례 · 밴" / "20" 타이머 텍스트 찾기
        var hudGo = new GameObject("BanPickHUD");
        hudGo.transform.SetParent(canvas.transform, false);
        var hud = hudGo.AddComponent<BanPickHUD>();

        TextMeshProUGUI phaseLabel = null, turnLabel = null, timerLabel = null;
        Image timerFill = null;

        foreach (var tmp in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string t = (tmp.text ?? "").Trim();
            if (t.Contains("밴 단계") || t.Contains("픽 단계")) phaseLabel = tmp;
            else if (t.Contains("내 차례") || t.Contains("상대 차례")) turnLabel = tmp;
            else if (t == "20" || (t.Length <= 2 && int.TryParse(t, out _))) timerLabel = tmp;
        }
        // 노란 원형 타이머 = 노란계열 Image
        foreach (var img in canvas.GetComponentsInChildren<Image>(true))
        {
            if (img.color.r > 0.9f && img.color.g > 0.7f && img.color.b < 0.5f && img.fillMethod == Image.FillMethod.Radial360)
            { timerFill = img; break; }
        }

        hud.phaseLabel = phaseLabel;
        hud.turnLabel = turnLabel;
        hud.timerLabel = timerLabel;
        hud.timerFill = timerFill;
        hud.allySlotUI = mgr.allySlots;
        hud.enemySlotUI = mgr.enemySlots;
        mgr.hud = hud;

        Debug.Log($"[BanPickWirer] HUD wire — phase:{phaseLabel != null}, turn:{turnLabel != null}, timer:{timerLabel != null}, fill:{timerFill != null}");
    }

    static void WireCardGrid(BanPickManager mgr, Canvas canvas, HashSet<Transform> excludeTransforms)
    {
        // 가운데 카드 그리드 = pickCards 가 아닌, 화면 중앙 X 밴드의 사각형 RectTransform
        var canvasRt = canvas.transform as RectTransform;
        Vector3[] canvasCorners = new Vector3[4];
        canvasRt.GetWorldCorners(canvasCorners);
        float canvasLeft  = canvasCorners[0].x;
        float canvasRight = canvasCorners[2].x;
        float centerXMin  = canvasLeft + (canvasRight - canvasLeft) * 0.22f;
        float centerXMax  = canvasLeft + (canvasRight - canvasLeft) * 0.78f;

        // 1) 1차 후보 — "Name"/"Role" TMP 가 있는 RT 의 parent + ChampionCardUI 컴포넌트 가진 RT
        var grid = new List<RectTransform>();
        var seen = new HashSet<RectTransform>();

        // 1a) ChampionCardUI 컴포넌트 가진 RT 직접 — 이전 wirer 가 부착한 거
        foreach (var cu in canvas.GetComponentsInChildren<ChampionCardUI>(true))
        {
            var rt = cu.transform as RectTransform;
            if (rt == null) continue;
            if (IsExcluded(rt, excludeTransforms)) continue;
            if (seen.Contains(rt)) continue;
            seen.Add(rt);
            grid.Add(rt);
        }

        // 1b) "Name"/"Role" TMP 의 parent
        foreach (var tmp in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string t = (tmp.text ?? "").Trim();
            if (t != "Name" && t != "Role") continue;
            var parent = tmp.transform.parent as RectTransform;
            if (parent == null) continue;
            if (seen.Contains(parent)) continue;
            if (IsExcluded(parent, excludeTransforms)) continue;
            seen.Add(parent);
            grid.Add(parent);
        }

        // 2) 2차 후보 — 중앙 X 밴드에서 사각형 Image 자식을 가진 RT, 위 1차 후보에 안 들어간 거
        if (grid.Count < 9)
        {
            foreach (var rt in canvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (seen.Contains(rt)) continue;
                if (IsExcluded(rt, excludeTransforms)) continue;

                float wx = GetWorldX(rt);
                if (wx < centerXMin || wx > centerXMax) continue;

                // rect 사이즈 적당 (카드 크기)
                Vector2 sz = rt.sizeDelta;
                if (sz.x < 80 || sz.x > 280) continue;
                if (sz.y < 100 || sz.y > 320) continue;

                // 자식에 Image 가 있어야 카드 후보
                bool hasImg = false;
                foreach (var img in rt.GetComponentsInChildren<Image>(true))
                {
                    if (img.transform == rt) continue;
                    if (excludeTransforms.Contains(img.transform)) continue;
                    hasImg = true; break;
                }
                if (!hasImg) continue;

                seen.Add(rt);
                grid.Add(rt);
            }
        }

        Debug.Log($"[BanPickWirer] 가운데 카드 그리드 후보 {grid.Count} 개 (centerXMin={centerXMin:F0}, max={centerXMax:F0})");
        if (grid.Count == 0) return;

        // Y→X 정렬 (위→아래, 좌→우)
        grid.Sort((a, b) =>
        {
            float dy = GetWorldY(b) - GetWorldY(a);
            if (Mathf.Abs(dy) > 30f) return dy > 0 ? 1 : -1;
            return GetWorldX(a).CompareTo(GetWorldX(b));
        });

        // 진단 — 발견된 모든 grid 카드 상세 출력
        Debug.Log($"[BanPickWirer] === 발견된 모든 중앙 카드 (pre-dedup) ===");
        for (int i = 0; i < grid.Count; i++)
        {
            var rt = grid[i];
            Debug.Log($"  [{i}] '{GetPath(rt)}' pos=({GetWorldX(rt):F0},{GetWorldY(rt):F0}) size=({rt.sizeDelta.x:F0}×{rt.sizeDelta.y:F0}) active={rt.gameObject.activeSelf}");
        }

        // 중복 제거 — 진짜로 같은 위치에 stacked 된 거 (5px 이내). 그리드 셀 간격(보통 100+px)보다 훨씬 작음
        const float dedupThreshold = 5f;
        var dedup = new List<RectTransform>();
        foreach (var rt in grid)
        {
            float x = GetWorldX(rt), y = GetWorldY(rt);
            bool isDup = false;
            foreach (var k in dedup)
                if (Mathf.Abs(GetWorldX(k) - x) < dedupThreshold && Mathf.Abs(GetWorldY(k) - y) < dedupThreshold)
                { isDup = true; break; }
            if (isDup) continue;
            dedup.Add(rt);
        }
        Debug.Log($"[BanPickWirer] 중복 제거 후 {dedup.Count} 개 (원래 {grid.Count})");

        // 모든 keepers SetActive(true) — 이전 wirer 실수로 꺼진 카드 복구
        foreach (var rt in dedup) if (rt != null) rt.gameObject.SetActive(true);

        // mgr.cards 배열 + 잉여 OLD 카드 자동 삭제
        // 안전 규칙: 삭제 대상은 OLD BanPickUIBuilder 가 만든 "Card_NN" 패턴만. NEW UI (PickCard_Ally/Enemy_N) 는 안전
        int poolCount = mgr.config != null ? mgr.config.championPool.Count : dedup.Count;
        if (dedup.Count > poolCount)
        {
            int extraCount = dedup.Count - poolCount;
            var extras = dedup.GetRange(poolCount, extraCount);
            int deletedOld = 0, skippedNew = 0;
            foreach (var rt in extras)
            {
                if (rt == null || rt.gameObject == null) continue;
                string n = rt.gameObject.name;
                // 옛 builder 의 Card_NN / Card_NN (M) 패턴만 삭제. 그 외 (사용자 명명) 는 보존
                bool isOldCard = System.Text.RegularExpressions.Regex.IsMatch(n, @"^Card_\d{1,3}( \(\d+\))?$");
                if (isOldCard)
                {
                    Debug.Log($"[BanPickWirer] 잉여 OLD 카드 삭제: '{GetPath(rt)}'");
                    Object.DestroyImmediate(rt.gameObject);
                    deletedOld++;
                }
                else
                {
                    Debug.LogWarning($"[BanPickWirer] 잉여이지만 OLD 패턴 아님 — 보존: '{GetPath(rt)}' (이름:'{n}')");
                    skippedNew++;
                }
            }
            Debug.Log($"[BanPickWirer] 잉여 카드 처리 — 삭제 {deletedOld} 개, 보존 {skippedNew} 개");
            dedup = dedup.GetRange(0, poolCount);
        }

        // final list
        grid = dedup;

        var cards = new List<ChampionCardUI>();
        for (int i = 0; i < grid.Count; i++)
        {
            var cu = grid[i].GetComponent<ChampionCardUI>();
            if (cu == null) cu = grid[i].gameObject.AddComponent<ChampionCardUI>();

            // 흰 박스 portrait — 카드 안 Image 중 가장 큰 거 (자기 자신 제외)
            Image portrait = null, background = null;
            float maxArea = 0f;
            foreach (var img in grid[i].GetComponentsInChildren<Image>(true))
            {
                if (img.transform == grid[i]) { background = img; continue; }
                var rt = img.rectTransform;
                float area = Mathf.Abs(rt.sizeDelta.x * rt.sizeDelta.y);
                if (area > maxArea) { maxArea = area; portrait = img; }
            }
            cu.portrait = portrait;
            cu.background = background;

            // 자식 TMP — Name / Role
            foreach (var tmp in grid[i].GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                string t = (tmp.text ?? "").Trim();
                if (t == "Name" || tmp.gameObject.name.ToLower().Contains("name")) cu.nameLabel = tmp;
                else if (t == "Role" || tmp.gameObject.name.ToLower().Contains("role")) cu.roleLabel = tmp;
            }

            // 클릭 가능하게 — Button 보강
            var btn = grid[i].GetComponent<Button>();
            if (btn == null)
            {
                btn = grid[i].gameObject.AddComponent<Button>();
                if (background != null) btn.targetGraphic = background;
                else if (portrait != null) btn.targetGraphic = portrait;
            }
            cards.Add(cu);
        }
        mgr.cards = cards.ToArray();
        Debug.Log($"[BanPickWirer] 중앙 카드 {mgr.cards.Length} 개 wire 완료. championPool 크기: {(mgr.config != null ? mgr.config.championPool.Count : -1)}");
    }

    static bool IsExcluded(Transform t, HashSet<Transform> excludes)
    {
        Transform cur = t;
        while (cur != null)
        {
            if (excludes.Contains(cur)) return true;
            cur = cur.parent;
        }
        return false;
    }

    static Image FindBanSlot(Canvas canvas, bool leftSide, HashSet<Transform> excludeTransforms)
    {
        // 1) "밴" 글자 TMP — 좌/우 사이드 매칭
        var canvasRt = canvas.transform as RectTransform;
        Vector3[] canvasCorners = new Vector3[4];
        canvasRt.GetWorldCorners(canvasCorners);
        float canvasMidX = (canvasCorners[0].x + canvasCorners[2].x) * 0.5f;

        TextMeshProUGUI banLabel = null;
        foreach (var tmp in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string t = (tmp.text ?? "").Trim();
            if (t != "밴") continue;
            float wx = GetWorldX(tmp.rectTransform);
            bool isLeft = wx < canvasMidX;
            if (isLeft == leftSide) { banLabel = tmp; break; }
        }
        if (banLabel == null) { Debug.LogWarning($"[BanPickWirer] '밴' TMP 없음 (leftSide={leftSide})"); return null; }

        // 2) 캔버스 전체에서 "밴" 라벨에 공간적으로 가장 가까운 Image 찾기 — PickCardUI 자식 제외
        Vector3 banPos = banLabel.rectTransform.position;
        Image best = null;
        float bestDist = float.MaxValue;
        foreach (var img in canvas.GetComponentsInChildren<Image>(true))
        {
            if (img.transform == canvas.transform) continue;
            if (IsExcluded(img.transform, excludeTransforms)) continue;
            // PickCardUI 가 붙은 GO 자체도 제외
            if (img.GetComponentInParent<PickCardUI>() != null) continue;

            var rt = img.rectTransform;
            // 크기 필터: 너무 작거나 너무 큰 거 제외 (밴 슬롯은 보통 60~120 정사각형 정도)
            float w = Mathf.Abs(rt.sizeDelta.x), h = Mathf.Abs(rt.sizeDelta.y);
            if (w < 30 || w > 200) continue;
            if (h < 30 || h > 200) continue;

            float d = Vector3.Distance(banPos, rt.position);
            // "밴" 라벨에서 200px 이내만
            if (d > 200f) continue;
            if (d < bestDist) { bestDist = d; best = img; }
        }
        Debug.Log($"[BanPickWirer] 밴 슬롯 (leftSide={leftSide}): {(best != null ? best.gameObject.name : "null")}, dist:{bestDist:F0}");
        return best;
    }

    static string GetPath(Transform t)
    {
        var stack = new System.Collections.Generic.Stack<string>();
        while (t != null) { stack.Push(t.name); t = t.parent; }
        return string.Join("/", stack.ToArray());
    }

    static bool IsDescendantWithin(Transform t, Transform root, int maxDepth)
    {
        int d = 0;
        while (t != null && d <= maxDepth)
        {
            if (t == root) return true;
            t = t.parent;
            d++;
        }
        return false;
    }

    static bool IsAncestor(Transform potentialAncestor, Transform t)
    {
        Transform cur = t.parent;
        while (cur != null) { if (cur == potentialAncestor) return true; cur = cur.parent; }
        return false;
    }

    static float GetWorldX(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return (corners[0].x + corners[2].x) * 0.5f;
    }
    static float GetWorldY(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return (corners[0].y + corners[2].y) * 0.5f;
    }

    static void EnsureEventSystem()
    {
        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
        var newType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (newType != null) go.AddComponent(newType);
        else go.AddComponent<StandaloneInputModule>();
    }
}
#endif
