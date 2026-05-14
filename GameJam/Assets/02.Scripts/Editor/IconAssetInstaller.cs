#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 04.Images/Icons + 04.Images/Effects 안의 PNG 들에 Sprite 임포트 설정 자동 적용 +
/// ChampionData 들의 basicSkillIcon / ultimateIcon 자동 매핑.
/// </summary>
public static class IconAssetInstaller
{
    static readonly string[] IconFolders =
    {
        "Assets/04.Images/Icons/Skills",
        "Assets/04.Images/Icons/Buffs",
        "Assets/04.Images/Effects/Arrows",
        "Assets/04.Images/Effects/VFX"
    };

    [MenuItem("TFM/Install Icons + Map To Champions")]
    public static void Install()
    {
        // 1) 모든 PNG → Sprite 임포트 설정 강제
        int processed = 0;
        foreach (var folder in IconFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (Mathf.Abs(importer.spritePixelsPerUnit - 100f) > 0.1f) { importer.spritePixelsPerUnit = 100f; dirty = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
                if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; dirty = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                if (dirty) { importer.SaveAndReimport(); processed++; }
            }
        }
        Debug.Log($"[TFM] Sprite import settings applied to {processed} textures.");

        // 2) ChampionData 들에 아이콘 자동 매핑
        var champGuids = AssetDatabase.FindAssets("t:ChampionData", new[] { "Assets/06.ScriptableObjects/Champions" });
        int mapped = 0;
        foreach (var guid in champGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<ChampionData>(path);
            if (data == null) continue;

            var basic = LoadSkillIcon(BasicIconName(data.role));
            var ult = LoadSkillIcon(UltIconName(data.role));
            if (basic != null) data.basicSkillIcon = basic;
            if (ult != null) data.ultimateIcon = ult;
            EditorUtility.SetDirty(data);
            mapped++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[TFM] Mapped icons to {mapped} ChampionData assets.");
    }

    static Sprite LoadSkillIcon(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/04.Images/Icons/Skills/{name}.png");

    // 챔프 역할 → 기본 스킬 아이콘 매핑
    static string BasicIconName(ChampionRole role) => role switch
    {
        ChampionRole.Tank       => "skill_shield_block",   // 방패강타
        ChampionRole.Fighter    => "skill_whirlwind",      // 회전베기
        ChampionRole.Marksman   => "skill_multishot",      // 3연사
        ChampionRole.Mage       => "skill_fireball",       // 마법탄
        ChampionRole.Healer     => "skill_heal",           // 치유의 빛
        ChampionRole.Disruptor  => "skill_rend",           // 지진강타
        ChampionRole.Skirmisher => "skill_charge",         // 돌격창
        ChampionRole.Duelist    => "skill_execute",        // 쾌검
        ChampionRole.Assassin   => "skill_teleport",       // 배후습격
        _ => null
    };

    // 챔프 역할 → 궁극기 아이콘 매핑
    static string UltIconName(ChampionRole role) => role switch
    {
        ChampionRole.Tank       => "skill_fortify",         // 수호의 결의
        ChampionRole.Fighter    => "skill_berserk",         // 광폭
        ChampionRole.Marksman   => "skill_explosive_arrow", // 화살비
        ChampionRole.Mage       => "skill_meteor",          // 광역폭발
        ChampionRole.Healer     => "skill_barrier",         // 성역
        ChampionRole.Disruptor  => "skill_strike",          // 분쇄의 일격
        ChampionRole.Skirmisher => "skill_lightning",       // 짓밟기
        ChampionRole.Duelist    => "skill_piercing_shot",   // 연참
        ChampionRole.Assassin   => "skill_arcane_missile",  // 잔영난무
        _ => null
    };
}
#endif
