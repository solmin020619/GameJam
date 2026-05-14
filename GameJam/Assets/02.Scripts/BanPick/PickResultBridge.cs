using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 밴픽 결과를 다음 씬의 BattleManager 로 자동 주입.
/// 빈 MonoBehaviour 컴포넌트(호환용) + RuntimeInitializeOnLoadMethod 로 무조건 동작.
/// 어떤 씬에서 시작하든 sceneLoaded 콜백이 자동 등록됨 → HScene 에 GameObject 안 둬도 됨.
/// </summary>
public class PickResultBridge : MonoBehaviour
{
    // ==== 진짜 핵심: 정적 초기화 ====
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void StaticInit()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedStatic;
        SceneManager.sceneLoaded += OnSceneLoadedStatic;
        Debug.Log("[Bridge] RuntimeInitialize — sceneLoaded 후크 등록");
    }

    static void OnSceneLoadedStatic(Scene scene, LoadSceneMode mode)
    {
        if (PickResult.AllyPicks == null || PickResult.AllyPicks.Count == 0)
        {
            // 픽 없음 → BanPick 안 거치고 직접 진입한 씬. 무시.
            return;
        }
        if (PickResult.EnemyPicks == null || PickResult.EnemyPicks.Count == 0) return;

        // 이 씬의 BattleManager 찾기 (Additive 로 올라온 UI 씬엔 없음)
        BattleManager bm = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            bm = root.GetComponentInChildren<BattleManager>(true);
            if (bm != null) break;
        }
        if (bm == null) return;

        var team0 = BuildArray(PickResult.AllyPicks);
        var team1 = BuildArray(PickResult.EnemyPicks);
        bm.SetTeams(team0, team1);

        Debug.Log($"[Bridge] '{scene.name}' 에 픽 주입 — 우리: {Names(PickResult.AllyPicks)} / 상대: {Names(PickResult.EnemyPicks)}");
    }

    static ChampionSO[] BuildArray(List<ChampionData> picks)
    {
        var arr = new ChampionSO[picks.Count];
        for (int i = 0; i < picks.Count; i++)
            arr[i] = ToChampionSO(picks[i]);
        return arr;
    }

    static string Names(List<ChampionData> list)
    {
        var n = new string[list.Count];
        for (int i = 0; i < list.Count; i++) n[i] = list[i].displayName;
        return string.Join(", ", n);
    }

    static ChampionSO ToChampionSO(ChampionData d)
    {
        var so = ScriptableObject.CreateInstance<ChampionSO>();
        so.ChampionName = d.displayName;
        so.Icon = d.portrait;
        so.KillIcon = d.killIcon;
        so.KillIconZoom = d.killIconZoom;
        so.KillIconOffset = d.killIconOffset;
        so.Prefab = d.unitPrefab;
        so.Role = d.role;
        so.MaxHp = d.maxHealth;
        so.AttackDamage = d.attackDamage;
        so.AttackSpeed = d.attackSpeed;
        so.AttackRange = d.attackRange;
        so.Defense = d.defense;
        so.MoveSpeed = d.moveSpeed;
        so.BasicSkillName = d.basicSkillName;
        so.BasicSkillCooldown = d.basicSkillCooldown;
        so.BasicSkillIcon = d.basicSkillIcon;
        so.UltimateName = d.ultimateName;
        so.UltimateCooldown = d.ultimateCooldown;
        so.UltimateIcon = d.ultimateIcon;
        so.AutoAttackSfx = d.autoAttackSfx;
        so.BasicSkillSfx = d.basicSkillSfx;
        so.UltimateSfx = d.ultimateSfx;
        return so;
    }
}
