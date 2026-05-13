#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SpumChampionGenerator
{
    const string UnitsFolder = "Assets/SPUM/Resources/Units";
    const string OutDir = "Assets/06.ScriptableObjects/Champions";
    const string ConfigPath = "Assets/06.ScriptableObjects/BanPickConfig.asset";

    [MenuItem("TFM/Generate Champions From SPUM Units")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(OutDir))
        {
            Directory.CreateDirectory(OutDir);
            AssetDatabase.Refresh();
        }

        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { UnitsFolder });
        if (prefabGuids.Length == 0)
        {
            Debug.LogWarning($"[TFM] No SPUM prefabs in {UnitsFolder}. Make some via SPUM_Scene first.");
            return;
        }

        var roleCounters = new Dictionary<ChampionRole, int>();
        var generated = new List<ChampionData>();

        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var spum = prefab.GetComponent<SPUM_Prefabs>();
            if (spum == null) spum = prefab.GetComponentInChildren<SPUM_Prefabs>(true);
            if (spum == null)
            {
                Debug.LogWarning($"[TFM] Skip (no SPUM_Prefabs): {path}");
                continue;
            }

            var weapons = ExtractWeapons(spum);
            var role = ClassifyRole(weapons);

            if (!roleCounters.ContainsKey(role)) roleCounters[role] = 0;
            roleCounters[role]++;
            int idx = roleCounters[role];

            var champPath = $"{OutDir}/Champion_{role}_{idx:D2}.asset";
            var champ = AssetDatabase.LoadAssetAtPath<ChampionData>(champPath);
            if (champ == null)
            {
                champ = ScriptableObject.CreateInstance<ChampionData>();
                AssetDatabase.CreateAsset(champ, champPath);
            }

            ApplyPreset(champ, role, idx);
            champ.unitPrefab = prefab;

            EditorUtility.SetDirty(champ);
            generated.Add(champ);
        }

        var config = AssetDatabase.LoadAssetAtPath<BanPickConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<BanPickConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        config.championPool = generated;
        config.bansPerTeam = 2;
        config.picksPerTeam = 3;
        config.phaseTimeLimit = 20f;
        config.aiActionDelay = 0.8f;
        config.autoConfirmGrace = 1.5f;
        config.turnOrderIsAlly = new List<bool>
        {
            true, false, true, false,           // bans: A E A E
            true, false, false, true, true, false // picks: A E E A A E (snake)
        };
        EditorUtility.SetDirty(config);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var summary = string.Join(", ", roleCounters.Select(kv => $"{kv.Key} x{kv.Value}"));
        Debug.Log($"[TFM] Generated {generated.Count} champions from SPUM units. Roles: {summary}");
        Selection.activeObject = config;
    }

    static List<string> ExtractWeapons(SPUM_Prefabs spum)
    {
        var result = new List<string>();
        if (spum.ImageElement == null) return result;
        foreach (var el in spum.ImageElement)
        {
            if (el == null) continue;
            if (el.PartType != "Weapons") continue;
            if (string.IsNullOrEmpty(el.ItemPath)) continue;
            if (string.IsNullOrEmpty(el.PartSubType)) continue;
            if (!result.Contains(el.PartSubType)) result.Add(el.PartSubType);
        }
        return result;
    }

    static ChampionRole ClassifyRole(List<string> weapons)
    {
        bool has(string w) => weapons.Any(x => x.Equals(w, System.StringComparison.OrdinalIgnoreCase));
        if (has("Shield")) return ChampionRole.Tank;
        if (has("Bow")) return ChampionRole.Marksman;
        if (has("Wand")) return ChampionRole.Mage;
        if (has("Mace")) return ChampionRole.Healer;
        if (weapons.Count == 0) return ChampionRole.Fighter;
        return ChampionRole.Fighter;
    }

    static void ApplyPreset(ChampionData c, ChampionRole role, int idx)
    {
        c.championId = $"{role.ToString().ToLower()}_{idx:D2}";
        c.displayName = NameFor(role, idx);
        c.role = role;
        c.themeColor = ColorFor(role);

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

    static string NameFor(ChampionRole role, int idx)
    {
        string baseName = role switch
        {
            ChampionRole.Tank => "수호기사",
            ChampionRole.Fighter => "광전사",
            ChampionRole.Marksman => "저격수",
            ChampionRole.Mage => "마법사",
            ChampionRole.Healer => "성기사",
            _ => "전사"
        };
        return $"{baseName} {idx}";
    }

    static Color ColorFor(ChampionRole role) => role switch
    {
        ChampionRole.Tank => new Color(0.3f, 0.6f, 1.0f),
        ChampionRole.Fighter => new Color(1.0f, 0.4f, 0.3f),
        ChampionRole.Marksman => new Color(0.4f, 1.0f, 0.4f),
        ChampionRole.Mage => new Color(1.0f, 0.3f, 1.0f),
        ChampionRole.Healer => new Color(1.0f, 0.9f, 0.4f),
        _ => Color.white
    };
}
#endif
