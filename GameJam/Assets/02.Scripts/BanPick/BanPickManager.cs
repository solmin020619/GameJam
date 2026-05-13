using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BanPickManager : MonoBehaviour
{
    public static BanPickManager Instance;

    [Header("Config")]
    public BanPickConfig config;

    [Header("Refs")]
    public BanPickHUD hud;
    public ChampionCardUI[] cards;
    public TeamSlotUI allySlots;
    public TeamSlotUI enemySlots;
    public BanPickAI ai;

    [Header("Events")]
    public bool autoLoadBattleSceneOnDone = false;
    public string battleSceneName = "Main";

    [Header("Sound (드래그앤드롭)")]
    public AudioClip banSfx;
    public AudioClip pickSfx;
    public float sfxVolume = 0.9f;

    [Header("BGM (드래그앤드롭 — 루프 재생)")]
    public AudioClip banPickBgm;
    [Range(0f, 1f)] public float bgmVolume = 0.5f;
    AudioSource _bgmSrc;

    // ---- runtime state ----
    public BanPickPhase Phase { get; private set; } = BanPickPhase.Idle;
    public int CurrentStep { get; private set; } = 0;
    public bool IsAllyTurn
    {
        get
        {
            if (Phase == BanPickPhase.Done) return false;
            if (config?.turnOrderIsAlly == null || CurrentStep < 0 || CurrentStep >= config.turnOrderIsAlly.Count)
                return false;
            return config.turnOrderIsAlly[CurrentStep];
        }
    }
    public bool IsBanStep => CurrentStep < config.BanSteps;

    public float TurnRemaining { get; private set; }

    public Action<BanPickPhase> OnPhaseChanged;
    public Action OnStepAdvanced;
    public Action OnAllDone;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        StartBgm();
        BeginBanPick();
    }

    void StartBgm()
    {
        if (banPickBgm == null) return;
        _bgmSrc = gameObject.AddComponent<AudioSource>();
        _bgmSrc.clip = banPickBgm;
        _bgmSrc.loop = true;
        _bgmSrc.volume = bgmVolume;
        _bgmSrc.playOnAwake = false;
        _bgmSrc.spatialBlend = 0f; // 2D
        _bgmSrc.Play();
    }

    void StopBgm()
    {
        if (_bgmSrc != null) _bgmSrc.Stop();
    }

    public void BeginBanPick()
    {
        PickResult.Clear();
        CurrentStep = 0;
        EnsureTurnOrder();
        BindCards();
        SetPhase(IsBanStep ? BanPickPhase.Banning : BanPickPhase.Picking);
        StartTurn();
    }

    // 에셋에서 turnOrderIsAlly 가 깨졌거나 길이가 부족하면 스네이크 드래프트로 자동 생성
    void EnsureTurnOrder()
    {
        if (config == null) return;
        int need = config.TotalSteps;
        if (config.turnOrderIsAlly != null && config.turnOrderIsAlly.Count >= need) return;

        Debug.LogWarning($"[BanPick] turnOrderIsAlly 길이 부족 ({(config.turnOrderIsAlly?.Count ?? 0)} < {need}) → 스네이크 드래프트 자동 생성");
        var list = new System.Collections.Generic.List<bool>(need);
        // bans: 1 ban each per round — A, E, A, E, ...
        for (int i = 0; i < config.bansPerTeam; i++) { list.Add(true); list.Add(false); }
        // picks: snake — A, E, E, A, A, E, E, A, …
        for (int i = 0; i < config.picksPerTeam * 2; i++)
        {
            bool isAlly = ((i + 1) / 2) % 2 == 0;
            list.Add(isAlly);
        }
        config.turnOrderIsAlly = list;
    }

    void BindCards()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            if (i < config.championPool.Count)
            {
                cards[i].gameObject.SetActive(true);
                cards[i].Bind(config.championPool[i], this);
            }
            else
            {
                cards[i].gameObject.SetActive(false);
            }
        }
    }

    void StartTurn()
    {
        TurnRemaining = config.phaseTimeLimit;
        RefreshCardStates();
        hud?.RefreshAll(this);

        if (!IsAllyTurn)
            StartCoroutine(AITurnRoutine());
    }

    IEnumerator AITurnRoutine()
    {
        yield return new WaitForSeconds(config.aiActionDelay);
        if (Phase == BanPickPhase.Done) yield break;

        var choice = ai != null ? ai.Choose(this) : ChooseFallback();
        if (choice != null) Submit(choice);
    }

    ChampionData ChooseFallback()
    {
        foreach (var c in config.championPool)
            if (IsSelectable(c)) return c;
        return null;
    }

    void Update()
    {
        if (Phase == BanPickPhase.Done || Phase == BanPickPhase.Idle) return;
        TurnRemaining -= Time.deltaTime;
        hud?.UpdateTimer(TurnRemaining);

        if (TurnRemaining <= 0f)
        {
            var fallback = ChooseFallback();
            if (fallback != null) Submit(fallback);
        }
    }

    // ---- selection logic ----
    public bool IsSelectable(ChampionData c)
    {
        if (c == null) return false;
        if (PickResult.AllyBans.Contains(c) || PickResult.EnemyBans.Contains(c)) return false;
        if (PickResult.AllyPicks.Contains(c) || PickResult.EnemyPicks.Contains(c)) return false;
        return true;
    }

    public void OnPlayerClickCard(ChampionData c)
    {
        if (!IsAllyTurn) return;
        if (!IsSelectable(c)) return;
        Submit(c);
    }

    void Submit(ChampionData c)
    {
        bool ally = IsAllyTurn;
        bool isBan = IsBanStep;
        if (isBan)
        {
            if (ally) PickResult.AllyBans.Add(c);
            else PickResult.EnemyBans.Add(c);
        }
        else
        {
            if (ally) PickResult.AllyPicks.Add(c);
            else PickResult.EnemyPicks.Add(c);
        }

        // 사운드
        var clip = isBan ? banSfx : pickSfx;
        if (clip != null)
            PlaySfx(clip);

        Advance();
    }

    void PlaySfx(AudioClip clip)
    {
        var cam = Camera.main;
        var pos = cam != null ? cam.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(clip, pos, sfxVolume);
    }

    void Advance()
    {
        CurrentStep++;
        OnStepAdvanced?.Invoke();

        // 새 픽이 카드/슬롯에 즉시 반영되도록 (마지막 픽 포함)
        RefreshCardStates();
        hud?.allySlotUI?.Refresh();
        hud?.enemySlotUI?.Refresh();

        if (CurrentStep >= config.TotalSteps)
        {
            SetPhase(BanPickPhase.Done);
            StartCoroutine(FinishRoutine());
            return;
        }

        var nextPhase = IsBanStep ? BanPickPhase.Banning : BanPickPhase.Picking;
        if (nextPhase != Phase) SetPhase(nextPhase);

        StartTurn();
    }

    IEnumerator FinishRoutine()
    {
        // 마지막 픽이 슬롯에 표시된 상태를 잠시 보여준 뒤 READY 오버레이
        yield return new WaitForSeconds(1.2f);
        hud?.ShowDone();
        yield return new WaitForSeconds(config.autoConfirmGrace);

        // BGM 페이드아웃
        if (_bgmSrc != null)
        {
            float t = 0f, dur = 0.4f, startVol = _bgmSrc.volume;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                _bgmSrc.volume = Mathf.Lerp(startVol, 0f, t / dur);
                yield return null;
            }
            StopBgm();
        }

        OnAllDone?.Invoke();
        if (autoLoadBattleSceneOnDone && !string.IsNullOrEmpty(battleSceneName))
            UnityEngine.SceneManagement.SceneManager.LoadScene(battleSceneName);
    }

    void SetPhase(BanPickPhase p)
    {
        Phase = p;
        OnPhaseChanged?.Invoke(p);
        hud?.RefreshPhase(p);
    }

    void RefreshCardStates()
    {
        foreach (var card in cards)
        {
            if (!card.gameObject.activeSelf) continue;
            card.RefreshState();
        }
    }
}
