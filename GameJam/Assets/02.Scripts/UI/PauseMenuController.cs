using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 인게임 ESC → 일시정지 + StopUI 패널 표시.
/// Resume / Restart / Lobby로 버튼 자동 wire.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [Header("자동 wire — 이름으로 검색")]
    public GameObject pausePanel;     // StopUI GO
    public Button resumeButton;
    public Button exitButton;         // 로비로 가기
    public Button restartButton;      // 현재 세트 재시작 (선택)

    bool _paused;

    void Awake()
    {
        // panel — sizeDelta 큰 GO
        if (pausePanel == null) pausePanel = FindLargestByName("StopUI") ?? FindLargestByName("PausePanel");

        // ★ Button 컴포넌트 없는 텍스트 GO 들에 자동 추가 (ashyun1 이 Image+Text 만 만들었을 수도)
        if (pausePanel != null)
        {
            foreach (var t in pausePanel.GetComponentsInChildren<RectTransform>(true))
            {
                if (t == pausePanel.transform) continue;
                if (t.GetComponent<Button>() != null) continue;
                // 자식에 텍스트가 있고 + Image 컴포넌트 가짐 = 버튼 후보
                var tmp = t.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                var legacy = t.GetComponentInChildren<Text>(true);
                string text = tmp != null ? tmp.text : (legacy != null ? legacy.text : "");
                if (string.IsNullOrEmpty(text)) continue;

                bool isButton = text.Contains("돌아") || text.Contains("종료") || text.Contains("설정")
                             || text.Contains("Resume") || text.Contains("Exit") || text.Contains("Restart")
                             || text.Contains("재시작");
                if (!isButton) continue;
                if (t.GetComponent<Image>() == null) continue; // 클릭 받으려면 Graphic 필요

                var btn = t.gameObject.AddComponent<Button>();
                Debug.Log($"[Pause] '{t.gameObject.name}' (text:'{text}') 에 Button 자동 추가");
            }
        }

        // 버튼 자동 wire — 한국어 텍스트 또는 GO 이름 매칭
        if (pausePanel != null)
        {
            foreach (var b in pausePanel.GetComponentsInChildren<Button>(true))
            {
                string text = GetButtonText(b);
                string gn = b.gameObject.name;
                if (resumeButton == null && (text.Contains("돌아") || text.Contains("계속") || text.Contains("Resume") || gn.Contains("Resume")))
                    resumeButton = b;
                else if (exitButton == null && (text.Contains("게임 종료") || text.Contains("종료") || text.Contains("로비") || text.Contains("Exit") || text.Contains("Quit")))
                    exitButton = b;
                else if (restartButton == null && (text.Contains("재시작") || text.Contains("Restart")))
                    restartButton = b;
            }
        }
        // fallback — 일반 이름
        if (resumeButton == null) resumeButton = FindButtonByName("ResumeButton") ?? FindButtonByName("Resume");
        if (exitButton == null) exitButton = FindButtonByName("ExitButton") ?? FindButtonByName("Exit");
        if (restartButton == null) restartButton = FindButtonByName("RestartButton") ?? FindButtonByName("Restart");

        if (resumeButton != null) { resumeButton.onClick.RemoveAllListeners(); resumeButton.onClick.AddListener(Resume); }
        if (exitButton != null) { exitButton.onClick.RemoveAllListeners(); exitButton.onClick.AddListener(GoToLobby); }
        if (restartButton != null) { restartButton.onClick.RemoveAllListeners(); restartButton.onClick.AddListener(Restart); }

        // 슬라이더 — pause panel 안에 있으면 볼륨 wire
        if (pausePanel != null)
        {
            var sliders = pausePanel.GetComponentsInChildren<Slider>(true);
            if (sliders.Length >= 1) WireBgmSlider(sliders[0]);
            if (sliders.Length >= 2) WireSfxSlider(sliders[1]);
        }

        if (pausePanel != null) pausePanel.SetActive(false);

        Debug.Log($"[Pause] panel:{(pausePanel != null ? pausePanel.name : "null")}, resume:{(resumeButton != null ? "ok" : "null")}, exit:{(exitButton != null ? "ok" : "null")}, restart:{(restartButton != null ? "ok" : "null")}");
    }

    static string GetButtonText(Button b)
    {
        var tmp = b.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (tmp != null) return tmp.text ?? "";
        var legacy = b.GetComponentInChildren<Text>(true);
        return legacy != null ? (legacy.text ?? "") : "";
    }

    void WireBgmSlider(Slider s)
    {
        s.minValue = 0; s.maxValue = 1;
        s.value = VolumeSettings.BgmVolume;
        s.onValueChanged.RemoveAllListeners();
        s.onValueChanged.AddListener(v => VolumeSettings.BgmVolume = v);
    }
    void WireSfxSlider(Slider s)
    {
        s.minValue = 0; s.maxValue = 1;
        s.value = VolumeSettings.SfxVolume;
        s.onValueChanged.RemoveAllListeners();
        s.onValueChanged.AddListener(v => VolumeSettings.SfxVolume = v);
    }

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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
    }

    public void Toggle()
    {
        if (pausePanel == null) return;
        _paused = !pausePanel.activeSelf;
        pausePanel.SetActive(_paused);
        Time.timeScale = _paused ? 0f : 1f;
    }

    public void Resume()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        _paused = false;
        Time.timeScale = 1f;
    }

    public void GoToLobby()
    {
        Time.timeScale = 1f;
        MatchResult.Clear();
        PickResult.Clear();
        SceneManager.LoadScene("Lobby");
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    static Button FindButtonByName(string n)
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.gameObject.name == n) return b;
        return null;
    }
}
