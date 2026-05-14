#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// ashyun1 이 AScene/AScene_BanPick 에 만든 SettingUI / StopUI 를 우리 흐름 (Lobby / InGame) 으로 복사.
/// </summary>
public static class SettingsPauseInstaller
{
    const string AScenePath = "Assets/01.Scenes/AScene.unity";
    const string ABanPickPath = "Assets/01.Scenes/AScene_BanPick.unity";
    const string LobbyPath = "Assets/01.Scenes/Lobby.unity";
    const string InGamePath = "Assets/01.Scenes/InGame.unity";

    [MenuItem("TFM/Install SettingsUI to Lobby + StopUI to InGame")]
    public static void Install()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 1) AScene 에서 SettingUI 추출 → Lobby 로 복사
        // AScene 에 "Setting" 이름 GO 여러 개 있음 — sizeDelta 가장 큰 거가 panel
        bool settingDone = TryCopy(AScenePath, "Setting", LobbyPath, "SettingUI", typeof(SettingsMenuController));
        if (!settingDone) settingDone = TryCopy(AScenePath, "SettingUI", LobbyPath, "SettingUI", typeof(SettingsMenuController));
        if (!settingDone) Debug.LogWarning("[TFM] AScene 에서 SettingUI panel 못 찾음");

        // 2) AScene_BanPick 에서 StopUI 추출 → InGame 으로 복사
        CopyPanelBetweenScenes(ABanPickPath, "StopUI", InGamePath, "StopUI", addController: typeof(PauseMenuController));

        Debug.Log("[TFM] SettingUI → Lobby, StopUI → InGame 복사 완료. 인스펙터의 SettingsMenuController / PauseMenuController 확인.");
    }

    // 원본 GO 가 존재할 때만 복사 시도. 결과 — 성공/실패 bool 반환
    static bool TryCopy(string srcScenePath, string srcGoName, string dstScenePath, string dstGoName, System.Type addController)
    {
        var srcScene = EditorSceneManager.OpenScene(srcScenePath, OpenSceneMode.Single);
        if (!srcScene.IsValid()) return false;
        bool found = false;
        foreach (var root in srcScene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.gameObject.name == srcGoName) { found = true; break; }
            if (found) break;
        }
        if (!found) return false;
        CopyPanelBetweenScenes(srcScenePath, srcGoName, dstScenePath, dstGoName, addController);
        return true;
    }

    static void CopyPanelBetweenScenes(string srcScenePath, string srcGoName, string dstScenePath, string dstGoName, System.Type addController)
    {
        // 1) 원본 씬 열기
        var srcScene = EditorSceneManager.OpenScene(srcScenePath, OpenSceneMode.Single);
        if (!srcScene.IsValid()) { Debug.LogWarning($"[TFM] {srcScenePath} 못 엶"); return; }

        // 같은 이름 GO 가 여러 개 있을 수 있음 — sizeDelta 가 가장 큰 것 (panel 추정)
        GameObject src = null;
        float largestArea = 0f;
        foreach (var root in srcScene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.name != srcGoName) continue;
                var rt = t as RectTransform;
                if (rt == null) continue;
                float area = Mathf.Abs(rt.sizeDelta.x * rt.sizeDelta.y);
                if (area > largestArea) { largestArea = area; src = t.gameObject; }
            }
        }
        if (src == null) { Debug.LogWarning($"[TFM] {srcScenePath} 에 '{srcGoName}' 없음"); return; }
        Debug.Log($"[TFM] '{srcGoName}' panel 선택 — sizeDelta area={largestArea:F0}");

        // 2) 원본 GO 를 임시 clone (Editor 모드에선 DontDestroyOnLoad 못 씀 → hideFlags 사용)
        var tempClone = Object.Instantiate(src);
        tempClone.name = dstGoName;
        tempClone.hideFlags = HideFlags.HideAndDontSave; // 씬 전환에도 destroy 안 됨
        tempClone.SetActive(false);

        // 3) 대상 씬 열기
        var dstScene = EditorSceneManager.OpenScene(dstScenePath, OpenSceneMode.Single);
        if (!dstScene.IsValid()) { Debug.LogWarning($"[TFM] {dstScenePath} 못 엶"); Object.DestroyImmediate(tempClone); return; }

        // 기존에 같은 이름 GO 있으면 제거
        foreach (var root in dstScene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.name == dstGoName && t.gameObject != tempClone)
                {
                    Object.DestroyImmediate(t.gameObject);
                    break;
                }
            }
        }

        // Canvas 찾아서 자식으로 붙이기 + hideFlags 정상화 (씬에 저장되게)
        Canvas dstCanvas = Object.FindFirstObjectByType<Canvas>();
        if (dstCanvas != null)
        {
            tempClone.transform.SetParent(dstCanvas.transform, false);
        }
        else
        {
            tempClone.transform.SetParent(null);
        }
        tempClone.hideFlags = HideFlags.None;

        // ★ panel 정리:
        //   1. localScale = (1,1,1) — ashyun1 의 비대칭 scale (예 0.258, 0.549) 제거 → 가로:세로 비율 정상화
        //   2. anchor center + sizeDelta 원본
        //   3. 자식 Canvas + GraphicRaycaster — 부모 Canvas Scaler 영향 차단 + 클릭 받게
        var panelRt = tempClone.GetComponent<RectTransform>();
        if (panelRt != null)
        {
            Vector2 origSize = panelRt.sizeDelta;
            panelRt.localScale = Vector3.one; // ← 핵심: 비대칭 scale 제거
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = origSize;
            panelRt.anchoredPosition = Vector2.zero;
        }
        // 자체 Canvas + 1920x1080 — ashyun1 디자인 의도 그대로 표시
        var subCanvas = tempClone.GetComponent<UnityEngine.Canvas>();
        if (subCanvas == null) subCanvas = tempClone.AddComponent<UnityEngine.Canvas>();
        subCanvas.overrideSorting = true;
        subCanvas.sortingOrder = 100;
        var subScaler = tempClone.GetComponent<UnityEngine.UI.CanvasScaler>();
        if (subScaler == null) subScaler = tempClone.AddComponent<UnityEngine.UI.CanvasScaler>();
        subScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        subScaler.referenceResolution = new Vector2(1920, 1080);
        subScaler.matchWidthOrHeight = 0.5f;
        if (tempClone.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            tempClone.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        tempClone.SetActive(false);

        // Controller GO — 기존 있으면 삭제 후 재생성 (wire 다시 잡아야 함)
        if (addController != null)
        {
            string ctrlName = addController.Name;
            foreach (var existingGo in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existingGo.name == ctrlName) Object.DestroyImmediate(existingGo);
            }
            var ctrlGo = new GameObject(ctrlName);
            ctrlGo.AddComponent(addController);
            Debug.Log($"[TFM] {ctrlName} GO + 컴포넌트 추가 (재생성)");
        }

        EditorSceneManager.MarkSceneDirty(dstScene);
        EditorSceneManager.SaveScene(dstScene);
        Debug.Log($"[TFM] '{srcGoName}' → {dstScenePath} 복사 + {addController.Name} 추가");
    }
}
#endif
