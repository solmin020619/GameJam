#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// map_transparent.png 임포트 설정 자동화 + 현재 씬에 콜라이더 맵 GameObject 자동 추가.
/// MapColliderBuilder.cs 가 요구하는 Sprite 설정 (PPU 100, Single, Full Rect) 보장.
/// </summary>
public static class MapInstaller
{
    const string MapPng = "Assets/04.Images/Map/map_transparent.png";
    const string JsonPath = "Assets/Resources/walkable_polygon.json";

    [MenuItem("TFM/Install Map (Sprite Import + Add To Scene)")]
    public static void Install()
    {
        if (AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath) == null)
        {
            Debug.LogError($"[TFM] {JsonPath} 못 찾음. Resources 폴더에 walkable_polygon.json 있는지 확인.");
            return;
        }

        // 1) Sprite 임포트 설정 강제
        var importer = AssetImporter.GetAtPath(MapPng) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[TFM] {MapPng} 못 찾음.");
            return;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        // Unity 6+ 에서는 TextureImporterSettings 통해 sprite mesh type 변경
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MapPng);
        if (sprite == null)
        {
            Debug.LogError($"[TFM] Sprite 로드 실패. 임포트 설정 다시 확인 필요.");
            return;
        }

        // 2) 현재 씬에 Map GameObject 추가
        var existing = GameObject.Find("MapCollider");
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject("MapCollider");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -50;  // 배경 위, 캐릭 아래

        var builder = go.AddComponent<MapColliderBuilder>();
        builder.jsonResourceName = "walkable_polygon";
        builder.buildWallCollider = true;
        builder.inwardPadding = 0f;
        builder.rebuildOnEnable = true;

        // 카메라 중심으로 위치
        var cam = Camera.main;
        if (cam != null)
            go.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[TFM] Map installed. Sprite imported (PPU=100) + MapColliderBuilder attached.");
    }
}
#endif
