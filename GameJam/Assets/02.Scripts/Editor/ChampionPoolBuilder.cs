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
    const int CopiesPerPrefab = 1;  // prefab 1개당 ChampionData N개 (1 = 역할별 1챔 → 풀 9개)

    [MenuItem("TFM/Rebuild Champion Pool (From 03.Prefabs)")]
    public static void Rebuild()
    {
        if (!AssetDatabase.IsValidFolder(ChampionDataDir))
            Directory.CreateDirectory(ChampionDataDir);

        // 1) 03.Prefabs 안 SPUM_Prefabs 컴포넌트 보유 prefab (서브폴더 제외)
        var prefabPaths = new List<string>();
        var allGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder });
        foreach (var guid in allGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            // 03.Prefabs 바로 아래만 (Champions/ 같은 서브폴더는 제외)
            var rel = path.Substring(PrefabsFolder.Length + 1);
            if (rel.Contains("/")) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            // SPUM 캐릭터 prefab만 (DamageText 등 제외)
            if (prefab.GetComponentInChildren<SPUM_Prefabs>(true) == null) continue;
            prefabPaths.Add(path);
        }

        if (prefabPaths.Count == 0)
        {
            Debug.LogWarning($"[TFM] {PrefabsFolder} 에 SPUM_*.prefab 없음.");
            return;
        }

        Debug.Log($"[TFM] Found {prefabPaths.Count} SPUM prefabs in {PrefabsFolder}");

        // 2) 기존 ChampionData 자산 삭제
        var oldGuids = AssetDatabase.FindAssets("t:ChampionData", new[] { ChampionDataDir });
        foreach (var g in oldGuids)
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3) 각 prefab → 배틀 준비 + ChampionData 생성
        var roleCounters = new Dictionary<ChampionRole, int>();
        var generated = new List<ChampionData>();

        foreach (var path in prefabPaths)
        {
            EnsureBattleReady(path);

            // EnsureBattleReady 후 디스크 갱신 → AssetDatabase Refresh 한 번
            AssetDatabase.ImportAsset(path);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[TFM] Load 실패 (path={path})");
                continue;
            }

            var role = GuessRole(prefab);

            // 같은 prefab 으로 ChampionData N개 생성 (풀 크기 늘리기)
            for (int copy = 0; copy < CopiesPerPrefab; copy++)
            {
                if (!roleCounters.ContainsKey(role)) roleCounters[role] = 0;
                roleCounters[role]++;
                int idx = roleCounters[role];

                var dataPath = $"{ChampionDataDir}/Champion_{role}_{idx:D2}.asset";

                if (AssetDatabase.LoadAssetAtPath<ChampionData>(dataPath) != null)
                    AssetDatabase.DeleteAsset(dataPath);

                var data = ScriptableObject.CreateInstance<ChampionData>();
                ApplyPreset(data, role, idx, prefab);     // CreateAsset 전에 데이터 채움
                AssetDatabase.CreateAsset(data, dataPath);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);     // 즉시 디스크에 저장 강제

                if (string.IsNullOrEmpty(data.displayName) || data.unitPrefab == null)
                    Debug.LogError($"[TFM] {dataPath} 빈 상태! displayName='{data.displayName}', prefab={(data.unitPrefab == null ? "null" : data.unitPrefab.name)}");
                else
                    Debug.Log($"[TFM] {data.displayName} ({role}) ← {prefab.name}");

                generated.Add(data);
            }
        }

        // 4) BanPickConfig 갱신
        UpdateConfig(generated);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var summary = string.Join(", ", roleCounters.Select(kv => $"{kv.Key} x{kv.Value}"));
        Debug.Log($"[TFM] Champion pool rebuilt — {generated.Count} champions. {summary}");

        // 풀 재빌드 시 portrait / killIcon / skill icon 모두 비워졌음 → 자동으로 매핑 메뉴 호출
        try
        {
            ChampionPortraitMapper.Map();
            Debug.Log("[TFM] Portraits / KillIcons 자동 매핑 완료");
        }
        catch (System.Exception e) { Debug.LogWarning($"[TFM] Portrait 매핑 실패: {e.Message}"); }

        try
        {
            IconAssetInstaller.Install();
            Debug.Log("[TFM] Skill Icons 자동 매핑 완료");
        }
        catch (System.Exception e) { Debug.LogWarning($"[TFM] Skill Icon 매핑 실패: {e.Message}"); }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<BanPickConfig>(ConfigPath);
    }

    // ============ helpers ============

    /// <summary>
    /// Preview Scene 에 prefab 을 인스턴스화 → 깨진 스크립트 제거 + 컴포넌트 보장 → 저장.
    /// LoadPrefabContents 보다 인스턴스가 missing script 제거에 안전.
    /// </summary>
    static void EnsureBattleReady(string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject instance = null;
        try
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null) return;

            bool changed = false;

            // 1) 모든 자식의 missing script 제거 (instance 에서는 빌트인 API 가 잘 작동)
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            {
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                if (removed > 0) changed = true;
            }

            // 2) 컴포넌트 보장
            if (instance.GetComponent<Rigidbody2D>() == null)
            {
                var rb = instance.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                changed = true;
            }
            if (instance.GetComponent<Collider2D>() == null)
            {
                var col = instance.AddComponent<BoxCollider2D>();
                col.size = new Vector2(0.8f, 1.4f);
                col.offset = new Vector2(0f, 0.7f);
                changed = true;
            }
            if (instance.GetComponent<ChampionUnit>() == null)
            {
                instance.AddComponent<ChampionUnit>();
                changed = true;
            }

            // 3) 저장
            if (changed)
            {
                try { PrefabUtility.SaveAsPrefabAsset(instance, prefabPath); }
                catch (System.Exception ex) { Debug.LogWarning($"[TFM] {prefabPath} 저장 실패: {ex.Message}"); }
            }
        }
        finally
        {
            if (instance != null) Object.DestroyImmediate(instance);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static ChampionRole GuessRole(GameObject prefab)
    {
        // 1) 접미사 매칭 (_T, _F, _M, _A, _H, _D, _S, _U)
        var n = prefab.name.ToLower();
        if (n.EndsWith("_t")) return ChampionRole.Tank;
        if (n.EndsWith("_f")) return ChampionRole.Fighter;
        if (n.EndsWith("_m") && !n.EndsWith("man_m")) return ChampionRole.Mage;
        if (n.EndsWith("_a")) return ChampionRole.Marksman;   // Archer
        if (n.EndsWith("_h")) return ChampionRole.Healer;
        if (n.EndsWith("_d")) return ChampionRole.Disruptor;
        if (n.EndsWith("_s")) return ChampionRole.Skirmisher;
        if (n.EndsWith("_u")) return ChampionRole.Duelist;    // dUelist
        if (n.EndsWith("_n")) return ChampionRole.Assassin;   // nINja

        // 2) 이름 매칭
        if (n.Contains("ninja") || n.Contains("assassin") || n.Contains("닌자")) return ChampionRole.Assassin;
        if (n.Contains("swordsm") || n.Contains("duelist") || n.Contains("검사") || n.Contains("쾌검")) return ChampionRole.Duelist;
        if (n.Contains("crush") || n.Contains("hammer") || n.Contains("분쇄")) return ChampionRole.Disruptor;
        if (n.Contains("lancer") || n.Contains("horse") || n.Contains("rider") || n.Contains("기병") || n.Contains("돌격")) return ChampionRole.Skirmisher;
        if (n.Contains("guard") || n.Contains("tank") || n.Contains("paladin")) return ChampionRole.Tank;
        if (n.Contains("snip") || n.Contains("arch") || n.Contains("bow") || n.Contains("rang")) return ChampionRole.Marksman;
        if (n.Contains("mage") || n.Contains("wiz") || n.Contains("sorc")) return ChampionRole.Mage;
        if (n.Contains("heal") || n.Contains("priest") || n.Contains("cleric")) return ChampionRole.Healer;
        if (n.Contains("berserk") || n.Contains("warrior") || n.Contains("fight") || n.Contains("knight")) return ChampionRole.Fighter;

        // 3) 무기 기반 자동 분류 (SPUM_숫자 같은 모호한 이름)
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
            case ChampionRole.Disruptor:
                c.maxHealth = 600; c.attackDamage = 45; c.attackSpeed = 0.6f;
                c.attackRange = 1.4f; c.defense = 22; c.moveSpeed = 3.2f;
                break;
            case ChampionRole.Skirmisher:
                c.maxHealth = 450; c.attackDamage = 55; c.attackSpeed = 0.9f;
                c.attackRange = 1.6f; c.defense = 10; c.moveSpeed = 5.5f;
                break;
            case ChampionRole.Duelist:
                c.maxHealth = 420; c.attackDamage = 58; c.attackSpeed = 1.1f;
                c.attackRange = 1.4f; c.defense = 12; c.moveSpeed = 4.2f;
                break;
            case ChampionRole.Assassin:
                c.maxHealth = 360; c.attackDamage = 52; c.attackSpeed = 1.4f;
                c.attackRange = 1.3f; c.defense = 5; c.moveSpeed = 5.0f;
                break;
        }

        // 스킬 메타 자동 채움 (기획서 기반)
        switch (role)
        {
            case ChampionRole.Tank:       c.basicSkillName = "방패강타"; c.basicSkillCooldown = 6f; c.ultimateName = "수호의 결의";  c.ultimateCooldown = 20f; break;
            case ChampionRole.Fighter:    c.basicSkillName = "회전베기"; c.basicSkillCooldown = 5f; c.ultimateName = "광폭";        c.ultimateCooldown = 18f; break;
            case ChampionRole.Marksman:   c.basicSkillName = "3연사";    c.basicSkillCooldown = 6f; c.ultimateName = "화살비";      c.ultimateCooldown = 22f; break;
            case ChampionRole.Mage:       c.basicSkillName = "마법탄";   c.basicSkillCooldown = 7f; c.ultimateName = "광역폭발";    c.ultimateCooldown = 22f; break;
            case ChampionRole.Healer:     c.basicSkillName = "치유의 빛"; c.basicSkillCooldown = 5f; c.ultimateName = "성역";        c.ultimateCooldown = 20f; break;
            case ChampionRole.Disruptor:  c.basicSkillName = "지진강타"; c.basicSkillCooldown = 6f; c.ultimateName = "분쇄의 일격"; c.ultimateCooldown = 22f; break;
            case ChampionRole.Skirmisher: c.basicSkillName = "돌격창";   c.basicSkillCooldown = 6f; c.ultimateName = "짓밟기";      c.ultimateCooldown = 20f; break;
            case ChampionRole.Duelist:    c.basicSkillName = "쾌검";     c.basicSkillCooldown = 5f; c.ultimateName = "연참";        c.ultimateCooldown = 20f; break;
            case ChampionRole.Assassin:   c.basicSkillName = "배후습격"; c.basicSkillCooldown = 5f; c.ultimateName = "잔영난무";    c.ultimateCooldown = 18f; break;
        }
    }

    static void ApplySkillMeta(ChampionSO so, ChampionRole role)
    {
        // 기획서 (TFM_Champion_Reference.pdf) 기반
        switch (role)
        {
            case ChampionRole.Tank:
                so.BasicSkillName = "방패강타"; so.BasicSkillCooldown = 6f;
                so.UltimateName = "수호의 결의"; so.UltimateCooldown = 20f;
                break;
            case ChampionRole.Fighter:
                so.BasicSkillName = "회전베기"; so.BasicSkillCooldown = 5f;
                so.UltimateName = "광폭"; so.UltimateCooldown = 18f;
                break;
            case ChampionRole.Marksman:
                so.BasicSkillName = "3연사"; so.BasicSkillCooldown = 6f;
                so.UltimateName = "화살비"; so.UltimateCooldown = 22f;
                break;
            case ChampionRole.Mage:
                so.BasicSkillName = "마법탄"; so.BasicSkillCooldown = 7f;
                so.UltimateName = "광역폭발"; so.UltimateCooldown = 22f;
                break;
            case ChampionRole.Healer:
                so.BasicSkillName = "신성치유"; so.BasicSkillCooldown = 5f;
                so.UltimateName = "광역축복"; so.UltimateCooldown = 20f;
                break;
            case ChampionRole.Disruptor:
                so.BasicSkillName = "지진강타"; so.BasicSkillCooldown = 6f;
                so.UltimateName = "분쇄의 일격"; so.UltimateCooldown = 22f;
                break;
            case ChampionRole.Skirmisher:
                so.BasicSkillName = "돌격창"; so.BasicSkillCooldown = 6f;
                so.UltimateName = "짓밟기"; so.UltimateCooldown = 20f;
                break;
        }
    }

    static string KoreanNameFor(ChampionRole role) => role switch
    {
        ChampionRole.Tank => "수호기사",
        ChampionRole.Fighter => "광전사",
        ChampionRole.Marksman => "저격수",
        ChampionRole.Mage => "마법사",
        ChampionRole.Healer => "성직자",
        ChampionRole.Disruptor => "분쇄자",
        ChampionRole.Skirmisher => "돌격기병",
        ChampionRole.Duelist => "검사",
        ChampionRole.Assassin => "닌자",
        _ => "전사"
    };

    static Color ColorFor(ChampionRole role) => role switch
    {
        ChampionRole.Tank => new Color(0.3f, 0.6f, 1.0f),
        ChampionRole.Fighter => new Color(1.0f, 0.4f, 0.3f),
        ChampionRole.Marksman => new Color(0.4f, 1.0f, 0.4f),
        ChampionRole.Mage => new Color(1.0f, 0.3f, 1.0f),
        ChampionRole.Healer => new Color(1.0f, 0.9f, 0.4f),
        ChampionRole.Disruptor => new Color(0.7f, 0.45f, 0.2f),
        ChampionRole.Skirmisher => new Color(0.9f, 0.7f, 0.3f),
        ChampionRole.Duelist => new Color(0.85f, 0.85f, 0.95f),
        ChampionRole.Assassin => new Color(0.5f, 0.3f, 0.7f),
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
