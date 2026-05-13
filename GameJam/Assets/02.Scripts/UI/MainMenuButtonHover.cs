using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private Text buttonText;
    [SerializeField] private GameObject hoverArrow;

    [Header("Text Color")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = Color.white;

    [Header("Text Move")]
    [SerializeField] private float hoverMoveX = 18f;

    [Header("Optional Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hoverSound;

    private RectTransform buttonTextRect;
    private Vector2 originalTextPosition;
    private bool isHovered;

    private void Awake()
    {
        if (buttonText != null)
        {
            buttonTextRect = buttonText.GetComponent<RectTransform>();
            originalTextPosition = buttonTextRect.anchoredPosition;
            buttonText.color = normalColor;
        }

        if (hoverArrow != null)
        {
            hoverArrow.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isHovered) return;
        isHovered = true;

        if (hoverArrow != null)
        {
            hoverArrow.SetActive(true);
        }

        if (buttonText != null)
        {
            buttonText.color = hoverColor;
        }

        if (buttonTextRect != null)
        {
            buttonTextRect.anchoredPosition = originalTextPosition + new Vector2(hoverMoveX, 0f);
        }

        if (audioSource != null && hoverSound != null)
        {
            audioSource.PlayOneShot(hoverSound);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        if (hoverArrow != null)
        {
            hoverArrow.SetActive(false);
        }

        if (buttonText != null)
        {
            buttonText.color = normalColor;
        }

        if (buttonTextRect != null)
        {
            buttonTextRect.anchoredPosition = originalTextPosition;
        }
    }
}