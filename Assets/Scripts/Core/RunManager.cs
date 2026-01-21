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

    // Runtime perk options (current choice)
    private PerkDefinition perkA;
    private PerkDefinition perkB;

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

        // En vez de avanzar directo, abrimos selección de perk
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

        if (devMode)
        {
            maxHP = devStartHP;
            meatsRequiredForLevel = devMeatsRequiredOverride;
            burnDamage = devBurnDamage;
            undercookDamage = devUndercookDamage;
        }
        else
        {
            currentHP = maxHP;
            meatsRequiredForLevel = 5;
            burnDamage = 30;
            undercookDamage = 10;
        }

        currentHP = maxHP;
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

        // Aplica bonus de perks (puede ser negativo para bajar exigencia)
        float finalRatio = baseRatio + goodRatioBonus;

        // Clamp: nunca menos de 0.10 ni más de 0.95 (ajustable después)
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
            ui.ShowLevelComplete(); // opcional: mostrar panel de nivel completado igual
            // Si quieres que siga inmediatamente, comenta la línea de arriba y deja StartNextLevel directo.
            StartNextLevel();
            return;
        }

        int iA = Random.Range(0, allPerks.Length);
        int iB = Random.Range(0, allPerks.Length);
        while (iB == iA) iB = Random.Range(0, allPerks.Length);

        perkA = allPerks[iA];
        perkB = allPerks[iB];

        // Mostrar panel perks (pausa)
        ui.ShowPerkChoice();

        // Setear textos de botones (requiere que ui tenga estas refs)
        if (ui.btnAText != null) ui.btnAText.text = perkA.perkName;
        if (ui.btnBText != null) ui.btnBText.text = perkB.perkName;

        Debug.Log($"🎁 Perks: A={perkA.perkName} | B={perkB.perkName}");
    }

    public void PickPerkA()
    {
        ApplyPerk(perkA);
    }

    public void PickPerkB()
    {
        ApplyPerk(perkB);
    }

    void ApplyPerk(PerkDefinition perk)
    {
        if (perk == null)
        {
            Debug.Log("⚠️ Perk nulo, avanzando.");
            ui?.HideAll();
            StartNextLevel();
            return;
        }

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
                    goodRatioBonus -= perk.value; // si perk.value = 0.10, bonus queda -0.10
                    break;
                }
            case PerkType.ReduceMeatsRequired:
                {
                    int reduce = Mathf.RoundToInt(perk.value);   // ej: value = 1
                    meatsRequiredForLevel = Mathf.Max(1, meatsRequiredForLevel - reduce);
                    Debug.Log($"🍖 Perk: -{reduce} carnes requeridas (ahora {meatsRequiredForLevel})");
                    break;
                }

        }

        ui?.HideAll();
        StartNextLevel();
        Debug.Log($"📌 Modificadores: xScore={scoreMultiplier:0.00} | burnDmg={burnDamage} | underDmg={undercookDamage} | ratioBonus={goodRatioBonus:0.00}");

    }
}
