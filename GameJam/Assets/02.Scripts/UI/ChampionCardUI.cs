using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChampionCardUI : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Wiring")]
    public Image portrait;
    public Image background;
    public TextMeshProUGUI nameLabel;
    public TextMeshProUGUI roleLabel;
    public GameObject bannedOverlay;
    public GameObject pickedOverlay;
    public GameObject hoverFrame;

    [Header("Tween")]
    public float hoverScale = 1.08f;
    public float hoverLerp = 12f;

    [Header("SFX (optional)")]
    public AudioClip hoverSfx;
    public AudioClip clickSfx;

    ChampionData data;
    BanPickManager manager;
    Vector3 baseScale;
    bool isHover;

    void Awake()
    {
        baseScale = transform.localScale;
        if (hoverFrame != null) hoverFrame.SetActive(false);
    }

    public void Bind(ChampionData championData, BanPickManager mgr)
    {
        data = championData;
        manager = mgr;

        if (portrait != null)
        {
            portrait.sprite = championData.portrait;
            portrait.enabled = championData.portrait != null;
            portrait.color = championData.portrait != null
                ? Color.white
                : championData.themeColor;
        }
        if (background != null) background.color = championData.themeColor * 0.6f + Color.black * 0.4f;
        if (nameLabel != null) nameLabel.text = championData.displayName;
        if (roleLabel != null) roleLabel.text = championData.role.ToString();

        RefreshState();
    }

    public void RefreshState()
    {
        bool banned = PickResult.AllyBans.Contains(data) || PickResult.EnemyBans.Contains(data);
        bool picked = PickResult.AllyPicks.Contains(data) || PickResult.EnemyPicks.Contains(data);

        if (bannedOverlay != null) bannedOverlay.SetActive(banned);
        if (pickedOverlay != null) pickedOverlay.SetActive(picked && !banned);

        var cg = GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = (banned || picked) ? 0.35f : 1f;
    }

    void Update()
    {
        Vector3 target = isHover && IsInteractable() ? baseScale * hoverScale : baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, target, Time.unscaledDeltaTime * hoverLerp);
    }

    bool IsInteractable()
    {
        if (manager == null || data == null) return false;
        return manager.IsAllyTurn && manager.IsSelectable(data);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!IsInteractable()) return;
        if (clickSfx != null) AudioSource.PlayClipAtPoint(clickSfx, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        manager.OnPlayerClickCard(data);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        isHover = true;
        if (hoverFrame != null) hoverFrame.SetActive(IsInteractable());
        if (hoverSfx != null && IsInteractable())
            AudioSource.PlayClipAtPoint(hoverSfx, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
    }

    public void OnPointerExit(PointerEventData e)
    {
        isHover = false;
        if (hoverFrame != null) hoverFrame.SetActive(false);
    }
}
