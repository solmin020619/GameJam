#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// AScene_Wait 자동 셋업:
///  1) WaitSceneController GameObject 추가
///  2) Character_1/2/3 GO 없으면 SPUM 프리팹 3개 인스턴스 생성
///  3) 각 캐릭터에 CharacterWander 추가 + 이름 (한종욱/안승현/김솔민)
///  4) EventSystem 의 InputModule 누락 시 자동 보강 (New Input System 우선)
/// </summary>
public static class WaitSceneInstaller
{
    const string WaitScenePath = "Assets/01.Scenes/AScene_Wait.unity";
    static readonly string[] PlayerNames = { "한종욱", "안승현", "김솔민" };
    static readonly string[] FallbackPrefabs = {
        "Assets/03.Prefabs/Berserker_Fighter.prefab",
        "Assets/03.Prefabs/Cleric_heal.prefab",
        "Assets/03.Prefabs/Crusher_Distruptor.prefab",
    };
    // 카메라 중심 기준 offset (런타임에 동적 계산)
    static readonly Vector3[] SpawnOffsetsFromCamera = {
        new Vector3(-2f, -1f, 0f),
        new Vector3( 0f, -1.5f, 0f),
        new Vector3( 2f, -1f, 0f),
    };
    const float CharacterScale = 2.5f; // SPUM 기본 1 → 화면에 보일 만한 크기로

    [MenuItem("TFM/Setup Wait Scene (한종욱/안승현/김솔민 등장)", priority = -48)]
    public static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(WaitScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[TFM] {WaitScenePath} 열기 실패");
            return;
        }

        // 1) WaitSceneController
        var existing = Object.FindFirstObjectByType<WaitSceneController>();
        if (existing == null)
        {
            var go = new GameObject("WaitSceneController");
            go.AddComponent<WaitSceneController>();
            Debug.Log("[TFM] WaitSceneController 추가");
        }

        // 카메라 위치 추출 — Spawn 좌표를 카메라 기준으로 동적 계산
        Camera cam = Object.FindFirstObjectByType<Camera>();
        Vector3 camCenter = cam != null
            ? new Vector3(cam.transform.position.x, cam.transform.position.y, 0)
            : Vector3.zero;
        Debug.Log($"[TFM] 카메라 위치 ({camCenter.x}, {camCenter.y}) 기준으로 캐릭터 spawn");

        // Canvas RenderMode 는 그대로 (ScreenSpaceOverlay) — 다른 씬에 영향 없게
        // 대신 BG 의 Image alpha 를 살짝 낮춰서 캐릭터가 BG 뒤로 비치게
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            // 혹시 이전 메뉴 실행으로 ScreenSpaceCamera 로 변경됐다면 원복
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                canvas.sortingOrder = 0;
                Debug.Log("[TFM] Canvas RenderMode 원복 → ScreenSpaceOverlay");
            }

            // BG 의 Image alpha 낮춤 → 캐릭터가 BG 뒤로 비치게
            var bg = GameObject.Find("BG");
            if (bg != null)
            {
                var img = bg.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    img.raycastTarget = false; // 버튼 클릭 막지 않게
                    var c = img.color;
                    c.a = 0.55f;
                    img.color = c;
                    Debug.Log("[TFM] BG alpha 0.55 적용 — 캐릭터가 비치게");
                }
            }
        }

        // 2) EventSystem 의 InputModule 보강
        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            var esGo = new GameObject("EventSystem", typeof(EventSystem));
            es = esGo.GetComponent<EventSystem>();
        }
        EnsureInputModule(es.gameObject);

        // 3) Character_1/2/3 추가 (없으면 SPUM 프리팹 인스턴스)
        // 기존 SPUM 이름 또는 Character_ 이름의 캐릭터 모두 제거 (중복 방지)
        foreach (var oldName in new[] { "Character_1", "Character_2", "Character_3",
                                         "Berserker_Fighter", "Cleric_heal", "Crusher_Distruptor",
                                         "Berserker_Fighter(Clone)", "Cleric_heal(Clone)", "Crusher_Distruptor(Clone)" })
        {
            var old = GameObject.Find(oldName);
            if (old != null && old.GetComponent<CharacterWander>() == null)
            {
                // wander 컴포넌트 없는 거(이전 잘못 생성된 거) 제거
                Object.DestroyImmediate(old);
            }
        }

        for (int i = 0; i < 3; i++)
        {
            string targetName = $"Character_{i + 1}";
            var ch = GameObject.Find(targetName);
            if (ch == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackPrefabs[i]);
                if (prefab == null)
                {
                    Debug.LogWarning($"[TFM] {FallbackPrefabs[i]} 프리팹 없음");
                    continue;
                }
                ch = Object.Instantiate(prefab);
                ch.name = targetName;
                Debug.Log($"[TFM] {targetName} 생성 — {prefab.name} 인스턴스");
            }

            // ★ 핵심 수정: Canvas 자식에서 빼서 scene root 로 + Layer 를 Default 로
            if (ch.GetComponentInParent<Canvas>() != null)
            {
                ch.transform.SetParent(null);
                Debug.Log($"[TFM] {targetName} Canvas 자식에서 root 로 이동");
            }
            SetLayerRecursive(ch, 0); // Default layer
            // 위치 강제 reset — 카메라 중심 기준 offset 으로 (카메라 어디든 보임)
            ch.transform.position = camCenter + SpawnOffsetsFromCamera[i];
            ch.transform.localScale = Vector3.one * CharacterScale;
            ch.SetActive(true);

            StripBattleComponents(ch);
            StripMissingScripts(ch);

            var wander = ch.GetComponent<CharacterWander>();
            if (wander == null) wander = ch.AddComponent<CharacterWander>();
            wander.displayName = PlayerNames[i];
            wander.labelFontSize = 1f;     // 강제 적용 — Inspector 옛 값 무시
            wander.labelYOffset = 0.9f;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TFM] AScene_Wait 셋업 완료 — 한종욱/안승현/김솔민 wandering + 버튼 wire");
    }

    // 전투 전용 컴포넌트 (자식 포함) 모두 제거 — Wait 씬에서는 NullRef 방지용
    static void StripBattleComponents(GameObject root)
    {
        foreach (var cu in root.GetComponentsInChildren<ChampionUnit>(true))
            Object.DestroyImmediate(cu);
        foreach (var rb in root.GetComponentsInChildren<Rigidbody2D>(true))
            Object.DestroyImmediate(rb);
        foreach (var col in root.GetComponentsInChildren<Collider2D>(true))
            Object.DestroyImmediate(col);
    }

    // missing script 자식까지 다 제거 (SPUM 프리팹의 잔여 누락 스크립트)
    static void StripMissingScripts(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    static void EnsureInputModule(GameObject esGo)
    {
        // New Input System 우선
        var newType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (newType != null)
        {
            // 기존 Standalone 있으면 제거
            var legacy = esGo.GetComponent<StandaloneInputModule>();
            if (legacy != null) Object.DestroyImmediate(legacy);
            if (esGo.GetComponent(newType) == null) esGo.AddComponent(newType);
        }
        else
        {
            if (esGo.GetComponent<StandaloneInputModule>() == null)
                esGo.AddComponent<StandaloneInputModule>();
        }
    }
}
#endif
