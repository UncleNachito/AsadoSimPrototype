using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class MeatItem : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField] private bool useCustomShape = false;
    [SerializeField] private Vector2Int[] customCells;

    [Header("Rectangle fallback")]
    [SerializeField] private int widthCells = 2;
    [SerializeField] private int heightCells = 1;

    [Header("Visual")]
    [SerializeField] private Sprite cellSprite;
    [SerializeField] private int cellSortingOrder = 10;

    [Header("Feedback - Pulse")]
    [SerializeField] private bool pulseWhileCooking = true;
    [SerializeField] private float pulseSpeed = 6f;
    [SerializeField] private float pulseAmount = 0.06f;

    [Header("Feedback - Glow (Ideal)")]
    [SerializeField] private bool glowOnIdeal = true;
    [SerializeField] private float glowScale = 1.18f;
    [SerializeField] private float glowAlpha = 0.22f;

    [Header("Feedback - Flash on state change")]
    [SerializeField] private bool flashOnStateChange = true;
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private float flashIntensity = 0.65f;

    [Header("Data")]
    public CutDefinition cut;

    [Header("Debug")]
    public bool debugLogs = true;

    public event Action<MeatItem, int> OnServed;

    public bool IsPlaced { get; private set; } = false;
    public int GridX { get; private set; }
    public int GridY { get; private set; }

    private GrillGrid grid;

    private bool dragging = false;
    private Vector3 dragOffset;
    private Vector3 startWorldPos;

    private bool wasPlacedBeforeDrag = false;
    private int prevX, prevY;
    private int prevRotIndex;

    private float cookTime = 0f;
    private bool served = false;

    private bool rotateLatch = false;
    private int rotIndex = 0;

    private GameObject visualRoot;
    private readonly List<SpriteRenderer> cellRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> glowRenderers = new List<SpriteRenderer>();

    private BoxCollider2D col;
    private SpriteRenderer parentSrIfExists;

    private Vector3 baseScale;

    private enum CookState { Raw, Under, Ideal, Over, Burnt }
    private CookState lastState = CookState.Raw;
    private Coroutine flashCo;

    // ---------------- UX - Placement Preview ----------------
    [Header("UX - Placement Preview")]
    [SerializeField] private bool placementPreview = true;
    [SerializeField] private float ghostAlpha = 0.35f;
    [SerializeField] private float liftScale = 1.05f;
    [SerializeField] private int dragSortingBoost = 50;
    [SerializeField] private float popDuration = 0.12f;
    [SerializeField] private float popAmount = 0.08f;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeAmount = 0.06f;

    private GameObject ghostRoot;
    private readonly List<SpriteRenderer> ghostRenderers = new List<SpriteRenderer>();
    private int _baseSorting;
    private Coroutine _shakeCo;
    private Coroutine _popCo;

    // Centro visual real (bounds de las celdas)
    public Vector3 PivotToVisualCenterOffsetWorld()
    {
        if (cellRenderers.Count == 0) return Vector3.zero;

        Bounds b = cellRenderers[0].bounds;
        for (int i = 1; i < cellRenderers.Count; i++)
            b.Encapsulate(cellRenderers[i].bounds);

        return transform.position - b.center;
    }

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        parentSrIfExists = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        baseScale = transform.localScale;

        if (grid == null) grid = FindObjectOfType<GrillGrid>();

        if (cellSprite == null && parentSrIfExists != null)
            cellSprite = parentSrIfExists.sprite;

        if (parentSrIfExists != null)
            parentSrIfExists.enabled = false;

        BuildVisualFromShape();
        UpdateParentColliderFromShape();
        UpdateColor();
        ApplyGlow(false);
        lastState = GetCookState();
    }

    // ---------------- SHAPE ----------------
    public List<Vector2Int> GetFootprintOffsets()
    {
        List<Vector2Int> baseCells = new List<Vector2Int>();

        if (useCustomShape && customCells != null && customCells.Length > 0)
            baseCells.AddRange(customCells);
        else
        {
            for (int y = 0; y < heightCells; y++)
                for (int x = 0; x < widthCells; x++)
                    baseCells.Add(new Vector2Int(x, y));
        }

        List<Vector2Int> rotated = new List<Vector2Int>(baseCells.Count);
        foreach (var c in baseCells)
            rotated.Add(RotateCell(c, rotIndex));

        NormalizeToTopLeft(rotated);
        return rotated;
    }

    public Vector2Int GetFootprintSize()
    {
        var cells = GetFootprintOffsets();
        if (cells.Count == 0) return Vector2Int.one;

        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var c in cells)
        {
            if (c.x > maxX) maxX = c.x;
            if (c.y > maxY) maxY = c.y;
        }
        return new Vector2Int(maxX + 1, maxY + 1);
    }

    Vector2Int RotateCell(Vector2Int c, int timesCW)
    {
        int x = c.x, y = c.y;
        timesCW = ((timesCW % 4) + 4) % 4;

        for (int i = 0; i < timesCW; i++)
        {
            int nx = y;
            int ny = -x;
            x = nx; y = ny;
        }
        return new Vector2Int(x, y);
    }

    void NormalizeToTopLeft(List<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0) return;

        int minX = int.MaxValue, minY = int.MaxValue;
        foreach (var c in cells)
        {
            if (c.x < minX) minX = c.x;
            if (c.y < minY) minY = c.y;
        }

        for (int i = 0; i < cells.Count; i++)
            cells[i] = new Vector2Int(cells[i].x - minX, cells[i].y - minY);
    }

    // ---------------- VISUAL ----------------
    void BuildVisualFromShape()
    {
        if (visualRoot == null)
        {
            visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(transform);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;
            visualRoot.transform.localScale = Vector3.one;
        }

        for (int i = visualRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(visualRoot.transform.GetChild(i).gameObject);

        cellRenderers.Clear();
        glowRenderers.Clear();

        float cs = (grid != null) ? grid.cellSize : 1f;
        var cells = GetFootprintOffsets();
        var size = GetFootprintSize();

        Vector3 topLeftLocal = new Vector3(-(size.x - 1) * cs * 0.5f, (size.y - 1) * cs * 0.5f, 0f);

        foreach (var c in cells)
        {
            GameObject cell = new GameObject($"Cell_{c.x}_{c.y}");
            cell.transform.SetParent(visualRoot.transform);

            Vector3 localPos = topLeftLocal + new Vector3(c.x * cs, -c.y * cs, 0f);
            cell.transform.localPosition = localPos;

            // Glow (detrás)
            GameObject glow = new GameObject("Glow");
            glow.transform.SetParent(cell.transform);
            glow.transform.localPosition = Vector3.zero;
            glow.transform.localRotation = Quaternion.identity;
            glow.transform.localScale = Vector3.one * glowScale;

            var glowSr = glow.AddComponent<SpriteRenderer>();
            glowSr.sprite = cellSprite;
            glowSr.sortingOrder = cellSortingOrder - 1;
            glowSr.color = new Color(0.6f, 1f, 0.6f, glowAlpha);
            glowRenderers.Add(glowSr);

            // Main sprite
            var sr = cell.AddComponent<SpriteRenderer>();
            sr.sprite = cellSprite;
            sr.sortingOrder = cellSortingOrder;
            cellRenderers.Add(sr);

            // Collider por celda (para raycast)
            var bc = cell.AddComponent<BoxCollider2D>();
            bc.size = Vector2.one * cs;
            bc.offset = Vector2.zero;
            bc.isTrigger = false;
        }
    }

    void UpdateParentColliderFromShape()
    {
        float cs = (grid != null) ? grid.cellSize : 1f;
        var size = GetFootprintSize();
        col.size = new Vector2(size.x * cs, size.y * cs);
        col.offset = Vector2.zero;
        col.isTrigger = false;
    }

    // ---------------- INPUT ----------------
    void Update()
    {
        if (Time.timeScale == 0f) return;

        // SERVIR
        if (!dragging && IsPlaced && Input.GetMouseButtonDown(1) && !served && IsMouseOverThisMeat())
        {
            int score = CalculateScore();
            if (debugLogs) Debug.Log($"🍽️ Serviste {cut?.displayName} a los {cookTime:0.0}s → Puntaje: {score}");

            served = true;

            if (grid != null) grid.RemoveMeat(this);
            IsPlaced = false;

            OnServed?.Invoke(this, score);

            // UX: limpieza por si acaso
            if (grid != null) grid.ClearPlacementHighlight();
            SetGhost(false);

            gameObject.SetActive(false);
            return;
        }

        // START DRAG
        if (!dragging && Input.GetMouseButtonDown(0) && !served && IsMouseOverThisMeat())
        {
            StartDrag();
        }

        // DRAGGING
        if (dragging)
        {
            DraggingUpdate();

            bool pressed = Input.GetKey(KeyCode.R);
            if (pressed && !rotateLatch)
            {
                rotateLatch = true;
                Rotate90();
            }
            else if (!pressed)
                rotateLatch = false;

            if (Input.GetMouseButtonUp(0))
                EndDrag();
        }

        // COOKING (solo cuando está colocada y no se arrastra)
        if (IsPlaced && !dragging && cut != null)
        {
            float heatMult = 1f;
            if (grid != null) heatMult = grid.GetAverageHeatForItem(this);

            cookTime += Time.deltaTime * heatMult;
            if (cookTime >= cut.burnSeconds) cookTime = cut.burnSeconds;

            UpdateColor();
            UpdateCookingFeedback();
        }

        UpdatePulse();
    }

    void StartDrag()
    {
        dragging = true;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        dragOffset = transform.position - mouseWorld;
        startWorldPos = transform.position;

        wasPlacedBeforeDrag = IsPlaced;
        prevX = GridX; prevY = GridY;
        prevRotIndex = rotIndex;

        if (IsPlaced && grid != null)
        {
            grid.RemoveMeat(this);
            IsPlaced = false;
        }

        ApplyGlow(false);

        // ✅ Importante: al empezar a arrastrar, mantenemos el color actual (no blanco)
        UpdateColor();

        // UX: levantar pieza + ghost
        ApplyDragSorting(true);
        transform.localScale = baseScale * liftScale;

        if (placementPreview && grid != null)
        {
            RebuildGhost();
            SetGhost(true);
        }

        // -------- UX: lift + sorting + ghost ----------
        ApplyDragSorting(true);
        transform.localScale = baseScale * liftScale;

        if (placementPreview && grid != null)
        {
            RebuildGhost();
            SetGhost(true);
        }
    }

    void DraggingUpdate()
    {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        transform.position = mouseWorld + dragOffset;

        // Ghost + highlight mientras arrastras
        if (placementPreview && grid != null)
        {
            int bx, by;
            bool any = grid.GetBestPlacementPreview(this, transform.position, out bx, out by);

            if (any)
            {
                Vector3 snap = grid.GetSnappedWorldFor(this, bx, by);

                if (ghostRoot != null)
                {
                    ghostRoot.transform.position = snap;
                    SetGhost(true);
                }

                SetGhostValid(true);
                grid.ShowPlacementHighlight(this, bx, by, true);
            }
            else
            {
                SetGhost(false);
                grid.ClearPlacementHighlight();
            }
        }
    }


    void EndDrag()
    {
        dragging = false;

        // Limpiar ghost y highlight
        ApplyDragSorting(false);
        transform.localScale = baseScale;

        if (grid != null) grid.ClearPlacementHighlight();
        SetGhost(false);

        bool placed = false;
        if (grid != null)
            placed = grid.TryPlaceMeat(this, transform.position);

        if (!placed)
        {
            ReturnToPrevious();

            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(ShakeRoutine());
        }
        else
        {
            if (_popCo != null) StopCoroutine(_popCo);
            _popCo = StartCoroutine(PopRoutine());

            UpdateColor();
            lastState = GetCookState();
            UpdateCookingFeedback();
        }
    }


    void ReturnToPrevious()
    {
        rotIndex = prevRotIndex;
        BuildVisualFromShape();
        UpdateParentColliderFromShape();

        if (wasPlacedBeforeDrag && grid != null)
        {
            bool ok = grid.TryPlaceMeatAtCell(this, prevX, prevY);
            if (!ok)
            {
                transform.position = startWorldPos;
                IsPlaced = false;
            }
        }
        else
        {
            transform.position = startWorldPos;
            IsPlaced = false;
        }

        ApplyGlow(false);
        UpdateColor();
        lastState = GetCookState();

        // Limpiar ghost y highlight
        if (grid != null) grid.ClearPlacementHighlight();
        SetGhost(false);
        ApplyDragSorting(false);
        transform.localScale = baseScale;


        // -------- UX cleanup ----------
        if (grid != null) grid.ClearPlacementHighlight();
        SetGhost(false);
        ApplyDragSorting(false);
        transform.localScale = baseScale;
    }

    void Rotate90()
    {
        rotIndex = (rotIndex + 1) % 4;
        BuildVisualFromShape();
        UpdateParentColliderFromShape();
        ApplyGlow(false);
        UpdateColor();
        lastState = GetCookState();

        // Si estamos arrastrando, actualizar ghost
        if (placementPreview && dragging)
        {
            RebuildGhost();
        }

        // UX: si estás arrastrando, el ghost debe rehacerse con el nuevo footprint
        if (placementPreview && dragging)
        {
            RebuildGhost();
        }
    }

    public void SetPlaced(GrillGrid g, int x, int y)
    {
        grid = g;
        GridX = x;
        GridY = y;
        IsPlaced = true;

        BuildVisualFromShape();
        UpdateParentColliderFromShape();
        UpdateColor();
        lastState = GetCookState();
        UpdateCookingFeedback();
    }

    // ---------------- COOK FEEDBACK ----------------
    void UpdateCookingFeedback()
    {
        bool ideal = (GetCookState() == CookState.Ideal);
        ApplyGlow(ideal && glowOnIdeal);

        if (flashOnStateChange)
        {
            CookState now = GetCookState();
            if (now != lastState)
            {
                lastState = now;
                TriggerFlash();
            }
        }
    }

    CookState GetCookState()
    {
        if (cut == null) return CookState.Raw;

        float t = cookTime;

        float underMax = Mathf.Max(0f, cut.idealSeconds - cut.toleranceSeconds);
        float idealMax = cut.idealSeconds + cut.toleranceSeconds;

        if (t >= cut.burnSeconds) return CookState.Burnt;
        if (t < underMax) return CookState.Under;
        if (t <= idealMax) return CookState.Ideal;
        return CookState.Over;
    }

    void ApplyGlow(bool on)
    {
        for (int i = 0; i < glowRenderers.Count; i++)
        {
            if (glowRenderers[i] == null) continue;
            glowRenderers[i].enabled = on;
        }
    }

    void TriggerFlash()
    {
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        float t = 0f;

        Color[] baseCols = new Color[cellRenderers.Count];
        for (int i = 0; i < cellRenderers.Count; i++)
            baseCols[i] = (cellRenderers[i] != null) ? cellRenderers[i].color : Color.white;

        while (t < flashDuration)
        {
            t += Time.deltaTime;
            float k = 1f - (t / flashDuration);
            float a = k * flashIntensity;

            for (int i = 0; i < cellRenderers.Count; i++)
            {
                if (cellRenderers[i] == null) continue;
                cellRenderers[i].color = Color.Lerp(baseCols[i], Color.white, a);
            }

            yield return null;
        }

        for (int i = 0; i < cellRenderers.Count; i++)
            if (cellRenderers[i] != null)
                cellRenderers[i].color = baseCols[i];

        flashCo = null;
    }

    void UpdatePulse()
    {
        if (!pulseWhileCooking) return;

        if (IsPlaced && !dragging && !served)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float s = 1f + (t * pulseAmount);
            transform.localScale = baseScale * s;
        }
        else
        {
            transform.localScale = baseScale;
        }
    }

    // ---------------- MOUSE HIT ----------------
    bool IsMouseOverThisMeat()
    {
        if (Camera.main == null) return false;

        Vector2 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        int meatMask = LayerMask.GetMask("Meat");

        RaycastHit2D hit = Physics2D.Raycast(world, Vector2.zero, 0f, meatMask);
        if (hit.collider == null) return false;

        return hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform;
    }

    // ---------------- SCORE ----------------
    public int CalculateScore()
    {
        if (cut == null) return 0;

        float t = cookTime;
        if (t >= cut.burnSeconds) return 0;

        float diff = Mathf.Abs(t - cut.idealSeconds);

        if (diff <= cut.toleranceSeconds)
        {
            float k = 1f - (diff / cut.toleranceSeconds) * 0.3f;
            return Mathf.RoundToInt(cut.baseScore * k);
        }
        else
        {
            float extra = diff - cut.toleranceSeconds;
            float window = Mathf.Max(0.0001f, cut.burnSeconds - (cut.idealSeconds + cut.toleranceSeconds));
            float k = Mathf.Clamp01(1f - extra / window);
            return Mathf.RoundToInt(cut.baseScore * 0.6f * k);
        }
    }

    // ✅ FIX PRINCIPAL: mantener color durante drag si ya hay cookTime/cut
    void UpdateColor()
    {
        Color c;

        if (cut == null)
        {
            c = Color.white;
        }
        else
        {
            bool showCookColor = IsPlaced || dragging || cookTime > 0f;

            if (!showCookColor)
            {
                c = Color.white;
            }
            else if (cookTime < cut.idealSeconds - cut.toleranceSeconds)
                c = new Color(1f, 0.6f, 0.6f);
            else if (cookTime <= cut.idealSeconds + cut.toleranceSeconds)
                c = new Color(0.6f, 1f, 0.6f);
            else if (cookTime < cut.burnSeconds)
                c = new Color(1f, 0.9f, 0.5f);
            else
                c = new Color(0.2f, 0.2f, 0.2f);
        }

        for (int i = 0; i < cellRenderers.Count; i++)
            if (cellRenderers[i] != null)
                cellRenderers[i].color = c;

        if (glowRenderers.Count > 0)
        {
            CookState st = GetCookState();
            Color gc = new Color(0.6f, 1f, 0.6f, glowAlpha);
            if (st == CookState.Over) gc = new Color(1f, 0.9f, 0.5f, glowAlpha);
            if (st == CookState.Burnt) gc = new Color(0.25f, 0.25f, 0.25f, glowAlpha);

            for (int i = 0; i < glowRenderers.Count; i++)
                if (glowRenderers[i] != null)
                    glowRenderers[i].color = gc;
        }
    }

    // ---------------- UX HELPERS ----------------
    void EnsureGhost()
    {
        if (ghostRoot != null) return;

        ghostRoot = new GameObject("Ghost");
        ghostRoot.transform.SetParent(null);
        ghostRoot.transform.position = transform.position;
        ghostRoot.transform.rotation = Quaternion.identity;
        ghostRoot.transform.localScale = Vector3.one;
        ghostRoot.SetActive(false);
    }

    void RebuildGhost()
    {
        EnsureGhost();

        for (int i = ghostRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(ghostRoot.transform.GetChild(i).gameObject);

        ghostRenderers.Clear();

        float cs = (grid != null) ? grid.cellSize : 1f;
        var cells = GetFootprintOffsets();
        var size = GetFootprintSize();

        Vector3 topLeftLocal = new Vector3(-(size.x - 1) * cs * 0.5f, (size.y - 1) * cs * 0.5f, 0f);

        foreach (var c in cells)
        {
            GameObject cell = new GameObject($"G_{c.x}_{c.y}");
            cell.transform.SetParent(ghostRoot.transform);

            Vector3 localPos = topLeftLocal + new Vector3(c.x * cs, -c.y * cs, 0f);
            cell.transform.localPosition = localPos;

            var sr = cell.AddComponent<SpriteRenderer>();
            sr.sprite = cellSprite;
            sr.sortingOrder = cellSortingOrder + dragSortingBoost;
            sr.color = new Color(1f, 1f, 1f, ghostAlpha);
            ghostRenderers.Add(sr);
        }
    }

    void SetGhost(bool on)
    {
        if (ghostRoot == null) EnsureGhost();
        if (ghostRoot != null) ghostRoot.SetActive(on);
    }

    void SetGhostValid(bool valid)
    {
        Color c = valid ? new Color(0.35f, 1f, 0.35f, ghostAlpha)
                        : new Color(1f, 0.35f, 0.35f, ghostAlpha);

        for (int i = 0; i < ghostRenderers.Count; i++)
            if (ghostRenderers[i] != null)
                ghostRenderers[i].color = c;
    }

    void ApplyDragSorting(bool draggingNow)
    {
        if (cellRenderers.Count == 0) return;

        if (draggingNow)
        {
            _baseSorting = cellSortingOrder;
            for (int i = 0; i < cellRenderers.Count; i++)
                if (cellRenderers[i] != null)
                    cellRenderers[i].sortingOrder = _baseSorting + dragSortingBoost;
        }
        else
        {
            for (int i = 0; i < cellRenderers.Count; i++)
                if (cellRenderers[i] != null)
                    cellRenderers[i].sortingOrder = _baseSorting;
        }
    }

    IEnumerator PopRoutine()
    {
        Vector3 s0 = baseScale;
        Vector3 s1 = baseScale * (1f + popAmount);

        float t = 0f;
        while (t < popDuration)
        {
            t += Time.deltaTime;
            float k = t / popDuration;
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            transform.localScale = Vector3.Lerp(s1, s0, ease);
            yield return null;
        }

        transform.localScale = s0;
        _popCo = null;
    }

    IEnumerator ShakeRoutine()
    {
        Vector3 p0 = transform.position;
        float t = 0f;

        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            float k = t / shakeDuration;
            float s = Mathf.Sin(k * Mathf.PI * 8f) * (1f - k);
            transform.position = p0 + new Vector3(s * shakeAmount, 0f, 0f);
            yield return null;
        }

        transform.position = p0;
        _shakeCo = null;
    }
    public bool IsBurnt()
    {
        return GetCookStateDebug() == "Burnt";
    }

    public bool IsUnderCooked()
    {
        return GetCookStateDebug() == "Under" || GetCookStateDebug() == "Raw";
    }

    public bool IsIdeal()
    {
        if (cut == null) return false;
        return GetCookState() == CookState.Ideal;
    }


    // Helper solo para lógica externa
    string GetCookStateDebug()
    {
        float t = cookTime;

        if (cut == null) return "Raw";
        if (t >= cut.burnSeconds) return "Burnt";
        if (t < cut.idealSeconds - cut.toleranceSeconds) return "Under";
        if (t <= cut.idealSeconds + cut.toleranceSeconds) return "Ideal";
        return "Over";
    }
    void OnEnable()
    {
        var rm = FindObjectOfType<RunManager>();
        if (rm != null) rm.RegisterMeat(this);
    }

    void OnDisable()
    {
        var rm = FindObjectOfType<RunManager>();
        if (rm != null) rm.UnregisterMeat(this);
    }

}
