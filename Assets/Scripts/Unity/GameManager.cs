using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("Data")]
    public TextAsset cutsJson;

    [Header("Refs")]
    public GrillGrid grillGrid;

    [Header("Spawning")]
    public MeatItem meatPrefab;
    public Transform spawnPoint;
    public int maxActiveMeats = 3;

    [Header("UI")]
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private GameObject floatingTextPrefab;
    [SerializeField] private TextMeshProUGUI scoreText;

    private int totalScore = 0;

    private CutDatabase db;
    private CutDefinition activeCut;

    void Start()
    {
        Debug.Log("✅ GameManager Start corriendo");

        // ✅ mostrar score inicial
        UpdateScoreUI();

        if (cutsJson == null)
        {
            Debug.LogError("❌ No se asignó cuts.json en el Inspector");
            return;
        }

        db = JsonUtility.FromJson<CutDatabase>(cutsJson.text);
        if (db == null || db.cuts == null || db.cuts.Count == 0)
        {
            Debug.LogError("❌ No hay cuts en cuts.json");
            return;
        }

        if (grillGrid == null)
        {
            Debug.LogError("❌ No se asignó GrillGrid en el Inspector");
            return;
        }

        grillGrid.BuildGrid();

        activeCut = db.cuts[0];
        Debug.Log($"✅ Usando cut: {activeCut.displayName}");

        foreach (var meat in FindObjectsOfType<MeatItem>())
        {
            meat.cut = activeCut;
            meat.OnServed += HandleServed;
        }

        if (meatPrefab != null && spawnPoint != null)
            SpawnMeat();
        else
            Debug.LogWarning("⚠️ No puedo spawnear: falta Meat Prefab o Spawn Point");
    }

    // ✅ AHORA es método de la clase (se puede llamar desde cualquier lado)
    void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"SCORE: {totalScore}";
    }

    void HandleServed(MeatItem meat, int score)
    {
        totalScore += score;
        Debug.Log($"🏁 SCORE TOTAL: {totalScore} ( +{score} )");

        UpdateScoreUI();
        SpawnFloatingText(meat.transform.position, score);

        int alive = CountActiveMeatsInScene();
        if (alive < maxActiveMeats)
            SpawnMeat();
    }

    void SpawnFloatingText(Vector3 meatWorldPos, int score)
    {
        if (floatingTextPrefab == null || uiCanvas == null || Camera.main == null)
            return;

        Vector2 screen = Camera.main.WorldToScreenPoint(meatWorldPos);

        RectTransform canvasRt = uiCanvas.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRt, screen, null, out Vector2 localPoint);

        GameObject go = Instantiate(floatingTextPrefab, uiCanvas.transform);
        FloatingText ft = go.GetComponent<FloatingText>();

        if (ft != null)
        {
            ft.Setup($"+{score}", localPoint + Vector2.up * 40f);
        }
        else
        {
            Debug.LogError("❌ El prefab FloatingText NO tiene el script FloatingText.cs");
        }
    }

    int CountActiveMeatsInScene()
    {
        int count = 0;
        foreach (var m in FindObjectsOfType<MeatItem>())
        {
            if (m.gameObject.activeInHierarchy) count++;
        }
        return count;
    }

    void SpawnMeat()
    {
        if (meatPrefab == null || spawnPoint == null) return;

        MeatItem newMeat = Instantiate(meatPrefab, spawnPoint.position, Quaternion.identity);
        newMeat.cut = activeCut;
        newMeat.OnServed += HandleServed;

        Debug.Log($"🥩 Spawn nueva carne en {spawnPoint.position}");
    }
}
