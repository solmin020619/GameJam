#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 현재 씬의 BattleManager 에 3v3 SpawnPoints 6개를 자동 배치하고 와이어링.
/// 카메라 위치 기준으로 왼쪽 3개(Team0) + 오른쪽 3개(Team1).
/// </summary>
public static class BattleSceneSetup
{
    [MenuItem("TFM/Setup 3v3 SpawnPoints in Current Scene")]
    public static void Setup()
    {
        var bm = Object.FindFirstObjectByType<BattleManager>();
        if (bm == null)
        {
            EditorUtility.DisplayDialog("BattleManager 없음",
                "현재 씬에 BattleManager 가 없습니다. KScene 열고 다시 실행.", "OK");
            return;
        }

        // 기존 SpawnPoints 컨테이너 제거
        var oldRoot = GameObject.Find("BattleSpawnPoints");
        if (oldRoot != null) Object.DestroyImmediate(oldRoot);

        var root = new GameObject("BattleSpawnPoints");

        // 카메라 위치 기준
        var cam = Camera.main;
        Vector3 center = cam != null ? cam.transform.position : Vector3.zero;
        center.z = 0;

        float spacing = 1.5f;
        float halfSpread = 4f;  // 양 팀 사이 거리

        var team0 = new Transform[3];
        var team1 = new Transform[3];

        for (int i = 0; i < 3; i++)
        {
            float y = center.y + (i - 1) * spacing;  // -1, 0, +1 * spacing

            var go0 = new GameObject($"Team0_Spawn_{i}");
            go0.transform.SetParent(root.transform);
            go0.transform.position = new Vector3(center.x - halfSpread, y, 0);
            team0[i] = go0.transform;

            var go1 = new GameObject($"Team1_Spawn_{i}");
            go1.transform.SetParent(root.transform);
            go1.transform.position = new Vector3(center.x + halfSpread, y, 0);
            team1[i] = go1.transform;
        }

        bm.Team0SpawnPoints = team0;
        bm.Team1SpawnPoints = team1;

        EditorUtility.SetDirty(bm);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Selection.activeGameObject = root;
        Debug.Log($"[TFM] 3v3 SpawnPoints setup done. Team0: 왼쪽, Team1: 오른쪽 (카메라 중심 기준 좌우 {halfSpread} 거리).");
    }
}
#endif
