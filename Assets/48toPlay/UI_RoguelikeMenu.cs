using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UI_RoguelikeMenu : MonoBehaviour
{
    [Header("Contenedor Principal")]
    public GameObject PanelContainer;
    public TextMeshProUGUI TitleText;
    public TextMeshProUGUI RoundText;

    [Header("Botones de Opción")]
    // Asigna los 3 botones del canvas aquí
    public Button ButtonOption1;
    public Button ButtonOption2;
    public Button ButtonOption3;

    [Header("Textos de los Botones")]
    // Asigna los textos hijos de cada botón (TextMeshPro)
    public TextMeshProUGUI TextOption1;
    public TextMeshProUGUI TextOption2;
    public TextMeshProUGUI TextOption3;

    private GameMode_Survival gameMode;

    void Start()
    {
        gameMode = FindFirstObjectByType<GameMode_Survival>();
        
        // Configurar Listeners (0, 1, 2 son los índices de la opción elegida)
        ButtonOption1.onClick.AddListener(() => SelectOption(0));
        ButtonOption2.onClick.AddListener(() => SelectOption(1));
        ButtonOption3.onClick.AddListener(() => SelectOption(2));

        Hide();
    }

    // Este método recibe la lista de descripciones generadas por el GameMode
    public void Show(int nextRound, List<string> optionDescriptions)
    {
        PanelContainer.SetActive(true);
        TitleText.text = $"¡RONDA {nextRound - 1} COMPLETADA!";
        RoundText.text = $"Elige una mejora para sobrevivir a la Ronda {nextRound}";

        // Actualizar textos de los botones con la info recibida
        if (optionDescriptions.Count > 0) TextOption1.text = optionDescriptions[0];
        if (optionDescriptions.Count > 1) TextOption2.text = optionDescriptions[1];
        if (optionDescriptions.Count > 2) TextOption3.text = optionDescriptions[2];

        // Pausar juego
        Time.timeScale = 0f; 
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        PanelContainer.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked; 
        Cursor.visible = false;
    }

    private void SelectOption(int index)
    {
        if (gameMode != null)
        {
            gameMode.ApplySelectedOption(index);
        }
        Hide();
    }
}