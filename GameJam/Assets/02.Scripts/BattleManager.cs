using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("Spawn")]
    public Transform[] Team0SpawnPoints;
    public Transform[] Team1SpawnPoints;

    [Header("Champion Data (replaced by PickManager later)")]
    public ChampionSO Team0ChampData;
    public ChampionSO Team1ChampData;

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
    private int _speedIndex;

    private readonly float[] _speeds = { 1f, 2f, 3f };

    public bool IsBattleRunning { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ResultPanel.SetActive(false);
        StartBattle();
    }

    void StartBattle()
    {
        _timer = BattleDuration;

        SpawnChampion(Team0ChampData, teamId: 0, Team0SpawnPoints[0]);
        SpawnChampion(Team1ChampData, teamId: 1, Team1SpawnPoints[0]);

        IsBattleRunning = true;
        UpdateTimerUI();
    }

    void SpawnChampion(ChampionSO data, int teamId, Transform spawnPoint)
    {
        var go = Instantiate(data.Prefab, spawnPoint.position, Quaternion.identity);
        var unit = go.GetComponent<ChampionUnit>();
        unit.Init(data, teamId);

        if (teamId == 0) _team0.Add(unit);
        else _team1.Add(unit);

        // flip enemy sprites to face left
        if (teamId == 1)
            go.transform.localScale = new Vector3(-1, 1, 1);
    }

    void Update()
    {
        if (!IsBattleRunning) return;

        if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
        if (Input.GetKeyDown(KeyCode.Tab)) CycleSpeed();

        _timer -= Time.deltaTime;
        UpdateTimerUI();

        if (_timer <= 0f) EndBattle(timeUp: true);
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
        int alive0 = _team0.FindAll(u => !u.IsDead).Count;
        int alive1 = _team1.FindAll(u => !u.IsDead).Count;

        if (alive0 == 0 || alive1 == 0)
            EndBattle(timeUp: false);
    }

    void EndBattle(bool timeUp)
    {
        IsBattleRunning = false;
        Time.timeScale = 1f;

        int alive0 = _team0.FindAll(u => !u.IsDead).Count;
        int alive1 = _team1.FindAll(u => !u.IsDead).Count;

        string result;
        if (alive0 > alive1) result = "VICTORY!";
        else if (alive1 > alive0) result = "DEFEAT";
        else result = "DRAW";

        ResultPanel.SetActive(true);
        ResultText.text = $"{result}\nSurvivors: {alive0} vs {alive1}";
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

    public void SpawnDamageText(Vector3 worldPos, float amount, bool isHeal)
    {
        if (DamageTextPrefab == null || WorldCanvas == null) return;

        var go = Instantiate(DamageTextPrefab, WorldCanvas.transform);
        var tmp = go.GetComponent<TextMeshProUGUI>();

        tmp.text = isHeal ? $"+{Mathf.RoundToInt(amount)}" : $"{Mathf.RoundToInt(amount)}";
        tmp.color = isHeal ? Color.green : Color.white;

        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            WorldCanvas.GetComponent<RectTransform>(),
            screenPos, WorldCanvas.worldCamera,
            out Vector2 localPos);

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