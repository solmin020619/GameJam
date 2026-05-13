using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public partial class ChampionUnit : MonoBehaviour
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

    // Skill timers (CD 가 0 도달하면 발동 → CD 리셋)
    private float _basicCd;
    private float _ultCd;

    // 상태이상
    private float _stunTimer;          // > 0 이면 모든 행동 정지
    private float _rootTimer;          // > 0 이면 이동만 정지 (공격은 OK)
    private float _backAttackTimer;    // > 0 이면 닌자 배후 상태 (평타 1.3배)

    // 킬로그용 — 마지막으로 데미지 입힌 attacker 추적
    public ChampionUnit LastAttacker { get; private set; }
    public bool IsBackAttacking => _backAttackTimer > 0f;
    public void ApplyBackAttack(float duration) { _backAttackTimer = Mathf.Max(_backAttackTimer, duration); }

    // 버프 (남은 시간 + 배수)
    private float _atkSpeedBuffEnd;  private float _atkSpeedBuffPct;
    private float _moveSpeedBuffEnd; private float _moveSpeedBuffPct;
    private float _defenseBuffEnd;   private float _defenseBuffPct;

    public bool IsStunned => _stunTimer > 0f;
    public bool IsRooted => _rootTimer > 0f;

    public void ApplyStun(float duration) { _stunTimer = Mathf.Max(_stunTimer, duration); }
    public void ApplyRoot(float duration) { _rootTimer = Mathf.Max(_rootTimer, duration); }
    public void ApplyAttackSpeedBuff(float percent, float duration) { _atkSpeedBuffPct = percent; _atkSpeedBuffEnd = Time.time + duration; }
    public void ApplyMoveSpeedBuff(float percent, float duration) { _moveSpeedBuffPct = percent; _moveSpeedBuffEnd = Time.time + duration; }
    public void ApplyDefenseBuff(float percent, float duration) { _defenseBuffPct = percent; _defenseBuffEnd = Time.time + duration; }

    float GetEffectiveAttackSpeed() => Data.AttackSpeed * (1f + (_atkSpeedBuffEnd > Time.time ? _atkSpeedBuffPct : 0f));
    float GetEffectiveMoveSpeed() => Data.MoveSpeed * (1f + (_moveSpeedBuffEnd > Time.time ? _moveSpeedBuffPct : 0f));
    public float GetEffectiveDefense() => Data.Defense * (1f + (_defenseBuffEnd > Time.time ? _defenseBuffPct : 0f));

    // 발밑 외곽선 (팀 색 / 우선타겟이면 진한 빨강)
    private GameObject _footRing;
    private SpriteRenderer _footRingSr;

    // Champion info UI refs (HP + Basic CD + Ult slot + Name)
    private GameObject _infoUiRoot;
    private Image _hpFill;
    private Image _hpDelayedFill;      // 빨간 잔여 (lerp 따라감)
    private Image _basicCdFill;
    private Image _ultSlotImage;       // 필살기 배경 (CD 진행에 따라 색 변화)
    private Image _ultSlotFill;        // 필살기 채워지는 게이지 (옵션)
    private TMPro.TextMeshProUGUI _nameLabel;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spum = GetComponentInChildren<SPUM_Prefabs>();

        // Dynamic 유지 → 벽 콜라이더(PolygonCollider2D)에 막힘
        // 캐릭터끼리는 BattleManager 에서 IgnoreCollision 처리 → 미끄러짐 X
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.linearDamping = 0f;  // 우리가 velocity 직접 제어하니 자연 감속 X
    }

    public void Init(ChampionSO data, int teamId)
    {
        Data = data;
        TeamId = teamId;
        CurrentHp = data.MaxHp;
        IsDead = false;
        _attackTimer = 0f;
        _healTimer = data.HealCooldown * 0.5f;  // 시작 시 절반쯤 차있게

        // 스킬 CD 초기화 (시작 시 일부 차있게 — 첫 5초 공백 방지)
        _basicCd = data.BasicSkillCooldown * 0.6f;
        _ultCd = data.UltimateCooldown * 0.8f;

        if (_spum != null) _spum.OverrideControllerInit();
        CreateInfoUI();
        CreateFootRing();
        PlayAnim(PlayerState.IDLE);
    }

    static Sprite _ringSprite;
    static Sprite GetRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;
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
                // 링 모양 (가장자리만 진하게)
                float ring = Mathf.Clamp01(1f - Mathf.Abs(dist - r * 0.85f) / (r * 0.15f));
                tex.SetPixel(x, y, new Color(1, 1, 1, ring));
            }
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64);
        return _ringSprite;
    }

    void CreateFootRing()
    {
        if (_footRing != null) return;
        _footRing = new GameObject($"FootRing_{name}");
        // 부모-자식 X (flipX 영향 안 받음). LateUpdate에서 위치 추적
        _footRingSr = _footRing.AddComponent<SpriteRenderer>();
        _footRingSr.sprite = GetRingSprite();
        _footRingSr.sortingOrder = -5;  // 캐릭 뒤
        // 납작한 타원
        _footRing.transform.localScale = new Vector3(0.9f, 0.45f, 1f);
    }

    void Update()
    {
        if (IsDead || !BattleManager.Instance.IsBattleRunning) return;

        // 매 프레임 velocity reset → 단체 미끄러짐 방지
        _rb.linearVelocity = Vector2.zero;

        _attackTimer -= Time.deltaTime;
        _healTimer -= Time.deltaTime;
        _basicCd -= Time.deltaTime;
        _ultCd -= Time.deltaTime;
        _stunTimer -= Time.deltaTime;
        _rootTimer -= Time.deltaTime;
        _backAttackTimer -= Time.deltaTime;

        UpdateInfoUI();

        // Stun 중이면 모든 행동 정지
        if (IsStunned)
        {
            _rb.linearVelocity = Vector2.zero;
            PlayAnim(PlayerState.DEBUFF);
            return;
        }

        // 스킬 자동 발동 (CD 0 도달 + 사거리 내 적 있음)
        TryCastUltimate();
        TryCastBasicSkill();

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
            if (IsRooted) { _rb.linearVelocity = Vector2.zero; return; }
            Vector2 away = ((Vector2)transform.position - (Vector2)nearest.transform.position).normalized;
            _rb.linearVelocity = away * GetEffectiveMoveSpeed();
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
        if (IsRooted) { _rb.linearVelocity = Vector2.zero; return; }
        Vector2 dir = ((Vector2)targetPos - (Vector2)transform.position).normalized;
        _rb.linearVelocity = dir * GetEffectiveMoveSpeed();
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
        _attackTimer = 1f / GetEffectiveAttackSpeed();
        PlayAnim(PlayerState.ATTACK);

        SpawnRangedVfx();

        float atkMul = IsBackAttacking ? 1.3f : 1f;   // 닌자 배후 상태 평타 +30%
        float dmg = CalcDamage(Data.AttackDamage * atkMul, _currentTarget.GetEffectiveDefense());
        _currentTarget.TakeDamage(dmg, DamageType.Basic, this);
    }

    /// <summary>카이팅 중 평타 (애니 안 바꿈, 데미지만)</summary>
    void TryAttackNoAnim()
    {
        if (_attackTimer > 0f) return;
        _attackTimer = 1f / GetEffectiveAttackSpeed();
        SpawnRangedVfx();
        float atkMul = IsBackAttacking ? 1.3f : 1f;
        float dmg = CalcDamage(Data.AttackDamage * atkMul, _currentTarget.GetEffectiveDefense());
        _currentTarget.TakeDamage(dmg, DamageType.Basic, this);
    }

    void SpawnRangedVfx()
    {
        if (_currentTarget == null) return;
        if (Data.Role == ChampionRole.Marksman)
        {
            BattleVfx.SpawnArrowProjectile(
                transform.position + Vector3.up * 0.7f,
                _currentTarget.transform.position + Vector3.up * 0.7f,
                isMagic: false);
        }
        else if (Data.Role == ChampionRole.Mage)
        {
            BattleVfx.SpawnArrowProjectile(
                transform.position + Vector3.up * 0.7f,
                _currentTarget.transform.position + Vector3.up * 0.7f,
                isMagic: true);
        }
    }

    // damage formula: ATK * (100 / (100 + DEF * 1.8))
    float CalcDamage(float atk, float def)
        => atk * (100f / (100f + def * 1.8f));

    public void TakeDamage(float amount, DamageType type = DamageType.Basic, ChampionUnit attacker = null)
    {
        if (IsDead) return;

        if (attacker != null) LastAttacker = attacker;
        CurrentHp -= amount;
        BattleManager.Instance.SpawnDamageText(transform.position, amount, type);

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
        if (_infoUiRoot != null) _infoUiRoot.SetActive(false);
        if (_footRing != null) _footRing.SetActive(false);
        PlayAnim(PlayerState.DEATH);

        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.18f, 0.12f);

        BattleManager.Instance.OnUnitDied(LastAttacker, this);
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
        if (_infoUiRoot != null) Destroy(_infoUiRoot);
        if (_footRing != null) Destroy(_footRing);
    }

    void UpdateFootRing()
    {
        if (_footRing == null || _footRingSr == null) return;
        _footRing.transform.position = transform.position + Vector3.up * 0.05f;

        // 색상: 팀 색 (낮은 알파) / 우선타겟이면 진한 빨강 + 펄스
        bool isPriorityTarget = isAllyPriorityTarget();
        if (isPriorityTarget)
        {
            float pulse = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
            _footRingSr.color = Color.Lerp(new Color(1f, 0.2f, 0.2f, 0.7f), new Color(1f, 0.6f, 0.3f, 0.9f), pulse);
        }
        else
        {
            _footRingSr.color = TeamId == 0
                ? new Color(0.35f, 0.6f, 1f, 0.5f)
                : new Color(1f, 0.4f, 0.4f, 0.5f);
        }
    }

    bool isAllyPriorityTarget()
    {
        // 자신이 적팀이고, 우리팀 챔프 중 자기를 우선타겟으로 지정한 게 있는지 (BattleManager 의 playerFocusTarget)
        if (TeamId == 0) return false;
        if (BattleManager.Instance == null) return false;
        // BattleManager 내 _team0 의 PriorityTarget == this 면 우선타겟
        foreach (var u in BattleManager.Instance.GetAllies(0))
        {
            if (u != null && u.PriorityTarget == this) return true;
        }
        return false;
    }

    /// <summary>
    /// 매 프레임 다른 챔프와 너무 가까우면 부드럽게 분리 (Steering Separation).
    /// 캐릭터들이 한 점에 겹치지 않고 자연스럽게 거리 유지.
    /// </summary>
    void LateUpdate()
    {
        if (IsDead || Data == null) return;

        // 발밑 외곽선 위치/색 갱신
        UpdateFootRing();

        if (BattleManager.Instance == null || !BattleManager.Instance.IsBattleRunning) return;

        const float minDist = 0.7f;        // 이 거리 이하면 분리 적용
        const float weight = 3.0f;          // 분리 속도 (이속 단위)

        Vector2 separation = Vector2.zero;
        int neighbors = 0;

        foreach (var ally in BattleManager.Instance.GetAllies(TeamId))
        {
            if (ally == null || ally == this || ally.IsDead) continue;
            Vector2 diff = (Vector2)transform.position - (Vector2)ally.transform.position;
            float d = diff.magnitude;
            if (d < minDist && d > 0.001f)
            {
                separation += diff.normalized * (minDist - d);
                neighbors++;
            }
        }
        foreach (var enemy in BattleManager.Instance.GetEnemies(TeamId))
        {
            if (enemy == null || enemy.IsDead) continue;
            Vector2 diff = (Vector2)transform.position - (Vector2)enemy.transform.position;
            float d = diff.magnitude;
            if (d < minDist && d > 0.001f)
            {
                separation += diff.normalized * (minDist - d);
                neighbors++;
            }
        }

        if (neighbors == 0) return;
        // position 직접 살짝 보정 (velocity 안 건드림 — 행동 로직과 독립)
        transform.position += (Vector3)(separation * Time.deltaTime * weight);
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

    void CreateInfoUI()
    {
        if (_infoUiRoot != null) return;

        // 별도 root (부모-자식 X → flipX 영향 안 받음)
        _infoUiRoot = new GameObject($"ChampInfo_{name}");
        _infoUiRoot.transform.localScale = Vector3.one * 0.01f;

        var canvas = _infoUiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 200;

        Color allyColor = new Color(0.4f, 0.7f, 1f);
        Color enemyColor = new Color(1f, 0.4f, 0.4f);
        Color teamColor = TeamId == 0 ? allyColor : enemyColor;

        // ============== HP 바 ==============
        var hpBg = MakeUIImage("HpBg", _infoUiRoot.transform, new Vector2(110, 12),
                               new Vector3(0, 24, 0), new Color(0, 0, 0, 0.85f));
        // 잔여 빨강 (delayed lerp) — fill 아래 깔림
        _hpDelayedFill = MakeFilledImage("HpDelayed", _infoUiRoot.transform, new Vector2(104, 8),
                                         new Vector3(0, 24, 0),
                                         new Color(1f, 0.25f, 0.25f, 0.95f));
        _hpFill = MakeFilledImage("HpFill", _infoUiRoot.transform, new Vector2(104, 8),
                                  new Vector3(0, 24, 0),
                                  TeamId == 0 ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.4f, 0.4f));

        // ============== 기본 스킬 CD 바 ==============
        var cdBg = MakeUIImage("BasicCdBg", _infoUiRoot.transform, new Vector2(110, 8),
                               new Vector3(0, 12, 0), new Color(0, 0, 0, 0.85f));
        _basicCdFill = MakeFilledImage("BasicCdFill", _infoUiRoot.transform, new Vector2(104, 5),
                                       new Vector3(0, 12, 0),
                                       new Color(0.35f, 0.7f, 1f));

        // ============== 필살기 아이콘 + 이름 ==============
        // 필살기 박스 (왼쪽). 실제 아이콘 있으면 sprite 로, 없으면 회색 박스.
        _ultSlotImage = MakeUIImage("UltSlot", _infoUiRoot.transform, new Vector2(20, 20),
                                    new Vector3(-46, -4, 0), Color.white);
        if (Data.UltimateIcon != null) _ultSlotImage.sprite = Data.UltimateIcon;
        else _ultSlotImage.color = new Color(0.3f, 0.3f, 0.3f, 0.95f);

        // 어두운 오버레이 (CD 안 찬 만큼 가림 — 다 차면 사라짐)
        _ultSlotFill = MakeFilledImage("UltCdOverlay", _infoUiRoot.transform, new Vector2(20, 20),
                                       new Vector3(-46, -4, 0),
                                       new Color(0, 0, 0, 0.75f));
        _ultSlotFill.fillMethod = Image.FillMethod.Radial360;
        _ultSlotFill.fillOrigin = (int)Image.Origin360.Top;
        _ultSlotFill.fillClockwise = false;

        // 이름 라벨 (오른쪽)
        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(_infoUiRoot.transform, false);
        var nameRt = nameGo.AddComponent<RectTransform>();
        nameRt.sizeDelta = new Vector2(90, 18);
        nameRt.localPosition = new Vector3(8, -4, 0);
        _nameLabel = nameGo.AddComponent<TMPro.TextMeshProUGUI>();
        _nameLabel.text = Data != null ? Data.ChampionName : name;
        _nameLabel.fontSize = 12;
        _nameLabel.color = Color.white;
        _nameLabel.alignment = TMPro.TextAlignmentOptions.Left;
        _nameLabel.enableAutoSizing = false;
        _nameLabel.raycastTarget = false;
    }

    Image MakeUIImage(string n, Transform parent, Vector2 size, Vector3 pos, Color color)
    {
        var go = new GameObject(n);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.localPosition = pos;
        var img = go.AddComponent<Image>();
        img.sprite = WhiteSprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Image MakeFilledImage(string n, Transform parent, Vector2 size, Vector3 pos, Color color)
    {
        var img = MakeUIImage(n, parent, size, pos, color);
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 1f;
        return img;
    }

    void UpdateInfoUI()
    {
        if (_infoUiRoot == null || Data == null) return;
        _infoUiRoot.transform.position = transform.position + Vector3.up * 1.8f;

        if (_hpFill != null)
        {
            float ratio = Mathf.Clamp01(CurrentHp / Data.MaxHp);
            _hpFill.fillAmount = ratio;

            // 잔여 빨강 — fill 보다 클 때만 천천히 따라감 (데미지 받았을 때 잔여 효과)
            if (_hpDelayedFill != null)
            {
                if (_hpDelayedFill.fillAmount > ratio)
                    _hpDelayedFill.fillAmount = Mathf.MoveTowards(_hpDelayedFill.fillAmount, ratio, Time.deltaTime * 0.45f);
                else
                    _hpDelayedFill.fillAmount = ratio;  // 힐 시 즉시
            }
        }

        if (_basicCdFill != null)
        {
            float cdMax = Data.BasicSkillCooldown;
            float cdRemaining = Mathf.Max(0f, _basicCd);
            float ratio = cdMax > 0f ? 1f - (cdRemaining / cdMax) : 1f;
            _basicCdFill.fillAmount = Mathf.Clamp01(ratio);
            // 가득 차면 노란색 깜빡
            if (_basicCd <= 0f)
            {
                float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
                _basicCdFill.color = Color.Lerp(new Color(0.4f, 0.9f, 1f), new Color(1f, 1f, 0.4f), pulse);
            }
            else _basicCdFill.color = new Color(0.35f, 0.7f, 1f);
        }

        if (_ultSlotFill != null)
        {
            float cdMax = Data.UltimateCooldown;
            float cdRemaining = Mathf.Max(0f, _ultCd);
            // 어두운 오버레이가 CD 남은 만큼 채움 → 다 차면 0 으로 사라져서 아이콘 컬러풀
            _ultSlotFill.fillAmount = cdMax > 0f ? cdRemaining / cdMax : 0f;

            // 다 차면 아이콘 가장자리 빛나는 효과
            if (_ultCd <= 0f && _ultSlotImage != null)
            {
                float pulse = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                _ultSlotImage.color = Color.Lerp(Color.white, new Color(1f, 1f, 0.6f), pulse * 0.5f);
            }
            else if (_ultSlotImage != null && Data.UltimateIcon != null)
            {
                _ultSlotImage.color = Color.white;
            }
        }
    }

    // ============== 스킬 발동 라우터 (실제 효과는 ChampionSkills.cs partial 파일) ==============

    void TryCastBasicSkill()
    {
        if (_basicCd > 0f) return;
        if (Data == null) return;

        bool casted = Data.Role switch
        {
            ChampionRole.Tank       => CastShieldBash(),
            ChampionRole.Fighter    => CastWhirlwind(),
            ChampionRole.Marksman   => CastTripleShot(),
            ChampionRole.Mage       => CastFireball(),
            ChampionRole.Healer     => CastHolyHeal(),
            ChampionRole.Disruptor  => CastEarthquake(),
            ChampionRole.Skirmisher => CastLanceCharge(),
            ChampionRole.Duelist    => CastSwiftBlade(),
            ChampionRole.Assassin   => CastBackstab(),
            _ => false
        };
        if (casted) _basicCd = Data.BasicSkillCooldown;
    }

    void TryCastUltimate()
    {
        if (_ultCd > 0f) return;
        if (Data == null) return;

        bool casted = Data.Role switch
        {
            ChampionRole.Tank       => CastUltDefiance(),
            ChampionRole.Fighter    => CastUltFrenzy(),
            ChampionRole.Marksman   => CastUltArrowRain(),
            ChampionRole.Mage       => CastUltAoeExplosion(),
            ChampionRole.Healer     => CastUltSanctuary(),
            ChampionRole.Disruptor  => CastUltCrushingBlow(),
            ChampionRole.Skirmisher => CastUltTrampling(),
            ChampionRole.Duelist    => CastUltFiveStrike(),
            ChampionRole.Assassin   => CastUltShadowDance(),
            _ => false
        };
        if (casted) _ultCd = Data.UltimateCooldown;
    }
}
