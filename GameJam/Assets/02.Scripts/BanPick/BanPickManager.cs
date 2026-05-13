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

    // ---- runtime state ----
    public BanPickPhase Phase { get; private set; } = BanPickPhase.Idle;
    public int CurrentStep { get; private set; } = 0;
    public bool IsAllyTurn => Phase != BanPickPhase.Done && config.turnOrderIsAlly[CurrentStep];
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
        BeginBanPick();
    }

    public void BeginBanPick()
    {
        PickResult.Clear();
        CurrentStep = 0;
        BindCards();
        SetPhase(IsBanStep ? BanPickPhase.Banning : BanPickPhase.Picking);
        StartTurn();
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
        if (IsBanStep)
        {
            if (ally) PickResult.AllyBans.Add(c);
            else PickResult.EnemyBans.Add(c);
        }
        else
        {
            if (ally) PickResult.AllyPicks.Add(c);
            else PickResult.EnemyPicks.Add(c);
        }

        Advance();
    }

    void Advance()
    {
        CurrentStep++;
        OnStepAdvanced?.Invoke();

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
        hud?.ShowDone();
        yield return new WaitForSeconds(config.autoConfirmGrace);
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
