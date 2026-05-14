#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class TourmentInstaller
{
    const string TourmentPath = "Assets/01.Scenes/Tourment.unity";
    const string ShinguLogoPath = "Assets/04.Images/Team logo/Shingu_logo.png";
    const string YonseiLogoPath = "Assets/04.Images/Team logo/YONSEI.png";

    [MenuItem("TFM/Setup Tourment Scene (5s → BanPick)", priority = -43)]
    public static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(TourmentPath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"[TFM] {TourmentPath} 열기 실패"); return; }

        var ctrl = Object.FindFirstObjectByType<TourmentSceneController>();
        if (ctrl == null)
        {
            var go = new GameObject("TourmentSceneController");
            ctrl = go.AddComponent<TourmentSceneController>();
            Debug.Log("[TFM] TourmentSceneController 추가");
        }
        // 5초로 강제 셋팅 (이미 직렬화된 값 덮어쓰기)
        ctrl.autoNextSeconds = 5f;

        // 매칭 대상 — 상단 학교명 ("신구대학교" / "연세대학교") + 하단 전광판 글자별 ("신구겜콘" / "연세컴공" 8글자)
        var singleChars = new System.Collections.Generic.HashSet<string> { "신", "구", "갬", "겜", "콘", "연", "세", "컴", "공" };
        var topTexts = new System.Collections.Generic.List<TMPro.TextMeshProUGUI>();
        var bottomChars = new System.Collections.Generic.List<TMPro.TextMeshProUGUI>();

        foreach (var tmp in Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tmp.text == null) continue;
            string t = tmp.text.Trim();

            // 상단 학교명 — 길이가 긴 텍스트 (신구대학교, 연세대학교)
            if (t.Contains("신구대") || t.Contains("연세대"))
            {
                topTexts.Add(tmp);
            }
            // 하단 전광판 — 단일 글자 텍스트
            else if (t.Length <= 2 && singleChars.Contains(t))
            {
                bottomChars.Add(tmp);
            }
            // 추가 fallback — "신구겜콘" / "연세컴공" 통째로 들어있는 경우
            else if (t == "신구겜콘" || t == "연세컴공")
            {
                bottomChars.Add(tmp);
            }
        }

        // 하단 글자들을 화면 좌→우 순서로 정렬 (월드 X 좌표 기준)
        bottomChars.Sort((a, b) => a.rectTransform.position.x.CompareTo(b.rectTransform.position.x));

        int animated = 0;

        // 상단 학교명 부착 (먼저 떨어짐, 동시에)
        foreach (var tmp in topTexts)
        {
            if (tmp.GetComponent<DropTextAnimator>() == null)
            {
                var anim = tmp.gameObject.AddComponent<DropTextAnimator>();
                anim.startDelay = 0f;
                animated++;
            }
        }

        // 하단 전광판 글자 — 좌→우 순서대로 0.25s 간격 (천천히 보이게)
        float delay = 1.2f; // 상단 학교명 끝나갈 즈음 시작
        foreach (var tmp in bottomChars)
        {
            if (tmp.GetComponent<DropTextAnimator>() == null)
            {
                var anim = tmp.gameObject.AddComponent<DropTextAnimator>();
                anim.startDelay = delay;
                anim.charInterval = 0f; // 단일 글자이므로 char 간격 불필요
                delay += 0.25f;
                animated++;
            }
        }

        // SPUM 캐릭터 idle 자동 재생 — 숨쉬는 느낌 복원
        // GetComponentInChildren 가 self 도 포함하므로 spum.gameObject 에 바로 붙이면 됨
        int idleAdded = 0;
        foreach (var spum in Object.FindObjectsByType<SPUM_Prefabs>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (spum.GetComponent<SpumIdlePlayer>() == null)
            {
                spum.gameObject.AddComponent<SpumIdlePlayer>();
                idleAdded++;
            }
        }

        // ★ 신구 로고 자동 배치 — "신구대학교" TMP 위치 옆 (대칭으로 YONSEI 가 있는 곳 참고)
        int logoAdded = AddSchoolLogos(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[TFM] Tourment 셋업 완료 — DropAnimator {animated} 개 (상단 {topTexts.Count} + 하단 {bottomChars.Count}), SpumIdlePlayer {idleAdded} 개 부착, 로고 {logoAdded} 개 추가, 5초 후 → AScene_BanPick");
    }

    /// <summary>신구대학교 / 연세대학교 TMP 옆에 로고 Image 자동 생성</summary>
    static int AddSchoolLogos(UnityEngine.SceneManagement.Scene scene)
    {
        var shinguSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShinguLogoPath);
        var yonseiSprite = AssetDatabase.LoadAssetAtPath<Sprite>(YonseiLogoPath);

        TMPro.TextMeshProUGUI shinguLabel = null, yonseiLabel = null;
        foreach (var tmp in Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tmp.text == null) continue;
            string t = tmp.text.Trim();
            if (t.Contains("신구대") && shinguLabel == null) shinguLabel = tmp;
            if (t.Contains("연세대") && yonseiLabel == null) yonseiLabel = tmp;
        }

        int added = 0;

        // 기존 로고 있으면 skip
        bool hasShinguLogo = false, hasYonseiLogo = false;
        foreach (var img in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (img.sprite == shinguSprite) hasShinguLogo = true;
            if (img.sprite == yonseiSprite) hasYonseiLogo = true;
        }

        if (shinguLabel != null && shinguSprite != null && !hasShinguLogo)
        {
            AddLogoNextToLabel(shinguLabel, shinguSprite, "Shingu_Logo_Tourment");
            added++;
        }
        if (yonseiLabel != null && yonseiSprite != null && !hasYonseiLogo)
        {
            AddLogoNextToLabel(yonseiLabel, yonseiSprite, "Yonsei_Logo_Tourment");
            added++;
        }
        return added;
    }

    static void AddLogoNextToLabel(TMPro.TextMeshProUGUI label, Sprite sprite, string goName)
    {
        // 라벨의 부모를 parent 로 — 같은 layer 에 sibling 으로 추가
        var parent = label.transform.parent;
        if (parent == null) return;

        var go = new GameObject(goName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;

        // 라벨의 anchored 정보 복사 + 가로로 살짝 오른쪽 (또는 왼쪽) — user 가 수동 조정
        var labelRt = label.rectTransform;
        rt.anchorMin = labelRt.anchorMin;
        rt.anchorMax = labelRt.anchorMax;
        rt.pivot = labelRt.pivot;
        // 라벨 옆에 같은 Y, 옆으로 80px (대략적인 위치 — user 가 조정)
        var p = labelRt.anchoredPosition;
        rt.anchoredPosition = new Vector2(p.x + labelRt.sizeDelta.x * 0.5f + 50f, p.y);
        rt.sizeDelta = new Vector2(80, 80);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = false;
        Debug.Log($"[Tourment] 로고 '{goName}' 생성 ({sprite.name}) — 라벨 '{label.text.Trim()}' 옆에");
    }
}
#endif
