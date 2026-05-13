using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    private PlayerState _currentAnim = PlayerState.IDLE;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spum = GetComponentInChildren<SPUM_Prefabs>();

        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
    }

    public void Init(ChampionSO data, int teamId)
    {
        Data = data;
        TeamId = teamId;
        CurrentHp = data.MaxHp;
        IsDead = false;
        _attackTimer = 0f;

        // must run before PlayAnimation to populate StateAnimationPairs
        if (_spum != null) _spum.OverrideControllerInit();

        PlayAnim(PlayerState.IDLE);
    }

    void Update()
    {
        if (IsDead || !BattleManager.Instance.IsBattleRunning) return;

        _attackTimer -= Time.deltaTime;
        _currentTarget = GetTarget();

        if (_currentTarget == null)
        {
            _rb.linearVelocity = Vector2.zero;
            PlayAnim(PlayerState.IDLE);
            return;
        }

        float dist = Vector2.Distance(transform.position, _currentTarget.transform.position);

        if (dist <= Data.AttackRange)
        {
            _rb.linearVelocity = Vector2.zero;
            TryAttack();
        }
        else
        {
            MoveToward(_currentTarget.transform.position);
        }
    }

    ChampionUnit GetTarget()
    {
        var enemies = BattleManager.Instance.GetEnemies(TeamId);
        if (enemies == null || enemies.Count == 0) return null;

        if (PriorityTarget != null && !PriorityTarget.IsDead)
            return PriorityTarget;

        ChampionUnit closest = null;
        float minDist = float.MaxValue;

        foreach (var e in enemies)
        {
            if (e.IsDead) continue;
            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d < minDist) { minDist = d; closest = e; }
        }

        return closest;
    }

    void MoveToward(Vector3 targetPos)
    {
        Vector2 dir = ((Vector2)targetPos - (Vector2)transform.position).normalized;
        _rb.linearVelocity = dir * Data.MoveSpeed;

        if (dir.x != 0)
            transform.localScale = new Vector3(dir.x > 0 ? -1 : 1, 1, 1);

        PlayAnim(PlayerState.MOVE);
    }

    void TryAttack()
    {
        if (_attackTimer > 0f)
        {
            PlayAnim(PlayerState.IDLE);
            return;
        }

        _attackTimer = 1f / Data.AttackSpeed;
        PlayAnim(PlayerState.ATTACK);

        float dmg = CalcDamage(Data.AttackDamage, _currentTarget.Data.Defense);
        _currentTarget.TakeDamage(dmg);
    }

    // damage formula: ATK * (100 / (100 + DEF * 1.8))
    float CalcDamage(float atk, float def)
        => atk * (100f / (100f + def * 1.8f));

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        CurrentHp -= amount;
        BattleManager.Instance.SpawnDamageText(transform.position, amount, isHeal: false);

        if (CurrentHp <= 0f)
            Die();
        else
            StartCoroutine(HitFlash());
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
        PlayAnim(PlayerState.DEATH);
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
            foreach (var r in renderers)
                r.color = new Color(1, 1, 1, a);
            yield return null;
        }

        gameObject.SetActive(false);
    }

    IEnumerator HitFlash()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var r in renderers) r.color = Color.white;
        PlayAnim(PlayerState.DAMAGED);
        yield return new WaitForSeconds(0.1f);
        foreach (var r in renderers) r.color = Color.white;
    }

    void PlayAnim(PlayerState state)
    {
        if (_currentAnim == state || _spum == null) return;
        _currentAnim = state;
        _spum.PlayAnimation(state, 0);
    }

    void OnDrawGizmosSelected()
    {
        if (Data == null) return;
        Gizmos.color = TeamId == 0 ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, Data.AttackRange);
    }
}