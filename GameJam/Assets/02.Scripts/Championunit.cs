using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public class ChampionUnit : MonoBehaviour
{
    [Header("Data")]
    public ChampionSO Data;

    [Header("Team (0 = ally / 1 = enemy)")]
    public int TeamId;

    public float CurrentHp { get; private set; }
    public bool IsDead { get; private set; }

    public ChampionUnit PriorityTarget { get; set; }

    private Rigidbody2D _rb;
    private SPUM_Prefabs _spum;
    private ChampionUnit _currentTarget;
    private float _attackTimer;
    private float _healTimer;          // healer 자동 힐 쿨다운
    private bool _isKiting;            // hysteresis state for ranged
    private PlayerState _currentAnim = PlayerState.IDLE;

    // HP bar refs
    private Image _hpFill;
    private GameObject _hpBarRoot;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spum = GetComponentInChildren<SPUM_Prefabs>();

        // Kinematic: 외부 임펄스 영향 0 → 시작 시 미끄러짐 사라짐
        // velocity 는 우리가 직접 설정 (이동 코드 그대로 작동)
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.freezeRotation = true;
        _rb.linearVelocity = Vector2.zero;
    }

    public void Init(ChampionSO data, int teamId)
    {
        Data = data;
        TeamId = teamId;
        CurrentHp = data.MaxHp;
        IsDead = false;
        _attackTimer = 0f;
        _healTimer = data.HealCooldown * 0.5f;  // 시작 시 절반쯤 차있게

        if (_spum != null) _spum.OverrideControllerInit();
        CreateHpBar();
        PlayAnim(PlayerState.IDLE);
    }

    void Update()
    {
        if (IsDead || !BattleManager.Instance.IsBattleRunning) return;

        // 매 프레임 velocity reset → 단체 미끄러짐 방지
        _rb.linearVelocity = Vector2.zero;

        _attackTimer -= Time.deltaTime;
        _healTimer -= Time.deltaTime;
        UpdateHpBar();

        // 힐러 자동 힐 패시브
        if (Data.Role == ChampionRole.Healer) TryAutoHeal();

        _currentTarget = GetTarget();

        if (_currentTarget == null)
        {
            _rb.linearVelocity = Vector2.zero;
            PlayAnim(PlayerState.IDLE);
            return;
        }

        // 역할별 행동 분기
        switch (Data.Role)
        {
            case ChampionRole.Marksman:
            case ChampionRole.Mage:
                BehaveRanged();
                break;
            case ChampionRole.Healer:
                BehaveHealer();
                break;
            default:
                BehaveMelee();
                break;
        }
    }

    // ========== 행동 패턴 ==========

    /// <summary>탱커/전사: 타겟에게 직진 → 사거리 내면 공격</summary>
    void BehaveMelee()
    {
        float dist = Vector2.Distance(transform.position, _currentTarget.transform.position);
        if (dist <= Data.AttackRange)
        {
            _rb.linearVelocity = Vector2.zero;
            TryAttack();
        }
        else MoveToward(_currentTarget.transform.position);
    }

    /// <summary>궁수/마법사: 카이팅 (적이 너무 가까우면 뒤로, 사거리 끝에서 공격)</summary>
    void BehaveRanged()
    {
        var nearest = GetNearestAliveEnemy();
        if (nearest == null) { BehaveMelee(); return; }

        float distToNearest = Vector2.Distance(transform.position, nearest.transform.position);

        // Hysteresis: 카이팅 시작/중지 임계값 분리 → 핑퐁/무한후퇴 방지
        float enterThreshold = Data.KitingDistance;            // 안으로 들어오면 카이팅 시작
        float exitThreshold = Data.KitingDistance * 1.4f;      // 충분히 멀어지면 카이팅 중지

        if (!_isKiting && distToNearest < enterThreshold) _isKiting = true;
        else if (_isKiting && distToNearest > exitThreshold) _isKiting = false;

        if (_isKiting)
        {
            Vector2 away = ((Vector2)transform.position - (Vector2)nearest.transform.position).normalized;
            _rb.linearVelocity = away * Data.MoveSpeed;
            FaceTarget(_currentTarget.transform.position);
            PlayAnim(PlayerState.MOVE);
            // 후퇴 중에도 사거리 안이면 공격
            if (Vector2.Distance(transform.position, _currentTarget.transform.position) <= Data.AttackRange)
                TryAttackNoAnim();
            return;
        }

        // 안전 거리 → 사거리 끝에서 공격
        float distToTarget = Vector2.Distance(transform.position, _currentTarget.transform.position);
        if (distToTarget <= Data.AttackRange)
        {
            _rb.linearVelocity = Vector2.zero;
            TryAttack();
        }
        else MoveToward(_currentTarget.transform.position);
    }

    /// <summary>힐러: 백라인 위치 유지 (아군 평균 위치보다 뒤) + 평타는 가장 가까운 적</summary>
    void BehaveHealer()
    {
        var allies = BattleManager.Instance.GetAllies(TeamId).Where(a => a != this && !a.IsDead).ToList();
        Vector2 backlinePos = ComputeBacklinePosition(allies);

        float distToBackline = Vector2.Distance(transform.position, backlinePos);
        if (distToBackline > 0.5f)
        {
            MoveToward(backlinePos);
            return;
        }

        // 백라인 도착 → 평타 (사거리 내 적이 있으면)
        float distToTarget = Vector2.Distance(transform.position, _currentTarget.transform.position);
        if (distToTarget <= Data.AttackRange)
        {
            _rb.linearVelocity = Vector2.zero;
            TryAttack();
        }
        else
        {
            _rb.linearVelocity = Vector2.zero;
            FaceTarget(_currentTarget.transform.position);
            PlayAnim(PlayerState.IDLE);
        }
    }

    Vector2 ComputeBacklinePosition(List<ChampionUnit> allies)
    {
        if (allies.Count == 0) return transform.position;
        Vector2 center = Vector2.zero;
        foreach (var a in allies) center += (Vector2)a.transform.position;
        center /= allies.Count;
        // 적과 반대 방향으로 1.2 unit 뒤
        var nearestEnemy = GetNearestAliveEnemy();
        if (nearestEnemy == null) return center;
        Vector2 awayDir = (center - (Vector2)nearestEnemy.transform.position).normalized;
        return center + awayDir * 1.2f;
    }

    // ========== 타게팅 ==========

    ChampionUnit GetTarget()
    {
        var enemies = BattleManager.Instance.GetEnemies(TeamId);
        if (enemies == null) return null;
        var alive = enemies.Where(e => !e.IsDead).ToList();
        if (alive.Count == 0) return null;

        // 1) 플레이어 우선타겟 지정
        if (PriorityTarget != null && !PriorityTarget.IsDead) return PriorityTarget;

        // 2) 역할별
        switch (Data.Role)
        {
            case ChampionRole.Marksman:
            case ChampionRole.Mage:
                // 마무리: HP 비율 가장 낮은 적
                return alive.OrderBy(e => e.CurrentHp / e.Data.MaxHp).First();

            case ChampionRole.Tank:
            case ChampionRole.Fighter:
            case ChampionRole.Healer:
            default:
                return GetNearestFrom(alive);
        }
    }

    ChampionUnit GetNearestAliveEnemy()
    {
        var enemies = BattleManager.Instance.GetEnemies(TeamId);
        if (enemies == null) return null;
        return GetNearestFrom(enemies.Where(e => !e.IsDead).ToList());
    }

    ChampionUnit GetNearestFrom(List<ChampionUnit> list)
    {
        ChampionUnit closest = null;
        float minDist = float.MaxValue;
        foreach (var e in list)
        {
            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d < minDist) { minDist = d; closest = e; }
        }
        return closest;
    }

    // ========== 힐러 자동 힐 ==========
    void TryAutoHeal()
    {
        if (_healTimer > 0f) return;

        var allies = BattleManager.Instance.GetAllies(TeamId);
        if (allies == null) return;

        ChampionUnit weakest = null;
        float lowestPct = 0.85f;  // HP 85% 이하인 아군만 힐 대상
        foreach (var a in allies)
        {
            if (a.IsDead) continue;
            float pct = a.CurrentHp / a.Data.MaxHp;
            if (pct < lowestPct) { lowestPct = pct; weakest = a; }
        }
        if (weakest == null) return;

        float healAmount = weakest.Data.MaxHp * Data.HealAmountPercent;
        weakest.Heal(healAmount);
        _healTimer = Data.HealCooldown;
        PlayAnim(PlayerState.ATTACK);  // 힐 시전 = 공격 애니로 임시 표현 (스킬 추가 시 OTHER로 교체)

        // 힐 빛 이펙트 — 시전자 위와 대상 위에
        var healColor = new Color(0.4f, 1f, 0.5f, 0.9f);
        BattleVfx.SpawnRingPulse(weakest.transform.position + Vector3.up * 0.5f, healColor, 0.5f, 0.9f);
        BattleVfx.SpawnRingPulse(transform.position + Vector3.up * 0.5f, healColor, 0.4f, 0.6f);
    }

    // ========== 이동/공격 헬퍼 ==========

    void MoveToward(Vector3 targetPos)
    {
        Vector2 dir = ((Vector2)targetPos - (Vector2)transform.position).normalized;
        _rb.linearVelocity = dir * Data.MoveSpeed;
        FaceDirection(dir);
        PlayAnim(PlayerState.MOVE);
    }

    void FaceTarget(Vector3 targetPos)
    {
        Vector2 dir = ((Vector2)targetPos - (Vector2)transform.position).normalized;
        FaceDirection(dir);
    }

    void FaceDirection(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) < 0.01f) return;
        transform.localScale = new Vector3(dir.x > 0 ? -1 : 1, 1, 1);
    }

    void TryAttack()
    {
        if (_attackTimer > 0f)
        {
            PlayAnim(PlayerState.IDLE);
            return;
        }
        FaceTarget(_currentTarget.transform.position);
        _attackTimer = 1f / Data.AttackSpeed;
        PlayAnim(PlayerState.ATTACK);

        SpawnRangedVfx();

        float dmg = CalcDamage(Data.AttackDamage, _currentTarget.Data.Defense);
        _currentTarget.TakeDamage(dmg);
    }

    /// <summary>카이팅 중 평타 (애니 안 바꿈, 데미지만)</summary>
    void TryAttackNoAnim()
    {
        if (_attackTimer > 0f) return;
        _attackTimer = 1f / Data.AttackSpeed;
        SpawnRangedVfx();
        float dmg = CalcDamage(Data.AttackDamage, _currentTarget.Data.Defense);
        _currentTarget.TakeDamage(dmg);
    }

    void SpawnRangedVfx()
    {
        if (_currentTarget == null) return;
        // 원거리 챔프는 발사체 라인 VFX
        if (Data.Role == ChampionRole.Marksman)
        {
            BattleVfx.SpawnProjectileLine(transform.position + Vector3.up * 0.7f,
                _currentTarget.transform.position + Vector3.up * 0.7f,
                new Color(1f, 1f, 0.6f, 1f));  // 노란 화살 트레일
        }
        else if (Data.Role == ChampionRole.Mage)
        {
            BattleVfx.SpawnProjectileLine(transform.position + Vector3.up * 0.7f,
                _currentTarget.transform.position + Vector3.up * 0.7f,
                new Color(1f, 0.4f, 1f, 1f));  // 마젠타 마법 트레일
        }
    }

    // damage formula: ATK * (100 / (100 + DEF * 1.8))
    float CalcDamage(float atk, float def)
        => atk * (100f / (100f + def * 1.8f));

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        CurrentHp -= amount;
        BattleManager.Instance.SpawnDamageText(transform.position, amount, isHeal: false);

        // 큰 피해(HP 20% 이상)는 작은 셰이크
        if (Data != null && amount >= Data.MaxHp * 0.2f && CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.08f, 0.05f);

        if (CurrentHp <= 0f) Die();
        else StartCoroutine(HitFlash());
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHp = Mathf.Min(Data.MaxHp, CurrentHp + amount);
        BattleManager.Instance.SpawnDamageText(transform.position, amount, isHeal: true);
    }

    void Die()
    {
        IsDead = true;
        _rb.linearVelocity = Vector2.zero;
        if (_hpBarRoot != null) _hpBarRoot.SetActive(false);
        PlayAnim(PlayerState.DEATH);

        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.18f, 0.12f);

        BattleManager.Instance.OnUnitDied(this);
        StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        yield return new WaitForSeconds(1f);
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, t);
            foreach (var r in renderers) r.color = new Color(1, 1, 1, a);
            yield return null;
        }
        gameObject.SetActive(false);
    }

    IEnumerator HitFlash()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        var originals = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) originals[i] = renderers[i].color;

        // 빨강 깜빡
        var flash = new Color(1f, 0.4f, 0.4f, 1f);
        foreach (var r in renderers) r.color = flash;
        PlayAnim(PlayerState.DAMAGED);
        yield return new WaitForSeconds(0.08f);

        // 원래 색 복구
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = originals[i];
    }

    void PlayAnim(PlayerState state)
    {
        if (_currentAnim == state || _spum == null) return;
        _currentAnim = state;
        _spum.PlayAnimation(state, 0);
    }

    void OnDestroy()
    {
        if (_hpBarRoot != null) Destroy(_hpBarRoot);
    }

    void OnDrawGizmosSelected()
    {
        if (Data == null) return;
        Gizmos.color = TeamId == 0 ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, Data.AttackRange);
        if (Data.Role == ChampionRole.Marksman || Data.Role == ChampionRole.Mage)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, Data.KitingDistance);
        }
    }

    // ========== HP Bar ==========

    static Sprite _whiteSprite;
    static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null)
                _whiteSprite = Sprite.Create(Texture2D.whiteTexture,
                    new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            return _whiteSprite;
        }
    }

    void CreateHpBar()
    {
        if (_hpBarRoot != null) return;

        // 별도 root (부모-자식 X → flipX 영향 안 받음)
        _hpBarRoot = new GameObject($"HpBar_{name}");
        _hpBarRoot.transform.localScale = Vector3.one * 0.01f;

        var canvas = _hpBarRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 200;

        // 배경
        var bgGo = new GameObject("BG");
        bgGo.transform.SetParent(_hpBarRoot.transform, false);
        var bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.sizeDelta = new Vector2(110, 14);
        bgRt.localPosition = Vector3.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = WhiteSprite;
        bgImg.color = new Color(0, 0, 0, 0.85f);
        bgImg.raycastTarget = false;

        // Fill (좌측에서 채워짐)
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(_hpBarRoot.transform, false);
        var fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.sizeDelta = new Vector2(100, 8);
        fillRt.localPosition = Vector3.zero;
        _hpFill = fillGo.AddComponent<Image>();
        _hpFill.sprite = WhiteSprite;
        _hpFill.color = TeamId == 0 ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.35f, 0.35f);
        _hpFill.type = Image.Type.Filled;
        _hpFill.fillMethod = Image.FillMethod.Horizontal;
        _hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _hpFill.fillAmount = 1f;
        _hpFill.raycastTarget = false;
    }

    void UpdateHpBar()
    {
        if (_hpBarRoot == null || _hpFill == null || Data == null) return;
        _hpBarRoot.transform.position = transform.position + Vector3.up * 1.7f;
        _hpFill.fillAmount = Mathf.Clamp01(CurrentHp / Data.MaxHp);
    }
}
