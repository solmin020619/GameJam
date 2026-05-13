#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 03.Prefabs/SPUM_*.prefab 을 마스터 풀로 사용해 챔프 풀 자동 재구성.
/// - 각 prefab 에 ChampionUnit/Rigidbody2D/Collider2D 자동 추가
/// - 이름/무기 기반 Role 자동 분류 + 한글 이름 매핑
/// - 06.ScriptableObjects/Champions 폴더의 기존 ChampionData 자산 모두 삭제 후 재생성
/// - BanPickConfig 자동 갱신
/// </summary>
public static class ChampionPoolBuilder
{
    const string PrefabsFolder = "Assets/03.Prefabs";
    const string ChampionDataDir = "Assets/06.ScriptableObjects/Champions";
    const string ConfigPath = "Assets/06.ScriptableObjects/BanPickConfig.asset";

    [MenuItem("TFM/Rebuild Champion Pool (From 03.Prefabs)")]
    public static void Rebuild()
    {
        if (!AssetDatabase.IsValidFolder(ChampionDataDir))
            Directory.CreateDirectory(ChampionDataDir);

        // 1) 03.Prefabs 안 SPUM_*.prefab 찾기 (서브폴더 제외)
        var prefabPaths = new List<string>();
        var allGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder });
        foreach (var guid in allGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            // 03.Prefabs 바로 아래만 (Champions/ 같은 서브폴더는 제외)
            var rel = path.Substring(PrefabsFolder.Length + 1);
            if (rel.Contains("/")) continue;
            if (!Path.GetFileName(path).StartsWith("SPUM_")) continue;
            prefabPaths.Add(path);
        }

        if (prefabPaths.Count == 0)
        {
            Debug.LogWarning($"[TFM] {PrefabsFolder} 에 SPUM_*.prefab 없음.");
            return;
        }

        // 2) 기존 ChampionData 자산 삭제
        var oldGuids = AssetDatabase.FindAssets("t:ChampionData", new[] { ChampionDataDir });
        foreach (var g in oldGuids)
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));

        // 3) 각 prefab → 배틀 준비 + ChampionData 생성
        var roleCounters = new Dictionary<ChampionRole, int>();
        var generated = new List<ChampionData>();

        foreach (var path in prefabPaths)
        {
            EnsureBattleReady(path);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var role = GuessRole(prefab);
            if (!roleCounters.ContainsKey(role)) roleCounters[role] = 0;
            roleCounters[role]++;
            int idx = roleCounters[role];

            var dataPath = $"{ChampionDataDir}/Champion_{role}_{idx:D2}.asset";
            var data = ScriptableObject.CreateInstance<ChampionData>();
            AssetDatabase.CreateAsset(data, dataPath);
            ApplyPreset(data, role, idx, prefab);
            EditorUtility.SetDirty(data);
            generated.Add(data);
        }

        // 4) BanPickConfig 갱신
        UpdateConfig(generated);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var summary = string.Join(", ", roleCounters.Select(kv => $"{kv.Key} x{kv.Value}"));
        Debug.Log($"[TFM] Champion pool rebuilt — {generated.Count} champions. {summary}");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<BanPickConfig>(ConfigPath);
    }

    // ============ helpers ============

    static void EnsureBattleReady(string prefabPath)
    {
        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        bool changed = false;

        if (contents.GetComponent<Rigidbody2D>() == null)
        {
            var rb = contents.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            changed = true;
        }
        if (contents.GetComponent<Collider2D>() == null)
        {
            var col = contents.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 1.4f);
            col.offset = new Vector2(0f, 0.7f);
            changed = true;
        }
        if (contents.GetComponent<ChampionUnit>() == null)
        {
            contents.AddComponent<ChampionUnit>();
            changed = true;
        }

        if (changed) PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
    }

    static ChampionRole GuessRole(GameObject prefab)
    {
        // 1) 이름 매칭
        var n = prefab.name.ToLower();
        if (n.Contains("guard") || n.Contains("tank") || n.Contains("paladin")) return ChampionRole.Tank;
        if (n.Contains("snip") || n.Contains("arch") || n.Contains("bow") || n.Contains("rang")) return ChampionRole.Marksman;
        if (n.Contains("mage") || n.Contains("wiz") || n.Contains("sorc")) return ChampionRole.Mage;
        if (n.Contains("heal") || n.Contains("priest") || n.Contains("cleric")) return ChampionRole.Healer;
        if (n.Contains("berserk") || n.Contains("warrior") || n.Contains("fight") || n.Contains("knight")) return ChampionRole.Fighter;

        // 2) 무기 기반 자동 분류 (SPUM_숫자 같은 모호한 이름)
        var spum = prefab.GetComponent<SPUM_Prefabs>() ?? prefab.GetComponentInChildren<SPUM_Prefabs>();
        if (spum != null) return ClassifyByWeapon(spum);
        return ChampionRole.Fighter;
    }

    static ChampionRole ClassifyByWeapon(SPUM_Prefabs spum)
    {
        if (spum.ImageElement == null) return ChampionRole.Fighter;
        var weapons = new HashSet<string>();
        foreach (var el in spum.ImageElement)
        {
            if (el == null) continue;
            if (el.PartType != "Weapons") continue;
            if (string.IsNullOrEmpty(el.ItemPath)) continue;
            if (string.IsNullOrEmpty(el.PartSubType)) continue;
            weapons.Add(el.PartSubType);
        }
        if (weapons.Contains("Shield")) return ChampionRole.Tank;
        if (weapons.Contains("Bow")) return ChampionRole.Marksman;
        if (weapons.Contains("Wand")) return ChampionRole.Mage;
        if (weapons.Contains("Mace")) return ChampionRole.Healer;
        return ChampionRole.Fighter;
    }

    static void ApplyPreset(ChampionData c, ChampionRole role, int idx, GameObject prefab)
    {
        c.championId = $"{role.ToString().ToLower()}_{idx:D2}";
        c.displayName = $"{KoreanNameFor(role)} {idx}";
        c.role = role;
        c.themeColor = ColorFor(role);
        c.unitPrefab = prefab;

        switch (role)
        {
            case ChampionRole.Tank:
                c.maxHealth = 700; c.attackDamage = 25; c.attackSpeed = 0.8f;
                c.attackRange = 1.2f; c.defense = 30; c.moveSpeed = 3.0f;
                break;
            case ChampionRole.Fighter:
                c.maxHealth = 500; c.attackDamage = 50; c.attackSpeed = 1.0f;
                c.attackRange = 1.5f; c.defense = 15; c.moveSpeed = 4.0f;
                break;
            case ChampionRole.Marksman:
                c.maxHealth = 350; c.attackDamage = 60; c.attackSpeed = 1.2f;
                c.attackRange = 4.5f; c.defense = 5; c.moveSpeed = 4.5f;
                break;
            case ChampionRole.Mage:
                c.maxHealth = 350; c.attackDamage = 70; c.attackSpeed = 0.6f;
                c.attackRange = 4.0f; c.defense = 5; c.moveSpeed = 3.5f;
                break;
            case ChampionRole.Healer:
                c.maxHealth = 400; c.attackDamage = 25; c.attackSpeed = 0.8f;
                c.attackRange = 3.5f; c.defense = 10; c.moveSpeed = 3.5f;
                break;
        }
    }

    static string KoreanNameFor(ChampionRole role) => role switch
    {
        ChampionRole.Tank => "수호기사",
        ChampionRole.Fighter => "광전사",
        ChampionRole.Marksman => "저격수",
        ChampionRole.Mage => "마법사",
        ChampionRole.Healer => "성기사",
        _ => "전사"
    };

    static Color ColorFor(ChampionRole role) => role switch
    {
        ChampionRole.Tank => new Color(0.3f, 0.6f, 1.0f),
        ChampionRole.Fighter => new Color(1.0f, 0.4f, 0.3f),
        ChampionRole.Marksman => new Color(0.4f, 1.0f, 0.4f),
        ChampionRole.Mage => new Color(1.0f, 0.3f, 1.0f),
        ChampionRole.Healer => new Color(1.0f, 0.9f, 0.4f),
        _ => Color.white
    };

    static void UpdateConfig(List<ChampionData> champions)
    {
        var config = AssetDatabase.LoadAssetAtPath<BanPickConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<BanPickConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        config.championPool = champions;
        config.bansPerTeam = 1;
        config.picksPerTeam = 3;
        config.phaseTimeLimit = 20f;
        config.aiActionDelay = 0.8f;
        config.autoConfirmGrace = 1.5f;
        config.turnOrderIsAlly = new List<bool>
        {
            true, false,                          // bans: A E
            true, false, false, true, true, false // picks: A E E A A E (snake)
        };
        EditorUtility.SetDirty(config);
    }
}
#endif
