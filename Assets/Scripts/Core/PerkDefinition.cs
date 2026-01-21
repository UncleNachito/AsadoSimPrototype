using UnityEngine;

public enum PerkType
{
    IncreaseMaxHP,
    ReduceBurnDamage,
    IncreaseScoreMultiplier,
    ReduceRequiredGoodRatio,
    ReduceMeatsRequired, 
}


[CreateAssetMenu(menuName = "AsadoSim/Perk Definition")]
public class PerkDefinition : ScriptableObject
{
    public string perkName;
    [TextArea] public string description;
    public PerkType type;

    public float value = 10f; // depende del perk (ej: +20 HP, -5 dmg, etc.)
}
