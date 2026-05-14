using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AScene_Win 의 Firecracker_N Image 들을 폭죽 터지듯이 펄스 애니메이션.
/// 각 인스턴스마다 랜덤 phase 로 자연스러운 연속 폭발 효과.
/// </summary>
[RequireComponent(typeof(Image))]
public class FireworkSpriteAnimator : MonoBehaviour
{
    [Header("폭죽 펄스")]
    [Tooltip("폭발 1회 주기 (s)")]
    public float burstInterval = 1.2f;
    [Tooltip("폭발 시 최대 스케일 배수")]
    public float maxScale = 1.4f;
    [Tooltip("폭발 후 페이드 시간 비율 (전체 주기 대비)")]
    [Range(0.3f, 0.9f)] public float fadeRatio = 0.7f;

    [Header("색상 사이클 (랜덤)")]
    public Color[] colors = new[] {
        new Color(1f, 0.4f, 0.6f, 1f),    // 핑크
        new Color(0.5f, 1f, 0.8f, 1f),    // 민트
        new Color(0.4f, 0.7f, 1f, 1f),    // 시안
        new Color(1f, 0.9f, 0.3f, 1f),    // 노랑
        new Color(0.9f, 0.5f, 1f, 1f),    // 보라
    };

    Image _img;
    Vector3 _baseScale;
    float _phaseOffset;
    Color _currentColor;
    float _lastCycle;

    void Awake()
    {
        _img = GetComponent<Image>();
        _baseScale = transform.localScale;
        // 인스턴스마다 다른 phase 로 시작 — 동시에 터지지 않고 자연스럽게
        _phaseOffset = Random.Range(0f, burstInterval);
        _currentColor = colors[Random.Range(0, colors.Length)];
        _img.color = _currentColor;
    }

    void Update()
    {
        if (_img == null) return;

        float t = (Time.unscaledTime + _phaseOffset) % burstInterval;
        float ratio = t / burstInterval;  // 0 → 1 over 1 cycle

        // 사이클 시작 시 색 바꿈
        int cycleIdx = Mathf.FloorToInt((Time.unscaledTime + _phaseOffset) / burstInterval);
        if (cycleIdx != _lastCycle)
        {
            _lastCycle = cycleIdx;
            _currentColor = colors[Random.Range(0, colors.Length)];
        }

        // 스케일 — 0 ~ peakRatio 까지 확대, 그 후 유지/축소
        float scaleProgress;
        const float peakRatio = 0.25f;
        if (ratio < peakRatio)
            scaleProgress = ratio / peakRatio; // 0 → 1
        else
            scaleProgress = 1f - ((ratio - peakRatio) / (1f - peakRatio)) * 0.5f; // 1 → 0.5 (절반 정도 유지)
        float scale = Mathf.Lerp(1f, maxScale, EaseOutBack(scaleProgress));
        transform.localScale = _baseScale * scale;

        // 알파 — 0 → 1 → 0
        float alpha;
        if (ratio < peakRatio)
            alpha = ratio / peakRatio; // fade in
        else
        {
            float fadeStart = peakRatio;
            float fadeEnd = peakRatio + (1f - peakRatio) * fadeRatio;
            if (ratio < fadeEnd)
                alpha = 1f - (ratio - fadeStart) / (fadeEnd - fadeStart);
            else
                alpha = 0f;
        }

        var c = _currentColor;
        c.a = alpha;
        _img.color = c;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
