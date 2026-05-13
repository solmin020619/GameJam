using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 가장자리 빨간 그라데이션 비네트. 아군 사망 시 0.5초 페이드인-아웃.
/// </summary>
public class DamageVignette : MonoBehaviour
{
    public static DamageVignette Instance;

    Image _image;
    static Sprite _vignetteSprite;

    void Awake()
    {
        Instance = this;
        _image = GetComponent<Image>();
        if (_image == null) _image = gameObject.AddComponent<Image>();
        _image.sprite = GetVignetteSprite();
        _image.color = new Color(1, 0, 0, 0);
        _image.raycastTarget = false;
    }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Flash(float maxAlpha = 0.55f, float duration = 0.5f)
    {
        StopAllCoroutines();
        StartCoroutine(FlashRoutine(maxAlpha, duration));
    }

    IEnumerator FlashRoutine(float maxAlpha, float duration)
    {
        float t = 0f;
        // fade in 30%
        float inT = duration * 0.3f;
        while (t < inT)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(0f, maxAlpha, t / inT);
            _image.color = new Color(1, 0, 0, a);
            yield return null;
        }
        // fade out 70%
        t = 0f;
        float outT = duration * 0.7f;
        while (t < outT)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(maxAlpha, 0f, t / outT);
            _image.color = new Color(1, 0, 0, a);
            yield return null;
        }
        _image.color = new Color(1, 0, 0, 0);
    }

    /// <summary>radial gradient 텍스처 (가운데 투명, 가장자리 빨강)</summary>
    static Sprite GetVignetteSprite()
    {
        if (_vignetteSprite != null) return _vignetteSprite;
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                // 0.5 이하 = 투명, 1.0 = 진함
                float alpha = Mathf.SmoothStep(0.5f, 1f, dist);
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        tex.Apply();
        _vignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        return _vignetteSprite;
    }
}
