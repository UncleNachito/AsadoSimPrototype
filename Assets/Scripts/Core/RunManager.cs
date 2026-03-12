using System.Collections.Generic;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    [Header("Player HP")]
    public int maxHP = 100;
    public int currentHP;

    [Header("Level Progression")]
    public int currentLevel = 1;
    public int meatsServedThisLevel = 0;
    public int meatsRequiredForLevel = 5;

    [Header("Quality Requirement")]
    public int goodServesThisLevel = 0;

    [Header("Perks")]
    public PerkDefinition[] allPerks;

    [Header("Perk Offer - Rarity Chances")]
    [Range(0f, 1f)] public float commonChance = 0.75f;
    [Range(0f, 1f)] public float rareChance = 0.20f;
    [Range(0f, 1f)] public float epicChance = 0.05f;

    [Header("Perk Offer - Rules")]
    public bool avoidSameTypeInOffer = true;

    // Runtime perk options (current choice)
    private PerkDefinition perkA;
    private PerkDefinition perkB;
    private PerkDefinition perkC;

    // Runtime modifiers (affected by perks)
    [Header("DEV Tuning (testing)")]
    public bool devMode = true;
    public int devMeatsRequiredOverride = 2; // para test rápido
    public int devStartHP = 30;
    public int devBurnDamage = 30;
    public int devUndercookDamage = 10;

    private float scoreMultiplier = 1f;
    private float goodRatioBonus = 0f; // perks la bajan (negativo)
    public int totalScore = 0;
    private int burnDamage = 30;
    private int undercookDamage = 10;

    private PerkDefinition lastPickedPerk = null;

    private bool runOver = false;
    private GameUIController ui;

    void Awake()
    {
        Time.timeScale = 1f;

        ui = FindObjectOfType<GameUIController>();

        StartNewRun();

        Debug.Log($"🟢 Run iniciada | Nivel {currentLevel} | HP = {currentHP}");
        Debug.Log(ui != null ? "✅ UI encontrada" : "❌ NO se encontró GameUIController");
    }

    void Update()
    {
        // Dev shortcut opcional: ENTER reinicia si estás en game over
        if (runOver && Input.GetKeyDown(KeyCode.Return))
            RestartRun();

        if (!devMode) return;

        // F1: Completar nivel (abrir perks)
        if (Input.GetKeyDown(KeyCode.F1) && !runOver)
        {
            LevelComplete();
        }

        // F2: GameOver instantáneo
        if (Input.GetKeyDown(KeyCode.F2) && !runOver)
        {
            LoseHP(999, "DEV GameOver");
        }

        // F3: Avanzar nivel sin perks
        if (Input.GetKeyDown(KeyCode.F3) && !runOver)
        {
            StartNextLevel();
        }

        // Debug: elegir perks con teclas si el panel está abierto
        if (devMode && ui != null && ui.panelPerkChoice != null && ui.panelPerkChoice.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) PickPerkA();
            if (Input.GetKeyDown(KeyCode.Alpha2)) PickPerkB();
            if (Input.GetKeyDown(KeyCode.Alpha3)) PickPerkC(); // opcional si ya tienes 3 perks en UI
        }
    }

    // ----------------- EVENTS FROM MEAT -----------------
    public void OnMeatServed(MeatItem meat, int score)
    {
        if (runOver) return;

        meatsServedThisLevel++;
        if (meat.IsIdeal()) goodServesThisLevel++;

        // ✅ Ratio base + bonus (perks lo bajan)
        float ratio = Mathf.Clamp01(GetGoodRatioForLevel(currentLevel) + goodRatioBonus);
        int goodRequired = Mathf.CeilToInt(meatsRequiredForLevel * ratio);

        // ✅ Score real con multiplicador
        int finalScore = Mathf.RoundToInt(score * scoreMultiplier);
        totalScore += finalScore;

        Debug.Log(
            $"🍖 Carne servida ({meatsServedThisLevel}/{meatsRequiredForLevel}) | " +
            $"✅ Buenas: {goodServesThisLevel}/{goodRequired} (ratio {ratio:0.00}) | " +
            $"🏆 +{finalScore} (x{scoreMultiplier:0.00}) Total: {totalScore}"
        );

        // Penalizaciones por calidad
        if (meat.IsBurnt())
            LoseHP(burnDamage, "🔥 Carne quemada");
        else if (meat.IsUnderCooked())
            LoseHP(undercookDamage, "🥶 Carne cruda");

        // ¿Nivel completado? (si no cumples, sigues jugando)
        if (!runOver)
        {
            if (meatsServedThisLevel >= meatsRequiredForLevel && goodServesThisLevel >= goodRequired)
            {
                LevelComplete();
            }
            else if (meatsServedThisLevel >= meatsRequiredForLevel && goodServesThisLevel < goodRequired)
            {
                Debug.Log($"⚠️ Nivel aún no completo: faltan carnes buenas ({goodServesThisLevel}/{goodRequired}).");
            }
        }
    }

    void LoseHP(int amount, string reason)
    {
        currentHP -= amount;
        if (currentHP < 0) currentHP = 0;

        Debug.Log($"{reason} → -{amount} HP | HP actual: {currentHP}");

        if (currentHP <= 0)
            GameOver();
    }

    // ----------------- LEVEL FLOW -----------------
    void LevelComplete()
    {
        Debug.Log($"✅ NIVEL {currentLevel} COMPLETADO");
        ShowPerkSelection();
    }

    void StartNextLevel()
    {
        // cerrar UI de perks y reanudar juego
        ui?.HideAll();
        Time.timeScale = 1f;

        // avanzar nivel y resetear contadores del nivel
        currentLevel++;
        meatsServedThisLevel = 0;
        goodServesThisLevel = 0;

        // dificultad simple por ahora
        meatsRequiredForLevel = 5 + (currentLevel - 1);

        Debug.Log($"➡️ Comienza nivel {currentLevel} | Carnes requeridas: {meatsRequiredForLevel} | ratio bonus: {goodRatioBonus:0.00}");
    }

    void GameOver()
    {
        runOver = true;
        ui?.ShowGameOver();
        Debug.Log("💀 GAME OVER - Run terminada");
        // OJO: NO pongas Time.timeScale aquí, lo maneja la UI
    }

    public void RestartRun()
    {
        Time.timeScale = 1f;
        runOver = false;

        StartNewRun();

        ui?.HideAll();
        Debug.Log("🔄 Run reiniciada");
    }

    void StartNewRun()
    {
        // Reset base run
        currentLevel = 1;
        meatsServedThisLevel = 0;
        goodServesThisLevel = 0;

        // Reset modifiers
        scoreMultiplier = 1f;
        goodRatioBonus = 0f;
        totalScore = 0;

        if (devMode)
        {
            maxHP = devStartHP;
            meatsRequiredForLevel = devMeatsRequiredOverride;
            burnDamage = devBurnDamage;
            undercookDamage = devUndercookDamage;
        }
        else
        {
            meatsRequiredForLevel = 5;
            burnDamage = 30;
            undercookDamage = 10;
        }

        currentHP = maxHP;
        lastPickedPerk = null;
    }

    // ----------------- MEAT SUBSCRIBE (spawns dinámicos) -----------------
    public void RegisterMeat(MeatItem meat)
    {
        if (meat == null) return;
        meat.OnServed += OnMeatServed;
    }

    public void UnregisterMeat(MeatItem meat)
    {
        if (meat == null) return;
        meat.OnServed -= OnMeatServed;
    }

    // ----------------- QUALITY CURVE -----------------
    float GetGoodRatioForLevel(int level)
    {
        float baseRatio;
        if (level <= 2) baseRatio = 0.4f;
        else if (level <= 4) baseRatio = 0.6f;
        else baseRatio = 0.75f;

        float finalRatio = baseRatio + goodRatioBonus;
        finalRatio = Mathf.Clamp(finalRatio, 0.10f, 0.95f);
        return finalRatio;
    }

    // ----------------- PERKS -----------------
    void ShowPerkSelection()
    {
        if (ui == null)
        {
            Debug.Log("⚠️ No hay UI, avanzando sin perk.");
            StartNextLevel();
            return;
        }

        if (allPerks == null || allPerks.Length < 2)
        {
            Debug.Log("⚠️ No hay perks suficientes, avanzando sin perk.");
            StartNextLevel();
            return;
        }

        // Pausa la run mientras eliges
        Time.timeScale = 0f;

        // Conjunto de tipos prohibidos para esta oferta (para que no repita tipo)
        HashSet<PerkType> bannedTypes = avoidSameTypeInOffer ? new HashSet<PerkType>() : null;

        // A
        perkA = PickRandomPerkByRarity(RollRarity(), exclude: lastPickedPerk, alsoExclude: null, bannedTypes: bannedTypes);
        if (perkA != null && bannedTypes != null) bannedTypes.Add(perkA.type);

        // B (evita duplicar A y lastPicked)
        perkB = PickRandomPerkByRarity(RollRarity(), exclude: perkA, alsoExclude: lastPickedPerk, bannedTypes: bannedTypes);
        if (perkB != null && bannedTypes != null) bannedTypes.Add(perkB.type);

        // C (evita duplicar A/B y lastPicked)
        perkC = PickRandomPerkByRarity(RollRarity(), exclude: perkA, alsoExclude: perkB, bannedTypes: bannedTypes);
        // Además, que no sea lastPicked (porque acá alsoExclude es perkB)
        if (perkC == lastPickedPerk)
        {
            perkC = PickRandomPerkByRarity(PerkRarity.Common, exclude: perkA, alsoExclude: perkB, bannedTypes: bannedTypes);
        }

        // Fallbacks: relajar rareza pero mantener exclusiones
        if (perkA == null) perkA = PickRandomPerkByRarity(PerkRarity.Common, exclude: lastPickedPerk, alsoExclude: null, bannedTypes: null);
        if (perkB == null) perkB = PickRandomPerkByRarity(PerkRarity.Common, exclude: perkA, alsoExclude: lastPickedPerk, bannedTypes: null);
        if (perkC == null) perkC = PickRandomPerkByRarity(PerkRarity.Common, exclude: perkA, alsoExclude: perkB, bannedTypes: null);

        // Si aún así no hay oferta mínima, sigue
        if (perkA == null || perkB == null)
        {
            Debug.Log("⚠️ No se pudo armar oferta de perks, avanzando sin perk.");
            Time.timeScale = 1f;
            StartNextLevel();
            return;
        }

        ui.ShowPerkChoice();
        ui.SetPerkOffer("Elige un perk", perkA.perkName, perkB.perkName, perkC != null ? perkC.perkName : null);


        // Si tienes btnCText en tu UI, puedes agregarlo después.
        Debug.Log(
            $"🎁 Perks oferta: " +
            $"A={perkA.perkName} ({perkA.rarity}) | " +
            $"B={perkB.perkName} ({perkB.rarity}) | " +
            $"C={(perkC != null ? perkC.perkName : "NULL")} ({(perkC != null ? perkC.rarity.ToString() : "-")})"
        );
    }

    public void PickPerkA() => ApplyPerk(perkA);
    public void PickPerkB() => ApplyPerk(perkB);
    public void PickPerkC() => ApplyPerk(perkC);

    void ApplyPerk(PerkDefinition perk)
    {
        if (perk == null)
        {
            Debug.Log("⚠️ Perk nulo, avanzando.");
            ui?.HideAll();
            Time.timeScale = 1f;
            StartNextLevel();
            return;
        }

        lastPickedPerk = perk;

        Debug.Log($"✅ Elegiste perk: {perk.perkName}");

        switch (perk.type)
        {
            case PerkType.IncreaseMaxHP:
                {
                    int add = Mathf.RoundToInt(perk.value);
                    maxHP += add;
                    currentHP = Mathf.Min(currentHP + add, maxHP);
                    break;
                }

            case PerkType.ReduceBurnDamage:
                {
                    int reduce = Mathf.RoundToInt(perk.value);
                    burnDamage = Mathf.Max(0, burnDamage - reduce);
                    break;
                }

            case PerkType.IncreaseScoreMultiplier:
                {
                    scoreMultiplier += perk.value;
                    break;
                }

            case PerkType.ReduceRequiredGoodRatio:
                {
                    goodRatioBonus -= perk.value;
                    break;
                }

            case PerkType.ReduceMeatsRequired:
                {
                    int reduce = Mathf.RoundToInt(perk.value);
                    meatsRequiredForLevel = Mathf.Max(1, meatsRequiredForLevel - reduce);
                    Debug.Log($"🍖 Perk: -{reduce} carnes requeridas (ahora {meatsRequiredForLevel})");
                    break;
                }
        }

        ui?.HideAll();
        Time.timeScale = 1f;
        StartNextLevel();
        Debug.Log($"📌 Modificadores: xScore={scoreMultiplier:0.00} | burnDmg={burnDamage} | underDmg={undercookDamage} | ratioBonus={goodRatioBonus:0.00}");
    }

    // ----------------- PERK SELECTION HELPERS -----------------
    PerkRarity RollRarity()
    {
        float total = commonChance + rareChance + epicChance;
        if (total <= 0.0001f) return PerkRarity.Common;

        float r = Random.value * total;

        if (r < commonChance) return PerkRarity.Common;
        r -= commonChance;

        if (r < rareChance) return PerkRarity.Rare;
        return PerkRarity.Epic;
    }

    PerkDefinition PickRandomPerkByRarity(
        PerkRarity target,
        PerkDefinition exclude = null,
        PerkDefinition alsoExclude = null,
        HashSet<PerkType> bannedTypes = null
    )
    {
        // Intento con rareza objetivo, luego fallbacks
        PerkDefinition picked = PickWeightedFromPool(target, exclude, alsoExclude, bannedTypes);
        if (picked != null) return picked;

        if (target == PerkRarity.Epic)
        {
            picked = PickWeightedFromPool(PerkRarity.Rare, exclude, alsoExclude, bannedTypes);
            if (picked != null) return picked;
        }

        picked = PickWeightedFromPool(PerkRarity.Common, exclude, alsoExclude, bannedTypes);
        return picked;
    }

    PerkDefinition PickWeightedFromPool(
        PerkRarity rarity,
        PerkDefinition exclude,
        PerkDefinition alsoExclude,
        HashSet<PerkType> bannedTypes
    )
    {
        if (allPerks == null || allPerks.Length == 0) return null;

        int totalWeight = 0;

        foreach (var p in allPerks)
        {
            if (p == null) continue;
            if (p == exclude) continue;
            if (p == alsoExclude) continue;
            if (bannedTypes != null && bannedTypes.Contains(p.type)) continue;
            if (p.rarity != rarity) continue;

            totalWeight += Mathf.Max(0, p.weight);
        }

        if (totalWeight <= 0) return null;

        int roll = Random.Range(0, totalWeight);

        foreach (var p in allPerks)
        {
            if (p == null) continue;
            if (p == exclude) continue;
            if (p == alsoExclude) continue;
            if (bannedTypes != null && bannedTypes.Contains(p.type)) continue;
            if (p.rarity != rarity) continue;

            roll -= Mathf.Max(0, p.weight);
            if (roll < 0) return p;
        }

        return null;
    }
}
