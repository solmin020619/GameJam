#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 두 폴더 자동 매핑:
///   Assets/04.Images/CharImg/  → ChampionData.portrait  (밴픽 카드용)
///   Assets/04.Images/KillImg/  → ChampionData.killIcon  (킬로그 머리컷용)
///
/// 파일명 패턴 (소문자 매칭):
///   guardian/tank        → Tank
///   berserker/fighter    → Fighter
///   swordsman/duelist    → Duelist
///   sniper/marksman/bow  → Marksman
///   mage/wizard          → Mage
///   cleric/healer/priest → Healer
///   crusher/disruptor    → Disruptor
///   lancer/horse/rider   → Skirmisher
///   ninja/assassin       → Assassin
/// </summary>
public static class ChampionPortraitMapper
{
    const string CharImgFolder = "Assets/04.Images/CharImg";
    const string KillImgFolder = "Assets/04.Images/KillImg";
    const string ChampionDir = "Assets/06.ScriptableObjects/Champions";

    [MenuItem("TFM/Map Champion Portraits From Folder")]
    public static void Map()
    {
        var charByRole = ScanFolder(CharImgFolder);
        var killByRole = ScanFolder(KillImgFolder);

        if (charByRole.Count == 0 && killByRole.Count == 0)
        {
            Debug.LogWarning($"[TFM] {CharImgFolder} 또는 {KillImgFolder} 안에 매칭되는 PNG 없음.");
            return;
        }

        var champGuids = AssetDatabase.FindAssets("t:ChampionData", new[] { ChampionDir });
        int applied = 0;
        foreach (var g in champGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var data = AssetDatabase.LoadAssetAtPath<ChampionData>(path);
            if (data == null) continue;

            bool changed = false;
            if (charByRole.TryGetValue(data.role, out var portrait))
            {
                data.portrait = portrait;
                changed = true;
            }
            if (killByRole.TryGetValue(data.role, out var killIcon))
            {
                data.killIcon = killIcon;
                changed = true;
            }
            if (changed)
            {
                EditorUtility.SetDirty(data);
                applied++;
                Debug.Log($"[TFM] {data.displayName} ({data.role}) ← portrait={(charByRole.ContainsKey(data.role) ? "✓" : "-")} kill={(killByRole.ContainsKey(data.role) ? "✓" : "-")}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TFM] Portrait + KillIcon 매핑 완료. {applied} ChampionData 갱신. CharImg: {charByRole.Count} 역할, KillImg: {killByRole.Count} 역할.");
    }

    /// <summary>폴더 스캔 → Sprite 임포트 강제 + 역할별 sprite 매핑</summary>
    static Dictionary<ChampionRole, Sprite> ScanFolder(string folder)
    {
        var result = new Dictionary<ChampionRole, Sprite>();
        if (!AssetDatabase.IsValidFolder(folder)) return result;

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);

            // Sprite 임포트 설정 강제
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (Mathf.Abs(importer.spritePixelsPerUnit - 100f) > 0.1f) { importer.spritePixelsPerUnit = 100f; dirty = true; }
                if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; dirty = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                if (dirty) importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;
            var role = GuessRoleFromFilename(System.IO.Path.GetFileNameWithoutExtension(path));
            if (role == null) continue;
            result[role.Value] = sprite;
        }
        return result;
    }

    static ChampionRole? GuessRoleFromFilename(string name)
    {
        var n = name.ToLower();
        if (n.Contains("guardian") || n.Contains("tank") || n.Contains("paladin")) return ChampionRole.Tank;
        if (n.Contains("berserk") || n.Contains("fighter") || n.Contains("warrior")) return ChampionRole.Fighter;
        if (n.Contains("swordm") || n.Contains("swordsm") || n.Contains("duelist") || n.Contains("검사")) return ChampionRole.Duelist;
        if (n.Contains("sniper") || n.Contains("marksman") || n.Contains("archer") || n.Contains("bow")) return ChampionRole.Marksman;
        if (n.Contains("mage") || n.Contains("wizard") || n.Contains("sorc")) return ChampionRole.Mage;
        if (n.Contains("cleric") || n.Contains("healer") || n.Contains("priest") || n.Contains("heal")) return ChampionRole.Healer;
        if (n.Contains("crusher") || n.Contains("disruptor") || n.Contains("distrupt") || n.Contains("hammer")) return ChampionRole.Disruptor;
        if (n.Contains("lancer") || n.Contains("skirmisher") || n.Contains("horse") || n.Contains("rider")) return ChampionRole.Skirmisher;
        if (n.Contains("ninja") || n.Contains("assassin")) return ChampionRole.Assassin;
        return null;
    }
}
#endif
