#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class KoreanFontSetup
{
    const string TtfPath = "Assets/06.Fonts/Galmuri11.ttf";
    const string SdfPath = "Assets/06.Fonts/Galmuri11_Dynamic_SDF.asset";

    [MenuItem("TFM/Setup Korean Font (Galmuri11)")]
    public static void Setup()
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (ttf == null)
        {
            Debug.LogError($"[TFM] TTF 못 찾음: {TtfPath}");
            return;
        }

        // 기존 dynamic 자산 있으면 삭제 (안정성)
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfPath) != null)
            AssetDatabase.DeleteAsset(SdfPath);

        // 1) 코드로 Dynamic OS 폰트 자산 새로 생성
        //    Dynamic OS = atlas 에 없는 글자를 런타임에 OS 시스템 폰트에서 자동 보충
        //    + atlas texture 가 readable 로 만들어짐 → "make readable" 경고 사라짐
        var fontAsset = TMP_FontAsset.CreateFontAsset(
            ttf,
            32,                              // sampling point size
            9,                               // atlas padding
            GlyphRenderMode.SDFAA,
            2048, 2048,                      // atlas size (한글용 큰 텍스처)
            AtlasPopulationMode.Dynamic,     // ttf 글리프 atlas 에 런타임 추가 (readable 보장)
            true                             // multi atlas
        );
        AssetDatabase.CreateAsset(fontAsset, SdfPath);

        // atlas texture + material 을 sub-asset 으로 저장 (직렬화 후 reference 안 깨짐)
        if (fontAsset.atlasTexture != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(fontAsset.atlasTexture)))
        {
            fontAsset.atlasTexture.name = "Galmuri11 Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        }
        if (fontAsset.material != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(fontAsset.material)))
        {
            fontAsset.material.name = "Galmuri11 Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }
        AssetDatabase.ImportAsset(SdfPath);
        Debug.Log($"[TFM] Created Dynamic OS font asset at {SdfPath}");

        // 2) TMP Settings 의 Default Font Asset 변경
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

        // 3) 현재 씬 안의 모든 TMP 컴포넌트의 font 교체
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

        Debug.Log($"[TFM] Dynamic OS font applied. TMP UI: {countUI}, TMP 3D: {count3D}. Default font set.");
        Selection.activeObject = fontAsset;
    }
}
#endif
