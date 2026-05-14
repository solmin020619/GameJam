#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 05.Sounds/Char/ 의 mp3 들을 ChampionData asset 의 autoAttackSfx/basicSkillSfx/ultimateSfx 에 자동 wire.
/// 파일 이름: Guardian_hit/skill/ult.mp3 → Champion_Tank_01.
/// typo 처리: Celeric → Cleric (Healer), Swormman → Swordman (Duelist).
/// </summary>
public static class ChampionSoundWirer
{
    const string SoundDir = "Assets/05.Sounds/Char";
    const string ChampDir = "Assets/06.ScriptableObjects/Champions";

    // 챔프 prefix 매핑: 영어 prefix → ChampionRole
    static readonly (string prefix, ChampionRole role)[] Mapping = {
        ("Guardian",  ChampionRole.Tank),
        ("Berserker", ChampionRole.Fighter),
        ("Sniper",    ChampionRole.Marksman),
        ("Mage",      ChampionRole.Mage),
        ("Cleric",    ChampionRole.Healer),
        ("Celeric",   ChampionRole.Healer),    // typo
        ("Crusher",   ChampionRole.Disruptor),
        ("Horse",     ChampionRole.Skirmisher),
        ("Swordman",  ChampionRole.Duelist),
        ("Swormman",  ChampionRole.Duelist),   // typo
        ("Ninja",     ChampionRole.Assassin),
    };

    [MenuItem("TFM/Wire Champion Sounds From 05.Sounds/Char", priority = -44)]
    public static void Wire()
    {
        if (!AssetDatabase.IsValidFolder(SoundDir)) { Debug.LogWarning($"[TFM] {SoundDir} 없음"); return; }

        // Resources/BGM 의 mp3 들 Sprite 가 아니라 AudioClip 이므로 type 강제 불필요 (Unity 기본 AudioClip)
        // 단지 ChampionData 의 wire 만 처리

        // 1) 모든 ChampionData 로드, role 별 dict
        var roleToData = new System.Collections.Generic.Dictionary<ChampionRole, ChampionData>();
        foreach (var g in AssetDatabase.FindAssets("t:ChampionData", new[] { ChampDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var data = AssetDatabase.LoadAssetAtPath<ChampionData>(path);
            if (data != null) roleToData[data.role] = data;
        }

        // 2) 05.Sounds/Char/*.mp3 순회 → 파일명 분석 → 매핑
        int wired = 0;
        foreach (var file in Directory.GetFiles(SoundDir, "*.mp3"))
        {
            string name = Path.GetFileNameWithoutExtension(file); // "Guardian_hit"
            string assetPath = file.Replace('\\', '/');
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null) continue;

            // 매핑된 prefix 찾기
            foreach (var (prefix, role) in Mapping)
            {
                if (!name.StartsWith(prefix)) continue;
                if (!roleToData.TryGetValue(role, out var data)) continue;

                string suffix = name.Substring(prefix.Length).TrimStart('_').ToLower();
                bool changed = false;
                if (suffix == "hit")        { data.autoAttackSfx = clip; changed = true; }
                else if (suffix == "skill") { data.basicSkillSfx = clip; changed = true; }
                else if (suffix == "ult" || suffix == "ultimate") { data.ultimateSfx = clip; changed = true; }

                if (changed)
                {
                    EditorUtility.SetDirty(data);
                    wired++;
                    Debug.Log($"[Sound] {name} → {role} {suffix}");
                }
                break;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[TFM] Champion 사운드 wire 완료 — {wired} 개 매핑");
    }
}
#endif
