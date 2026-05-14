using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// AScene_FightUI 의 ResultPanel (승리 화면) 자동 wire.
/// 매치 종료 (3판 2선) 시 BattleManager 가 Show() 호출 → 활성 + 텍스트/점수 표시.
/// ResultButton 클릭 → Lobby 로.
/// </summary>
public class VictoryScreenController : MonoBehaviour
{
    public static VictoryScreenController Instance;

    [Header("자동 wire — 이름 검색")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultText;
    public Text resultTextLegacy;
    public TextMeshProUGUI resultScore1;   // 파랑 점수
    public TextMeshProUGUI resultScore2;   // 빨강 점수
    public Text resultScore1Legacy;
    public Text resultScore2Legacy;
    public Button resultButton;            // Lobby 로 버튼

    System.Collections.Generic.List<GameObject> _headerPanels = new();

    void Awake()
    {
        Instance = this;

        // 모든 ResultPanel 이름 GO 비활성 (시작 시 안 보이게 — Show 호출 시 활성)
        foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.gameObject.name == "ResultPanel") t.gameObject.SetActive(false);
        }
        // "경기 결과" 헤더 panel — 시작 시는 비활성, Show 시 활성
        foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tmp.text != null && tmp.text.Contains("경기 결과"))
            {
                var panel = tmp.transform.parent != null ? tmp.transform.parent.gameObject : tmp.gameObject;
                panel.SetActive(false);
                _headerPanels.Add(panel);
            }
        }
        foreach (var lt in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (lt.text != null && lt.text.Contains("경기 결과"))
            {
                var panel = lt.transform.parent != null ? lt.transform.parent.gameObject : lt.gameObject;
                panel.SetActive(false);
                _headerPanels.Add(panel);
            }
        }

        // wire — AScene_FightUI 씬 안 ResultPanel 만 (InGame 의 BattleManager.ResultPanel 과 분리)
        if (resultPanel == null) resultPanel = FindInScene("AScene_FightUI", "ResultPanel");
        if (resultPanel == null) resultPanel = FindLargestByName("ResultPanel"); // fallback

        // panel 안 우선 검색 — ashyun1 의 ResultText/Score 잡기 (InGame BattleManager.ResultText 와 분리)
        if (resultPanel != null)
        {
            foreach (var t in resultPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t.gameObject.name == "ResultText" && resultText == null) resultText = t;
                else if (t.gameObject.name == "ResultScore_1" && resultScore1 == null) resultScore1 = t;
                else if (t.gameObject.name == "ResultScore_2" && resultScore2 == null) resultScore2 = t;
            }
            foreach (var lt in resultPanel.GetComponentsInChildren<Text>(true))
            {
                if (lt.gameObject.name == "ResultText" && resultText == null && resultTextLegacy == null) resultTextLegacy = lt;
                else if (lt.gameObject.name == "ResultScore_1" && resultScore1 == null && resultScore1Legacy == null) resultScore1Legacy = lt;
                else if (lt.gameObject.name == "ResultScore_2" && resultScore2 == null && resultScore2Legacy == null) resultScore2Legacy = lt;
            }
            foreach (var b in resultPanel.GetComponentsInChildren<Button>(true))
            {
                if (b.gameObject.name == "ResultButton" && resultButton == null) resultButton = b;
            }
        }
        // fallback (panel 못 찾으면 전역)
        if (resultText == null && resultTextLegacy == null) resultTextLegacy = FindLegacyByName("ResultText");
        if (resultButton == null) resultButton = FindButtonByName("ResultButton");

        if (resultButton != null)
        {
            resultButton.onClick.RemoveAllListeners();
            resultButton.onClick.AddListener(OnResultButtonClicked);
        }

        // ★ 전역 검색 — panel reference 무관하게 흰 박스 + "New Text" 정리
        foreach (var img in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (img.gameObject.name == "ResultScore_1" || img.gameObject.name == "ResultScore_2")
            {
                var c = img.color; c.a = 0f; img.color = c;
                img.enabled = false;
            }
        }
        foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tmp.text == "New Text") tmp.gameObject.SetActive(false);
        }
        foreach (var lt in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (lt.text == "New Text") lt.gameObject.SetActive(false);
        }

        Debug.Log($"[Victory] panel:{(resultPanel != null ? "ok" : "null")}, text:{(resultText != null ? "ok" : "null")}, s1:{(resultScore1 != null ? "ok" : "null")}, s2:{(resultScore2 != null ? "ok" : "null")}, btn:{(resultButton != null ? "ok" : "null")}");
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Show(int team0Wins, int team1Wins)
    {
        if (resultPanel == null) return;
        resultPanel.SetActive(true);
        // "경기 결과" 헤더 panel 도 같이 활성
        foreach (var hp in _headerPanels) if (hp != null) hp.SetActive(true);
        Time.timeScale = 0f;

        string label;
        Color color;
        if (team0Wins > team1Wins)        { label = "승리"; color = new Color(1f, 0.85f, 0.3f); }
        else if (team1Wins > team0Wins)   { label = "패배"; color = new Color(1f, 0.3f, 0.3f); }
        else                              { label = "무승부"; color = Color.white; }

        // 사용자가 흰박스 = 팀 로고 자리로 의도. 비활성 안 함. 사용자 디자인 그대로 유지.
        int hiddenCount = 0;

        // ★ panel 안 모든 TMP/Text 중 큰 폰트 (fontSize > 30) 또는 헤더 텍스트("승리"/"패배"/"Victory"/"Defeat") 다 변경
        int changed = 0;
        if (resultPanel != null)
        {
            foreach (var t in resultPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                string ex = t.text ?? "";
                bool isHeader = t.fontSize > 30 || ex.Contains("승리") || ex.Contains("패배") || ex.Contains("무승부")
                             || ex.Contains("Victory") || ex.Contains("Defeat") || ex.Contains("VICTORY") || ex.Contains("DEFEAT");
                if (isHeader)
                {
                    t.text = label;
                    t.color = color;
                    changed++;
                }
            }
            foreach (var lt in resultPanel.GetComponentsInChildren<Text>(true))
            {
                string ex = lt.text ?? "";
                bool isHeader = lt.fontSize > 30 || ex.Contains("승리") || ex.Contains("패배") || ex.Contains("무승부")
                             || ex.Contains("Victory") || ex.Contains("Defeat") || ex.Contains("VICTORY") || ex.Contains("DEFEAT");
                if (isHeader)
                {
                    lt.text = label;
                    lt.color = color;
                    changed++;
                }
            }
        }

        Debug.Log($"[Victory] Show — t0:{team0Wins}, t1:{team1Wins}, label:{label}, header changed:{changed}, hidden:{hiddenCount}");

        // 일반 wire 도 적용
        if (resultText != null) { resultText.text = label; resultText.color = color; }
        if (resultTextLegacy != null) { resultTextLegacy.text = label; resultTextLegacy.color = color; }
        if (resultScore1 != null) resultScore1.text = team0Wins.ToString();
        if (resultScore2 != null) resultScore2.text = team1Wins.ToString();
        if (resultScore1Legacy != null) resultScore1Legacy.text = team0Wins.ToString();
        if (resultScore2Legacy != null) resultScore2Legacy.text = team1Wins.ToString();
    }

    static Text FindLegacyByName(string n)
    {
        foreach (var t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.gameObject.name == n) return t;
        return null;
    }

    public void OnResultButtonClicked()
    {
        Time.timeScale = 1f;
        MatchResult.Clear();
        PickResult.Clear();
        SceneManager.LoadScene("Lobby");
    }

    static GameObject FindByName(string n)
    {
        foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.gameObject.name == n) return t.gameObject;
        return null;
    }
    // 특정 씬 안에서만 검색
    static GameObject FindInScene(string sceneName, string goName)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid()) return null;
        GameObject best = null; float bestArea = 0f;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.name != goName) continue;
                var rt = t as RectTransform;
                if (rt == null) { best = t.gameObject; continue; }
                float a = Mathf.Abs(rt.sizeDelta.x * rt.sizeDelta.y);
                if (a > bestArea) { bestArea = a; best = t.gameObject; }
            }
        }
        return best;
    }
    // 같은 이름 GO 여러 개면 sizeDelta 큰 것 (panel 추정)
    static GameObject FindLargestByName(string n)
    {
        GameObject best = null; float bestArea = 0f;
        foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.gameObject.name != n) continue;
            float a = Mathf.Abs(t.sizeDelta.x * t.sizeDelta.y);
            if (a > bestArea) { bestArea = a; best = t.gameObject; }
        }
        return best;
    }
    static TextMeshProUGUI FindTmpByName(string n)
    {
        foreach (var t in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.gameObject.name == n) return t;
        return null;
    }
    static Button FindButtonByName(string n)
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.gameObject.name == n) return b;
        return null;
    }
}
