using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BanPickConfig", menuName = "TFM/BanPickConfig")]
public class BanPickConfig : ScriptableObject
{
    [Header("Champion Pool")]
    public List<ChampionData> championPool = new();

    [Header("Counts (per team)")]
    public int bansPerTeam = 1;
    public int picksPerTeam = 3;

    [Header("Timing")]
    public float phaseTimeLimit = 20f;
    public float aiActionDelay = 0.8f;
    public float autoConfirmGrace = 1.5f;

    [Header("Pick Order (snake)")]
    [Tooltip("Pick 순서. true = Ally, false = Enemy. 길이는 bansPerTeam*2 + picksPerTeam*2 와 일치해야 함.")]
    public List<bool> turnOrderIsAlly = new() {
        true, false,                       // bans: A, E
        true, false, false, true, true, false // picks: A, E, E, A, A, E (snake)
    };

    public int TotalSteps => bansPerTeam * 2 + picksPerTeam * 2;
    public int BanSteps => bansPerTeam * 2;
}
