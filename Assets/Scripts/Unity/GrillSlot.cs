using UnityEngine;

public class GrillSlot : MonoBehaviour
{
    [Header("Base Visual")]
    public SpriteRenderer sr;

    [Header("Highlight Overlay")]
    [SerializeField] private bool autoCreateHighlight = true;
    [SerializeField] private float highlightAlpha = 0.35f;
    [SerializeField] private int highlightSortingOffset = 10;

    private SpriteRenderer highlightSr;

    void Reset()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
    }

    void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (autoCreateHighlight) EnsureHighlight();
        SetHighlight(false, true);
    }

    void EnsureHighlight()
    {
        if (highlightSr != null) return;

        Transform t = transform.Find("Highlight");
        if (t != null)
        {
            highlightSr = t.GetComponent<SpriteRenderer>();
        }
        else
        {
            GameObject go = new GameObject("Highlight");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            highlightSr = go.AddComponent<SpriteRenderer>();
        }

        highlightSr.sprite = sr != null ? sr.sprite : null;
        highlightSr.sortingLayerID = sr.sortingLayerID;
        highlightSr.sortingOrder = (sr != null ? sr.sortingOrder : 0) + highlightSortingOffset;
        highlightSr.enabled = false;
    }

    public void SetHighlight(bool on, bool valid)
    {
        if (autoCreateHighlight && highlightSr == null) EnsureHighlight();
        if (highlightSr == null) return;

        highlightSr.enabled = on;
        if (!on) return;

        Color c = valid ? new Color(0.35f, 1f, 0.35f, highlightAlpha)
                        : new Color(1f, 0.35f, 0.35f, highlightAlpha);
        highlightSr.color = c;
    }
}
