#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TourmentInstaller
{
    const string TourmentPath = "Assets/01.Scenes/Tourment.unity";

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

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[TFM] Tourment 셋업 완료 — DropAnimator {animated} 개 (상단 {topTexts.Count} + 하단 {bottomChars.Count}), SpumIdlePlayer {idleAdded} 개 부착, 5초 후 → AScene_BanPick");
    }
}
#endif
