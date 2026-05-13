using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BanPickHUD : MonoBehaviour
{
    [Header("Phase / Turn")]
    public TextMeshProUGUI phaseLabel;
    public TextMeshProUGUI turnLabel;
    public Image turnSideIndicator;
    public Color allyColor = new Color(0.4f, 0.7f, 1f);
    public Color enemyColor = new Color(1f, 0.4f, 0.4f);

    [Header("Timer")]
    public Image timerFill;
    public TextMeshProUGUI timerLabel;

    [Header("Done overlay")]
    public GameObject doneOverlay;
    public TextMeshProUGUI doneLabel;

    [Header("Slot refs (refresh on step)")]
    public TeamSlotUI allySlotUI;
    public TeamSlotUI enemySlotUI;

    public void RefreshAll(BanPickManager mgr)
    {
        RefreshPhase(mgr.Phase);
        RefreshTurn(mgr);
        UpdateTimer(mgr.TurnRemaining);
        allySlotUI?.Refresh();
        enemySlotUI?.Refresh();
    }

    public void RefreshPhase(BanPickPhase p)
    {
        if (phaseLabel == null) return;
        phaseLabel.text = p switch
        {
            BanPickPhase.Banning => "BAN PHASE",
            BanPickPhase.Picking => "PICK PHASE",
            BanPickPhase.Done => "READY!",
            _ => ""
        };
        phaseLabel.color = p == BanPickPhase.Banning ? new Color(1f, 0.4f, 0.4f)
                          : p == BanPickPhase.Picking ? new Color(1f, 0.9f, 0.4f)
                          : new Color(0.4f, 1f, 0.5f);
    }

    public void RefreshTurn(BanPickManager mgr)
    {
        if (turnLabel == null) return;
        string side = mgr.IsAllyTurn ? "YOUR TURN" : "ENEMY...";
        string act = mgr.IsBanStep ? "BAN" : "PICK";
        turnLabel.text = $"{side}  ·  {act}";
        turnLabel.color = mgr.IsAllyTurn ? allyColor : enemyColor;
        if (turnSideIndicator != null)
            turnSideIndicator.color = mgr.IsAllyTurn ? allyColor : enemyColor;

        allySlotUI?.Refresh();
        enemySlotUI?.Refresh();
    }

    public void UpdateTimer(float remaining)
    {
        if (timerLabel != null)
            timerLabel.text = Mathf.Max(0f, Mathf.CeilToInt(remaining)).ToString();
        if (timerFill != null && BanPickManager.Instance != null)
        {
            float max = BanPickManager.Instance.config.phaseTimeLimit;
            timerFill.fillAmount = max > 0 ? Mathf.Clamp01(remaining / max) : 0f;
        }
    }

    public void ShowDone()
    {
        if (doneOverlay != null) doneOverlay.SetActive(true);
        if (doneLabel != null) doneLabel.text = "READY!";
    }
}
