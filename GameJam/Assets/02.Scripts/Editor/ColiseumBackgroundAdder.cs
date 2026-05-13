#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ColiseumBackgroundAdder
{
    const string MapFolder = "Assets/04.Images/Map/Sand Dungeon Pack by Captainskeleto";

    [MenuItem("TFM/Add Coliseum Background (Coliseum1) to Current Scene")]
    public static void Add1() => Add(1);

    [MenuItem("TFM/Add Coliseum Background (Coliseum2) to Current Scene")]
    public static void Add2() => Add(2);

    [MenuItem("TFM/Add Coliseum Background (Coliseum3) to Current Scene")]
    public static void Add3() => Add(3);

    [MenuItem("TFM/Add Coliseum Background (Coliseum4) to Current Scene")]
    public static void Add4() => Add(4);

    static void Add(int idx)
    {
        var path = $"{MapFolder}/Coliseum{idx}.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogError($"[TFM] Coliseum sprite 못 찾음: {path}");
            return;
        }

        // 기존 배경 제거 (재실행 시 덮어쓰기)
        var existing = GameObject.Find("Coliseum_Background");
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject("Coliseum_Background");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = -100;  // 모든 캐릭/UI 뒤

        // 카메라 시야에 꽉 차게 스케일
        var cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float worldHeight = cam.orthographicSize * 2f;
            float worldWidth = worldHeight * cam.aspect;
            float spriteH = sprite.bounds.size.y;
            float spriteW = sprite.bounds.size.x;
            float scale = Mathf.Max(worldWidth / spriteW, worldHeight / spriteH);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 5f);
        }
        else
        {
            go.transform.position = new Vector3(0, 0, 5f);
        }

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[TFM] Coliseum{idx} background added.");
    }
}
#endif
