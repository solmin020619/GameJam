using UnityEngine;

/// <summary>
/// 3판 2선승제 세트 점수 추적 (씬 전환에도 살아남는 static).
/// </summary>
public static class MatchResult
{
    public static int team0Wins;  // 우리(파랑) 세트 승수
    public static int team1Wins;  // 상대(빨강) 세트 승수
    public const int WinsToVictory = 2; // 2승하면 최종 우승

    public static void Clear()
    {
        team0Wins = 0;
        team1Wins = 0;
    }

    public static void AddWin(int teamId)
    {
        if (teamId == 0) team0Wins++;
        else if (teamId == 1) team1Wins++;
    }

    public static bool IsMatchOver => team0Wins >= WinsToVictory || team1Wins >= WinsToVictory;
    public static int WinningTeam =>
        team0Wins >= WinsToVictory ? 0 :
        team1Wins >= WinsToVictory ? 1 : -1;
}
