using UnityEngine;

[CreateAssetMenu(fileName = "NewChampion", menuName = "TFM/Champion")]
public class ChampionSO : ScriptableObject
{
    [Header("Info")]
    public string ChampionName;
    public Sprite Icon;
    public GameObject Prefab;  // SPUM prefab for this champion

    [Header("Stats")]
    public float MaxHp = 500f;
    public float AttackDamage = 50f;
    public float AttackSpeed = 1f;    // attacks per second
    public float AttackRange = 1.5f;  // unity units
    public float Defense = 10f;
    public float MoveSpeed = 3.5f;
}