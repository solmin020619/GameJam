using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴 SettingUI panel — 열기/닫기 + 볼륨 슬라이더 + 나가기 버튼.
/// </summary>
public class SettingsMenuController : MonoBehaviour
{
    [Header("자동 wire — 이름으로 검색")]
    public GameObject settingsPanel;  // SettingUI panel
    public Button openButton;         // Setting 버튼 (메인 메뉴)
    public Button closeButton;        // 닫기/X 버튼
    public Button exitButton;         // "나가기" — 게임 종료
    public Slider bgmSlider;          // 배경 음악 슬라이더
    public Slider sfxSlider;          // 효과음 슬라이더

    void Awake()
    {
        // panel — sizeDelta 큰 SettingUI / Setting (1) / Setting 중 panel
        if (settingsPanel == null) settingsPanel = FindLargestByName("SettingUI") ?? FindLargestByName("Setting (1)");
        // 열기 버튼 — panel 밖의 Setting (작은 버튼)
        if (openButton == null) openButton = FindButtonByName("Setting");

        // panel 안 버튼 — "나가기" 는 panel 닫기 (게임 종료 아님)
        if (settingsPanel != null)
        {
            foreach (var b in settingsPanel.GetComponentsInChildren<Button>(true))
            {
                string text = GetButtonText(b);
                // 닫기/X/나가기 — panel 닫기로 통일
                if (closeButton == null && (text.Contains("닫기") || text.Contains("나가기") || text.Contains("Close") || text.Contains("X") || b.gameObject.name.Contains("Close")))
                    closeButton = b;
            }
        }
        // exitButton 은 메인메뉴 Exit 만 (panel 안 검색 X)
        if (exitButton == null) exitButton = FindButtonByNameOutside(settingsPanel, "Exit");

        // 슬라이더 — panel 안. Slider 컴포넌트 없으면 SlideBar/SettingSlide 같은 GO 에 자동 추가
        if (settingsPanel != null)
        {
            var sliders = settingsPanel.GetComponentsInChildren<Slider>(true);
            // Slider 컴포넌트 없으면 SlideBar 이름 GO 에 자동 추가
            if (sliders.Length < 2)
            {
                foreach (var t in settingsPanel.GetComponentsInChildren<RectTransform>(true))
                {
                    if (t.gameObject.name.Contains("SlideBar") || t.gameObject.name.Contains("SettingSlide"))
                    {
                        if (t.GetComponent<Slider>() == null)
                        {
                            var newSlider = t.gameObject.AddComponent<Slider>();
                            newSlider.minValue = 0; newSlider.maxValue = 1; newSlider.value = 0.6f;
                            Debug.Log($"[Settings] '{t.gameObject.name}' 에 Slider 자동 추가");
                        }
                    }
                }
                sliders = settingsPanel.GetComponentsInChildren<Slider>(true);
            }
            if (sliders.Length > 0 && bgmSlider == null) bgmSlider = sliders[0];
            if (sliders.Length > 1 && sfxSlider == null) sfxSlider = sliders[1];
        }

        // Wire
        if (openButton != null) { openButton.onClick.RemoveAllListeners(); openButton.onClick.AddListener(Open); }
        if (closeButton != null) { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(Close); }
        if (exitButton != null) { exitButton.onClick.RemoveAllListeners(); exitButton.onClick.AddListener(QuitGame); }

        if (bgmSlider != null)
        {
            bgmSlider.minValue = 0f; bgmSlider.maxValue = 1f;
            bgmSlider.value = VolumeSettings.BgmVolume;
            bgmSlider.onValueChanged.RemoveAllListeners();
            bgmSlider.onValueChanged.AddListener(v => VolumeSettings.BgmVolume = v);
        }
        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f; sfxSlider.maxValue = 1f;
            sfxSlider.value = VolumeSettings.SfxVolume;
            sfxSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.onValueChanged.AddListener(v => VolumeSettings.SfxVolume = v);
        }

        if (settingsPanel != null) settingsPanel.SetActive(false);

        Debug.Log($"[Settings] panel:{(settingsPanel != null ? settingsPanel.name : "null")}, open:{(openButton != null ? "ok" : "null")}, close:{(closeButton != null ? "ok" : "null")}, exit:{(exitButton != null ? "ok" : "null")}, bgm:{(bgmSlider != null ? "ok" : "null")}, sfx:{(sfxSlider != null ? "ok" : "null")}");
    }

    public void Open()  { if (settingsPanel != null) settingsPanel.SetActive(true); }
    public void Close() { if (settingsPanel != null) settingsPanel.SetActive(false); }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 같은 이름 GO 가 여러 개면 sizeDelta 큰 것 (panel 추정)
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
    static Button FindButtonByName(string n)
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.gameObject.name == n) return b;
        return null;
    }
    // panel 의 자식이 아닌 (= 메인 메뉴) 버튼만 찾음
    static Button FindButtonByNameOutside(GameObject panel, string n)
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b.gameObject.name != n) continue;
            if (panel != null && b.transform.IsChildOf(panel.transform)) continue;
            return b;
        }
        return null;
    }
    static Slider FindFirstSlider()
    {
        var all = Object.FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return all.Length > 0 ? all[0] : null;
    }

    static string GetButtonText(Button b)
    {
        var tmp = b.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (tmp != null) return tmp.text ?? "";
        var legacy = b.GetComponentInChildren<Text>(true);
        return legacy != null ? (legacy.text ?? "") : "";
    }
}
