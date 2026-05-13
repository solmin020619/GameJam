using UnityEngine;
using TMPro;

/// <summary>
/// AScene_Wait 의 캐릭터를 화면 안에서 천천히 좌우로 왔다갔다.
/// 머리 위에 이름 라벨도 표시.
/// </summary>
public class CharacterWander : MonoBehaviour
{
    [Header("이름")]
    public string displayName = "플레이어";

    [Header("Wander 영역 (월드 좌표)")]
    public float minX = -3f;
    public float maxX = 3f;
    public float minY = -2f;
    public float maxY = 2f;

    [Header("이동")]
    public float moveSpeed = 1.2f;
    [Tooltip("새 목적지 잡을 때까지 대기 시간 (잠깐 멈추는 효과)")]
    public float pauseMin = 0.4f;
    public float pauseMax = 1.5f;

    [Header("이름 라벨 (자동 생성)")]
    public Color labelColor = Color.white;
    [Tooltip("World Space TMP — 1~3 정도가 적당")]
    public float labelFontSize = 1f;
    public float labelYOffset = 0.9f;

    [Header("Sprite Layer (BG 뒤에 가려질 때 키워라)")]
    public int sortingOrder = 100;
    [Tooltip("비워두면 기본 sorting layer. BG 가 다른 layer 면 그것보다 위 layer 이름 입력")]
    public string sortingLayerName = "";

    Vector3 _target;
    float _pauseTimer;
    SpriteRenderer _sprite;
    TextMeshPro _nameLabel;
    Vector3 _spawnPos;

    void Start()
    {
        // Inspector 에 옛 값 (큰 fontSize) 저장돼 있으면 자동 보정
        if (labelFontSize > 3f) labelFontSize = 1f;
        if (labelYOffset > 2f) labelYOffset = 0.9f;

        _spawnPos = transform.position;
        _sprite = GetComponentInChildren<SpriteRenderer>();
        // 모든 자식 SpriteRenderer 의 sortingOrder + layer 강제
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingOrder = sortingOrder + sr.sortingOrder; // 기존 order 위에 더함 (캐릭터 부위별 상대 순서 유지)
            if (!string.IsNullOrEmpty(sortingLayerName))
                sr.sortingLayerName = sortingLayerName;
        }
        PickNewTarget();
        CreateNameLabel();
    }

    void Update()
    {
        if (_pauseTimer > 0f)
        {
            _pauseTimer -= Time.deltaTime;
            UpdateLabelPos();
            return;
        }

        // 목적지로 이동
        Vector3 toTarget = _target - transform.position;
        if (toTarget.sqrMagnitude < 0.05f)
        {
            _pauseTimer = Random.Range(pauseMin, pauseMax);
            PickNewTarget();
        }
        else
        {
            Vector3 dir = toTarget.normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
            // 좌우 뒤집기
            if (_sprite != null && Mathf.Abs(dir.x) > 0.01f)
                _sprite.flipX = dir.x < 0f;
        }

        UpdateLabelPos();
    }

    void PickNewTarget()
    {
        _target = new Vector3(
            _spawnPos.x + Random.Range(minX, maxX),
            _spawnPos.y + Random.Range(minY, maxY),
            transform.position.z);
    }

    void CreateNameLabel()
    {
        var go = new GameObject($"NameLabel_{displayName}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0, labelYOffset, 0);
        // 부모 (캐릭터) 가 scale 2.5 라면 라벨이 2.5배로 커지므로 보정 — 항상 1:1
        Vector3 parentScale = transform.localScale;
        if (parentScale.x != 0 && parentScale.y != 0)
            go.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f);

        _nameLabel = go.AddComponent<TextMeshPro>();
        _nameLabel.text = displayName;
        _nameLabel.fontSize = labelFontSize;
        _nameLabel.color = labelColor;
        _nameLabel.alignment = TextAlignmentOptions.Center;
        _nameLabel.fontStyle = FontStyles.Bold;
        _nameLabel.sortingOrder = 100;
        // 가로로 한 줄 — 글자별 줄바꿈 방지
        _nameLabel.enableWordWrapping = false;
        _nameLabel.overflowMode = TextOverflowModes.Overflow;
        _nameLabel.enableAutoSizing = false;
        var rt = _nameLabel.rectTransform;
        rt.sizeDelta = new Vector2(4f, 0.6f);
    }

    void UpdateLabelPos()
    {
        if (_nameLabel == null) return;
        // 캐릭터 flipX 영향 안 받게 — 부모가 따라가지만 label 자체 scale x 강제 정상
        var s = _nameLabel.transform.localScale;
        if (s.x < 0) _nameLabel.transform.localScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);
    }
}
