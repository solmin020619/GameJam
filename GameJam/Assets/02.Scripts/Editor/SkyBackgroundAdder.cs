#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// background_sky.png 를 현재 씬에 배경(가장 뒤)으로 추가.
/// 콜로세움보다 뒤에 깔리도록 sortingOrder = -100.
/// 카메라 시야 + 양옆 여유 있게 가로 1.5배로 깔아서 가로 잘림 자연스럽게 처리.
/// </summary>
public static class SkyBackgroundAdder
{
    const string SkyPath = "Assets/04.Images/Map/background_sky.png";

    [MenuItem("TFM/Add Sky Background to Current Scene")]
    public static void Add()
    {
        // Sprite 임포트 설정 강제
        var importer = AssetImporter.GetAtPath(SkyPath) as TextureImporter;
        if (importer != null)
        {
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (Mathf.Abs(importer.spritePixelsPerUnit - 100f) > 0.1f) { importer.spritePixelsPerUnit = 100f; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; dirty = true; }
            if (dirty) importer.SaveAndReimport();
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SkyPath);
        if (sprite == null)
        {
            Debug.LogError($"[TFM] Sky sprite 못 찾음: {SkyPath}");
            return;
        }

        // 기존 Sky_Background 제거 (재실행 시 덮어쓰기)
        var existing = GameObject.Find("Sky_Background");
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject("Sky_Background");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = -100;  // 콜로세움(-50)보다 뒤

        // 카메라 시야 + 여유로 스케일 (가로 1.4배 여유로 양 옆 자름 자연스럽게)
        var cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float worldHeight = cam.orthographicSize * 2f;
            float worldWidth = worldHeight * cam.aspect;
            float spriteH = sprite.bounds.size.y;
            float spriteW = sprite.bounds.size.x;
            // 세로는 정확히 채우고, 가로는 sprite 가로비율에 맞춰 확장 (가로 짤림 자연)
            float scaleY = worldHeight / spriteH;
            float scaleX = scaleY;  // 비율 유지 (sprite 가로가 충분히 길어 양옆 자연스럽게 잘림)
            // 만약 sprite 가로가 너무 짧으면 카메라 가로 폭에 맞춤
            if (scaleX * spriteW < worldWidth)
                scaleX = worldWidth / spriteW * 1.05f;  // 5% 여유
            go.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            go.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 10f);
        }
        else
        {
            go.transform.position = new Vector3(0, 0, 10f);
        }

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[TFM] Sky background added (sortingOrder=-100, 콜로세움 뒤).");
    }
}
#endif
