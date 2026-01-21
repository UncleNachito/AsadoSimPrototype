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

    void Start()
    {
        // Evita el crash si algo está null (y te lo avisa claro)
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
}
