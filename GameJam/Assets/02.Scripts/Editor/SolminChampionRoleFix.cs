#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 솔민이 만든 06.SO 폴더의 ChampionSO 자산에 Role 필드가 비어있을 수 있어서
/// 이름으로 추정해 Role 을 채워줌. 한 번만 누르면 됨.
/// </summary>
public static class SolminChampionRoleFix
{
    const string SoFolder = "Assets/06.SO";

    [MenuItem("TFM/Fix Solmin's ChampionSO Roles")]
    public static void Fix()
    {
        if (!AssetDatabase.IsValidFolder(SoFolder))
        {
            Debug.LogWarning($"[TFM] {SoFolder} 폴더 없음. 솔민 작업 자산 없으면 패스해도 됨.");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:ChampionSO", new[] { SoFolder });
        int fixedCount = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<ChampionSO>(path);
            if (so == null) continue;

            var name = so.ChampionName ?? so.name;
            so.Role = GuessRole(name);
            EditorUtility.SetDirty(so);
            fixedCount++;
            Debug.Log($"[TFM] {so.name}: Role → {so.Role}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[TFM] Fixed Role on {fixedCount} ChampionSO assets.");
    }

    static ChampionRole GuessRole(string name)
    {
        var n = (name ?? "").ToLower();
        if (n.Contains("guard") || n.Contains("tank") || n.Contains("수호") || n.Contains("방패")) return ChampionRole.Tank;
        if (n.Contains("snip") || n.Contains("arch") || n.Contains("bow") || n.Contains("궁") || n.Contains("저격")) return ChampionRole.Marksman;
        if (n.Contains("mage") || n.Contains("wiz") || n.Contains("마법")) return ChampionRole.Mage;
        if (n.Contains("heal") || n.Contains("priest") || n.Contains("cleric") || n.Contains("힐") || n.Contains("성기")) return ChampionRole.Healer;
        // 기본: Berserker / 광전사 / 등 → Fighter
        return ChampionRole.Fighter;
    }
}
#endif
