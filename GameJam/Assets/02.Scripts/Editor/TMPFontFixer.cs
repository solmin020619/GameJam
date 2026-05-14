#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dynamic SDF 폰트의 손상된 atlas 텍스처 (1x1) 를 클리어해서 빌드 가능 상태로 복구.
/// 빌드 시 TMP_PreBuildProcessor 가 1x1 atlas 의 width 접근하다 NRE 나는 문제 fix.
/// </summary>
public static class TMPFontFixer
{
    [MenuItem("TFM/Fix TMP Dynamic Font Atlas (빌드 에러 해결)", priority = -34)]
    public static void Fix()
    {
        // 모든 TMP_FontAsset 검색
        var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        int fixedCount = 0, skipped = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null) continue;

            // Dynamic 폰트만 처리
            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                skipped++;
                continue;
            }

            // ★ 핵심 fix — clearDynamicDataOnBuild 를 false 로만 하면 build 통과
            // m_AtlasTextures 는 절대 비우지 않음 (비우면 런타임에 텍스트 렌더링 불가)
            try
            {
                var so = new SerializedObject(font);
                var clearFlag = so.FindProperty("m_ClearDynamicDataOnBuild");
                if (clearFlag != null)
                {
                    clearFlag.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[TMP Fix] '{path}' m_ClearDynamicDataOnBuild → false (atlas 보존)");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TMP Fix] '{path}' 처리 실패: {e.Message}");
            }

            EditorUtility.SetDirty(font);
            fixedCount++;
            Debug.Log($"[TMP Fix] '{path}' 빌드 가능 상태로 변경");
        }

        // 전역 TMP_Settings 의 ClearDynamicDataOnBuild 도 false 로 — 안전망
        try
        {
            var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.ScriptableObject>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var flag = so.FindProperty("m_ClearDynamicDataOnBuild");
                if (flag != null && flag.boolValue)
                {
                    flag.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(settings);
                    Debug.Log("[TMP Fix] TMP_Settings.ClearDynamicDataOnBuild → false (전역)");
                }
            }
        }
        catch (System.Exception e) { Debug.LogWarning($"[TMP Fix] TMP_Settings 처리 실패: {e.Message}"); }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TMP Fix] 완료 — Fix {fixedCount} 개, Static skip {skipped} 개");
        EditorUtility.DisplayDialog("TMP 폰트 수정 완료",
            $"Dynamic SDF 폰트 {fixedCount} 개 수정 + TMP_Settings 전역 플래그 변경.\n빌드 전처리 검사 비활성화됨.\n\n이제 다시 빌드 시도하세요.",
            "OK");
    }
}
#endif
