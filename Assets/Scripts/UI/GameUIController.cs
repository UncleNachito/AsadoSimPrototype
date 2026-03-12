using UnityEngine;
using TMPro;

public class GameUIController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject panelLevelComplete;
    public GameObject panelGameOver;
    public GameObject panelPerkChoice;

    [Header("Perk Choice UI")]
    public TextMeshProUGUI txtPerkTitle;
    public TextMeshProUGUI btnAText;
    public TextMeshProUGUI btnBText;
    public TextMeshProUGUI btnCText; // NUEVO (opcional)

    void Start()
    {
        Debug.Log("UI Start -> panelLevelComplete: " + (panelLevelComplete ? panelLevelComplete.name : "NULL"));
        Debug.Log("UI Start -> panelGameOver: " + (panelGameOver ? panelGameOver.name : "NULL"));
        Debug.Log("UI Start -> panelPerkChoice: " + (panelPerkChoice ? panelPerkChoice.name : "NULL"));

        HideAll();
    }

    public void HideAll()
    {
        if (panelLevelComplete != null) panelLevelComplete.SetActive(false);
        if (panelGameOver != null) panelGameOver.SetActive(false);
        if (panelPerkChoice != null) panelPerkChoice.SetActive(false);

        Time.timeScale = 1f;
    }

    public void ShowLevelComplete()
    {
        HideAll();
        if (panelLevelComplete != null) panelLevelComplete.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ShowGameOver()
    {
        HideAll();
        if (panelGameOver != null) panelGameOver.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ShowPerkChoice()
    {
        HideAll();
        if (panelPerkChoice != null) panelPerkChoice.SetActive(true);
        Time.timeScale = 0f;
    }

    // NUEVO: setter de oferta (para que RunManager no toque UI directo)
    public void SetPerkOffer(string title, string a, string b, string c = null)
    {
        if (txtPerkTitle != null && !string.IsNullOrEmpty(title))
            txtPerkTitle.text = title;

        if (btnAText != null) btnAText.text = a ?? "-";
        if (btnBText != null) btnBText.text = b ?? "-";

        // C es opcional (si no lo conectas, no revienta)
        if (btnCText != null)
        {
            bool hasC = !string.IsNullOrEmpty(c);
            btnCText.gameObject.SetActive(hasC);
            btnCText.text = hasC ? c : "";
        }
    }
}
