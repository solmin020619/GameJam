#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class KoreanFontSetup
{
    const string TtfPath = "Assets/06.Fonts/Galmuri11.ttf";
    const string SdfPath = "Assets/06.Fonts/Galmuri11_SDF.asset";

    [MenuItem("TFM/Setup Korean Font (Galmuri11)")]
    public static void Setup()
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (ttf == null)
        {
            Debug.LogError($"[TFM] TTF not found at {TtfPath}");
            return;
        }

        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfPath);
        if (fontAsset == null)
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(
                ttf,
                32,                              // sampling point size
                9,                               // atlas padding
                GlyphRenderMode.SDFAA,
                1024, 1024,                      // atlas size
                AtlasPopulationMode.Dynamic,     // dynamic: 런타임에 한글 자동 채움
                true                             // multi atlas
            );
            AssetDatabase.CreateAsset(fontAsset, SdfPath);
            Debug.Log($"[TFM] Created TMP font asset at {SdfPath}");
        }

        // 1) TMP Settings의 Default Font Asset 변경 → 이후 생성되는 TMP 모두 한글 폰트 사용
        var settings = TMP_Settings.instance;
        if (settings != null)
        {
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultFontAsset");
            if (prop != null)
            {
                prop.objectReferenceValue = fontAsset;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
            }
        }

        // 2) 현재 씬 안의 모든 TMP 컴포넌트의 font 교체
        int countUI = 0, count3D = 0;
        foreach (var t in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            t.font = fontAsset;
            EditorUtility.SetDirty(t);
            countUI++;
        }
        foreach (var t in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            t.font = fontAsset;
            EditorUtility.SetDirty(t);
            count3D++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"[TFM] Galmuri11 applied. TMP UI: {countUI}, TMP 3D: {count3D}. Default font set.");
        Selection.activeObject = fontAsset;
    }
}
#endif
