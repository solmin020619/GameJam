using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// AScene_Wait — 경기장 이동(BanPick) / 뒤로가기(Lobby) 버튼 wire + 캐릭터 wandering 자동 설정.
/// 두 버튼은 인스펙터에서 직접 끌어넣거나, 자동 검색 (MoveButton / MoveButton (1)).
/// </summary>
public class WaitSceneController : MonoBehaviour
{
    [Header("Scene Names")]
    public string forwardScene = "BanPick";   // 경기장 이동
    public string backScene    = "Lobby";     // 뒤로가기

    [Header("Buttons (인스펙터에서 wire 또는 자동검색)")]
    public Button moveButton;     // → BanPick 으로
    public Button backButton;     // → Lobby 로

    void Awake()
    {
        // 런타임 안전망 — Wait 씬 안에 남아있는 ChampionUnit / Rigidbody2D / Collider2D 강제 제거
        foreach (var cu in Object.FindObjectsByType<ChampionUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(cu);
        foreach (var rb in Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(rb);
        foreach (var col in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(col);

        // Canvas worldCamera 자동 wire (안전망 — ScreenSpaceCamera 모드일 때만)
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null)
            {
                c.worldCamera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            }
        }

        // ★ 핵심 — 캐릭터들을 카메라 시야 안으로 강제 이동 (LoadScene 진입 시에도 안 보이는 문제 fix)
        var mainCam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (mainCam != null)
        {
            Vector3 c0 = mainCam.transform.position;
            Vector3[] offsets = {
                new Vector3(-2.5f, -1f, 0f),
                new Vector3( 0f, -1.5f, 0f),
                new Vector3( 2.5f, -1f, 0f),
            };
            int idx = 0;
            foreach (var w in Object.FindObjectsByType<CharacterWander>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (idx >= 3) break;
                Vector3 newPos = new Vector3(c0.x + offsets[idx].x, c0.y + offsets[idx].y, 0f);
                w.transform.position = newPos;
                w.transform.localScale = Vector3.one * 2.5f;
                Debug.Log($"[WaitScene] '{w.displayName}' 카메라 시야 안으로 강제 이동 → {newPos}");
                idx++;
            }
        }

        // EventSystem InputModule 보강 — 호버/클릭 동작
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es != null)
        {
            EnsureInputModule(es.gameObject);
        }

        // 자동 wire — 이름이 MoveButton / MoveButton (1) 인 GO 찾고, Button 없으면 자동 추가
        if (moveButton == null) moveButton = FindOrEnsureButton("MoveButton");
        if (backButton == null) backButton = FindOrEnsureButton("MoveButton (1)");

        // 텍스트 매칭 fallback (한국어/영어 둘 다)
        if (moveButton == null || backButton == null)
        {
            var allButtons = new System.Collections.Generic.List<Button>();
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                allButtons.Add(b);

            // 디버그 로그
            var names = new System.Collections.Generic.List<string>();
            foreach (var b in allButtons) names.Add(b.gameObject.name);
            Debug.Log($"[WaitScene] 씬 안 모든 Button: {string.Join(", ", names)}");

            foreach (var b in allButtons)
            {
                string text = GetButtonText(b);
                if ((text.Contains("뒤로") || text.Contains("Back")) && backButton == null) backButton = b;
                else if ((text.Contains("이동") || text.Contains("경기장") || text.Contains("시작") || text.Contains("Move")) && moveButton == null) moveButton = b;
            }
        }

        // 그래도 못 찾으면 — Button 컴포넌트 없는 GO 라도 이름 매칭 후 Button 추가
        if (moveButton == null) moveButton = FindOrEnsureButton("MoveButton");
        if (backButton == null) backButton = FindOrEnsureButton("MoveButton (1)");

        if (moveButton != null)
        {
            moveButton.onClick.RemoveAllListeners();
            moveButton.onClick.AddListener(OnMoveClicked);
        }
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnBackClicked);
        }

        Debug.Log($"[WaitScene] moveButton:{(moveButton != null ? moveButton.name : "null")}, backButton:{(backButton != null ? backButton.name : "null")}");
    }

    public void OnMoveClicked() => SceneManager.LoadScene(forwardScene);
    public void OnBackClicked() => SceneManager.LoadScene(backScene);

    static Button FindButtonByName(string n)
    {
        var all = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in all) if (b.gameObject.name == n) return b;
        return null;
    }

    // GO 를 이름으로 찾고 Button 없으면 자동 추가 + 호버 시각효과 설정
    static Button FindOrEnsureButton(string n)
    {
        foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.gameObject.name != n) continue;
            var btn = t.GetComponent<Button>();
            if (btn == null)
            {
                btn = t.gameObject.AddComponent<Button>();
                Debug.Log($"[WaitScene] '{n}' 에 Button 자동 추가");
            }
            // targetGraphic 자동 설정
            if (btn.targetGraphic == null)
            {
                var img = t.GetComponent<Graphic>();
                if (img == null) img = t.GetComponentInChildren<Graphic>(true);
                if (img != null) btn.targetGraphic = img;
            }
            // 호버 시 살짝 노란 톤, 클릭 시 어둡게 — 시각 피드백 강하게
            var cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1f,    0.92f, 0.65f, 1f);  // 호버 노란빛
            cb.pressedColor     = new Color(0.7f,  0.6f,  0.35f, 1f);  // 클릭 어두운 황색
            cb.selectedColor    = new Color(1f,    0.92f, 0.65f, 1f);
            cb.colorMultiplier  = 1.2f;
            cb.fadeDuration     = 0.08f;
            btn.colors          = cb;
            btn.transition      = Selectable.Transition.ColorTint;
            return btn;
        }
        return null;
    }

    static string GetButtonText(Button b)
    {
        var tmp = b.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (tmp != null) return tmp.text ?? "";
        var legacy = b.GetComponentInChildren<Text>(true);
        return legacy != null ? (legacy.text ?? "") : "";
    }

    static void EnsureInputModule(GameObject esGo)
    {
        var newType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (newType != null)
        {
            var legacy = esGo.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (legacy != null) Destroy(legacy);
            if (esGo.GetComponent(newType) == null) esGo.AddComponent(newType);
        }
        else
        {
            if (esGo.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }
}
