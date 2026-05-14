using UnityEngine;
using TMPro;

/// <summary>
/// TMP 텍스트의 글자별 vertex 를 위에서 아래로 떨어뜨리는 애니메이션.
/// 부착하면 Awake 시점에 자동 시작.
/// 시작 전에는 vertex alpha=0 으로 숨김 — 다른 텍스트와 겹쳐 보이는 문제 해결.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class DropTextAnimator : MonoBehaviour
{
    [Header("애니메이션 파라미터")]
    public float charInterval = 0.25f;     // 글자별 시작 간격
    public float dropDuration = 0.8f;      // 각 글자 떨어지는 시간
    public float fallHeight = 120f;        // 위에서 시작하는 높이 (픽셀)
    public float startDelay = 0f;          // 전체 시작 지연

    TextMeshProUGUI _tmp;
    float _elapsed;
    int _charCount;
    bool _done;
    bool _meshCaptured;
    Vector3[][] _baseVerts;        // 글자별 원본 vertex 캐시
    Color32[][] _baseColors;       // 글자별 원본 color 캐시

    void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
        _tmp.ForceMeshUpdate();
        _charCount = _tmp.textInfo.characterCount;
        _elapsed = -startDelay;
    }

    void CaptureBase()
    {
        var ti = _tmp.textInfo;
        _baseVerts = new Vector3[ti.meshInfo.Length][];
        _baseColors = new Color32[ti.meshInfo.Length][];
        for (int m = 0; m < ti.meshInfo.Length; m++)
        {
            var srcV = ti.meshInfo[m].vertices;
            _baseVerts[m] = new Vector3[srcV.Length];
            System.Array.Copy(srcV, _baseVerts[m], srcV.Length);

            var srcC = ti.meshInfo[m].colors32;
            _baseColors[m] = new Color32[srcC.Length];
            System.Array.Copy(srcC, _baseColors[m], srcC.Length);
        }
        _meshCaptured = true;
    }

    void LateUpdate()
    {
        if (_done) return;
        _elapsed += Time.deltaTime;

        if (!_meshCaptured)
        {
            _tmp.ForceMeshUpdate();
            CaptureBase();
        }

        var textInfo = _tmp.textInfo;
        bool allDone = true;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var ci = textInfo.characterInfo[i];
            if (!ci.isVisible) continue;

            int mi = ci.materialReferenceIndex;
            int vi = ci.vertexIndex;
            var verts  = textInfo.meshInfo[mi].vertices;
            var cols   = textInfo.meshInfo[mi].colors32;
            var baseV  = _baseVerts[mi];
            var baseC  = _baseColors[mi];

            float charStart = i * charInterval;
            float t = Mathf.Clamp01((_elapsed - charStart) / dropDuration);
            if (t < 1f) allDone = false;

            // ease out — 빠르게 시작 → 천천히 도착
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float yOff = (1f - eased) * fallHeight;

            // 시작 전엔 완전히 숨김 (vertex alpha=0)
            bool beforeStart = _elapsed < charStart;
            // 시작 직후 짧게 fade-in (0.1s)
            float fadeIn = beforeStart ? 0f : Mathf.Clamp01((_elapsed - charStart) / 0.1f);

            for (int v = 0; v < 4; v++)
            {
                verts[vi + v] = baseV[vi + v] + new Vector3(0, yOff, 0);

                Color32 baseCol = baseC[vi + v];
                byte newA = (byte)(baseCol.a * fadeIn);
                cols[vi + v] = new Color32(baseCol.r, baseCol.g, baseCol.b, newA);
            }
        }

        // mesh apply
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            var mesh = textInfo.meshInfo[i].mesh;
            mesh.vertices = textInfo.meshInfo[i].vertices;
            mesh.colors32 = textInfo.meshInfo[i].colors32;
            _tmp.UpdateGeometry(mesh, i);
        }

        if (allDone) _done = true;
    }
}
