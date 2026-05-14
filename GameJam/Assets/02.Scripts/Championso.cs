using UnityEngine;

[CreateAssetMenu(fileName = "NewChampion", menuName = "TFM/Champion")]
public class ChampionSO : ScriptableObject
{
    [Header("Info")]
    public string ChampionName;
    public Sprite Icon;
    public Sprite KillIcon;    // 킬로그 머리 컷
    public float KillIconZoom = 1.0f;
    public Vector2 KillIconOffset = Vector2.zero;
    public GameObject Prefab;  // SPUM prefab for this champion
    public ChampionRole Role = ChampionRole.Fighter;

    [Header("Stats")]
    public float MaxHp = 500f;
    public float AttackDamage = 50f;
    public float AttackSpeed = 1f;    // attacks per second
    public float AttackRange = 1.5f;  // unity units
    public float Defense = 10f;
    public float MoveSpeed = 3.5f;

    [Header("Role-specific (auto)")]
    [Tooltip("Marksman/Mage: 이 거리보다 가까운 적은 카이팅으로 피함")]
    public float KitingDistance = 2.5f;
    [Tooltip("Healer: 힐 쿨타임 (초)")]
    public float HealCooldown = 2.2f;          // 2.5 → 2.2 (살짝 회복)
    [Tooltip("Healer: 힐량 (% of MaxHp). 0.14 = MaxHp의 14%")]
    public float HealAmountPercent = 0.14f;    // 0.13 → 0.14

    [Header("Basic Skill (기본 스킬, CD 자동 회전)")]
    public string BasicSkillName = "기본 스킬";
    public float BasicSkillCooldown = 6f;
    public Sprite BasicSkillIcon;

    [Header("Ultimate (궁극기)")]
    public string UltimateName = "궁극기";
    public float UltimateCooldown = 20f;
    public Sprite UltimateIcon;

    [Header("Sounds")]
    public AudioClip AutoAttackSfx;
    public AudioClip BasicSkillSfx;
    public AudioClip UltimateSfx;
}