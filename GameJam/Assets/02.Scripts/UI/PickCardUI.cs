using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 밴픽에서 픽 된 챔프의 상세 카드 — 포트레이트(흰박스), 이름, 스탯 6종, 스킬/궁극 설명 버튼.
/// TeamSlotUI 의 pickCards 배열에 wire 됨.
/// </summary>
public class PickCardUI : MonoBehaviour
{
    [Header("핵심 표시")]
    public Image portrait;                  // 흰 박스
    public TextMeshProUGUI nameLabel;       // TMP 이름
    public Text nameLabelLegacy;            // Legacy 이름 (Character_Name 등)

    [Header("스탯 TMP — 6종 (위→아래 순서: ATK / HP / DEF / RANGE / MOVE / ATKSPD)")]
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI defText;
    public TextMeshProUGUI rangeText;
    public TextMeshProUGUI moveSpeedText;
    public TextMeshProUGUI atkSpeedText;

    [Header("스탯 Legacy Text — 사용자 디자인이 UI.Text 일 때")]
    public Text atkTextLegacy;
    public Text hpTextLegacy;
    public Text defTextLegacy;
    public Text rangeTextLegacy;
    public Text moveSpeedTextLegacy;
    public Text atkSpeedTextLegacy;

    [Header("스킬/궁극 설명 버튼 (옵션)")]
    public Button skillDescButton;          // "스킬 설명"
    public Button ultDescButton;            // "궁극기 설명"

    [Header("툴팁 (옵션 — 없으면 nameLabel 자리에 텍스트 잠깐 표시)")]
    public GameObject tooltipPanel;         // 공용 패널 (옵션)
    public TextMeshProUGUI tooltipText;     // 공용 텍스트 (옵션)

    [Header("팀 색상 (옵션)")]
    public Color allyTint = new Color(0.4f, 0.7f, 1f, 1f);
    public Color enemyTint = new Color(1f, 0.4f, 0.4f, 1f);
    public bool isAlly = true;
    public Image colorFrame;                // 외곽선 image (옵션 — wire 되면 팀색 적용)

    ChampionData _data;

    void Awake()
    {
        if (colorFrame != null) colorFrame.color = isAlly ? allyTint : enemyTint;
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    /// <summary>픽 안 된 빈 상태로 표시 (placeholder)</summary>
    public void Clear()
    {
        _data = null;
        if (portrait != null) { portrait.sprite = null; portrait.color = new Color(1, 1, 1, 1f); }
        SetName("캐릭터 이름");
        SetStat("atk", "");
        SetStat("hp", "");
        SetStat("def", "");
        SetStat("range", "");
        SetStat("move", "");
        SetStat("atkSpd", "");
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    /// <summary>챔프 데이터 적용 — 포트레이트, 이름, 스탯, 버튼 wire</summary>
    public void Apply(ChampionData d)
    {
        _data = d;
        if (d == null) { Clear(); return; }

        if (portrait != null)
        {
            portrait.sprite = d.portrait;
            portrait.color = d.portrait != null ? Color.white : d.themeColor;
            portrait.preserveAspect = true;
        }
        SetName(d.displayName);

        SetStat("atk",    $"{Mathf.RoundToInt(d.attackDamage)}");
        SetStat("hp",     $"{Mathf.RoundToInt(d.maxHealth)}");
        SetStat("def",    $"{Mathf.RoundToInt(d.defense)}");
        SetStat("range",  $"{d.attackRange:F1}");
        SetStat("move",   $"{d.moveSpeed:F1}");
        SetStat("atkSpd", $"{d.attackSpeed:F2}");

        if (skillDescButton != null)
        {
            skillDescButton.onClick.RemoveAllListeners();
            skillDescButton.onClick.AddListener(() => ShowTooltip(
                $"<b>{d.basicSkillName}</b>\nCD: {d.basicSkillCooldown:F1}s\n{d.description}"));
        }
        if (ultDescButton != null)
        {
            ultDescButton.onClick.RemoveAllListeners();
            ultDescButton.onClick.AddListener(() => ShowTooltip(
                $"<b>{d.ultimateName}</b>\nCD: {d.ultimateCooldown:F1}s\n{d.description}"));
        }
    }

    void ShowTooltip(string content)
    {
        if (tooltipPanel == null || tooltipText == null) { Debug.Log($"[PickCard] {content}"); return; }
        // 토글 — 이미 열려있으면 닫기
        if (tooltipPanel.activeSelf) { tooltipPanel.SetActive(false); return; }
        tooltipText.text = content;
        tooltipPanel.SetActive(true);
    }

    static void Set(TextMeshProUGUI t, string s) { if (t != null) t.text = s; }
    static void Set(Text t, string s) { if (t != null) t.text = s; }

    void SetName(string s)
    {
        if (nameLabel != null) nameLabel.text = s;
        if (nameLabelLegacy != null) nameLabelLegacy.text = s;
    }

    /// <summary>스탯 텍스트 set — TMP 와 Legacy 둘 다 set</summary>
    void SetStat(string key, string value)
    {
        switch (key)
        {
            case "atk":    Set(atkText, value);       Set(atkTextLegacy, value);       break;
            case "hp":     Set(hpText, value);        Set(hpTextLegacy, value);        break;
            case "def":    Set(defText, value);       Set(defTextLegacy, value);       break;
            case "range":  Set(rangeText, value);     Set(rangeTextLegacy, value);     break;
            case "move":   Set(moveSpeedText, value); Set(moveSpeedTextLegacy, value); break;
            case "atkSpd": Set(atkSpeedText, value);  Set(atkSpeedTextLegacy, value);  break;
        }
    }
}
