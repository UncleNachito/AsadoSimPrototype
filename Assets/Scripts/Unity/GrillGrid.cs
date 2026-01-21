using System.Collections.Generic;
using UnityEngine;

public class GrillGrid : MonoBehaviour
{
    [Header("Prefab del slot")]
    public GrillSlot slotPrefab;

    [Header("Grid")]
    public int columns = 3;
    public int rows = 2;
    public float cellSize = 1.2f;
    public Vector2 origin = Vector2.zero;

    [Header("Blocked cells")]
    public bool randomizeBlocked = false;
    [Range(0, 12)] public int randomBlockedCount = 0;

    [Header("Heat zones")]
    public bool randomizeHeat = false;
    public Vector2 heatRange = new Vector2(0.7f, 1.5f);
    public bool useSimpleHeatPattern = true;

    private GrillSlot[,] slots;
    private MeatItem[,] occupancy;

    private bool[,] blocked;
    private float[,] heat;

    void Start()
    {
        BuildGrid();
    }

    public void BuildGrid()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        slots = new GrillSlot[columns, rows];
        occupancy = new MeatItem[columns, rows];

        blocked = new bool[columns, rows];
        heat = new float[columns, rows];

        InitCellData();
        InstantiateSlotsAndApplyVisual();
    }

    void InitCellData()
    {
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                blocked[x, y] = false;
                heat[x, y] = 1f;
            }

        if (!randomizeHeat && useSimpleHeatPattern)
        {
            for (int x = 0; x < columns; x++)
            {
                if (rows >= 1) heat[x, 0] = 1.25f;
                if (rows >= 2) heat[x, 1] = 0.9f;
            }
        }

        if (randomizeHeat)
        {
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                    heat[x, y] = Random.Range(heatRange.x, heatRange.y);
        }

        if (randomizeBlocked && randomBlockedCount > 0)
        {
            int attempts = 0;
            int placed = 0;
            while (placed < randomBlockedCount && attempts < 500)
            {
                attempts++;
                int rx = Random.Range(0, columns);
                int ry = Random.Range(0, rows);

                if (!blocked[rx, ry])
                {
                    blocked[rx, ry] = true;
                    placed++;
                }
            }
        }
    }

    void InstantiateSlotsAndApplyVisual()
    {
        if (slotPrefab == null)
        {
            Debug.LogError("❌ No asignaste slotPrefab en GrillGrid");
            return;
        }

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Vector2 posLocal = origin + new Vector2(x * cellSize, -y * cellSize);
                GrillSlot slot = Instantiate(slotPrefab, transform);
                slot.transform.localPosition = posLocal;
                slots[x, y] = slot;

                ApplySlotVisual(x, y);
            }
        }
    }

    void ApplySlotVisual(int x, int y)
    {
        var sr = slots[x, y].GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (blocked[x, y])
        {
            sr.color = new Color(0.45f, 0.45f, 0.45f, 1f);
        }
        else
        {
            float h = heat[x, y];
            if (h < 0.95f) sr.color = new Color(0.75f, 0.85f, 1f, 1f);
            else if (h > 1.15f) sr.color = new Color(1f, 0.78f, 0.78f, 1f);
            else sr.color = Color.white;
        }
    }

    public Vector3 CellToWorld(int cellX, int cellY)
    {
        Vector2 local = origin + new Vector2(cellX * cellSize, -cellY * cellSize);
        return transform.TransformPoint(local);
    }

    public void RemoveMeat(MeatItem meat)
    {
        if (meat == null || !meat.IsPlaced) return;

        for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
                if (occupancy[x, y] == meat)
                    occupancy[x, y] = null;
    }

    public float GetAverageHeatForItem(MeatItem meat)
    {
        if (meat == null || !meat.IsPlaced) return 1f;

        var offsets = meat.GetFootprintOffsets();

        float sum = 0f;
        int count = 0;

        foreach (var o in offsets)
        {
            int x = meat.GridX + o.x;
            int y = meat.GridY + o.y;

            if (x < 0 || x >= columns || y < 0 || y >= rows) continue;

            sum += heat[x, y];
            count++;
        }

        if (count <= 0) return 1f;
        return sum / count;
    }

    // ✅ Busca mejor celda probando todo (por centro del bounding box)
    public bool TryPlaceMeat(MeatItem meat, Vector3 worldPos)
    {
        var size = meat.GetFootprintSize();
        int w = size.x;
        int h = size.y;

        if (w > columns || h > rows) return false;

        float bestDist = float.PositiveInfinity;
        int bestX = -1, bestY = -1;

        for (int y = 0; y <= rows - h; y++)
        {
            for (int x = 0; x <= columns - w; x++)
            {
                if (!CanFitAt(meat, x, y)) continue;

                // Centro del bounding box
                Vector3 topLeft = CellToWorld(x, y);
                Vector3 centerOffset = new Vector3((w - 1) * cellSize * 0.5f, -(h - 1) * cellSize * 0.5f, 0f);
                Vector3 rectCenter = topLeft + centerOffset;

                float d = (worldPos - rectCenter).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestX = x; bestY = y;
                }
            }
        }

        if (bestX < 0) return false;
        return TryPlaceMeatAtCell(meat, bestX, bestY);
    }

    bool CanFitAt(MeatItem meat, int baseX, int baseY)
    {
        var offsets = meat.GetFootprintOffsets();

        foreach (var o in offsets)
        {
            int x = baseX + o.x;
            int y = baseY + o.y;

            if (x < 0 || x >= columns || y < 0 || y >= rows) return false;
            if (blocked[x, y]) return false;
            if (occupancy[x, y] != null) return false;
        }

        return true;
    }

    public bool TryPlaceMeatAtCell(MeatItem meat, int baseX, int baseY)
    {
        var offsets = meat.GetFootprintOffsets();
        var size = meat.GetFootprintSize();
        int w = size.x;
        int h = size.y;

        if (baseX < 0 || baseY < 0 || baseX + w > columns || baseY + h > rows)
            return false;

        if (!CanFitAt(meat, baseX, baseY)) return false;

        // ocupar solo las celdas del footprint
        foreach (var o in offsets)
        {
            int x = baseX + o.x;
            int y = baseY + o.y;
            occupancy[x, y] = meat;
        }

        // snap al centro del bounding box
        Vector3 topLeft = CellToWorld(baseX, baseY);
        Vector3 centerOffset = new Vector3((w - 1) * cellSize * 0.5f, -(h - 1) * cellSize * 0.5f, 0f);
        Vector3 target = topLeft + centerOffset;

        meat.transform.position = target + meat.PivotToVisualCenterOffsetWorld();
        meat.SetPlaced(this, baseX, baseY);
        return true;
    }

    // ---------------- UX: Placement Preview + Highlight ----------------

    private readonly List<Vector2Int> _highlighted = new List<Vector2Int>();

    public bool GetBestPlacementPreview(MeatItem meat, Vector3 worldPos, out int bestX, out int bestY)
    {
        bestX = -1;
        bestY = -1;

        if (meat == null) return false;

        var size = meat.GetFootprintSize();
        int w = size.x;
        int h = size.y;

        if (w > columns || h > rows) return false;

        float bestDist = float.PositiveInfinity;

        for (int y = 0; y <= rows - h; y++)
        {
            for (int x = 0; x <= columns - w; x++)
            {
                if (!CanFitAt(meat, x, y)) continue;

                Vector3 topLeft = CellToWorld(x, y);
                Vector3 centerOffset = new Vector3((w - 1) * cellSize * 0.5f, -(h - 1) * cellSize * 0.5f, 0f);
                Vector3 rectCenter = topLeft + centerOffset;

                float d = (worldPos - rectCenter).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestX = x;
                    bestY = y;
                }
            }
        }

        return bestX >= 0;
    }

    public Vector3 GetSnappedWorldFor(MeatItem meat, int baseX, int baseY)
    {
        var size = meat.GetFootprintSize();
        int w = size.x;
        int h = size.y;

        Vector3 topLeft = CellToWorld(baseX, baseY);
        Vector3 centerOffset = new Vector3((w - 1) * cellSize * 0.5f, -(h - 1) * cellSize * 0.5f, 0f);
        Vector3 target = topLeft + centerOffset;

        return target + meat.PivotToVisualCenterOffsetWorld();
    }

    public void ClearPlacementHighlight()
    {
        for (int i = 0; i < _highlighted.Count; i++)
        {
            var p = _highlighted[i];
            if (p.x < 0 || p.x >= columns || p.y < 0 || p.y >= rows) continue;
            if (slots[p.x, p.y] != null) slots[p.x, p.y].SetHighlight(false, true);
        }
        _highlighted.Clear();
    }

    public void ShowPlacementHighlight(MeatItem meat, int baseX, int baseY, bool valid)
    {
        ClearPlacementHighlight();

        if (meat == null) return;

        var offsets = meat.GetFootprintOffsets();
        foreach (var o in offsets)
        {
            int x = baseX + o.x;
            int y = baseY + o.y;

            if (x < 0 || x >= columns || y < 0 || y >= rows) continue;
            if (slots[x, y] == null) continue;

            slots[x, y].SetHighlight(true, valid);
            _highlighted.Add(new Vector2Int(x, y));
        }
    }


    // API opcional
    public void SetBlocked(int x, int y, bool value)
    {
        if (x < 0 || x >= columns || y < 0 || y >= rows) return;
        blocked[x, y] = value;
        ApplySlotVisual(x, y);
    }

    public void SetHeat(int x, int y, float multiplier)
    {
        if (x < 0 || x >= columns || y < 0 || y >= rows) return;
        heat[x, y] = Mathf.Max(0.05f, multiplier);
        ApplySlotVisual(x, y);
    }
}
