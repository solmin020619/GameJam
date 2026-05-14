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
    private float _burstLockTimer;     // > 0 이면 텔레포트 후 위치 고정 + 평타만 (target switch 방지)

    // 타게팅 stickiness — HP 변동으로 target 자주 바꿔서 와리가리 하는 거 방지
    private ChampionUnit _lastTarget;
    private float _targetSwitchCooldown;

    // 킬로그용 — 마지막으로 데미지 입힌 attacker 추적
    public ChampionUnit LastAttacker { get; private set; }
    public bool IsBackAttacking => _backAttackTimer > 0f;
    public void ApplyBackAttack(float duration) { _backAttackTimer = Mathf.Max(_backAttackTimer, duration); }
    public void ApplyBurstLock(float duration) { _burstLockTimer = Mathf.Max(_burstLockTimer, duration); }

    // 버프 (남은 시간 + 배수)
    private float _atkSpeedBuffEnd;  private float _atkSpeedBuffPct;
    private float _moveSpeedBuffEnd; private float _moveSpeedBuffPct;
    private float _defenseBuffEnd;   private float _defenseBuffPct;

    public bool IsStunned => _stunTimer > 0f;
    public bool IsRooted => _rootTimer > 0f;

    public void ApplyStun(float duration) { _stunTimer = Mathf.Max(_stunTimer, duration); }
    public void ApplyRoot(float duration) { _rootTimer = Mathf.Max(_rootTimer, duration); }
    public void ApplyAttackSpeedBuff(float percent, float duration)
    {
        float newEnd = Time.time + duration;
        if (_atkSpeedBuffEnd > Time.time) { _atkSpeedBuffPct = Mathf.Max(_atkSpeedBuffPct, percent); _atkSpeedBuffEnd = Mathf.Max(_atkSpeedBuffEnd, newEnd); }
        else { _atkSpeedBuffPct = percent; _atkSpeedBuffEnd = newEnd; }
    }
    public void ApplyMoveSpeedBuff(float percent, float duration)
    {
        float newEnd = Time.time + duration;
        if (_moveSpeedBuffEnd > Time.time) { _moveSpeedBuffPct = Mathf.Max(_moveSpeedBuffPct, percent); _moveSpeedBuffEnd = Mathf.Max(_moveSpeedBuffEnd, newEnd); }
        else { _moveSpeedBuffPct = percent; _moveSpeedBuffEnd = newEnd; }
    }
    public void ApplyDefenseBuff(float percent, float duration)
    {
        float newEnd = Time.time + duration;
        if (_defenseBuffEnd > Time.time) { _defenseBuffPct = Mathf.Max(_defenseBuffPct, percent); _defenseBuffEnd = Mathf.Max(_defenseBuffEnd, newEnd); }
        else { _defenseBuffPct = percent; _defenseBuffEnd = newEnd; }
    }

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

        if (_spum != null)
        {
            _spum.OverrideControllerInit();
            // ★ DAMAGED 애니메이션 root motion 이 캐릭터를 뒤로 밀어내는 거 방지
            // (velocity 기반 이동이라 root motion 은 안 씀)
            if (_spum._anim != null) _spum._anim.applyRootMotion = false;
        }
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
        if (IsDead) return;
        if (BattleManager.Instance == null || !BattleManager.Instance.IsBattleRunning) return;

        // 매 프레임 velocity reset → 단체 미끄러짐 방지
        _rb.linearVelocity = Vector2.zero;

        _attackTimer -= Time.deltaTime;
        _healTimer -= Time.deltaTime;
        _basicCd -= Time.deltaTime;
        _ultCd -= Time.deltaTime;
        _stunTimer -= Time.deltaTime;
        _rootTimer -= Time.deltaTime;
        _backAttackTimer -= Time.deltaTime;
        _burstLockTimer -= Time.deltaTime;
        _targetSwitchCooldown -= Time.deltaTime;

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
            case ChampionRole.Assassin:
                BehaveAssassin();
                break;
            default:
                BehaveMelee();
                break;
        }
    }

    /// <summary>
    /// 닌자 — 단순화 버전. 다른 melee 와 동일하게 추격 + 평타.
    /// Backstab (텔레포트) 은 Update 의 TryCastBasicSkill 이 쿨 차면 자동 발동.
    /// 백라인 우선 타게팅은 GetTarget 에서 처리.
    /// </summary>
    void BehaveAssassin()
    {
        BehaveMelee();
    }

    // ========== 행동 패턴 ==========

    /// <summary>탱커/전사: 타겟에게 직진 → 사거리 내면 공격. Hysteresis 적용으로 떨림 방지</summary>
    bool _inMeleeRange;
    void BehaveMelee()
    {
        float dist = Vector2.Distance(transform.position, _currentTarget.transform.position);
        // Hysteresis: 들어올 땐 0.85x, 나갈 땐 1.15x — 경계 위 핑퐁 방지
        float enterRange = Data.AttackRange * 0.85f;
        float exitRange  = Data.AttackRange * 1.15f;
        if (_inMeleeRange && dist > exitRange) _inMeleeRange = false;
        else if (!_inMeleeRange && dist < enterRange) _inMeleeRange = true;

        if (_inMeleeRange)
        {
            _rb.linearVelocity = Vector2.zero;
            FaceTarget(_currentTarget.transform.position);
            TryAttack();
        }
        else MoveToward(_currentTarget.transform.position);
    }

    /// <summary>궁수/마법사: 카이팅 (적이 너무 가까우면 뒤로, 사거리 끝에서 공격)</summary>
    // 카이팅 stuck 감지용 — 구석 몰리면 위치가 안 변하니까 멈춰서 공격으로 전환
    Vector3 _lastKitePos;
    float _kiteStuckTimer;
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

            // ★ stuck 감지 — 위치 변화량 누적해서 일정 시간 안 움직이면 카이팅 중지 (구석 몰림 fix)
            float moved = Vector3.Distance(transform.position, _lastKitePos);
            _lastKitePos = transform.position;
            // 매 프레임 기대 이동량의 30% 미만이면 stuck 으로 간주
            float expectedMove = GetEffectiveMoveSpeed() * Time.deltaTime * 0.3f;
            if (moved < expectedMove) _kiteStuckTimer += Time.deltaTime;
            else _kiteStuckTimer = 0f;

            // 0.4s 이상 stuck → 카이팅 강제 종료. 사거리 안이면 그 자리에서 공격
            if (_kiteStuckTimer > 0.4f)
            {
                _rb.linearVelocity = Vector2.zero;
                FaceTarget(_currentTarget.transform.position);
                if (Vector2.Distance(transform.position, _currentTarget.transform.position) <= Data.AttackRange)
                    TryAttack();
                else PlayAnim(PlayerState.IDLE);

                // 1s 누적되면 _isKiting 강제 false → 다음 프레임 normal 분기로 재평가
                // (적이 멀어졌거나 공간 생겼을 때 다시 움직일 수 있게)
                if (_kiteStuckTimer > 1.0f)
                {
                    _isKiting = false;
                    _kiteStuckTimer = 0f;
                }
                return;
            }

            Vector2 away = ((Vector2)transform.position - (Vector2)nearest.transform.position).normalized;
            _rb.linearVelocity = away * GetEffectiveMoveSpeed();
            FaceTarget(_currentTarget.transform.position);
            PlayAnim(PlayerState.MOVE);
            // 후퇴 중에도 사거리 안이면 공격
            if (Vector2.Distance(transform.position, _currentTarget.transform.position) <= Data.AttackRange)
                TryAttackNoAnim();
            return;
        }

        // 카이팅 안 하는 상태 — 사거리 안이면 공격, 밖이면 chase
        float distToTarget = Vector2.Distance(transform.position, _currentTarget.transform.position);
        if (distToTarget <= Data.AttackRange)
        {
            // 사거리 안 — 멈추고 공격. stuck 카운터 reset
            _kiteStuckTimer = 0f;
            _lastKitePos = transform.position;
            _rb.linearVelocity = Vector2.zero;
            TryAttack();
        }
        else
        {
            // chase — 사거리 밖이면 접근. 항상 movetoward (stuck IDLE 제거 — 마법사 가만히 있는 버그 fix)
            // 카이팅 stuck (구석 몰림) 만 별도 처리, chase 는 무조건 추격
            _kiteStuckTimer = 0f;
            _lastKitePos = transform.position;
            MoveToward(_currentTarget.transform.position);
        }
    }

    /// <summary>힐러: 백라인 위치 유지 (아군 평균 위치보다 뒤) + 평타는 가장 가까운 적</summary>
    bool _healerMoving;             // hysteresis 상태
    Vector2 _healerBacklineCache;   // backline 위치 캐시 (0.5s 마다 갱신)
    float _healerBacklineRefreshTimer;

    void BehaveHealer()
    {
        var allies = BattleManager.Instance.GetAllies(TeamId).Where(a => a != this && !a.IsDead).ToList();

        // backline 0.5s 마다만 재계산 — 매 프레임 새 위치로 갱신하면 healer 가 끊임없이 micro-adjust (떨림)
        _healerBacklineRefreshTimer -= Time.deltaTime;
        if (_healerBacklineRefreshTimer <= 0f)
        {
            _healerBacklineCache = ComputeBacklinePosition(allies);
            _healerBacklineRefreshTimer = 0.5f;
        }
        Vector2 backlinePos = _healerBacklineCache;

        float distToBackline = Vector2.Distance(transform.position, backlinePos);
        // Hysteresis: 1.0 이상 떨어지면 이동 시작, 0.4 이하 도착하면 정지 (0.5 단일 임계값 → 떨림 났음)
        if (_healerMoving && distToBackline < 0.4f) _healerMoving = false;
        else if (!_healerMoving && distToBackline > 1.0f) _healerMoving = true;

        if (_healerMoving)
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

        // 2) 역할별 — HP 기반 타게팅은 stickiness 적용 (자주 바꾸지 않음)
        ChampionUnit candidate;
        switch (Data.Role)
        {
            case ChampionRole.Marksman:
                candidate = alive.OrderBy(e => e.CurrentHp / e.Data.MaxHp).First();
                return StickyTarget(candidate);

            case ChampionRole.Mage:
                var inRange = alive.Where(e => Vector2.Distance(transform.position, e.transform.position) <= Data.AttackRange).ToList();
                if (inRange.Count > 0)
                    candidate = inRange.OrderBy(e => e.CurrentHp / e.Data.MaxHp).First();
                else
                    candidate = GetNearestFrom(alive);
                return StickyTarget(candidate);

            case ChampionRole.Assassin:
                var squishy = alive.Where(e =>
                    e.Data.Role == ChampionRole.Marksman ||
                    e.Data.Role == ChampionRole.Mage ||
                    e.Data.Role == ChampionRole.Healer).ToList();
                if (squishy.Count > 0)
                    candidate = squishy.OrderBy(e => e.CurrentHp / e.Data.MaxHp).First();
                else
                    candidate = GetNearestFrom(alive);
                return StickyTarget(candidate);

            case ChampionRole.Tank:
            case ChampionRole.Fighter:
            case ChampionRole.Healer:
            default:
                // 가장 가까운 적 — 거리 기반은 자연스럽게 안정적이라 stickiness 불필요
                return GetNearestFrom(alive);
        }
    }

    /// <summary>
    /// 타겟 stickiness — 마지막 타겟이 살아있으면 가급적 유지.
    /// HP 가 크게 다르거나 (15% 이상) 쿨다운 지나면 새 타겟으로 전환.
    /// </summary>
    ChampionUnit StickyTarget(ChampionUnit candidate)
    {
        if (candidate == null) return null;

        // 마지막 타겟이 살아있고, switch 쿨다운 안 끝났으면 → 유지
        if (_lastTarget != null && !_lastTarget.IsDead && _targetSwitchCooldown > 0f)
        {
            return _lastTarget;
        }

        // 마지막 타겟 살아있으면, candidate 의 HP 가 의미있게 낮은 경우만 switch
        if (_lastTarget != null && !_lastTarget.IsDead && _lastTarget != candidate)
        {
            float lastHpPct = _lastTarget.CurrentHp / _lastTarget.Data.MaxHp;
            float newHpPct  = candidate.CurrentHp / candidate.Data.MaxHp;
            // candidate HP 가 마지막 타겟의 85% 이하면 switch (의미있는 차이)
            if (newHpPct > lastHpPct * 0.85f) return _lastTarget;
        }

        // switch 결정
        if (_lastTarget != candidate)
        {
            _lastTarget = candidate;
            _targetSwitchCooldown = 1.0f;  // 1초간 lock — 와리가리 방지
        }
        return candidate;
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
        // Hysteresis flip — 데드존 0.05 (이전 0.3 → 너무 커서 반대방향 공격 증상 났음)
        // separation 영향으로 미세하게 흔들리는 건 hysteresis 가 막아주고, 진짜로 적이 반대편에 있으면 즉시 flip
        const float flipThreshold = 0.05f;
        bool facingRight = transform.localScale.x < 0f;
        if (facingRight && dir.x < -flipThreshold)
            transform.localScale = new Vector3(1f, 1f, 1f);
        else if (!facingRight && dir.x > flipThreshold)
            transform.localScale = new Vector3(-1f, 1f, 1f);
        // 그 외엔 유지 (데드존)
    }

    void TryAttack()
    {
        if (_attackTimer > 0f)
        {
            // 쿨다운 중엔 애니메이션 강제로 바꾸지 않음 — ATTACK 클립이 자연스레 끝나게 둠
            // (이전엔 IDLE 로 강제했지만 다음 프레임에 즉시 끊겨서 떨림 발생)
            return;
        }
        FaceTarget(_currentTarget.transform.position);
        _attackTimer = 1f / GetEffectiveAttackSpeed();
        PlayAnim(PlayerState.ATTACK);

        SpawnRangedVfx();
        PlaySfx(Data.AutoAttackSfx);  // 평타 사운드

        float atkMul = IsBackAttacking ? 1.3f : 1f;   // 닌자 배후 상태 평타 +30%
        float dmg = CalcDamage(Data.AttackDamage * atkMul, _currentTarget.GetEffectiveDefense());
        _currentTarget.TakeDamage(dmg, DamageType.Basic, this);
    }

    // 사운드 헬퍼 — 평타/기본스킬/궁극기 공통
    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        var pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(clip, pos, Mathf.Clamp01(VolumeSettings.SfxVolume * volumeScale));
    }

    /// <summary>카이팅 중 평타 (애니 안 바꿈, 데미지만)</summary>
    void TryAttackNoAnim()
    {
        if (_attackTimer > 0f) return;
        _attackTimer = 1f / GetEffectiveAttackSpeed();
        SpawnRangedVfx();
        PlaySfx(Data.AutoAttackSfx);  // 평타 사운드
        float atkMul = IsBackAttacking ? 1.3f : 1f;
        float dmg = CalcDamage(Data.AttackDamage * atkMul, _currentTarget.GetEffectiveDefense());
        _currentTarget.TakeDamage(dmg, DamageType.Basic, this);
    }

    /// <summary>
    /// 텔레포트 스킬용 — Rigidbody2D 와 transform 동시에 옮기고 Physics world 강제 동기화.
    /// (안 그러면 FixedUpdate 가 stale physics 상태로 ninja 원위치로 snap-back 시키는 버그 발생)
    /// </summary>
    public void TeleportTo(Vector3 pos)
    {
        transform.position = pos;
        if (_rb != null)
        {
            _rb.position = pos;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }
        // 즉시 physics world 동기화 — Update 와 FixedUpdate 간 lag 제거
        Physics2D.SyncTransforms();
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
            BattleVfx.SpawnMagicOrb(
                transform.position + Vector3.up * 0.7f,
                _currentTarget.transform.position + Vector3.up * 0.7f);
        }
    }

    // damage formula: ATK * (100 / (100 + DEF * 1.8))
    float CalcDamage(float atk, float def)
        => atk * (100f / (100f + def * 1.8f));

    static float _lastHurtSoundTime;
    static AudioClip _hurtClip;
    static bool _hurtClipTried;

    Coroutine _hitFlashRoutine;
    Color[] _flashOriginals;  // 처음 한 번만 캡쳐 — 동시 호출 시 빨간색 캡쳐 방지
    SpriteRenderer[] _flashRenderers;

    public void TakeDamage(float amount, DamageType type = DamageType.Basic, ChampionUnit attacker = null)
    {
        if (IsDead) return;

        if (attacker != null) LastAttacker = attacker;
        CurrentHp -= amount;
        BattleManager.Instance.SpawnDamageText(transform.position, amount, type);

        // 피격 사운드 — 전역 쿨다운 0.4s + 볼륨 0.3 (다른 사운드 비해 작게)
        if (Time.unscaledTime - _lastHurtSoundTime > 0.4f)
        {
            if (!_hurtClipTried) { _hurtClipTried = true; _hurtClip = Resources.Load<AudioClip>("hurt_sound"); }
            if (_hurtClip != null)
            {
                _lastHurtSoundTime = Time.unscaledTime;
                var camPos = Camera.main != null ? Camera.main.transform.position : transform.position;
                AudioSource.PlayClipAtPoint(_hurtClip, camPos, Mathf.Clamp01(VolumeSettings.SfxVolume * 0.3f));
            }
        }

        if (Data != null && amount >= Data.MaxHp * 0.2f && CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.08f, 0.05f);

        if (CurrentHp <= 0f) Die();
        else
        {
            // 기존 HitFlash 진행 중이면 중단하고 즉시 색 복구 → 새로 시작
            // (동시 호출 시 originals 가 빨간색으로 캡쳐되어 영구 빨강 고정되는 버그 방지)
            if (_hitFlashRoutine != null)
            {
                StopCoroutine(_hitFlashRoutine);
                RestoreFlashColors();
            }
            _hitFlashRoutine = StartCoroutine(HitFlash());
        }
    }

    void RestoreFlashColors()
    {
        if (_flashRenderers == null || _flashOriginals == null) return;
        for (int i = 0; i < _flashRenderers.Length; i++)
            if (_flashRenderers[i] != null) _flashRenderers[i].color = _flashOriginals[i];
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHp = Mathf.Min(Data.MaxHp, CurrentHp + amount);
        BattleManager.Instance.SpawnDamageText(transform.position, amount, isHeal: true);
        // 힐 받은 대상 발 밑에 oval aura 1초
        BattleVfx.SpawnHealAura(transform, duration: 1.0f);
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
        // renderers / originals 는 인스턴스 변수 — 첫 호출에만 캡쳐 (이후 호출은 정상색 캐시 유지)
        if (_flashRenderers == null)
        {
            _flashRenderers = GetComponentsInChildren<SpriteRenderer>();
            _flashOriginals = new Color[_flashRenderers.Length];
            for (int i = 0; i < _flashRenderers.Length; i++) _flashOriginals[i] = _flashRenderers[i].color;
        }

        // 빨강 깜빡
        var flash = new Color(1f, 0.4f, 0.4f, 1f);
        foreach (var r in _flashRenderers) if (r != null) r.color = flash;
        PlayAnim(PlayerState.DAMAGED);
        yield return new WaitForSeconds(0.08f);

        // 원래 색 복구
        RestoreFlashColors();
        _hitFlashRoutine = null;

        // _currentAnim 을 IDLE 로 리셋 — SPUM Animator 가 DAMAGED 클립 끝나면 자연스레 IDLE 로 돌아가므로 동기 맞음
        // 다음 PlayAnim(ATTACK) 호출이 제대로 트리거되도록 함 (DAMAGED 로 stuck 방지)
        _currentAnim = PlayerState.IDLE;
    }

    void PlayAnim(PlayerState state)
    {
        if (_spum == null) return;
        // ATTACK/DAMAGED/OTHER 는 SPUM 의 Trigger 기반 one-shot 이므로 매번 재발동
        // (캐싱하면 같은 state 호출 시 SetTrigger 가 안 불려서 두 번째 공격부턴 애니 안 나옴)
        bool isOneShot = state == PlayerState.ATTACK
                      || state == PlayerState.DAMAGED
                      || state == PlayerState.OTHER;
        if (!isOneShot && _currentAnim == state) return;
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

        const float minDist = 0.55f;
        const float weight = 0.6f;          // 1.5 → 0.6: 떨림/측면 슬라이드 줄임. velocity 와 충돌 약화

        Vector2 separation = Vector2.zero;
        int neighbors = 0;

        // 헬퍼 — 정확히 같은 위치 (d < 0.001) 일 때 random direction 으로 분리해서 영구 stuck 방지
        Vector2 SafeDir(Vector2 diff, float d, ChampionUnit other)
        {
            if (d > 0.001f) return diff / d; // = normalized
            // 같은 위치 — InstanceID 차이로 결정적 random direction (각 페어가 일관되게 같은 방향)
            int seed = GetInstanceID() ^ (other != null ? other.GetInstanceID() : 0);
            float angle = (seed * 0.001f) % (2f * Mathf.PI);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        foreach (var ally in BattleManager.Instance.GetAllies(TeamId))
        {
            if (ally == null || ally == this || ally.IsDead) continue;
            Vector2 diff = (Vector2)transform.position - (Vector2)ally.transform.position;
            float d = diff.magnitude;
            if (d < minDist)
            {
                separation += SafeDir(diff, d, ally) * (minDist - d);
                neighbors++;
            }
        }
        foreach (var enemy in BattleManager.Instance.GetEnemies(TeamId))
        {
            if (enemy == null || enemy.IsDead) continue;
            Vector2 diff = (Vector2)transform.position - (Vector2)enemy.transform.position;
            float d = diff.magnitude;
            if (d < minDist)
            {
                separation += SafeDir(diff, d, enemy) * (minDist - d);
                neighbors++;
            }
        }

        if (neighbors == 0) return;

        // position + Rigidbody 동시 보정 — Dynamic Rigidbody2D 는 _rb.position 으로 다음 FixedUpdate 동기
        // (transform.position 만 변경하면 stale position 으로 rollback 되어 "제자리 점프" 발생)
        Vector3 delta = (Vector3)(separation * Time.deltaTime * weight);
        transform.position += delta;
        if (_rb != null) _rb.position = transform.position;
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
        _infoUiRoot.transform.localScale = Vector3.one * 0.013f; // TFM 참고 — 더 잘 보이게 1.3배

        var canvas = _infoUiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 200;
        // 픽셀 아트 필터 — sharp 픽셀
        var scaler = _infoUiRoot.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100f;
        scaler.referencePixelsPerUnit = 100f;

        // ============ 레이아웃 ============ 사이즈 50% 정도 키움 (TFM 참고 기준)
        const float ultSize = 40f;          // 26 → 40 (궁극기 박스 잘 보이게)
        const float panelW  = 130f;         // 96 → 130
        const float gap     = 5f;
        const float barH    = 11f;          // 8 → 11
        const float nameH   = 14f;          // 10 → 14

        // 전체 가로폭 중앙정렬 → 캐릭터 정중앙 아래 위치
        float totalHalf = (ultSize + gap + panelW) * 0.5f;
        float ultX   = -totalHalf + ultSize * 0.5f;
        float panelX =  totalHalf - panelW   * 0.5f;

        // 세로 배치 (위 → 이름, 중간 → HP, 아래 → CD)
        float nameY = (barH + barH + nameH) * 0.5f - nameH * 0.5f;
        float hpY   = nameY - nameH * 0.5f - 1f - barH * 0.5f;
        float cdY   = hpY   - barH        - 1f;

        // ============ 궁극기 박스 (왼쪽) ============
        // 짙은 배경 (테두리 느낌)
        MakeUIImage("UltFrame", _infoUiRoot.transform, new Vector2(ultSize + 2, ultSize + 2),
                    new Vector3(ultX, 0, 0), new Color(0, 0, 0, 0.9f));
        _ultSlotImage = MakeUIImage("UltSlot", _infoUiRoot.transform, new Vector2(ultSize, ultSize),
                                    new Vector3(ultX, 0, 0), Color.white);
        if (Data.UltimateIcon != null) _ultSlotImage.sprite = Data.UltimateIcon;
        else _ultSlotImage.color = new Color(0.3f, 0.4f, 0.55f, 1f);

        // CD 오버레이 (radial 채워짐)
        _ultSlotFill = MakeFilledImage("UltCdOverlay", _infoUiRoot.transform, new Vector2(ultSize, ultSize),
                                       new Vector3(ultX, 0, 0),
                                       new Color(0, 0, 0, 0.75f));
        _ultSlotFill.fillMethod = Image.FillMethod.Radial360;
        _ultSlotFill.fillOrigin = (int)Image.Origin360.Top;
        _ultSlotFill.fillClockwise = false;

        // ============ 이름 라벨 (오른쪽 상단) ============
        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(_infoUiRoot.transform, false);
        var nameRt = nameGo.AddComponent<RectTransform>();
        nameRt.sizeDelta = new Vector2(panelW, nameH + 4);
        nameRt.localPosition = new Vector3(panelX, nameY, 0);
        _nameLabel = nameGo.AddComponent<TMPro.TextMeshProUGUI>();
        // 이름 — 인덱스 떼고 (예: "수호기사 1" → "수호기사")
        string nm = Data != null ? Data.ChampionName : name;
        int sp = nm.LastIndexOf(' ');
        if (sp > 0 && int.TryParse(nm.Substring(sp + 1), out _)) nm = nm.Substring(0, sp);
        _nameLabel.text = nm;
        _nameLabel.fontSize = 14;
        _nameLabel.fontStyle = TMPro.FontStyles.Bold;
        _nameLabel.color = Color.white;
        _nameLabel.alignment = TMPro.TextAlignmentOptions.Left;
        _nameLabel.enableAutoSizing = false;
        _nameLabel.raycastTarget = false;
        _nameLabel.enableWordWrapping = false;
        _nameLabel.overflowMode = TMPro.TextOverflowModes.Overflow;

        // ============ HP 바 (오른쪽 중단) ============
        MakeUIImage("HpBg", _infoUiRoot.transform, new Vector2(panelW + 2, barH + 2),
                    new Vector3(panelX, hpY, 0), new Color(0, 0, 0, 0.9f));
        _hpDelayedFill = MakeFilledImage("HpDelayed", _infoUiRoot.transform, new Vector2(panelW, barH),
                                         new Vector3(panelX, hpY, 0),
                                         new Color(1f, 0.25f, 0.25f, 0.95f));
        _hpFill = MakeFilledImage("HpFill", _infoUiRoot.transform, new Vector2(panelW, barH),
                                  new Vector3(panelX, hpY, 0),
                                  TeamId == 0 ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.4f, 0.4f));

        // ============ 기본 스킬 CD 바 (오른쪽 하단) ============
        MakeUIImage("BasicCdBg", _infoUiRoot.transform, new Vector2(panelW + 2, barH + 2),
                    new Vector3(panelX, cdY, 0), new Color(0, 0, 0, 0.9f));
        _basicCdFill = MakeFilledImage("BasicCdFill", _infoUiRoot.transform, new Vector2(panelW, barH),
                                       new Vector3(panelX, cdY, 0),
                                       new Color(0.35f, 0.7f, 1f));
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
        // 캐릭터 아래로 (이전: up * 1.8) — TFM 참고 레퍼런스 스타일
        _infoUiRoot.transform.position = transform.position + Vector3.down * 0.75f;

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
        if (casted)
        {
            _basicCd = Data.BasicSkillCooldown;
            PlaySfx(Data.BasicSkillSfx);  // 기본 스킬 사운드
            SpawnBasicSkillVfx();         // 기본 스킬 VFX
        }
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
        if (casted)
        {
            _ultCd = Data.UltimateCooldown;
            PlaySfx(Data.UltimateSfx);  // 궁극기 사운드
            SpawnUltVfx();              // 궁극기 VFX
        }
    }

    // Role 별 기본 스킬 VFX — sprite name + 위치 (타겟 또는 자기)
    void SpawnBasicSkillVfx()
    {
        Vector3 targetPos = _currentTarget != null ? _currentTarget.transform.position : transform.position;
        switch (Data.Role)
        {
            case ChampionRole.Tank:       BattleVfx.SpawnSkillVfx("guardian_skill", targetPos, 0.6f, 1.6f); break;
            case ChampionRole.Fighter:    BattleVfx.SpawnSkillVfx("berserker_skill", transform.position, 0.6f, 1.8f); break;
            case ChampionRole.Marksman:   BattleVfx.SpawnSkillVfx("sniper_skill", targetPos, 0.5f, 1.4f); break;
            case ChampionRole.Mage:       BattleVfx.SpawnSkillVfx("mage_skill", targetPos, 0.7f, 1.7f); break;
            case ChampionRole.Healer:     BattleVfx.SpawnSkillVfx("cleric_skill", targetPos, 0.7f, 1.5f); break;
            case ChampionRole.Disruptor:  BattleVfx.SpawnSkillVfx("crusher_skill", transform.position, 0.7f, 2.0f); break;
            case ChampionRole.Skirmisher: BattleVfx.SpawnSkillVfx("horse_skill", transform.position, 0.5f, 1.5f); break;
            case ChampionRole.Duelist:    BattleVfx.SpawnSkillVfx("swordman_skill", targetPos, 0.4f, 1.5f); break;
            case ChampionRole.Assassin:   BattleVfx.SpawnSkillVfx("ninja_skill", targetPos, 0.5f, 1.4f); break;
        }
    }

    // Role 별 궁극기 VFX
    void SpawnUltVfx()
    {
        Vector3 targetPos = _currentTarget != null ? _currentTarget.transform.position : transform.position;
        switch (Data.Role)
        {
            case ChampionRole.Tank:       BattleVfx.SpawnSkillVfx("guardian_ult", transform.position, 1.2f, 2.0f); break;
            case ChampionRole.Fighter:    BattleVfx.SpawnSkillVfx("berserker_ult", transform.position, 1.5f, 2.0f); break;
            case ChampionRole.Marksman:   BattleVfx.SpawnSkillVfx("sniper_ult", targetPos, 1.2f, 2.5f); break;
            case ChampionRole.Mage:       BattleVfx.SpawnSkillVfx("mage_ult", targetPos, 1.0f, 2.8f); break;
            case ChampionRole.Healer:     BattleVfx.SpawnSkillVfx("cleric_ult", transform.position, 1.5f, 2.5f); break;
            case ChampionRole.Disruptor:  BattleVfx.SpawnSkillVfx("crusher_ult", targetPos, 1.0f, 2.2f); break;
            case ChampionRole.Skirmisher: BattleVfx.SpawnSkillVfx("horse_ult", transform.position, 1.0f, 2.5f); break;
            case ChampionRole.Duelist:    BattleVfx.SpawnSkillVfx("swordman_ult", targetPos, 0.8f, 2.0f); break;
            case ChampionRole.Assassin:   BattleVfx.SpawnSkillVfx("ninja_ult", transform.position, 1.0f, 2.2f); break;
        }
    }
}
