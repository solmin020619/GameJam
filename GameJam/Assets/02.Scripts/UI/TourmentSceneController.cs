using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tourment 씬 — 진입 후 N 초 자동으로 AScene_BanPick 로 이동.
/// </summary>
public class TourmentSceneController : MonoBehaviour
{
    [Header("자동 전환")]
    public float autoNextSeconds = 5f;
    public string nextScene = "AScene_BanPick";

    void Start()
    {
        Debug.Log($"[Tourment] 씬 진입 — {autoNextSeconds}초 후 '{nextScene}' 로 자동 이동");
        StartCoroutine(GoNextRoutine());
    }

    IEnumerator GoNextRoutine()
    {
        yield return new WaitForSeconds(autoNextSeconds);
        // AScene_BanPick 없으면 BanPick fallback
        string target = nextScene;
        if (!Application.CanStreamedLevelBeLoaded(target))
            target = Application.CanStreamedLevelBeLoaded("BanPick") ? "BanPick" : "Lobby";
        SceneManager.LoadScene(target);
    }
}
