using UnityEngine;
using UnityEngine.UI;

public class UI_RadialMenu : MonoBehaviour
{
    public static UI_RadialMenu Instance; 

    [System.Serializable]
    public class RadialSlice
    {
        public GameObject Root;
        public Image BackgroundWedge; 
        public Image IconImage; 
    }

    [Header("Referencias UI")]
    public GameObject MenuContainer; 
    public RadialSlice[] Slices; 
    
    [Header("Botón de Cancelación (Centro)")]
    public Image CancelCenterImage;
    [Tooltip("Distancia desde el centro en la que se considera que el ratón está en el botón de cancelar")]
    public float CancelRadius = 50f;

    [Header("Configuración Visual")]
    public Color NormalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f); 
    public Color HighlightColor = new Color(1f, 0.8f, 0f, 1f);   

    private int selectedIndex = -1;
    private int optionCount = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        if (MenuContainer != null) MenuContainer.SetActive(false); 
    }

    void Update()
    {
        if (MenuContainer.activeSelf && optionCount > 0)
        {
            Vector2 mousePos = Input.mousePosition;
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 direction = mousePos - screenCenter;

            // --- LÓGICA DE CANCELACIÓN (CENTRO) ---
            if (direction.magnitude <= CancelRadius)
            {
                selectedIndex = -1; // -1 significa "Cancelar"
                
                if (CancelCenterImage != null) CancelCenterImage.color = HighlightColor;
                
                // Apagamos el brillo de las otras opciones
                for (int i = 0; i < optionCount; i++)
                {
                    if (i < Slices.Length && Slices[i].BackgroundWedge != null)
                        Slices[i].BackgroundWedge.color = NormalColor;
                }
            }
            // --- LÓGICA DE SELECCIÓN DE REBANADA ---
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

    public void Show(IRadialMenuAbility ability)
    {
        if (MenuContainer == null) return;
        
        Sprite[] icons = ability.RadialIcons;
        optionCount = icons != null ? icons.Length : 0;
        selectedIndex = -1; // Por defecto inicia en el centro (cancelado)

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

    public int HideAndGetSelection()
    {
        if (MenuContainer != null) MenuContainer.SetActive(false);
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        return selectedIndex;
    }
}