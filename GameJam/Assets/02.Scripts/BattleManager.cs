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

    // 킬 점수
    public int Team0Kills { get; private set; }
    public int Team1Kills { get; private set; }

    private readonly float[] _speeds = { 1f, 2f, 3f };

    public bool IsBattleRunning { get; private set; }

    void Awake()
    {
        // AScene_FightUI 가 Additive 로 올라올 때 그 안의 BattleManager 중복 차단
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[BM] 중복 BattleManager 발견 — '{gameObject.name}' (씬: {gameObject.scene.name}) 파괴. KScene 의 BM 만 사용.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // CameraShake 자동 부착 (Inspector 작업 안 해도 작동)
        if (Camera.main != null && Camera.main.GetComponent<CameraShake>() == null)
            Camera.main.gameObject.AddComponent<CameraShake>();
    }

    void Start()
    {
        ResultPanel.SetActive(false);

        // 셀프 주입 — Bridge 콜백 타이밍 의존 안 하고 여기서 직접 PickResult 흡수
        if (PickResult.AllyPicks != null && PickResult.AllyPicks.Count > 0 &&
            PickResult.EnemyPicks != null && PickResult.EnemyPicks.Count > 0)
        {
            var t0 = BuildFromPicks(PickResult.AllyPicks);
            var t1 = BuildFromPicks(PickResult.EnemyPicks);
            SetTeams(t0, t1);
            Debug.Log($"[BM] PickResult 셀프 주입 — Team0:{t0.Length}, Team1:{t1.Length}");
        }
        else
        {
            Debug.LogWarning(
                "[BM] PickResult 비어 있음 — HScene 거치지 않고 KScene 으로 직접 진입한 듯. " +
                "메뉴 'TFM/▶ Play From HScene' 으로 시작하면 밴픽 결과가 자동 주입돼.");
        }

        StartBattle();
    }

    static ChampionSO[] BuildFromPicks(System.Collections.Generic.List<ChampionData> picks)
    {
        var arr = new ChampionSO[picks.Count];
        for (int i = 0; i < picks.Count; i++)
        {
            var d = picks[i];
            var so = ScriptableObject.CreateInstance<ChampionSO>();
            so.ChampionName = d.displayName;
            so.Icon = d.portrait;
            so.KillIcon = d.killIcon;
            so.KillIconZoom = d.killIconZoom;
            so.KillIconOffset = d.killIconOffset;
            so.Prefab = d.unitPrefab;
            so.Role = d.role;
            so.MaxHp = d.maxHealth;
            so.AttackDamage = d.attackDamage;
            so.AttackSpeed = d.attackSpeed;
            so.AttackRange = d.attackRange;
            so.Defense = d.defense;
            so.MoveSpeed = d.moveSpeed;
            so.BasicSkillName = d.basicSkillName;
            so.BasicSkillCooldown = d.basicSkillCooldown;
            so.BasicSkillIcon = d.basicSkillIcon;
            so.UltimateName = d.ultimateName;
            so.UltimateCooldown = d.ultimateCooldown;
            so.UltimateIcon = d.ultimateIcon;
            arr[i] = so;
        }
        return arr;
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

        // ChampionUnit 없으면 런타임에 자동 추가 (풀 재빌드 누락 대비)
        var unit = go.GetComponent<ChampionUnit>();
        if (unit == null)
        {
            Debug.LogWarning($"[BM] '{data.ChampionName}' (prefab: {data.Prefab.name}) 에 ChampionUnit 없음 → 런타임 자동 추가");
            unit = go.AddComponent<ChampionUnit>();
        }
        // Rigidbody2D 자동 보강
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        // Collider2D 자동 보강
        if (go.GetComponent<Collider2D>() == null)
        {
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 1.4f);
            col.offset = new Vector2(0f, 0.7f);
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

    public void OnUnitDied(ChampionUnit killer, ChampionUnit victim)
    {
        if (_endingBattle) return;

        // 킬 카운트 (killer 팀이 +1)
        if (killer != null && !killer.IsDead)
        {
            if (killer.TeamId == 0) Team0Kills++;
            else Team1Kills++;
        }

        // 킬로그
        if (KillLogUI.Instance != null) KillLogUI.Instance.AddEntry(killer, victim);
        // 킬 점수 UI 갱신
        if (KillScoreUI.Instance != null) KillScoreUI.Instance.Refresh(Team0Kills, Team1Kills);
        // 아군 사망 시 빨간 비네트
        if (victim != null && victim.TeamId == 0 && DamageVignette.Instance != null)
            DamageVignette.Instance.Flash();

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

        int winnerTeam = -1;
        if (alive0 > alive1) winnerTeam = 0;
        else if (alive1 > alive0) winnerTeam = 1;
        // DRAW 면 winnerTeam = -1, 점수 안 올라감

        // 세트 결과 기록 — 3판 2선
        if (winnerTeam >= 0) MatchResult.AddWin(winnerTeam);

        string result;
        Color resultColor;
        if (winnerTeam == 0) { result = "VICTORY!"; resultColor = new Color(1f, 0.9f, 0.3f); }
        else if (winnerTeam == 1) { result = "DEFEAT"; resultColor = new Color(1f, 0.4f, 0.4f); }
        else { result = "DRAW"; resultColor = Color.white; }

        ResultPanel.SetActive(true);
        ResultText.text = $"{result}\nSurvivors: {alive0} vs {alive1}\nSet: {MatchResult.team0Wins} - {MatchResult.team1Wins}";
        ResultText.color = resultColor;

        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.4f, 0.25f);

        // 다음 씬으로 자동 전환 (2초 후)
        StartCoroutine(NextSetOrVictory());
    }

    IEnumerator NextSetOrVictory()
    {
        yield return new WaitForSecondsRealtime(2.0f);

        if (MatchResult.IsMatchOver)
        {
            // 최종 우승 — Victory 씬 (나중에 만들 예정. 우선 Lobby 로 fallback)
            int winner = MatchResult.WinningTeam;
            Debug.Log($"[BM] 매치 종료 — Team{winner} 우승. 우승씬 로드 (없으면 Lobby fallback)");
            string victoryScene = "Victory"; // 사용자가 나중에 만들 씬
            if (Application.CanStreamedLevelBeLoaded(victoryScene))
                UnityEngine.SceneManagement.SceneManager.LoadScene(victoryScene);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
            // 다음 매치 위해 점수 reset (우승씬 본 후 다시 시작 가능)
            MatchResult.Clear();
        }
        else
        {
            // 다음 세트 — BanPick 로 다시
            Debug.Log($"[BM] 세트 종료 — 점수 {MatchResult.team0Wins}:{MatchResult.team1Wins}. 다음 세트 BanPick 로 이동");
            PickResult.Clear(); // 새 밴픽 위해
            UnityEngine.SceneManagement.SceneManager.LoadScene("BanPick");
        }
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

        // 마지막 10초 빨간 깜빡
        if (_timer > 0f && _timer <= 10f)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 8f) + 1f) * 0.5f;  // 0~1
            TimerText.color = Color.Lerp(new Color(1f, 0.4f, 0.4f), new Color(1f, 0.9f, 0.2f), pulse);
        }
        else
        {
            TimerText.color = Color.white;
        }
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