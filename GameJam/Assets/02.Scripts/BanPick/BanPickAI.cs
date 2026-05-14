using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BanPickAI : MonoBehaviour
{
    [Header("랜덤성")]
    [Tooltip("밴 단계에서 완전 랜덤 밴 확률 (1.0 = 항상 wildcard, 0 = 항상 priority)")]
    [Range(0f, 1f)] public float banWildcardChance = 0.15f;

    [Tooltip("픽 단계에서 완전 랜덤 픽 확률")]
    [Range(0f, 1f)] public float pickWildcardChance = 0.20f;

    /// <summary>각 역할이 밴/픽 시 받을 가중치. 높을수록 자주 선택.</summary>
    [System.Serializable]
    public class RoleWeights
    {
        public float Tank = 1f;
        public float Fighter = 1f;
        public float Marksman = 1f;
        public float Mage = 1f;
        public float Healer = 1f;
        public float Disruptor = 1f;
        public float Skirmisher = 1f;
        public float Duelist = 1f;
        public float Assassin = 1f;

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

    [Header("밴 가중치 — 위협적인 역할 위주")]
    public RoleWeights banWeights = new RoleWeights {
        Healer = 3f, Mage = 2.5f, Marksman = 2.5f,
        Disruptor = 1.5f, Assassin = 1.5f,
        Tank = 1f, Fighter = 1f, Skirmisher = 1f, Duelist = 1f,
    };

    [Header("픽 가중치 — 균형있는 조합 위주")]
    public RoleWeights pickWeights = new RoleWeights {
        Tank = 3f, Healer = 2.5f, Marksman = 2.5f,
        Mage = 2f, Fighter = 2f,
        Disruptor = 1.5f, Skirmisher = 1.5f, Duelist = 1.5f,
        Assassin = 1f,
    };

    public ChampionData Choose(BanPickManager mgr)
    {
        var pool = mgr.config.championPool.Where(mgr.IsSelectable).ToList();
        if (pool.Count == 0) return null;

        if (mgr.IsBanStep)
            return ChooseBan(pool);
        return ChoosePick(pool);
    }

    ChampionData ChooseBan(List<ChampionData> pool)
    {
        // wildcard — 완전 랜덤
        if (Random.value < banWildcardChance)
            return pool[Random.Range(0, pool.Count)];

        // 가중치 랜덤 — 위협 역할 우선이지만 다양성 유지
        return WeightedPick(pool, banWeights);
    }

    ChampionData ChoosePick(List<ChampionData> pool)
    {
        // wildcard — 완전 랜덤
        if (Random.value < pickWildcardChance)
            return pool[Random.Range(0, pool.Count)];

        // 이미 픽한 역할 제외 (조합 다양성)
        var alreadyEnemy = new HashSet<ChampionRole>(PickResult.EnemyPicks.Select(c => c.role));
        var filtered = pool.Where(c => !alreadyEnemy.Contains(c.role)).ToList();

        // 다른 역할 후보 없으면 풀 전체 사용
        if (filtered.Count == 0) filtered = pool;

        return WeightedPick(filtered, pickWeights);
    }

    /// <summary>가중치 기반 랜덤 — 같은 역할 여러 ChampionData 있어도 각자 자기 weight 받음</summary>
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
