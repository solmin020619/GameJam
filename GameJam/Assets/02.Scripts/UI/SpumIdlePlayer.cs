using UnityEngine;

/// <summary>
/// SPUM_Prefabs 가진 GO 의 idle 애니메이션 자동 재생.
/// OverrideControllerInit 을 먼저 호출하고 IDLE_List 가 비어있지 않을 때만 PlayAnimation 호출.
/// </summary>
public class SpumIdlePlayer : MonoBehaviour
{
    void Start()
    {
        var spum = GetComponentInChildren<SPUM_Prefabs>(true);
        if (spum == null) return;

        // Animator 가 없으면 PlayAnimation 자체가 NRE — 직접 찾아서 wire
        if (spum._anim == null) spum._anim = spum.GetComponentInChildren<Animator>(true);
        if (spum._anim == null || spum._anim.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[SpumIdle] '{spum.name}' Animator/Controller 없음 — idle 스킵");
            return;
        }

        // StateAnimationPairs 가 비어있으면 Init 호출
        if (spum.StateAnimationPairs == null || spum.StateAnimationPairs.Count == 0)
        {
            try { spum.OverrideControllerInit(); }
            catch (System.Exception e) { Debug.LogWarning($"[SpumIdle] OverrideControllerInit 실패: {e.Message}"); return; }
        }

        // 그래도 IDLE 키가 없거나 비어있으면 스킵
        if (!spum.StateAnimationPairs.ContainsKey("IDLE") || spum.StateAnimationPairs["IDLE"].Count == 0)
        {
            // IDLE_List 직접 확인 — PopulateAnimationLists 가 안 돈 프리팹일 수 있음
            if (spum.IDLE_List == null || spum.IDLE_List.Count == 0 || spum.IDLE_List[0] == null)
            {
                Debug.LogWarning($"[SpumIdle] '{spum.name}' IDLE 클립 없음 — 스킵");
                return;
            }
            // IDLE_List 는 있는데 dict 에 안 들어간 케이스
            spum.StateAnimationPairs["IDLE"] = spum.IDLE_List;
        }

        try { spum.PlayAnimation(PlayerState.IDLE, 0); }
        catch (System.Exception e) { Debug.LogWarning($"[SpumIdle] PlayAnimation 실패: {e.Message}"); }
    }
}
