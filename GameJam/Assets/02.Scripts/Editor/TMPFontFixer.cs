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

            // 손상된 1x1 atlas 클리어 — TMP API 로 다이나믹 데이터 리셋
            try
            {
                font.ClearFontAssetData(setAtlasSizeToZero: true);
                EditorUtility.SetDirty(font);
                fixedCount++;
                Debug.Log($"[TMP Fix] '{path}' atlas 클리어 완료 → 빌드 시 재생성됨");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TMP Fix] '{path}' 클리어 실패: {e.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TMP Fix] 완료 — Fix {fixedCount} 개, Static skip {skipped} 개");
        EditorUtility.DisplayDialog("TMP 폰트 수정 완료",
            $"Dynamic SDF 폰트 atlas {fixedCount} 개 클리어.\n빌드 시 자동 재생성.\n\n이제 다시 빌드 시도하세요.",
            "OK");
    }
}
#endif
