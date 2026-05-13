public enum ChampionRole
{
    Tank,
    Fighter,
    Marksman,
    Mage,
    Healer,
    Disruptor,   // 분쇄자: CC + 탱딜
    Skirmisher,  // 돌격기병: 고기동 백라인 저격
    Duelist,     // 검사: 단일 학살, 백라인 침투
    Assassin     // 닌자: 배후 암살, 평타 +30% 보너스
}

public enum DamageType
{
    Basic,       // 평타 — 흰색 28
    Skill,       // 스킬 — 빨강 36
    Ultimate,    // 궁 — 주황 44
    Heal,        // 회복 — 초록 28
    Miss         // 미스/면역 — 회색 24
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
