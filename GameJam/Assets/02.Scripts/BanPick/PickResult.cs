using System.Collections.Generic;

public static class PickResult
{
    public static List<ChampionData> AllyPicks = new();
    public static List<ChampionData> EnemyPicks = new();
    public static List<ChampionData> AllyBans = new();
    public static List<ChampionData> EnemyBans = new();

    public static void Clear()
    {
        AllyPicks.Clear();
        EnemyPicks.Clear();
        AllyBans.Clear();
        EnemyBans.Clear();
    }
}
