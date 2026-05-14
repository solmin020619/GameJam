using UnityEngine;

[CreateAssetMenu(fileName = "Champion_", menuName = "TFM/ChampionData")]
public class ChampionData : ScriptableObject
{
    [Header("Identity")]
    public string championId;
    public string displayName;
    public ChampionRole role;

    [Header("UI")]
    public Sprite portrait;
    public Color themeColor = Color.white;
    [TextArea] public string description;

    [Header("Stats (battle dev will use)")]
    public float maxHealth = 500f;
    public float attackDamage = 30f;
    public float attackSpeed = 1f;
    public float attackRange = 2f;
    public float defense = 10f;
    public float moveSpeed = 3.5f;

    [Header("Prefab handoff (set by battle dev)")]
    public GameObject unitPrefab;

    [Header("Kill log icon (머리 컷)")]
    public Sprite killIcon;
    [Tooltip("KillLog 안 박스에서 얼굴을 얼마나 확대할지. 1.0 = 박스 꽉, 1.5 = 1.5배 확대(머리만 보임)")]
    [Range(0.5f, 5.0f)] public float killIconZoom = 1.0f;
    [Tooltip("KillLog 박스 안에서 얼굴 위치 미세조정 (음수 = 왼쪽/아래)")]
    public Vector2 killIconOffset = Vector2.zero;

    [Header("Basic Skill (기본 스킬)")]
    public string basicSkillName = "기본 스킬";
    public float basicSkillCooldown = 6f;
    public Sprite basicSkillIcon;

    [Header("Ultimate (궁극기)")]
    public string ultimateName = "궁극기";
    public float ultimateCooldown = 20f;
    public Sprite ultimateIcon;

    [Header("Sounds (드래그앤드롭)")]
    [Tooltip("평타 (기본 공격) 사운드 — 평타 1회 시 재생")]
    public AudioClip autoAttackSfx;
    [Tooltip("기본 스킬 사운드 — 기본 스킬 발동 시 재생")]
    public AudioClip basicSkillSfx;
    [Tooltip("궁극기 사운드 — 궁극기 발동 시 재생")]
    public AudioClip ultimateSfx;
}
