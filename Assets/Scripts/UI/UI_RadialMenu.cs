using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // <-- NECESARIO PARA LEER EL MANDO
using System.Collections.Generic;

public class UI_RadialMenu : MonoBehaviour
{
    public static UI_RadialMenu Instance;

    [Header("Configuración General")]
    public GameObject MenuContainer;
    [Tooltip("Distancia en píxeles desde el centro para cancelar (Ratón)")]
    public float CancelRadius = 50f;
    
    [Header("Visuales")]
    public Image CancelCenterImage;
    public Color NormalColor = new Color(1, 1, 1, 0.5f);
    public Color HighlightColor = new Color(1, 1, 1, 1f);

    [System.Serializable]
    public struct RadialSlice
    {
        public GameObject Root;
        public Image BackgroundWedge;
        public Image IconImage;
    }

    [Header("Rebanadas (Derecha, Arriba, Izquierda, Abajo)")]
    public List<RadialSlice> Slices;
    private int selectedIndex = -1;

    void Awake()
    {
        if (Instance == null) Instance = this;
        if (MenuContainer != null) MenuContainer.SetActive(false);
    }

    public void Show(IRadialMenuAbility ability)
    {
        if (ability == null || ability.RadialIcons == null) return;

        for (int i = 0; i < Slices.Count; i++)
        {
            if (i < ability.RadialIcons.Length && ability.RadialIcons[i] != null)
            {
                Slices[i].Root.SetActive(true);
                Slices[i].IconImage.sprite = ability.RadialIcons[i];
            }
            else
            {
                Slices[i].Root.SetActive(false);
            }
        }

        MenuContainer.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public int HideAndGetSelection()
    {
        MenuContainer.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        return selectedIndex;
    }

    void Update()
    {
        if (!MenuContainer.activeSelf) return;

        Vector2 inputDir = Vector2.zero;
        bool isUsingGamepad = false;

        // 1. INTENTAMOS LEER EL MANDO (Joystick Derecho)
        if (Gamepad.current != null && Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.05f)
        {
            // El joystick ya devuelve valores normalizados (-1 a 1) desde el centro
            inputDir = Gamepad.current.rightStick.ReadValue();
            isUsingGamepad = true;
        }
        // 2. SI NO HAY MANDO ACTIVO, LEEMOS EL RATÓN
        else if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            inputDir = mousePos - screenCenter;

            // Lógica de cancelación por estar en el centro de la pantalla (Ratón)
            if (inputDir.magnitude < CancelRadius)
            {
                SetSelectedIndex(-1);
                return;
            }
        }

        // Lógica de cancelación para Mando (Soltar el Joystick)
        if (isUsingGamepad && inputDir.sqrMagnitude < 0.05f)
        {
            SetSelectedIndex(-1);
            return;
        }

        // 3. CÁLCULO MATEMÁTICO DE LA DIRECCIÓN
        if (inputDir.sqrMagnitude > 0.01f) 
        {
            // Atan2 devuelve el ángulo; lo pasamos a grados
            float angle = Mathf.Atan2(inputDir.y, inputDir.x) * Mathf.Rad2Deg;
            
            // Convertimos de -180/180 a 0-360
            if (angle < 0) angle += 360f;

            // Ajuste matemático para 4 porciones:
            // Giramos el cálculo 45 grados para que los ejes formen una "X" y no una "+"
            float shiftedAngle = (angle + 45f) % 360f;
            
            // rawIndex: 0 = Derecha, 1 = Arriba, 2 = Izquierda, 3 = Abajo
            int newIndex = (int)(shiftedAngle / 90f);
            SetSelectedIndex(newIndex);
        }
    }

    private void SetSelectedIndex(int index)
    {
        selectedIndex = index;

        // Colorear el botón del centro (Resaltado si está cancelando)
        if (CancelCenterImage != null)
        {
            CancelCenterImage.color = (selectedIndex == -1) ? HighlightColor : NormalColor;
        }

        // Colorear las rebanadas
        for (int i = 0; i < Slices.Count; i++)
        {
            if (Slices[i].BackgroundWedge != null)
            {
                Slices[i].BackgroundWedge.color = (i == selectedIndex) ? HighlightColor : NormalColor;
            }
        }
    }
}