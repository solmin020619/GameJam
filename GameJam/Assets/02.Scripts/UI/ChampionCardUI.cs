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

    [Header("Pick Indicator (선택 사항)")]
    public Image teamColorBar;             // 위쪽 팀 색 바 (픽 시)
    public TextMeshProUGUI pickOrderLabel; // 픽 순서 번호 (1, 2, 3 ...)

    [Header("Colors")]
    public Color allyTeamColor = new Color(0.3f, 0.55f, 1f);
    public Color enemyTeamColor = new Color(1f, 0.3f, 0.3f);

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
        int allyPickIdx = PickResult.AllyPicks.IndexOf(data);
        int enemyPickIdx = PickResult.EnemyPicks.IndexOf(data);
        bool picked = allyPickIdx >= 0 || enemyPickIdx >= 0;
        bool isAllyPick = allyPickIdx >= 0;
        int pickOrder = isAllyPick ? allyPickIdx + 1 : enemyPickIdx + 1;

        // 밴된 카드: 어두운 오버레이 + ⊘ 마크
        if (bannedOverlay != null) bannedOverlay.SetActive(banned);

        // 픽된 카드: 어두운 오버레이는 끔 (전체가 팀 색으로 보이게)
        if (pickedOverlay != null) pickedOverlay.SetActive(false);

        // 카드 background 색: 픽 시 팀 색(밝게) / 밴 시 default / 평소 themeColor
        if (background != null && data != null)
        {
            if (picked && !banned)
                background.color = isAllyPick ? allyTeamColor : enemyTeamColor;
            else
                background.color = data.themeColor * 0.6f + Color.black * 0.4f;
        }

        // 상단 팀 색 바 (살짝 진하게)
        if (teamColorBar != null)
        {
            teamColorBar.gameObject.SetActive(picked && !banned);
            if (picked && !banned)
                teamColorBar.color = isAllyPick ? allyTeamColor * 1.2f : enemyTeamColor * 1.2f;
        }

        // 오른쪽 위 픽 순서 번호 박스
        if (pickOrderLabel != null)
        {
            var boxGo = pickOrderLabel.transform.parent != null
                ? pickOrderLabel.transform.parent.gameObject
                : pickOrderLabel.gameObject;
            boxGo.SetActive(picked && !banned);
            if (picked && !banned)
            {
                pickOrderLabel.text = pickOrder.ToString();
                pickOrderLabel.color = Color.white;
                var labelBg = pickOrderLabel.transform.parent != null ? pickOrderLabel.transform.parent.GetComponent<Image>() : null;
                if (labelBg != null) labelBg.color = isAllyPick ? allyTeamColor * 0.7f : enemyTeamColor * 0.7f;
            }
        }

        // 밴만 어둡게. 픽은 alpha 그대로
        var cg = GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = banned ? 0.4f : 1f;
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
