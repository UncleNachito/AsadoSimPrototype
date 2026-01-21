using TMPro;
using UnityEngine;

public class FloatingText : MonoBehaviour
{
    [SerializeField] private float lifetime = 1f;
    [SerializeField] private float rise = 60f; // pixeles hacia arriba
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private TextMeshProUGUI tmp;
    private RectTransform rt;
    private CanvasGroup cg;

    private Vector2 startPos;
    private float t;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
    }

    public void Setup(string text, Vector2 screenPos)
    {
        tmp.text = text;
        rt.anchoredPosition = screenPos;
        startPos = screenPos;
        t = 0f;
        cg.alpha = 1f;
    }

    void Update()
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / lifetime);

        float k = ease.Evaluate(u);
        rt.anchoredPosition = startPos + Vector2.up * (rise * k);
        cg.alpha = 1f - u;

        if (t >= lifetime) Destroy(gameObject);
    }
}
