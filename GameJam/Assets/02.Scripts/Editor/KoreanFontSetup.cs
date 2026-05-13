#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

public static class KoreanFontSetup
{
    const string FontsFolder = "Assets/06.Fonts";

    [MenuItem("TFM/Setup Korean Font (Galmuri11)")]
    public static void Setup()
    {
        // 06.Fonts 폴더에서 atlas 텍스처가 살아있는 TMP 폰트 자산을 자동 검색.
        // (GUI Font Asset Creator 로 만든 정상 자산을 우선 사용)
        var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontsFolder });
        TMP_FontAsset fontAsset = null;
        string pickedPath = null;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fa == null) continue;
            if (fa.atlasTexture == null) continue;  // 깨진 자산은 건너뜀

            fontAsset = fa;
            pickedPath = path;
            break;
        }

        if (fontAsset == null)
        {
            Debug.LogError($"[TFM] {FontsFolder} 안에 정상 TMP 폰트 자산이 없습니다. " +
                           "Window → TextMeshPro → Font Asset Creator 로 먼저 만들어 주세요.");
            return;
        }

        // 1) TMP Settings의 Default Font Asset 변경 → 이후 생성되는 TMP 모두 이 폰트 사용
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

        Debug.Log($"[TFM] '{pickedPath}' 폰트 적용. TMP UI: {countUI}, TMP 3D: {count3D}. Default font set.");
        Selection.activeObject = fontAsset;
    }
}
#endif
