using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_Nameplate  (la barra de arriba de la cabeza, como PREFAB — 19 de octubre de 2026)
//
// Un solo prefab, Prefabs/UI/Nameplate, para TODOS: jugadores, enemigos, tótems, compañeros,
// copias. Antes cada prefab tenía su propia copia de la barra (la del juego de la jam, pegada a
// mano, cada una de un tamaño), así que cambiar cómo se ven las vidas era tocar diez prefabs.
// Ahora cada uno lleva una INSTANCIA de este prefab a la altura de su cabeza: lo que se cambie
// en el prefab (tamaño, colores, dónde van los buffs, el nombre, el ícono de clase) cambia en
// todos. Lo único propio de cada uno es a qué altura está.
//
// Este componente solo dice cuáles son las piezas y cómo se ve. Quién la mueve es
// UI_WorldHealthbar, en la raíz del personaje (vida, color por equipo, esconderla, mirar a la
// cámara): la encuentra sola entre sus hijos.
// ============================================================
public class UI_Nameplate : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("La barra de vida (Image Type: Filled): se usa su fillAmount y su color.")]
    [Label("Relleno de vida")]
    public Image HealthFill;

    [Tooltip("Alternativa/complemento: un Slider de vida. Opcional.")]
    [Label("Barra de vida (opcional)")]
    public Slider HealthSlider;

    [Tooltip("El ESCUDO, como vida extra: un tramo de otro color justo después de la vida. Va DETRÁS " +
             "del relleno de vida, en el mismo lugar y con el mismo tamaño: se estira de 0 hasta vida + " +
             "escudo, y la vida lo tapa hasta donde llega. Si vida + escudo pasa de la máxima, sale por " +
             "la derecha de la barra (hasta el doble). Se le mueve el ancla derecha (como al relleno de " +
             "un Slider); si es Filled, no pasa de la barra. Vacío = sin escudo a la vista.")]
    [Label("Relleno de escudo")]
    public Image ShieldFill;

    [Tooltip("Los buffs y debuffs del personaje.")]
    [Label("Contenedor de efectos")]
    public UI_EffectContainer EffectContainer;

    [Tooltip("Aviso de Crítico mejorado disponible contra este enemigo.")]
    [Label("Aviso de crítico mejorado")]
    public GameObject FirstStrikeMarker;

    [Tooltip("El nombre del jugador. En los NPCs queda vacío.")]
    [Label("Texto del nombre")]
    public TMP_Text NameText;

    [Tooltip("El ícono de la clase (solo jugadores y bots; en los NPCs queda apagado).")]
    [Label("Ícono de clase")]
    public Image ClassIcon;

    [Header("Cómo se ve")]
    [Label("Color aliado")]
    public Color AllyColor = new Color(0.2f, 0.9f, 0.2f, 1f);

    [Label("Color enemigo")]
    public Color EnemyColor = new Color(0.9f, 0.2f, 0.2f, 1f);

    [Tooltip("Capas del entorno que TAPAN la barra de un enemigo (paredes, piso). Los aliados no se ocultan.")]
    [Label("Capas que tapan")]
    public LayerMask ObstacleLayer = 1;

    [Label("Mostrar ícono de clase")]
    public bool ShowClassIcon = true;
}
