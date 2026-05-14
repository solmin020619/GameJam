#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Resources/RoleIcons/ 의 PNG 들을 Sprite 로 자동 import 설정.
/// 처음 풀받았을 때 Texture 로 import 돼서 Sprite.LoadResource 가 실패하는 거 방지.
/// </summary>
public static class RoleIconInstaller
{
    static readonly string[] Names = { "shield", "bow", "staff", "cross", "hammer", "axe", "spear", "dagger" };

    [MenuItem("TFM/Setup Role Icons (Sprite Import)")]
    public static void Setup()
    {
        int fixedCount = 0;
        foreach (var n in Names)
        {
            string path = $"Assets/Resources/RoleIcons/{n}.png";
            if (FixSpriteImport(path)) fixedCount++;
        }
        Debug.Log($"[TFM] Role Icons import 보정 완료 — {fixedCount}/{Names.Length}");
    }

    [MenuItem("TFM/Setup All VFX + RoleIcons (Sprite Import)", priority = -45)]
    public static void SetupAllVfx()
    {
        // Resources/VFX/ + Resources/RoleIcons/ 의 모든 PNG 를 Sprite 로 강제 import
        int fixedCount = 0;
        foreach (var folder in new[] { "Assets/Resources/VFX", "Assets/Resources/RoleIcons" })
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (FixSpriteImport(path)) fixedCount++;
            }
        }
        Debug.Log($"[TFM] VFX + RoleIcons Sprite import 보정 완료 — {fixedCount} 개 fix");
    }

    static bool FixSpriteImport(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return false;
        bool dirty = false;
        if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; dirty = true; }
        if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (Mathf.Abs(imp.spritePixelsPerUnit - 100f) > 0.1f) { imp.spritePixelsPerUnit = 100f; dirty = true; }
        if (imp.alphaIsTransparency != true) { imp.alphaIsTransparency = true; dirty = true; }
        if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
        if (dirty) imp.SaveAndReimport();
        return dirty;
    }
}
#endif
