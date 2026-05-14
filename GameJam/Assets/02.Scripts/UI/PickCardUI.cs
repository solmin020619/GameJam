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
    public TextMeshProUGUI nameLabel;       // "캐릭터 이름"

    [Header("스탯 — 6종 (위→아래 순서: ATK / HP / DEF / RANGE / MOVE / ATKSPD)")]
    public TextMeshProUGUI atkText;         // ⚔ AttackDamage
    public TextMeshProUGUI hpText;          // ❤ MaxHp
    public TextMeshProUGUI defText;         // 🛡 Defense
    public TextMeshProUGUI rangeText;       // ⊕ AttackRange
    public TextMeshProUGUI moveSpeedText;   // 👟 MoveSpeed
    public TextMeshProUGUI atkSpeedText;    // ⚔ AttackSpeed

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
        if (nameLabel != null) nameLabel.text = "캐릭터 이름";
        Set(atkText, "");
        Set(hpText, "");
        Set(defText, "");
        Set(rangeText, "");
        Set(moveSpeedText, "");
        Set(atkSpeedText, "");
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
        if (nameLabel != null) nameLabel.text = d.displayName;

        Set(atkText, $"{Mathf.RoundToInt(d.attackDamage)}");
        Set(hpText, $"{Mathf.RoundToInt(d.maxHealth)}");
        Set(defText, $"{Mathf.RoundToInt(d.defense)}");
        Set(rangeText, $"{d.attackRange:F1}");
        Set(moveSpeedText, $"{d.moveSpeed:F1}");
        Set(atkSpeedText, $"{d.attackSpeed:F2}");

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
}
