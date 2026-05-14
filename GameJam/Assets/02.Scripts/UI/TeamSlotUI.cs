using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TeamSlotUI : MonoBehaviour
{
    public TeamSide side;

    [Header("Header")]
    public TextMeshProUGUI teamLabel;
    public Color allyColor = new Color(0.4f, 0.7f, 1f);
    public Color enemyColor = new Color(1f, 0.4f, 0.4f);

    [Header("Slots (옛 builder — 단순 Image)")]
    public Image[] banSlots;
    public Image[] pickSlots;

    [Header("새 디자인 — 스탯 카드 (3개, 옵션)")]
    public PickCardUI[] pickCards;   // wire 되어 있으면 pickSlots 대신 이걸로 표시

    [Header("Slot Look")]
    public Sprite emptySlotSprite;
    public Color emptySlotColor = new Color(1, 1, 1, 0.15f);

    [Header("Ban Overlay (X mark)")]
    public Sprite bannedXSprite;
    public Color bannedTint = new Color(1f, 1f, 1f, 1f);  // 풀 컬러 — portrait 또렷이 보이게 (회색 반투명 X)

    void Reset()
    {
        if (teamLabel != null) teamLabel.text = side == TeamSide.Ally ? "우리 팀" : "상대 팀";
    }

    void Awake()
    {
        if (teamLabel != null)
        {
            teamLabel.text = side == TeamSide.Ally ? "우리 팀" : "상대 팀";
            teamLabel.color = side == TeamSide.Ally ? allyColor : enemyColor;
        }
        // ClearAll 도 빈 슬롯 Image 안 건드리게 됐으므로 호출해도 안전
        ClearAll();
    }

    public void Refresh()
    {
        var bans = side == TeamSide.Ally ? PickResult.AllyBans : PickResult.EnemyBans;
        var picks = side == TeamSide.Ally ? PickResult.AllyPicks : PickResult.EnemyPicks;
        DrawSlots(banSlots, bans, true);
        // 새 PickCardUI 가 있으면 우선 사용
        if (pickCards != null && pickCards.Length > 0)
            DrawPickCards(picks);
        else
            DrawSlots(pickSlots, picks, false);
    }

    void DrawPickCards(List<ChampionData> picks)
    {
        for (int i = 0; i < pickCards.Length; i++)
        {
            if (pickCards[i] == null) continue;
            pickCards[i].isAlly = (side == TeamSide.Ally);
            if (i < picks.Count) pickCards[i].Apply(picks[i]);
            else pickCards[i].Clear();
        }
    }

    void DrawSlots(Image[] slots, List<ChampionData> data, bool isBan)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (i < data.Count)
            {
                // 밴/픽 된 상태 — portrait 으로 교체
                var c = data[i];
                slots[i].enabled = true;
                if (c.portrait != null)
                {
                    slots[i].sprite = c.portrait;
                    slots[i].color = isBan ? bannedTint : Color.white;
                    slots[i].preserveAspect = true;
                }
                else
                {
                    // portrait 없으면 themeColor 만 적용
                    slots[i].color = isBan ? bannedTint : c.themeColor;
                }
            }
            // 빈 상태 — Image 절대 건드리지 않음 (사용자 디자인 유지)
            // 이전엔 sprite=null + color=(1,1,1,0.15) 로 덮어써서 디자인 깨졌음
        }
    }

    void ClearAll()
    {
        DrawSlots(banSlots, new List<ChampionData>(), true);
        DrawSlots(pickSlots, new List<ChampionData>(), false);
        if (pickCards != null)
            foreach (var pc in pickCards) if (pc != null) pc.Clear();
    }
}
