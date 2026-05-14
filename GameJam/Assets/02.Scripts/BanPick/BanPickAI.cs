using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BanPickAI : MonoBehaviour
{
    [Header("랜덤성 (예측 못하게 살짝)")]
    [Range(0f, 1f)] public float banWildcardChance = 0.10f;   // 0.15 → 0.10 (더 영리하게)
    [Range(0f, 1f)] public float pickWildcardChance = 0.10f;  // 0.20 → 0.10

    /// <summary>역할별 가중치 (랜덤 fallback 시 사용)</summary>
    [System.Serializable]
    public class RoleWeights
    {
        public float Tank = 1f, Fighter = 1f, Marksman = 1f, Mage = 1f, Healer = 1f;
        public float Disruptor = 1f, Skirmisher = 1f, Duelist = 1f, Assassin = 1f;

        public float Of(ChampionRole r) => r switch
        {
            ChampionRole.Tank => Tank,
            ChampionRole.Fighter => Fighter,
            ChampionRole.Marksman => Marksman,
            ChampionRole.Mage => Mage,
            ChampionRole.Healer => Healer,
            ChampionRole.Disruptor => Disruptor,
            ChampionRole.Skirmisher => Skirmisher,
            ChampionRole.Duelist => Duelist,
            ChampionRole.Assassin => Assassin,
            _ => 1f,
        };
    }

    [Header("밴 가중치 (밸런스 패치 반영)")]
    public RoleWeights banWeights = new RoleWeights {
        Mage = 3f, Healer = 2.5f, Disruptor = 2f, Marksman = 2f,
        Duelist = 1.8f, Assassin = 1.5f,
        Tank = 1f, Fighter = 1f, Skirmisher = 1f,
    };

    [Header("픽 가중치 (밸런스 패치 반영)")]
    public RoleWeights pickWeights = new RoleWeights {
        Tank = 3f, Healer = 2.5f, Marksman = 2.5f,
        Mage = 2.2f, Skirmisher = 2f, Fighter = 2f, Duelist = 2f,
        Disruptor = 1.8f, Assassin = 1.8f,
    };

    /// <summary>역할 카운터 매핑 — A 가 B 를 카운터 (B 의 약점)</summary>
    static readonly Dictionary<ChampionRole, ChampionRole[]> Counters = new()
    {
        // Healer 는 burst 와 stun 에 약함
        { ChampionRole.Healer,     new[]{ ChampionRole.Mage, ChampionRole.Disruptor, ChampionRole.Assassin } },
        // Marksman 은 dive 류에 약함 (Assassin / Skirmisher 가 거리 좁힘)
        { ChampionRole.Marksman,   new[]{ ChampionRole.Assassin, ChampionRole.Skirmisher } },
        // Mage 는 burst dive 와 stun 에 약함
        { ChampionRole.Mage,       new[]{ ChampionRole.Assassin, ChampionRole.Disruptor } },
        // Tank 는 % 데미지 / 광역 AoE 에 약함
        { ChampionRole.Tank,       new[]{ ChampionRole.Mage, ChampionRole.Disruptor } },
        // Assassin 은 Tank/Healer 안정 조합에 약함 (burst 막혀버림)
        { ChampionRole.Assassin,   new[]{ ChampionRole.Tank, ChampionRole.Healer } },
        // Disruptor 는 카이팅 / 사거리 우위 에 약함
        { ChampionRole.Disruptor,  new[]{ ChampionRole.Marksman, ChampionRole.Mage } },
        // Fighter / Skirmisher / Duelist — 일반 melee, 특이 카운터 없음
        { ChampionRole.Fighter,    new[]{ ChampionRole.Marksman, ChampionRole.Healer } },
        { ChampionRole.Skirmisher, new[]{ ChampionRole.Tank, ChampionRole.Disruptor } },
        { ChampionRole.Duelist,    new[]{ ChampionRole.Tank, ChampionRole.Healer } },
    };

    public ChampionData Choose(BanPickManager mgr)
    {
        var pool = mgr.config.championPool.Where(mgr.IsSelectable).ToList();
        if (pool.Count == 0) return null;

        if (mgr.IsBanStep) return ChooseBan(pool);
        return ChoosePick(pool);
    }

    // ============== BAN — 영리하게 ==============
    ChampionData ChooseBan(List<ChampionData> pool)
    {
        if (Random.value < banWildcardChance)
            return pool[Random.Range(0, pool.Count)];

        // 1순위 전략: 사용자가 이미 픽 한 챔프의 "카운터" 를 ban
        // → 사용자가 자기 챔프 보호용 카운터 못 쓰게
        var allyPicks = PickResult.AllyPicks;
        if (allyPicks != null && allyPicks.Count > 0)
        {
            var counterRoles = new HashSet<ChampionRole>();
            foreach (var picked in allyPicks)
                if (Counters.TryGetValue(picked.role, out var cs))
                    foreach (var c in cs) counterRoles.Add(c);

            // 카운터 후보 중 풀에 있는 거 위에 가중치 적용
            var counterCandidates = pool.Where(c => counterRoles.Contains(c.role)).ToList();
            if (counterCandidates.Count > 0)
            {
                Debug.Log($"[AI Ban] 사용자 픽 카운터 ban: {string.Join(",", counterCandidates.Select(c => c.role))}");
                return WeightedPick(counterCandidates, banWeights);
            }
        }

        // 2순위: 위협 역할 가중치 기반 (Mage / Healer / Disruptor 등)
        return WeightedPick(pool, banWeights);
    }

    // ============== PICK — 팀 구성 인식 ==============
    ChampionData ChoosePick(List<ChampionData> pool)
    {
        if (Random.value < pickWildcardChance)
            return pool[Random.Range(0, pool.Count)];

        var enemyPicks = PickResult.EnemyPicks;
        var allyPicks  = PickResult.AllyPicks;
        var enemyRoles = enemyPicks != null
            ? new HashSet<ChampionRole>(enemyPicks.Select(c => c.role))
            : new HashSet<ChampionRole>();

        int pickIndex = enemyPicks?.Count ?? 0;  // AI 의 이번이 몇 번째 픽 (0, 1, 2)

        // 자기 팀이 빠진 코어 role (Tank → Healer → DPS) 우선 채우기
        bool hasTank   = enemyRoles.Contains(ChampionRole.Tank);
        bool hasHealer = enemyRoles.Contains(ChampionRole.Healer);
        bool hasDps    = enemyRoles.Any(r => r == ChampionRole.Marksman || r == ChampionRole.Mage
                                          || r == ChampionRole.Assassin || r == ChampionRole.Duelist);

        // 1순위 — 코어 역할 메우기
        List<ChampionData> priorityCandidates = null;
        string strategy = "";

        // 마지막 픽 (pickIndex == 2) 일 때 — 빈 역할 강제 채우기 우선
        if (pickIndex == 2)
        {
            if (!hasTank) { priorityCandidates = pool.Where(c => c.role == ChampionRole.Tank).ToList(); strategy = "마지막픽 Tank 보강"; }
            else if (!hasHealer) { priorityCandidates = pool.Where(c => c.role == ChampionRole.Healer).ToList(); strategy = "마지막픽 Healer 보강"; }
            else if (!hasDps)    { priorityCandidates = pool.Where(c => c.role == ChampionRole.Marksman || c.role == ChampionRole.Mage || c.role == ChampionRole.Assassin || c.role == ChampionRole.Duelist).ToList(); strategy = "마지막픽 DPS 보강"; }
        }

        // 2순위 — 사용자 픽 카운터 (사용자 챔프 약점 찌르기)
        if ((priorityCandidates == null || priorityCandidates.Count == 0) && allyPicks != null && allyPicks.Count > 0)
        {
            var counterRoles = new HashSet<ChampionRole>();
            foreach (var picked in allyPicks)
                if (Counters.TryGetValue(picked.role, out var cs))
                    foreach (var c in cs) counterRoles.Add(c);

            // 카운터 + 우리 팀이 안 가진 role
            var counters = pool.Where(c => counterRoles.Contains(c.role) && !enemyRoles.Contains(c.role)).ToList();
            if (counters.Count > 0) { priorityCandidates = counters; strategy = "사용자 픽 카운터"; }
        }

        // 3순위 — 같은 역할 중복 피하면서 가중치
        if (priorityCandidates == null || priorityCandidates.Count == 0)
        {
            priorityCandidates = pool.Where(c => !enemyRoles.Contains(c.role)).ToList();
            strategy = "역할 다양성";
        }
        if (priorityCandidates.Count == 0) { priorityCandidates = pool; strategy = "fallback 풀 전체"; }

        var result = WeightedPick(priorityCandidates, pickWeights);
        if (result != null) Debug.Log($"[AI Pick {pickIndex+1}/3] {strategy} → {result.role} ({result.displayName})");
        return result;
    }

    static ChampionData WeightedPick(List<ChampionData> candidates, RoleWeights weights)
    {
        if (candidates.Count == 0) return null;
        float total = 0f;
        foreach (var c in candidates) total += Mathf.Max(0.01f, weights.Of(c.role));
        float r = Random.value * total;
        float cumulative = 0f;
        foreach (var c in candidates)
        {
            cumulative += Mathf.Max(0.01f, weights.Of(c.role));
            if (r <= cumulative) return c;
        }
        return candidates[candidates.Count - 1];
    }
}
