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
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { Debug.LogWarning($"[TFM] {path} 없음"); continue; }

            bool dirty = false;
            if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; dirty = true; }
            if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (Mathf.Abs(imp.spritePixelsPerUnit - 100f) > 0.1f) { imp.spritePixelsPerUnit = 100f; dirty = true; }
            if (imp.filterMode != FilterMode.Bilinear) { imp.filterMode = FilterMode.Bilinear; dirty = true; }
            if (imp.alphaIsTransparency != true) { imp.alphaIsTransparency = true; dirty = true; }
            if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (dirty) { imp.SaveAndReimport(); fixedCount++; }
        }
        Debug.Log($"[TFM] Role Icons import 보정 완료 — {fixedCount}/{Names.Length}");
    }
}
#endif
