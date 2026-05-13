using System.Collections;
using UnityEngine;

/// <summary>
/// Main Camera 에 부착하면 작동.
/// 사망/큰 피해 시 BattleManager 등에서 Instance.Shake(...) 호출.
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    Vector3 _origin;
    Coroutine _routine;

    void Awake()
    {
        Instance = this;
        _origin = transform.localPosition;
    }

    /// <param name="duration">실시간 흔드는 시간 (Time.unscaledDeltaTime 기준)</param>
    /// <param name="amplitude">흔드는 폭 (월드 단위)</param>
    public void Shake(float duration, float amplitude)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShakeRoutine(duration, amplitude));
    }

    IEnumerator ShakeRoutine(float duration, float amplitude)
    {
        float t = 0f;
        Vector3 baseline = transform.localPosition;  // 매번 갱신 (카메라가 다른 곳으로 이동했을 수도)
        while (t < duration)
        {
            float falloff = 1f - (t / duration);
            transform.localPosition = baseline + (Vector3)(Random.insideUnitCircle * amplitude * falloff);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        transform.localPosition = baseline;
        _routine = null;
    }
}
