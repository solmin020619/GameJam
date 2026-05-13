public enum ChampionRole
{
    Tank,
    Fighter,
    Marksman,
    Mage,
    Healer,
    Disruptor,   // 분쇄자: CC + 탱딜
    Skirmisher,  // 돌격기병: 고기동 백라인 저격
    Duelist      // 검사: 단일 학살, 백라인 침투
}

public enum BanPickPhase
{
    Idle,
    Banning,
    Picking,
    Done
}

public enum TeamSide
{
    Ally,
    Enemy
}
