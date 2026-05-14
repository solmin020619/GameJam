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

    [Header("승리/패배 별도 패널 (옵션 — 신규 디자인)")]
    public GameObject winPanel;    // VictoryPanel — 승리 시 활성
    public GameObject losePanel;   // DefeatPanel — 패배 시 활성
    public Button winButton;       // VictoryPanel 안 진행 버튼 (자동 검색)
    public Button loseButton;      // DefeatPanel 안 진행 버튼 (자동 검색)

    [Header("자동 wire — 이름 검색")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultText;
    public Text resultTextLegacy;
    public TextMeshProUGUI resultScore1;   // 파랑 점수
    public TextMeshProUGUI resultScore2;   // 빨강 점수
    public Text resultScore1Legacy;
    public Text resultScore2Legacy;
    public Button resultButton;            // 승리: 시상식, 패배: 재도전, 무승부: Lobby

    [Header("씬 이름 (인스펙터에서 변경 가능)")]
    public string winSceneName = "AScene_Win";   // 우승 시 시상식 씬
    public string loseSceneName = "AScene_Wait"; // 패배 시 재도전 → Wait 씬으로
    public string drawSceneName = "Lobby";       // 무승부 fallback

    [Header("버튼 라벨")]
    public string winButtonText  = "시상식 가기";
    public string loseButtonText = "재도전하기";
    public string drawButtonText = "돌아가기";

    System.Collections.Generic.List<GameObject> _headerPanels = new();

    // 마지막 결과 — 버튼 클릭 시 어느 씬으로 갈지 결정
    enum LastResult { None, Win, Lose, Draw }
    LastResult _lastResult = LastResult.None;

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

        // 신규 — VictoryPanel / DefeatPanel 자동 wire (둘 다 시작 시 비활성)
        if (winPanel == null) winPanel = FindInScene("AScene_FightUI", "VictoryPanel") ?? FindLargestByName("VictoryPanel");
        if (losePanel == null) losePanel = FindInScene("AScene_FightUI", "DefeatPanel") ?? FindLargestByName("DefeatPanel");
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        // 각 패널 안 첫 Button 을 winButton / loseButton 으로 wire — 클릭 시 적절한 씬 이동
        if (winButton == null && winPanel != null) winButton = winPanel.GetComponentInChildren<Button>(true);
        if (loseButton == null && losePanel != null) loseButton = losePanel.GetComponentInChildren<Button>(true);

        if (winButton != null)
        {
            winButton.onClick.RemoveAllListeners();
            winButton.onClick.AddListener(OnWinButtonClicked);
        }
        if (loseButton != null)
        {
            loseButton.onClick.RemoveAllListeners();
            loseButton.onClick.AddListener(OnLoseButtonClicked);
        }
        Debug.Log($"[Victory] winPanel:{(winPanel != null ? "ok" : "null")}, losePanel:{(losePanel != null ? "ok" : "null")}, winBtn:{(winButton != null ? "ok" : "null")}, loseBtn:{(loseButton != null ? "ok" : "null")}");

        // wire — AScene_FightUI 씬 안 ResultPanel 만 (InGame 의 BattleManager.ResultPanel 과 분리) — fallback 으로 보관
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
            // fallback — panel 안의 첫 Button 도 시도 (디자이너가 이름을 "진행하기" 같은 한글로 했을 수 있음)
            if (resultButton == null)
            {
                var btns = resultPanel.GetComponentsInChildren<Button>(true);
                if (btns.Length > 0) { resultButton = btns[0]; Debug.Log($"[Victory] resultButton fallback wire: '{resultButton.gameObject.name}'"); }
            }
        }
        // fallback (panel 못 찾으면 전역)
        if (resultText == null && resultTextLegacy == null) resultTextLegacy = FindLegacyByName("ResultText");
        if (resultButton == null) resultButton = FindButtonByName("ResultButton");

        // 텍스트 내용 기반 fallback — "진행하기" / "돌아가기" / "재도전" / "시상식" 포함된 첫 Button
        if (resultButton == null)
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string t = "";
                var btmp = b.GetComponentInChildren<TextMeshProUGUI>(true);
                if (btmp != null) t = btmp.text ?? "";
                else
                {
                    var blegacy = b.GetComponentInChildren<Text>(true);
                    if (blegacy != null) t = blegacy.text ?? "";
                }
                if (t.Contains("진행") || t.Contains("돌아") || t.Contains("재도전") || t.Contains("시상식") || t.Contains("확인"))
                {
                    resultButton = b;
                    Debug.Log($"[Victory] resultButton 텍스트 기반 wire: '{b.gameObject.name}' (text:'{t}')");
                    break;
                }
            }
        }

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
        // 늦은 검색 — Awake 타이밍 문제로 wire 못 됐을 수 있으니 Show 시점에 한 번 더 시도
        if (winPanel == null) winPanel = FindAnywhere("VictoryPanel");
        if (losePanel == null) losePanel = FindAnywhere("DefeatPanel");

        // 버튼 — Button 컴포넌트가 없으면 자동 부착 (사용자가 Image 만 만든 경우 대비)
        if (winButton == null && winPanel != null) winButton = EnsureButton(winPanel, OnWinButtonClicked);
        if (loseButton == null && losePanel != null) loseButton = EnsureButton(losePanel, OnLoseButtonClicked);

        // 점수 텍스트 갱신 — 각 패널 안에서 점수 표시할 TMP/Text 찾고 setting
        if (winPanel != null) UpdateScoreText(winPanel, team0Wins, team1Wins);
        if (losePanel != null) UpdateScoreText(losePanel, team0Wins, team1Wins);

        Debug.Log($"[Victory] Show 진입 — t0:{team0Wins}, t1:{team1Wins}, winPanel:{(winPanel != null ? "ok" : "NULL")}, losePanel:{(losePanel != null ? "ok" : "NULL")}, winBtn:{(winButton != null ? "ok" : "NULL")}, loseBtn:{(loseButton != null ? "ok" : "NULL")}");

        Time.timeScale = 0f;

        // 신규 패널 분기 — 승리/패배 별 패널이 있으면 그걸로
        bool isWin = team0Wins > team1Wins;
        bool isLose = team1Wins > team0Wins;

        if ((isWin && winPanel != null) || (isLose && losePanel != null))
        {
            // 신규 동작 — VictoryPanel / DefeatPanel 분기. 옛 resultPanel 은 건드리지 않음
            if (isWin)
            {
                winPanel.SetActive(true);
                winPanel.transform.SetAsLastSibling();  // 가장 위 layer 로
                if (losePanel != null) losePanel.SetActive(false);
                _lastResult = LastResult.Win;
                Debug.Log("[Victory] VictoryPanel 활성 (승리)");
            }
            else
            {
                losePanel.SetActive(true);
                losePanel.transform.SetAsLastSibling();  // 가장 위 layer 로
                if (winPanel != null) winPanel.SetActive(false);
                _lastResult = LastResult.Lose;
                Debug.Log("[Victory] DefeatPanel 활성 (패배)");
            }
            // 옛 패널 비활성 (혹시 모를 중복 표시 방지)
            if (resultPanel != null) resultPanel.SetActive(false);
            return;
        }

        Debug.LogWarning($"[Victory] 신규 패널 못 찾음 (winPanel={(winPanel != null)}, losePanel={(losePanel != null)}, isWin={isWin}, isLose={isLose}) — 옛 resultPanel fallback 으로 시도");

        // === 옛 동작 (fallback) — VictoryPanel / DefeatPanel 미설정 시 기존 resultPanel 사용 ===
        if (resultPanel == null) return;
        resultPanel.SetActive(true);
        // "경기 결과" 헤더 panel 도 같이 활성
        foreach (var hp in _headerPanels) if (hp != null) hp.SetActive(true);

        string label;
        Color color;
        string buttonText;
        if (team0Wins > team1Wins)
        {
            label = "승리";
            color = new Color(1f, 0.85f, 0.3f);
            _lastResult = LastResult.Win;
            buttonText = winButtonText;
        }
        else if (team1Wins > team0Wins)
        {
            label = "패배";
            color = new Color(1f, 0.3f, 0.3f);
            _lastResult = LastResult.Lose;
            buttonText = loseButtonText;
        }
        else
        {
            label = "무승부";
            color = Color.white;
            _lastResult = LastResult.Draw;
            buttonText = drawButtonText;
        }

        // 버튼 라벨 변경 — resultButton 안의 모든 TMP/Text 갱신 (TMP 우선, 없으면 Legacy)
        if (resultButton != null)
        {
            int btnChanged = 0;
            foreach (var btmp in resultButton.GetComponentsInChildren<TextMeshProUGUI>(true))
            { btmp.text = buttonText; btnChanged++; }
            foreach (var blegacy in resultButton.GetComponentsInChildren<Text>(true))
            { blegacy.text = buttonText; btnChanged++; }
            Debug.Log($"[Victory] 버튼 텍스트 '{buttonText}' set — {btnChanged} 개 텍스트 변경");
        }
        else
        {
            Debug.LogWarning("[Victory] resultButton null — 버튼 텍스트 변경 불가");
        }

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

    /// <summary>VictoryPanel 의 진행 버튼 — 시상식 씬으로</summary>
    public void OnWinButtonClicked()
    {
        Time.timeScale = 1f;
        string target = winSceneName;
        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogWarning($"[Victory] '{target}' 씬 없음 (Build Settings 미등록?) — Lobby fallback");
            target = "Lobby";
            MatchResult.Clear();
            PickResult.Clear();
        }
        Debug.Log($"[Victory] 승리 버튼 클릭 → '{target}' 로 이동");
        SceneManager.LoadScene(target);
    }

    /// <summary>DefeatPanel 의 진행 버튼 — Wait 씬으로 (재도전)</summary>
    public void OnLoseButtonClicked()
    {
        Time.timeScale = 1f;
        MatchResult.Clear();
        PickResult.Clear();
        string target = loseSceneName;
        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogWarning($"[Victory] '{target}' 씬 없음 — Lobby fallback");
            target = "Lobby";
        }
        Debug.Log($"[Victory] 패배 버튼 클릭 → '{target}' 로 이동");
        SceneManager.LoadScene(target);
    }

    public void OnResultButtonClicked()
    {
        Time.timeScale = 1f;

        // 결과 별 이동 분기
        string target;
        switch (_lastResult)
        {
            case LastResult.Win:
                // 승리 → 시상식 씬 (AScene_Win). 매치 결과/픽 결과는 보존 (시상식에서 사용 가능)
                target = winSceneName;
                if (!Application.CanStreamedLevelBeLoaded(target))
                {
                    Debug.LogWarning($"[Victory] '{target}' 씬이 Build Settings 에 없음 — Lobby fallback");
                    target = "Lobby";
                    MatchResult.Clear();
                    PickResult.Clear();
                }
                break;
            case LastResult.Lose:
                // 패배 → 재도전 (밴픽으로). 매치 결과/픽 결과 리셋 (새 게임)
                MatchResult.Clear();
                PickResult.Clear();
                target = loseSceneName;
                if (!Application.CanStreamedLevelBeLoaded(target))
                {
                    Debug.LogWarning($"[Victory] '{target}' 씬 없음 — 'BanPick' 또는 'Lobby' fallback");
                    target = Application.CanStreamedLevelBeLoaded("BanPick") ? "BanPick" : "Lobby";
                }
                break;
            default:
                // 무승부 또는 알 수 없음 → 로비
                MatchResult.Clear();
                PickResult.Clear();
                target = drawSceneName;
                break;
        }
        Debug.Log($"[Victory] 결과:{_lastResult} → '{target}' 로 이동");
        SceneManager.LoadScene(target);
    }

    /// <summary>패널 안 첫 ResultButton/Button 후보를 찾아 Button 컴포넌트 보강 + hover/click 시각 효과 + 클릭 핸들러 wire</summary>
    Button EnsureButton(GameObject panel, UnityEngine.Events.UnityAction onClick)
    {
        if (panel == null) return null;

        // 1순위: 이미 Button 컴포넌트 있는 자식
        var btn = panel.GetComponentInChildren<Button>(true);

        // 2순위: 이름이 ResultButton 인 자식
        if (btn == null)
        {
            foreach (var rt in panel.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.gameObject.name != "ResultButton") continue;
                btn = rt.gameObject.AddComponent<Button>();
                Debug.Log($"[Victory] '{rt.gameObject.name}' 에 Button 컴포넌트 자동 부착");
                break;
            }
        }

        // 3순위: 패널 안 가장 아래 (Y 가 가장 작은) Image — 보통 버튼
        if (btn == null)
        {
            Image candidate = null; float lowestY = float.MaxValue;
            foreach (var img in panel.GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject == panel) continue;
                if (img.gameObject.GetComponent<Button>() != null) { candidate = img; break; }
                float y = img.rectTransform.position.y;
                if (y < lowestY) { lowestY = y; candidate = img; }
            }
            if (candidate != null)
            {
                btn = candidate.gameObject.GetComponent<Button>();
                if (btn == null)
                {
                    btn = candidate.gameObject.AddComponent<Button>();
                    Debug.Log($"[Victory] '{candidate.gameObject.name}' 에 Button 자동 부착 (Y 가장 아래 Image)");
                }
            }
        }

        if (btn == null) { Debug.LogWarning($"[Victory] '{panel.name}' 안에 Button 후보 없음"); return null; }

        // targetGraphic — Image 가 없으면 추가
        if (btn.targetGraphic == null)
        {
            var img = btn.GetComponent<Image>();
            if (img == null) img = btn.gameObject.AddComponent<Image>();
            btn.targetGraphic = img;
        }

        // hover/click 시각 효과 — ColorTint transition
        var cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1f,    0.92f, 0.5f, 1f);   // 호버: 노란빛
        cb.pressedColor     = new Color(0.7f,  0.6f,  0.3f, 1f);   // 클릭: 어두운 황색
        cb.selectedColor    = new Color(1f,    0.92f, 0.5f, 1f);
        cb.disabledColor    = new Color(0.5f,  0.5f,  0.5f, 0.5f);
        cb.colorMultiplier  = 1.2f;
        cb.fadeDuration     = 0.1f;
        btn.colors          = cb;
        btn.transition      = Selectable.Transition.ColorTint;

        // onClick wire (중복 호출 안전)
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(onClick);
        return btn;
    }

    /// <summary>패널 안 점수 TMP/Text 들 찾아서 team0Wins:team1Wins 표시</summary>
    static void UpdateScoreText(GameObject panel, int t0, int t1)
    {
        // 점수 텍스트 식별 — 보통 패널 안에 2 개의 작은 숫자 (각 팀 점수)
        // 우선순위: 이름이 Score / 점수 / 들어있는 거. 못 찾으면 fontSize 큰 TMP/Text 중 짧은 숫자 텍스트인 거
        var tmps = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        var legacies = panel.GetComponentsInChildren<Text>(true);

        // TMP 점수 후보 (이름 기반)
        var scoreTmps = new System.Collections.Generic.List<TextMeshProUGUI>();
        foreach (var t in tmps)
        {
            string n = t.gameObject.name.ToLower();
            if (n.Contains("score") || n.Contains("점수") || n.Contains("count")) scoreTmps.Add(t);
        }
        var scoreLegacies = new System.Collections.Generic.List<Text>();
        foreach (var t in legacies)
        {
            string n = t.gameObject.name.ToLower();
            if (n.Contains("score") || n.Contains("점수") || n.Contains("count")) scoreLegacies.Add(t);
        }

        // 이름 기반 못 찾으면 fontSize 큰 거 + 텍스트가 짧은 숫자/공백/콜론인 거 fallback
        if (scoreTmps.Count == 0 && scoreLegacies.Count == 0)
        {
            foreach (var t in tmps)
            {
                if (t.fontSize < 24) continue;  // 작은 텍스트 제외
                string txt = (t.text ?? "").Trim();
                // 점수처럼 보이는 거 — 짧고, 숫자 또는 콜론
                if (txt.Length > 5) continue;
                if (txt == "" || System.Text.RegularExpressions.Regex.IsMatch(txt, @"^[\d\s:]+$"))
                    scoreTmps.Add(t);
            }
            foreach (var t in legacies)
            {
                if (t.fontSize < 24) continue;
                string txt = (t.text ?? "").Trim();
                if (txt.Length > 5) continue;
                if (txt == "" || System.Text.RegularExpressions.Regex.IsMatch(txt, @"^[\d\s:]+$"))
                    scoreLegacies.Add(t);
            }
        }

        // 좌→우 정렬해서 첫번째 = team0, 마지막 = team1. 단일 텍스트면 "0 : 2" 형식
        scoreTmps.Sort((a, b) => a.rectTransform.position.x.CompareTo(b.rectTransform.position.x));
        scoreLegacies.Sort((a, b) => a.rectTransform.position.x.CompareTo(b.rectTransform.position.x));

        int totalScoreElems = scoreTmps.Count + scoreLegacies.Count;
        Debug.Log($"[Victory] '{panel.name}' 점수 텍스트 발견: TMP {scoreTmps.Count}, Legacy {scoreLegacies.Count}");

        if (totalScoreElems >= 2)
        {
            // 2 개 이상 — 좌:team0, 우:team1
            // TMP 리스트가 비어있을 수도 있으므로 통합 리스트로
            var allLeft = scoreTmps.Count >= 2 ? scoreTmps[0] : null;
            var allRight = scoreTmps.Count >= 2 ? scoreTmps[scoreTmps.Count - 1] : null;
            if (allLeft != null) allLeft.text = t0.ToString();
            if (allRight != null) allRight.text = t1.ToString();

            var allLeftLegacy = scoreLegacies.Count >= 2 ? scoreLegacies[0] : null;
            var allRightLegacy = scoreLegacies.Count >= 2 ? scoreLegacies[scoreLegacies.Count - 1] : null;
            if (allLeftLegacy != null) allLeftLegacy.text = t0.ToString();
            if (allRightLegacy != null) allRightLegacy.text = t1.ToString();
        }
        else if (totalScoreElems == 1)
        {
            // 1 개만 — "t0 : t1" 형식
            string combined = $"{t0} : {t1}";
            if (scoreTmps.Count == 1) scoreTmps[0].text = combined;
            else if (scoreLegacies.Count == 1) scoreLegacies[0].text = combined;
        }
    }

    static GameObject FindByName(string n)
    {
        foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.gameObject.name == n) return t.gameObject;
        return null;
    }

    /// <summary>이름으로 GameObject 검색 (모든 씬 + 비활성 포함) — Awake 타이밍 문제 우회</summary>
    static GameObject FindAnywhere(string n)
    {
        // 우선 AScene_FightUI 에서, 못 찾으면 RectTransform 전체에서, 그도 못 찾으면 모든 GO 에서
        var go = FindInScene("AScene_FightUI", n);
        if (go != null) return go;
        go = FindLargestByName(n);
        if (go != null) return go;
        // 마지막 — 모든 GO 의 transform 검색
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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
