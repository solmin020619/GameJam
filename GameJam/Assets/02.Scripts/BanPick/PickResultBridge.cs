using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 밴픽 결과를 다음 씬의 BattleManager 로 자동 주입.
/// Main(또는 HScene)에 GameObject 하나 만들고 이 컴포넌트 붙이면 끝.
/// DontDestroyOnLoad 로 씬 전환 후에도 살아남아서, KScene 로드되는 순간
/// 그 안의 BattleManager 를 찾아 ChampionSO 주입함.
/// → KScene 자체는 0건드림.
/// </summary>
public class PickResultBridge : MonoBehaviour
{
    static PickResultBridge _instance;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (PickResult.AllyPicks == null || PickResult.AllyPicks.Count == 0) return;
        if (PickResult.EnemyPicks == null || PickResult.EnemyPicks.Count == 0) return;

        var bm = FindObjectOfType<BattleManager>();
        if (bm == null) return;  // 배틀 매니저 없는 씬이면 무시

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
        so.Prefab = d.unitPrefab;
        so.Role = d.role;
        so.MaxHp = d.maxHealth;
        so.AttackDamage = d.attackDamage;
        so.AttackSpeed = d.attackSpeed;
        so.AttackRange = d.attackRange;
        so.Defense = d.defense;
        so.MoveSpeed = d.moveSpeed;
        return so;
    }
}
