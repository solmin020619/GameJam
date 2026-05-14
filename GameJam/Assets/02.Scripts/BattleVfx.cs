using System.Collections;
using UnityEngine;

/// <summary>
/// 도형 + 색만으로 만드는 가벼운 VFX 헬퍼.
/// 화살 트레일 (LineRenderer) / 힐 빛 (원형 페이드) / 광역 펄스.
/// </summary>
public static class BattleVfx
{
    static Sprite _circle;
    static Sprite _arrowWood, _arrowIron, _arrowFire, _arrowMagic;
    static Sprite _magicOrb, _healAura;
    static bool _arrowsLoaded, _customVfxLoaded;

    static void LoadArrows()
    {
        if (_arrowsLoaded) return;
        _arrowsLoaded = true;
        _arrowWood = UnityEditor_LoadSprite("Assets/04.Images/Effects/Arrows/arrow_wood.png");
        _arrowIron = UnityEditor_LoadSprite("Assets/04.Images/Effects/Arrows/arrow_iron.png");
        _arrowFire = UnityEditor_LoadSprite("Assets/04.Images/Effects/Arrows/arrow_fire.png");
        _arrowMagic = UnityEditor_LoadSprite("Assets/04.Images/Effects/Arrows/arrow_magic.png");
    }

    static Sprite UnityEditor_LoadSprite(string path)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return Resources.Load<Sprite>(path);
#endif
    }

    static void LoadCustomVfx()
    {
        if (_customVfxLoaded) return;
        _customVfxLoaded = true;
        _magicOrb = Resources.Load<Sprite>("VFX/magic_orb") ?? Resources.Load<Sprite>("VFX/MagicOrb");
        _healAura = Resources.Load<Sprite>("VFX/heal_aura") ?? Resources.Load<Sprite>("VFX/HealAura") ?? Resources.Load<Sprite>("VFX/heal_ring");
    }

    /// <summary>마법사 평타 — 보라색 마법구 (없으면 magic 화살 fallback)</summary>
    public static void SpawnMagicOrb(Vector3 from, Vector3 to, float duration = 0.24f)
    {
        LoadCustomVfx();
        if (_magicOrb == null)
        {
            SpawnArrowProjectile(from, to, isMagic: true, duration: duration);
            return;
        }
        var go = new GameObject("MagicOrb_Projectile");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _magicOrb;
        sr.sortingOrder = 70;
        go.transform.position = from;
        go.transform.localScale = Vector3.one * 0.45f;
        var helper = go.AddComponent<ArrowProjectileHelper>();
        helper.Init(from, to, duration);
    }

    /// <summary>스킬/궁극기 VFX 일반 — Resources/VFX/{name}.png 로드 + 위치에 띄우기 + 페이드아웃</summary>
    static System.Collections.Generic.Dictionary<string, Sprite> _vfxCache = new();
    public static void SpawnSkillVfx(string spriteName, Vector3 pos, float duration = 0.7f, float scale = 1.5f)
    {
        if (string.IsNullOrEmpty(spriteName)) return;
        if (!_vfxCache.TryGetValue(spriteName, out var sprite))
        {
            // 다양한 케이스 시도 — 사용자가 어떤 형식으로 저장했는지 모름
            sprite = Resources.Load<Sprite>($"VFX/{spriteName}")
                  ?? Resources.Load<Sprite>($"VFX/{spriteName.ToLower()}")
                  ?? Resources.Load<Sprite>($"VFX/{Capitalize(spriteName)}");
            _vfxCache[spriteName] = sprite;
        }
        if (sprite == null) return;

        var go = new GameObject($"VFX_{spriteName}");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 80;
        var fader = go.AddComponent<HealAuraFader>(); // 기존 fader 재활용 (커지면서 fade out)
        fader.duration = duration;
    }

    static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

    /// <summary>힐 받은 대상 발 밑에 oval aura — 1초 fade out (sprite 없으면 생략)</summary>
    public static void SpawnHealAura(Transform target, float duration = 1.0f)
    {
        if (target == null) return;
        LoadCustomVfx();
        if (_healAura == null) return; // sprite 없으면 VFX 안 띄움 (화살 fallback 제거)

        var go = new GameObject("HealAura");
        go.transform.SetParent(target, false);
        go.transform.localPosition = new Vector3(0, -0.05f, 0);
        go.transform.localScale = Vector3.one * 1.2f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 5;
        sr.sprite = _healAura;

        var fader = go.AddComponent<HealAuraFader>();
        fader.duration = duration;
    }

    /// <summary>실제 화살 sprite 가 날아가는 발사체 (Marksman 용)</summary>
    public static void SpawnArrowProjectile(Vector3 from, Vector3 to, bool isMagic = false, float duration = 0.18f)
    {
        LoadArrows();
        var sprite = isMagic ? _arrowMagic : _arrowIron;
        if (sprite == null) { SpawnProjectileLine(from, to, isMagic ? new Color(1f, 0.4f, 1f) : new Color(1f, 1f, 0.6f), duration); return; }

        var go = new GameObject("Arrow_Projectile");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 70;
        go.transform.position = from;
        go.transform.localScale = Vector3.one * 0.5f;
        // 화살 방향: 우측을 기본으로 가정. to-from 벡터를 향하게 회전.
        Vector3 dir = (to - from).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0, 0, angle);

        var helper = go.AddComponent<ArrowProjectileHelper>();
        helper.Init(from, to, duration);
    }

    /// <summary>활/마법탄 — from 위치에서 to 위치로 짧은 라인 표시 (fallback)</summary>
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

    /// <summary>잔상 이펙트 — 출발 위치에 현재 sprite 복사본 페이드 (순간이동 표현)</summary>
    public static void SpawnAfterImage(GameObject source, Color tint, float duration = 0.18f)
    {
        if (source == null) return;
        var srcRenderers = source.GetComponentsInChildren<SpriteRenderer>();
        if (srcRenderers.Length == 0) return;

        var go = new GameObject("AfterImage");
        go.transform.position = source.transform.position;
        go.transform.rotation = source.transform.rotation;
        go.transform.localScale = source.transform.localScale;

        // 현재 모든 sprite renderer 복제
        foreach (var sr in srcRenderers)
        {
            if (sr == null || sr.sprite == null) continue;
            var child = new GameObject(sr.gameObject.name + "_ghost");
            child.transform.SetParent(go.transform, false);
            child.transform.localPosition = source.transform.InverseTransformPoint(sr.transform.position);
            child.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * sr.transform.rotation;
            child.transform.localScale = sr.transform.lossyScale;

            var ghostSr = child.AddComponent<SpriteRenderer>();
            ghostSr.sprite = sr.sprite;
            ghostSr.color = tint;
            ghostSr.sortingOrder = sr.sortingOrder - 1;
            ghostSr.flipX = sr.flipX;
            ghostSr.flipY = sr.flipY;
        }

        var helper = go.AddComponent<AfterImageHelper>();
        helper.Init(duration, tint);
    }

    /// <summary>넉백 — 대상에게 force 적용 + 일시 root</summary>
    public static void ApplyKnockback(ChampionUnit target, Vector3 sourcePos, float force = 6f, float rootDuration = 0.5f)
    {
        if (target == null || target.IsDead) return;
        var rb = target.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        Vector2 dir = ((Vector2)target.transform.position - (Vector2)sourcePos).normalized;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dir * force, ForceMode2D.Impulse);
        target.ApplyRoot(rootDuration);
    }

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

class AfterImageHelper : MonoBehaviour
{
    float _duration, _elapsed;
    Color _tint;
    SpriteRenderer[] _renderers;

    public void Init(float duration, Color tint)
    {
        _duration = duration;
        _tint = tint;
        _renderers = GetComponentsInChildren<SpriteRenderer>();
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = _elapsed / _duration;
        if (t >= 1f) { Destroy(gameObject); return; }
        float a = _tint.a * (1f - t);
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            var c = _tint; c.a = a;
            r.color = c;
        }
    }
}

class HealAuraFader : MonoBehaviour
{
    public float duration = 1.0f;
    SpriteRenderer _sr;
    float _elapsed;

    void Awake() { _sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = _elapsed / duration;
        if (t >= 1f) { Destroy(gameObject); return; }
        // 살짝 커지면서 fade out
        transform.localScale = Vector3.one * (1.2f + t * 0.3f);
        if (_sr != null)
        {
            var c = _sr.color;
            c.a = Mathf.Lerp(0.8f, 0f, t);
            _sr.color = c;
        }
    }
}

class ArrowProjectileHelper : MonoBehaviour
{
    Vector3 _from, _to;
    float _duration, _elapsed;

    public void Init(Vector3 from, Vector3 to, float duration)
    {
        _from = from; _to = to; _duration = duration;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = _elapsed / _duration;
        if (t >= 1f) { Destroy(gameObject); return; }
        transform.position = Vector3.Lerp(_from, _to, t);
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
