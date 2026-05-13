using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ChampionUnit 의 스킬 효과 구현부. partial class 로 분리.
/// 각 메소드는 발동 성공 시 true, 발동 조건 불충족 시 false 반환.
/// false 면 CD 안 깎임 → 다음 프레임 재시도.
/// </summary>
public partial class ChampionUnit
{
    // ============== 공용 헬퍼 ==============

    List<ChampionUnit> AliveEnemiesInRadius(Vector3 center, float radius)
    {
        var result = new List<ChampionUnit>();
        var enemies = BattleManager.Instance.GetEnemies(TeamId);
        if (enemies == null) return result;
        foreach (var e in enemies)
        {
            if (e == null || e.IsDead) continue;
            if (Vector2.Distance(center, e.transform.position) <= radius) result.Add(e);
        }
        return result;
    }

    List<ChampionUnit> AliveEnemies()
    {
        var enemies = BattleManager.Instance.GetEnemies(TeamId);
        if (enemies == null) return new List<ChampionUnit>();
        return enemies.Where(e => e != null && !e.IsDead).ToList();
    }

    List<ChampionUnit> AliveAllies(bool includeSelf = true)
    {
        var allies = BattleManager.Instance.GetAllies(TeamId);
        if (allies == null) return new List<ChampionUnit>();
        return allies.Where(a => a != null && !a.IsDead && (includeSelf || a != this)).ToList();
    }

    ChampionUnit GetFarthestAliveEnemy()
    {
        ChampionUnit farthest = null;
        float maxDist = 0f;
        foreach (var e in AliveEnemies())
        {
            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d > maxDist) { maxDist = d; farthest = e; }
        }
        return farthest;
    }

    ChampionUnit GetLowestHpAlly(bool includeSelf = true)
    {
        ChampionUnit weakest = null;
        float lowestPct = 1.01f;
        foreach (var a in AliveAllies(includeSelf))
        {
            float pct = a.CurrentHp / a.Data.MaxHp;
            if (pct < lowestPct) { lowestPct = pct; weakest = a; }
        }
        return weakest;
    }

    // ============== 기본 스킬 (7개) ==============

    /// <summary>방패강타 — 가장 가까운 적 1명 120% + 스턴 1s</summary>
    bool CastShieldBash()
    {
        var target = _currentTarget;
        if (target == null || target.IsDead) return false;
        if (Vector2.Distance(transform.position, target.transform.position) > Data.AttackRange + 0.3f) return false;

        FaceTarget(target.transform.position);
        PlayAnim(PlayerState.ATTACK);
        float dmg = CalcDamage(Data.AttackDamage * 1.2f, target.GetEffectiveDefense());
        target.TakeDamage(dmg);
        target.ApplyStun(1f);
        BattleVfx.SpawnRingPulse(target.transform.position, new Color(0.4f, 0.7f, 1f, 0.8f), 0.4f, 0.7f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.1f, 0.08f);
        return true;
    }

    /// <summary>회전베기 — 주변 1.5m 모든 적 150%</summary>
    bool CastWhirlwind()
    {
        var hits = AliveEnemiesInRadius(transform.position, 1.5f);
        if (hits.Count == 0) return false;

        PlayAnim(PlayerState.ATTACK);
        foreach (var e in hits)
        {
            float dmg = CalcDamage(Data.AttackDamage * 1.5f, e.GetEffectiveDefense());
            e.TakeDamage(dmg);
        }
        BattleVfx.SpawnRingPulse(transform.position, new Color(1f, 0.4f, 0.3f, 0.7f), 0.4f, 1.5f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.12f, 0.1f);
        return true;
    }

    /// <summary>3연사 — 타겟에 70% × 3발 (0.2s 간격)</summary>
    bool CastTripleShot()
    {
        var target = _currentTarget;
        if (target == null || target.IsDead) return false;
        if (Vector2.Distance(transform.position, target.transform.position) > Data.AttackRange + 0.3f) return false;

        PlayAnim(PlayerState.ATTACK);
        StartCoroutine(TripleShotRoutine(target));
        return true;
    }

    IEnumerator TripleShotRoutine(ChampionUnit target)
    {
        for (int i = 0; i < 3; i++)
        {
            if (target == null || target.IsDead || IsDead) yield break;
            BattleVfx.SpawnProjectileLine(
                transform.position + Vector3.up * 0.7f,
                target.transform.position + Vector3.up * 0.7f,
                new Color(1f, 1f, 0.5f, 1f), 0.1f);
            float dmg = CalcDamage(Data.AttackDamage * 0.7f, target.GetEffectiveDefense());
            target.TakeDamage(dmg);
            yield return new WaitForSeconds(0.2f);
        }
    }

    /// <summary>마법탄 — 가장 먼 적 1명 180%</summary>
    bool CastFireball()
    {
        var target = GetFarthestAliveEnemy();
        if (target == null) return false;
        if (Vector2.Distance(transform.position, target.transform.position) > Data.AttackRange * 1.5f) return false;

        FaceTarget(target.transform.position);
        PlayAnim(PlayerState.ATTACK);
        BattleVfx.SpawnProjectileLine(
            transform.position + Vector3.up * 0.7f,
            target.transform.position + Vector3.up * 0.7f,
            new Color(1f, 0.4f, 1f, 1f), 0.2f);
        float dmg = CalcDamage(Data.AttackDamage * 1.8f, target.GetEffectiveDefense());
        target.TakeDamage(dmg);
        BattleVfx.SpawnRingPulse(target.transform.position, new Color(1f, 0.4f, 1f, 0.7f), 0.4f, 0.8f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.12f, 0.08f);
        return true;
    }

    /// <summary>신성치유 — HP 최저 아군 1명 ATK×150% 회복</summary>
    bool CastHolyHeal()
    {
        var target = GetLowestHpAlly(includeSelf: true);
        if (target == null) return false;
        float pct = target.CurrentHp / target.Data.MaxHp;
        if (pct >= 0.95f) return false;  // 다 풀피면 시전 안 함

        PlayAnim(PlayerState.OTHER);
        float healAmount = Data.AttackDamage * 1.5f;
        target.Heal(healAmount);
        BattleVfx.SpawnRingPulse(target.transform.position + Vector3.up * 0.5f,
            new Color(0.4f, 1f, 0.5f, 0.9f), 0.5f, 0.9f);
        return true;
    }

    /// <summary>지진강타 — 주변 1.8m 모든 적 130% + 루트 0.8s</summary>
    bool CastEarthquake()
    {
        var hits = AliveEnemiesInRadius(transform.position, 1.8f);
        if (hits.Count == 0) return false;

        PlayAnim(PlayerState.ATTACK);
        foreach (var e in hits)
        {
            float dmg = CalcDamage(Data.AttackDamage * 1.3f, e.GetEffectiveDefense());
            e.TakeDamage(dmg);
            e.ApplyRoot(0.8f);
        }
        BattleVfx.SpawnRingPulse(transform.position, new Color(0.8f, 0.5f, 0.2f, 0.8f), 0.6f, 1.8f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.18f, 0.13f);
        return true;
    }

    /// <summary>쾌검 — 타겟에 순간 달려들어 160% 단일. 거리 무관.</summary>
    bool CastSwiftBlade()
    {
        var target = _currentTarget;
        if (target == null || target.IsDead) return false;

        // 타겟 옆으로 순간 이동
        Vector3 origPos = transform.position;
        Vector3 dir = (target.transform.position - origPos);
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.right;
        Vector3 dest = target.transform.position - dir.normalized * 0.5f;

        BattleVfx.SpawnProjectileLine(origPos, dest, new Color(0.85f, 0.95f, 1f, 1f), 0.15f);
        transform.position = dest;

        FaceTarget(target.transform.position);
        PlayAnim(PlayerState.ATTACK);
        float dmg = CalcDamage(Data.AttackDamage * 1.6f, target.GetEffectiveDefense());
        target.TakeDamage(dmg);

        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.12f, 0.1f);
        return true;
    }

    /// <summary>돌격창 — 전방 3유닛 직선 돌진, 경로 전체 140% 관통</summary>
    bool CastLanceCharge()
    {
        if (_currentTarget == null || _currentTarget.IsDead) return false;

        Vector2 dir = ((Vector2)_currentTarget.transform.position - (Vector2)transform.position).normalized;
        if (dir.sqrMagnitude < 0.01f) return false;
        float maxDist = 3f;
        float width = 0.7f;

        var hits = new List<ChampionUnit>();
        foreach (var e in AliveEnemies())
        {
            Vector2 toEnemy = (Vector2)e.transform.position - (Vector2)transform.position;
            float forward = Vector2.Dot(toEnemy, dir);
            if (forward < 0f || forward > maxDist) continue;
            float perp = Mathf.Abs(toEnemy.x * (-dir.y) + toEnemy.y * dir.x);
            if (perp > width) continue;
            hits.Add(e);
        }
        if (hits.Count == 0) return false;

        FaceDirection(dir);
        PlayAnim(PlayerState.ATTACK);
        foreach (var e in hits)
        {
            float dmg = CalcDamage(Data.AttackDamage * 1.4f, e.GetEffectiveDefense());
            e.TakeDamage(dmg);
        }
        // 시각 라인 + 자신 약간 전진
        BattleVfx.SpawnProjectileLine(
            transform.position,
            transform.position + (Vector3)(dir * maxDist),
            new Color(1f, 0.85f, 0.4f, 1f), 0.25f);
        transform.position += (Vector3)(dir * Mathf.Min(1.2f, maxDist * 0.4f));
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.15f, 0.12f);
        return true;
    }

    // ============== 궁극기 (7개) ==============

    /// <summary>수호의 결의 — 아군 전체 방어 +50% (3s)</summary>
    bool CastUltDefiance()
    {
        var allies = AliveAllies(includeSelf: true);
        if (allies.Count == 0) return false;
        PlayAnim(PlayerState.DEBUFF);
        foreach (var a in allies)
        {
            a.ApplyDefenseBuff(0.5f, 3f);
            BattleVfx.SpawnRingPulse(a.transform.position + Vector3.up * 0.4f,
                new Color(0.4f, 0.7f, 1f, 0.8f), 0.6f, 0.9f);
        }
        return true;
    }

    /// <summary>광폭 — 자신 공속·이속 +50% (5s)</summary>
    bool CastUltFrenzy()
    {
        PlayAnim(PlayerState.OTHER);
        ApplyAttackSpeedBuff(0.5f, 5f);
        ApplyMoveSpeedBuff(0.5f, 5f);
        BattleVfx.SpawnRingPulse(transform.position + Vector3.up * 0.4f,
            new Color(1f, 0.4f, 0.3f, 0.9f), 0.7f, 1.2f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.1f, 0.08f);
        return true;
    }

    /// <summary>화살비 — 0.5s 캐스팅 후 적 전체 130%</summary>
    bool CastUltArrowRain()
    {
        if (AliveEnemies().Count == 0) return false;
        PlayAnim(PlayerState.ATTACK);
        StartCoroutine(ArrowRainRoutine());
        return true;
    }

    IEnumerator ArrowRainRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        foreach (var e in AliveEnemies())
        {
            BattleVfx.SpawnProjectileLine(
                e.transform.position + Vector3.up * 4f,
                e.transform.position,
                new Color(1f, 1f, 0.5f, 1f), 0.2f);
            float dmg = CalcDamage(Data.AttackDamage * 1.3f, e.GetEffectiveDefense());
            e.TakeDamage(dmg);
        }
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.3f, 0.2f);
    }

    /// <summary>광역폭발 — 1s 캐스팅 후 적 전체 250%</summary>
    bool CastUltAoeExplosion()
    {
        if (AliveEnemies().Count == 0) return false;
        PlayAnim(PlayerState.OTHER);
        StartCoroutine(AoeExplosionRoutine());
        return true;
    }

    IEnumerator AoeExplosionRoutine()
    {
        // 캐스팅 동안 적 위에 경고 마커
        var enemies = AliveEnemies();
        foreach (var e in enemies)
            BattleVfx.SpawnRingPulse(e.transform.position,
                new Color(1f, 0.3f, 0.3f, 0.5f), 1f, 0.9f);

        yield return new WaitForSeconds(1f);

        foreach (var e in AliveEnemies())
        {
            float dmg = CalcDamage(Data.AttackDamage * 2.5f, e.GetEffectiveDefense());
            e.TakeDamage(dmg);
            BattleVfx.SpawnRingPulse(e.transform.position,
                new Color(1f, 0.4f, 1f, 0.9f), 0.5f, 1.2f);
        }
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.5f, 0.3f);
    }

    /// <summary>성역 — 주변 2.5m 장막 3초, 안의 아군 매초 MaxHP 5% 틱힐</summary>
    bool CastUltSanctuary()
    {
        PlayAnim(PlayerState.OTHER);
        StartCoroutine(SanctuaryRoutine());
        return true;
    }

    IEnumerator SanctuaryRoutine()
    {
        const float radius = 2.5f;
        const float duration = 3f;
        const float tickInterval = 1f;
        var sanctuaryColor = new Color(0.5f, 1f, 0.7f, 0.5f);

        // 시각 — 성역 표시 (큰 원이 3초간 유지)
        BattleVfx.SpawnRingPulse(transform.position, sanctuaryColor, duration, radius);

        float elapsed = 0f;
        int tickCount = Mathf.RoundToInt(duration / tickInterval);
        for (int i = 0; i < tickCount; i++)
        {
            if (IsDead) yield break;
            // 매초 반경 내 아군 틱힐
            var allies = BattleManager.Instance.GetAllies(TeamId);
            if (allies != null)
            {
                foreach (var a in allies)
                {
                    if (a == null || a.IsDead) continue;
                    if (Vector2.Distance(transform.position, a.transform.position) > radius) continue;
                    float heal = a.Data.MaxHp * 0.05f;
                    a.Heal(heal);
                    BattleVfx.SpawnRingPulse(a.transform.position + Vector3.up * 0.4f,
                        new Color(0.5f, 1f, 0.6f, 0.7f), 0.4f, 0.5f);
                }
            }
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
        }
    }

    /// <summary>분쇄의 일격 — 1s 차징 후 단일 350% + 스턴 2s</summary>
    bool CastUltCrushingBlow()
    {
        var target = _currentTarget;
        if (target == null || target.IsDead) return false;

        PlayAnim(PlayerState.OTHER);
        StartCoroutine(CrushingBlowRoutine(target));
        return true;
    }

    IEnumerator CrushingBlowRoutine(ChampionUnit target)
    {
        // 차징 표시
        BattleVfx.SpawnRingPulse(transform.position,
            new Color(0.9f, 0.5f, 0.2f, 0.8f), 1f, 1.2f);
        yield return new WaitForSeconds(1f);
        if (target == null || target.IsDead || IsDead) yield break;

        PlayAnim(PlayerState.ATTACK);
        float dmg = CalcDamage(Data.AttackDamage * 3.5f, target.GetEffectiveDefense());
        target.TakeDamage(dmg);
        target.ApplyStun(2f);
        BattleVfx.SpawnRingPulse(target.transform.position,
            new Color(1f, 0.6f, 0.3f, 0.95f), 0.6f, 1.3f);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.45f, 0.3f);
    }

    /// <summary>연참 — 타겟 5회 연속 70% (총 350%), 0.15s 간격, 마지막 강한 셰이크</summary>
    bool CastUltFiveStrike()
    {
        var target = _currentTarget;
        if (target == null || target.IsDead) return false;

        PlayAnim(PlayerState.ATTACK);
        StartCoroutine(FiveStrikeRoutine(target));
        return true;
    }

    IEnumerator FiveStrikeRoutine(ChampionUnit target)
    {
        // 타겟 옆으로 순간 이동 (멀면)
        if (Vector2.Distance(transform.position, target.transform.position) > Data.AttackRange + 0.5f)
        {
            Vector3 dir = (target.transform.position - transform.position).normalized;
            transform.position = target.transform.position - dir * 0.5f;
        }

        for (int i = 0; i < 5; i++)
        {
            if (target == null || target.IsDead || IsDead) yield break;
            FaceTarget(target.transform.position);
            BattleVfx.SpawnProjectileLine(
                transform.position + Vector3.up * 0.6f,
                target.transform.position + Vector3.up * 0.6f,
                new Color(0.9f, 0.95f, 1f, 1f), 0.08f);
            float dmg = CalcDamage(Data.AttackDamage * 0.7f, target.GetEffectiveDefense());
            target.TakeDamage(dmg);

            // 마지막 타격은 강한 셰이크
            if (i == 4)
            {
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.4f, 0.25f);
                BattleVfx.SpawnRingPulse(target.transform.position,
                    new Color(0.9f, 0.95f, 1f, 0.9f), 0.5f, 1f);
            }
            yield return new WaitForSeconds(0.15f);
        }
    }

    /// <summary>짓밟기 — 적 전체 순차 돌진, 각 100% + 루트(넉백 대체) 0.5s</summary>
    bool CastUltTrampling()
    {
        if (AliveEnemies().Count == 0) return false;
        StartCoroutine(TramplingRoutine());
        return true;
    }

    IEnumerator TramplingRoutine()
    {
        var enemies = AliveEnemies();
        enemies.Sort((a, b) =>
            Vector2.Distance(transform.position, a.transform.position)
            .CompareTo(Vector2.Distance(transform.position, b.transform.position)));

        foreach (var e in enemies)
        {
            if (e == null || e.IsDead || IsDead) continue;

            Vector3 origPos = transform.position;
            Vector3 dest = e.transform.position - (e.transform.position - origPos).normalized * 0.5f;
            BattleVfx.SpawnProjectileLine(origPos, dest, new Color(1f, 0.85f, 0.4f, 1f), 0.15f);
            transform.position = dest;
            PlayAnim(PlayerState.ATTACK);

            float dmg = CalcDamage(Data.AttackDamage * 1.0f, e.GetEffectiveDefense());
            e.TakeDamage(dmg);
            e.ApplyRoot(0.5f);

            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.08f, 0.07f);
            yield return new WaitForSeconds(0.25f);
        }
    }
}
