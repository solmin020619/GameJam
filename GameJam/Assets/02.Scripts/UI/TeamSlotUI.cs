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

    [Header("Slots")]
    public Image[] banSlots;
    public Image[] pickSlots;

    [Header("Slot Look")]
    public Sprite emptySlotSprite;
    public Color emptySlotColor = new Color(1, 1, 1, 0.15f);

    [Header("Ban Overlay (X mark)")]
    public Sprite bannedXSprite;
    public Color bannedTint = new Color(0.6f, 0.6f, 0.6f, 0.7f);

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
        ClearAll();
    }

    public void Refresh()
    {
        var bans = side == TeamSide.Ally ? PickResult.AllyBans : PickResult.EnemyBans;
        var picks = side == TeamSide.Ally ? PickResult.AllyPicks : PickResult.EnemyPicks;
        DrawSlots(banSlots, bans, true);
        DrawSlots(pickSlots, picks, false);
    }

    void DrawSlots(Image[] slots, List<ChampionData> data, bool isBan)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (i < data.Count)
            {
                var c = data[i];
                slots[i].enabled = true;
                slots[i].sprite = c.portrait != null ? c.portrait : emptySlotSprite;
                slots[i].color = isBan ? bannedTint :
                                 (c.portrait != null ? Color.white : c.themeColor);
            }
            else
            {
                slots[i].enabled = true;
                slots[i].sprite = emptySlotSprite;
                slots[i].color = emptySlotColor;
            }
        }
    }

    void ClearAll()
    {
        DrawSlots(banSlots, new List<ChampionData>(), true);
        DrawSlots(pickSlots, new List<ChampionData>(), false);
    }
}
