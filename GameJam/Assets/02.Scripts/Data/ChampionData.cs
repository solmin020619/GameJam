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
}
