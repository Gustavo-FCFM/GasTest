using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_RadialMenu
//
// Menú circular genérico para habilidades que implementan
// IRadialMenuAbility (ej: elegir qué tótem invocar). Muestra hasta
// Slices.Length opciones alrededor del cursor y resalta la que está
// más cerca del mouse; el centro de la rueda funciona como botón de
// cancelar. PlayerController lo abre/cierra según el input.
// ============================================================
public class UI_RadialMenu : MonoBehaviour
{
    // Instancia única de la escena, para que cualquier habilidad pueda
    // pedir Show()/HideAndGetSelection() sin necesitar una referencia
    // asignada a mano.
    public static UI_RadialMenu Instance;

    // Referencias UI de una porción del menú.
    [System.Serializable]
    public class RadialSlice
    {
        public GameObject Root;         // Se activa/desactiva según haya opción o no
        public Image BackgroundWedge;   // Cambia de color al estar resaltada
        public Image IconImage;         // Ícono de la opción
    }

    [Header("Referencias UI")]
    public GameObject MenuContainer;
    // Porciones visuales disponibles; determina el máximo de opciones que
    // el menú puede mostrar de una vez.
    public RadialSlice[] Slices;

    [Header("Botón de Cancelación (Centro)")]
    public Image CancelCenterImage;
    [Tooltip("Distancia desde el centro en la que se considera que el ratón está en el botón de cancelar")]
    public float CancelRadius = 50f;

    [Header("Configuración Visual")]
    public Color NormalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    public Color HighlightColor = new Color(1f, 0.8f, 0f, 1f);

    // Índice de la opción resaltada actualmente (-1 = cancelar).
    private int selectedIndex = -1;
    // Cuántas opciones tiene el menú abierto actualmente.
    private int optionCount = 0;

    // Registra la instancia única y arranca con el menú oculto.
    void Awake()
    {
        if (Instance == null) Instance = this;
        if (MenuContainer != null) MenuContainer.SetActive(false);
    }

    // Mientras el menú está abierto, calcula qué porción está más cerca
    // del cursor (o si está en la zona central de cancelar) y actualiza
    // los colores para reflejarlo.
    void Update()
    {
        if (MenuContainer.activeSelf && optionCount > 0)
        {
            Vector2 mousePos = Input.mousePosition;
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 direction = mousePos - screenCenter;

            // Zona central: cancelar
            if (direction.magnitude <= CancelRadius)
            {
                selectedIndex = -1;

                if (CancelCenterImage != null) CancelCenterImage.color = HighlightColor;

                for (int i = 0; i < optionCount; i++)
                {
                    if (i < Slices.Length && Slices[i].BackgroundWedge != null)
                        Slices[i].BackgroundWedge.color = NormalColor;
                }
            }
            // Fuera del centro: elegir la porción más cercana al ángulo del mouse
            else
            {
                if (CancelCenterImage != null) CancelCenterImage.color = NormalColor;

                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;

                float sliceAngle = 360f / optionCount;
                float adjustedAngle = (angle + (sliceAngle / 2f)) % 360f;
                selectedIndex = Mathf.FloorToInt(adjustedAngle / sliceAngle);

                if (selectedIndex >= optionCount) selectedIndex = 0;

                for (int i = 0; i < optionCount; i++)
                {
                    if (i < Slices.Length && Slices[i].BackgroundWedge != null)
                    {
                        Slices[i].BackgroundWedge.color = (i == selectedIndex) ? HighlightColor : NormalColor;
                    }
                }
            }
        }
    }

    // Abre el menú con los íconos de la habilidad dada, y libera el
    // cursor para poder apuntar con el mouse.
    public void Show(IRadialMenuAbility ability)
    {
        if (MenuContainer == null) return;

        Sprite[] icons = ability.RadialIcons;
        optionCount = icons != null ? icons.Length : 0;
        selectedIndex = -1; // arranca en el centro (cancelado)

        if (CancelCenterImage != null) CancelCenterImage.color = NormalColor;

        for (int i = 0; i < Slices.Length; i++)
        {
            if (i < optionCount)
            {
                Slices[i].Root.SetActive(true);
                Slices[i].IconImage.sprite = icons[i];
                Slices[i].BackgroundWedge.color = NormalColor;
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

    // Cierra el menú, vuelve a bloquear el cursor para el modo juego, y
    // devuelve qué opción quedó seleccionada al soltar (-1 = cancelado).
    public int HideAndGetSelection()
    {
        if (MenuContainer != null) MenuContainer.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        return selectedIndex;
    }
}
