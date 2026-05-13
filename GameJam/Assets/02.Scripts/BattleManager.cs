using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("Spawn")]
    public Transform[] Team0SpawnPoints;   // 3 points
    public Transform[] Team1SpawnPoints;   // 3 points

    [Header("Champion Data (3 per team)")]
    public ChampionSO[] Team0Champions;    // size 3
    public ChampionSO[] Team1Champions;    // size 3

    [Header("Battle")]
    public float BattleDuration = 60f;

    [Header("UI")]
    public TextMeshProUGUI TimerText;
    public TextMeshProUGUI ResultText;
    public GameObject ResultPanel;
    public GameObject DamageTextPrefab;
    public Canvas WorldCanvas;

    private List<ChampionUnit> _team0 = new();
    private List<ChampionUnit> _team1 = new();

    private float _timer;
    private float _currentSpeed = 1f;
    private bool _isPaused;
    private bool _endingBattle;
    private int _speedIndex;

    private readonly float[] _speeds = { 1f, 2f, 3f };

    public bool IsBattleRunning { get; private set; }

    void Awake()
    {
        Instance = this;

        // CameraShake 자동 부착 (Inspector 작업 안 해도 작동)
        if (Camera.main != null && Camera.main.GetComponent<CameraShake>() == null)
            Camera.main.gameObject.AddComponent<CameraShake>();
    }

    void Start()
    {
        ResultPanel.SetActive(false);
        StartBattle();
    }

    void StartBattle()
    {
        _timer = BattleDuration;

        for (int i = 0; i < Team0Champions.Length; i++)
        {
            if (Team0Champions[i] == null || i >= Team0SpawnPoints.Length) continue;
            SpawnChampion(Team0Champions[i], teamId: 0, Team0SpawnPoints[i]);
        }

        for (int i = 0; i < Team1Champions.Length; i++)
        {
            if (Team1Champions[i] == null || i >= Team1SpawnPoints.Length) continue;
            SpawnChampion(Team1Champions[i], teamId: 1, Team1SpawnPoints[i]);
        }

        // 캐릭터끼리만 충돌 무시 → 미끄러짐 X / 벽엔 막힘
        var all = new List<ChampionUnit>();
        all.AddRange(_team0);
        all.AddRange(_team1);
        for (int i = 0; i < all.Count; i++)
        {
            var ci = all[i].GetComponent<Collider2D>();
            if (ci == null) continue;
            for (int j = i + 1; j < all.Count; j++)
            {
                var cj = all[j].GetComponent<Collider2D>();
                if (cj == null) continue;
                Physics2D.IgnoreCollision(ci, cj, true);
            }
        }

        IsBattleRunning = true;
        UpdateTimerUI();
    }

    void SpawnChampion(ChampionSO data, int teamId, Transform spawnPoint)
    {
        if (data == null) { Debug.LogWarning($"[BM] Team{teamId} 의 ChampionSO 가 null. 슬롯 비움."); return; }
        if (data.Prefab == null) { Debug.LogError($"[BM] '{data.ChampionName}' 의 Prefab 이 null. 풀 빌더 재실행 필요."); return; }
        if (spawnPoint == null) { Debug.LogError($"[BM] Team{teamId} spawnPoint 가 null. KScene SpawnPoints 와이어링 확인."); return; }

        var go = Instantiate(data.Prefab, spawnPoint.position, Quaternion.identity);
        var unit = go.GetComponent<ChampionUnit>();
        if (unit == null)
        {
            Debug.LogError($"[BM] '{data.ChampionName}' (prefab: {data.Prefab.name}) 에 ChampionUnit 컴포넌트 없음. TFM > Rebuild Champion Pool 메뉴 재실행 필요.");
            Destroy(go);
            return;
        }
        unit.Init(data, teamId);

        if (teamId == 0) _team0.Add(unit);
        else _team1.Add(unit);

        if (teamId == 1)
            go.transform.localScale = new Vector3(1, 1, 1);
    }

    void Update()
    {
        if (!IsBattleRunning) return;

        if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
        if (Input.GetKeyDown(KeyCode.Tab)) CycleSpeed();

        _timer -= Time.deltaTime;
        UpdateTimerUI();

        if (_timer <= 0f && !_endingBattle)
        {
            _endingBattle = true;
            StartCoroutine(EndBattleWithSlowmo(timeUp: true));
        }
    }

    void LateUpdate()
    {
        if (!IsBattleRunning) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var hit = Physics2D.OverlapPoint(worldPos);
        if (hit == null) return;

        var clicked = hit.GetComponent<ChampionUnit>();
        if (clicked == null || clicked.TeamId == 0) return;

        foreach (var ally in _team0)
            ally.PriorityTarget = clicked;
    }

    public List<ChampionUnit> GetEnemies(int myTeamId)
        => myTeamId == 0 ? _team1 : _team0;

    public List<ChampionUnit> GetAllies(int myTeamId)
        => myTeamId == 0 ? _team0 : _team1;

    public void OnUnitDied(ChampionUnit unit)
    {
        if (_endingBattle) return;

        int alive0 = _team0.FindAll(u => !u.IsDead).Count;
        int alive1 = _team1.FindAll(u => !u.IsDead).Count;

        // 마지막 한 마리가 죽는 순간 슬로우모션 + 결과 화면
        if (alive0 == 0 || alive1 == 0)
        {
            _endingBattle = true;
            StartCoroutine(EndBattleWithSlowmo(timeUp: false));
            return;
        }

        // 일반 사망 — 살짝 슬로우 (게임 정지 X)
        StartCoroutine(QuickSlowmo(0.45f, 0.12f));
    }

    IEnumerator QuickSlowmo(float scale, float realDuration)
    {
        Time.timeScale = scale * _currentSpeed;
        yield return new WaitForSecondsRealtime(realDuration);
        if (IsBattleRunning) Time.timeScale = _isPaused ? 0f : _currentSpeed;
    }

    IEnumerator EndBattleWithSlowmo(bool timeUp)
    {
        // 결정적 슬로우 모션
        Time.timeScale = 0.2f * _currentSpeed;
        yield return new WaitForSecondsRealtime(1.0f);
        Time.timeScale = 1f;
        EndBattle(timeUp);
    }

    void EndBattle(bool timeUp)
    {
        IsBattleRunning = false;
        Time.timeScale = 1f;

        int alive0 = _team0.FindAll(u => !u.IsDead).Count;
        int alive1 = _team1.FindAll(u => !u.IsDead).Count;

        string result;
        Color resultColor;
        if (alive0 > alive1) { result = "VICTORY!"; resultColor = new Color(1f, 0.9f, 0.3f); }
        else if (alive1 > alive0) { result = "DEFEAT"; resultColor = new Color(1f, 0.4f, 0.4f); }
        else { result = "DRAW"; resultColor = Color.white; }

        ResultPanel.SetActive(true);
        ResultText.text = $"{result}\nSurvivors: {alive0} vs {alive1}";
        ResultText.color = resultColor;

        // 결과 화면 등장 시 큰 카메라 셰이크
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.4f, 0.25f);
    }

    void TogglePause()
    {
        _isPaused = !_isPaused;
        Time.timeScale = _isPaused ? 0f : _currentSpeed;
    }

    void CycleSpeed()
    {
        _speedIndex = (_speedIndex + 1) % _speeds.Length;
        _currentSpeed = _speeds[_speedIndex];
        if (!_isPaused) Time.timeScale = _currentSpeed;
    }

    void UpdateTimerUI()
    {
        if (TimerText == null) return;
        TimerText.text = $"{Mathf.CeilToInt(_timer):00}";
    }

    // called by PickManager (teammate) to set teams before battle starts
    public void SetTeams(ChampionSO[] team0, ChampionSO[] team1)
    {
        Team0Champions = team0;
        Team1Champions = team1;
    }

    public void SpawnDamageText(Vector3 worldPos, float amount, bool isHeal)
    {
        SpawnDamageText(worldPos, amount, isHeal ? DamageType.Heal : DamageType.Basic);
    }

    public void SpawnDamageText(Vector3 worldPos, float amount, DamageType type)
    {
        if (DamageTextPrefab == null || WorldCanvas == null) return;

        var go = Instantiate(DamageTextPrefab, WorldCanvas.transform);
        var tmp = go.GetComponent<TextMeshProUGUI>();

        // 기획서 기반 색상/크기/굵기 분기
        string text;
        Color color;
        float fontSize;
        FontStyles style = FontStyles.Normal;
        switch (type)
        {
            case DamageType.Heal:
                text = $"+{Mathf.RoundToInt(amount)}";
                color = new Color(0.45f, 1f, 0.5f);
                fontSize = 28;
                break;
            case DamageType.Skill:
                text = $"{Mathf.RoundToInt(amount)}";
                color = new Color(1f, 0.4f, 0.4f);
                fontSize = 36;
                style = FontStyles.Bold;
                break;
            case DamageType.Ultimate:
                text = $"{Mathf.RoundToInt(amount)}";
                color = new Color(1f, 0.65f, 0.2f);
                fontSize = 44;
                style = FontStyles.Bold;
                break;
            case DamageType.Miss:
                text = "MISS";
                color = new Color(0.65f, 0.65f, 0.65f);
                fontSize = 24;
                break;
            case DamageType.Basic:
            default:
                text = $"{Mathf.RoundToInt(amount)}";
                color = Color.white;
                fontSize = 28;
                break;
        }

        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;

        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            WorldCanvas.GetComponent<RectTransform>(),
            screenPos, WorldCanvas.worldCamera,
            out Vector2 localPos);

        // 같은 위치 겹침 방지: X 축 ±10px 랜덤 오프셋
        localPos.x += Random.Range(-10f, 10f);
        go.GetComponent<RectTransform>().localPosition = localPos;

        StartCoroutine(FloatAndFade(go, tmp));
    }

    IEnumerator FloatAndFade(GameObject go, TextMeshProUGUI tmp)
    {
        float t = 0f;
        var rect = go.GetComponent<RectTransform>();
        Vector2 startPos = rect.localPosition;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.8f;
            rect.localPosition = startPos + Vector2.up * (t * 60f);
            tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 1f - t);
            yield return null;
        }

        Destroy(go);
    }
}