using TMPro;
using UnityEngine;

/// <summary>
/// 화면 상단 가운데에 양 팀 킬 점수 표시.  [N]  ⚔  [N]
/// </summary>
public class KillScoreUI : MonoBehaviour
{
    public static KillScoreUI Instance;

    public TextMeshProUGUI team0Text;
    public TextMeshProUGUI team1Text;

    public Color allyColor = new Color(0.4f, 0.7f, 1f);
    public Color enemyColor = new Color(1f, 0.4f, 0.4f);

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        Refresh(0, 0);
    }

    public void Refresh(int team0Kills, int team1Kills)
    {
        if (team0Text != null) team0Text.text = team0Kills.ToString();
        if (team1Text != null) team1Text.text = team1Kills.ToString();
    }
}
