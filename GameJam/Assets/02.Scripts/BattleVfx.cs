using System.Collections;
using UnityEngine;

/// <summary>
/// 도형 + 색만으로 만드는 가벼운 VFX 헬퍼.
/// 화살 트레일 (LineRenderer) / 힐 빛 (원형 페이드) / 광역 펄스.
/// </summary>
public static class BattleVfx
{
    static Sprite _circle;

    /// <summary>활/마법탄 — from 위치에서 to 위치로 짧은 라인 표시</summary>
    public static void SpawnProjectileLine(Vector3 from, Vector3 to, Color color, float lifetime = 0.12f)
    {
        var go = new GameObject("Projectile_Line");
        var lr = go.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = new Color(color.r, color.g, color.b, 0f);
        lr.startWidth = 0.08f;
        lr.endWidth = 0.02f;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.sortingOrder = 50;
        Object.Destroy(go, lifetime);
    }

    /// <summary>힐/버프 — 대상 위에 원이 커지면서 페이드</summary>
    public static void SpawnRingPulse(Vector3 worldPos, Color color, float duration = 0.5f, float maxRadius = 0.8f)
    {
        var go = new GameObject("Ring_Pulse");
        go.transform.position = worldPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetCircleSprite();
        sr.color = color;
        sr.sortingOrder = 60;

        var helper = go.AddComponent<RingPulseHelper>();
        helper.Init(duration, maxRadius);
    }

    /// <summary>모든 적/광역 — 캐스터 위치에 큰 원이 펄스</summary>
    public static void SpawnAoePulse(Vector3 worldPos, Color color, float radius = 2f, float duration = 0.4f)
        => SpawnRingPulse(worldPos, color, duration, radius);

    static Sprite GetCircleSprite()
    {
        if (_circle != null) return _circle;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float r = c - 1f;
                float ring = Mathf.Clamp01(1f - Mathf.Abs(dist - r * 0.7f) / (r * 0.3f));  // ring shape
                tex.SetPixel(x, y, new Color(1, 1, 1, ring));
            }
        tex.Apply();
        _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _circle;
    }
}

class RingPulseHelper : MonoBehaviour
{
    float _duration;
    float _maxRadius;
    float _elapsed;
    SpriteRenderer _sr;
    Color _baseColor;

    public void Init(float duration, float maxRadius)
    {
        _duration = duration;
        _maxRadius = maxRadius;
        _sr = GetComponent<SpriteRenderer>();
        _baseColor = _sr.color;
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        float t = _elapsed / _duration;
        if (t >= 1f) { Destroy(gameObject); return; }

        // scale: 0 → maxRadius
        float scale = Mathf.Lerp(0.1f, _maxRadius, t);
        transform.localScale = new Vector3(scale, scale, 1f);
        // alpha: 1 → 0
        var c = _baseColor;
        c.a = _baseColor.a * (1f - t);
        _sr.color = c;
    }
}
