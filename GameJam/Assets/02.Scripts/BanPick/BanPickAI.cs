using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BanPickAI : MonoBehaviour
{
    [Tooltip("밴 단계에서 적이 우선 밴할 역할 순서. 위에서부터 고르는 액을 밴.")]
    public ChampionRole[] banPriority = { ChampionRole.Healer, ChampionRole.Mage, ChampionRole.Marksman };

    [Tooltip("픽 단계에서 AI 팀이 원하는 역할 구성(이 순서대로 우선적으로 고른다).")]
    public ChampionRole[] pickPriority = { ChampionRole.Tank, ChampionRole.Marksman, ChampionRole.Healer, ChampionRole.Fighter, ChampionRole.Mage };

    public ChampionData Choose(BanPickManager mgr)
    {
        var pool = mgr.config.championPool.Where(mgr.IsSelectable).ToList();
        if (pool.Count == 0) return null;

        if (mgr.IsBanStep)
        {
            // banPriority 안에서 풀에 있는 거 다 모은 뒤 무작위 — 매 게임마다 다른 밴 결과
            var candidates = new List<ChampionData>();
            foreach (var role in banPriority)
            {
                var c = pool.FirstOrDefault(x => x.role == role);
                if (c != null) candidates.Add(c);
            }
            if (candidates.Count > 0)
                return candidates[Random.Range(0, candidates.Count)];
            return pool[Random.Range(0, pool.Count)];
        }

        // 픽 시: 아직 안 고른 역할 우선
        var alreadyEnemy = new HashSet<ChampionRole>(PickResult.EnemyPicks.Select(c => c.role));
        foreach (var role in pickPriority)
        {
            if (alreadyEnemy.Contains(role)) continue;
            var c = pool.FirstOrDefault(x => x.role == role);
            if (c != null) return c;
        }
        return pool[Random.Range(0, pool.Count)];
    }
}
