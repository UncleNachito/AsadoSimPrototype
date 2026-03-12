using UnityEngine;

public enum PerkType
{
    IncreaseMaxHP,
    ReduceBurnDamage,
    IncreaseScoreMultiplier,
    ReduceRequiredGoodRatio,
    ReduceMeatsRequired,
}

public enum PerkRarity
{
    Common,
    Rare,
    Epic
}

[CreateAssetMenu(menuName = "AsadoSim/Perk Definition")]
public class PerkDefinition : ScriptableObject
{
    public string perkName;
    [TextArea] public string description;
    public PerkType type;

    public float value = 10f; // depende del perk (ej: +20 HP, -5 dmg, etc.)

    [Header("Rarity")]
    public PerkRarity rarity = PerkRarity.Common;

    [Header("Weight (higher = more common)")]
    [Min(0)]
    public int weight = 10;
}
