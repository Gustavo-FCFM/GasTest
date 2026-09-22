using UnityEngine;
using System.Collections.Generic;

// ============================================================
// PlayerVisibility
//
// Qué se ve de un personaje con el tag Status_Invisible, y eso depende de QUIÉN mira:
//
//   · Un ENEMIGO no lo ve. Se le apagan los Renderers y listo.
//   · ÉL MISMO se ve FANTASMA: su modelo pasa a semitransparente y deja de proyectar
//     sombra. Es el aviso de que está invisible — el ícono del buff en el HUD se
//     pierde en medio de una pelea, el propio cuerpo no.
//   · Un ALIADO lo ve normal (o fantasma también, si prendés GhostForAllies).
//
// CÓMO FUNCIONA EN RED: no hace falta nada especial. El tag lo aplica el
// servidor (vía un GameplayEffect, ej. Emboscada sombría del Asesino) y viaja a
// todos los clientes por NetTags, así que la copia local del invisible tiene el
// tag en TODAS las pantallas. Lo que cambia en cada pantalla es la AFILIACIÓN:
// cada cliente compara al invisible contra SU jugador local (PlayerController.
// LocalPlayer) y decide qué hacer. Por eso el mismo personaje puede estar
// oculto en la pantalla del enemigo y fantasma en la suya, sin sincronizar
// nada extra.
//
// Nunca toca colliders: las habilidades del servidor tienen que poder seguir
// golpeando a un invisible.
//
// PREFAB: agregá este componente al prefab del Player (junto al ASC).
// ============================================================
[RequireComponent(typeof(AbilitySystemComponent))]
public class PlayerVisibility : MonoBehaviour
{
    [Header("Fantasma (cómo te ves vos mismo estando invisible)")]
    [Tooltip("Opacidad del modelo mientras estás invisible. 0 = no te ves nada, 1 = normal. " +
             "0.35 alcanza para ubicarte sin que parezca que la habilidad no hizo nada.")]
    [Range(0f, 1f)]
    public float GhostAlpha = 0.35f;

    [Tooltip("Que tus ALIADOS también te vean fantasma. Apagado, ellos te ven normal. " +
             "Prendido, de un vistazo saben que el enemigo no te ve.")]
    public bool GhostForAllies;

    private AbilitySystemComponent _asc;

    // En qué estado dejamos a este personaje EN ESTA PANTALLA.
    private enum EView { Normal, Hidden, Ghost }
    private EView _view = EView.Normal;

    // Los Renderers que apagamos NOSOTROS, para volver a prender exactamente esos
    // (y no uno que estaba apagado a propósito por otra razón).
    private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();

    // Lo que le sacamos a cada Renderer para volverlo fantasma, para poder devolvérselo
    // tal cual: sus materiales originales (los compartidos, no las copias que creamos) y
    // si proyectaba sombra.
    private struct GhostedRenderer
    {
        public Renderer Target;
        public Material[] OriginalMaterials;
        public UnityEngine.Rendering.ShadowCastingMode OriginalShadows;
        public Material[] Copies;
    }

    private readonly List<GhostedRenderer> _ghosted = new List<GhostedRenderer>();

    private void Awake()
    {
        _asc = GetComponent<AbilitySystemComponent>();
    }

    private void Update()
    {
        if (_asc == null) return;

        EView wanted = ResolveView();
        if (wanted == _view) return;

        // Siempre se pasa por Normal: así no hay que escribir las seis transiciones
        // posibles entre estados, y un cambio de afiliación en el medio no deja
        // Renderers apagados ni materiales de fantasma puestos.
        Restore();

        if      (wanted == EView.Hidden) Hide();
        else if (wanted == EView.Ghost)  Ghost();

        _view = wanted;
    }

    // Al destruirse (o despawnear) nos aseguramos de no dejar nada tocado.
    private void OnDisable()
    {
        Restore();
        _view = EView.Normal;
    }

    private EView ResolveView()
    {
        if (!_asc.HasTag(EGameplayTag.Status_Invisible)) return EView.Normal;

        PlayerController local = PlayerController.LocalPlayer;
        if (local == null) return EView.Normal;   // servidor sin cliente: no hay a quién mostrarle nada

        AbilitySystemComponent localASC = local.GetComponent<AbilitySystemComponent>();
        if (localASC == null) return EView.Normal;

        if (ReferenceEquals(localASC, _asc)) return EView.Ghost;          // soy yo
        if (localASC.IsEnemyOf(_asc))        return EView.Hidden;         // me ve un enemigo

        return GhostForAllies ? EView.Ghost : EView.Normal;               // un aliado
    }

    // =========================================================
    // ESCONDER (lo que ve un enemigo)
    // =========================================================

    // Apaga los Renderers visibles ahora mismo y se los guarda. Se recalculan cada
    // vez (y no se cachean en Awake) porque las armas se instancian en runtime al
    // equipar la clase — ver PlayerController.UpdateVisuals.
    private void Hide()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled) continue;
            r.enabled = false;
            _hiddenRenderers.Add(r);
        }
    }

    // =========================================================
    // FANTASMA (lo que ves vos)
    // =========================================================

    // Cambia los materiales por copias semitransparentes y apaga la sombra.
    //
    // POR QUÉ COPIAS Y NO r.materials a secas: tocar el material de un Renderer sin
    // copiarlo primero modifica el ASSET, y el modelo quedaría transparente para todo
    // el proyecto — también para los demás personajes que compartan ese material.
    //
    // Solo mallas: las partículas y las estelas traen sus propios materiales de efecto
    // y convertirlos a transparente los rompe. Un asesino invisible con su estela a
    // full es un detalle menor al lado de eso.
    private void Ghost()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(false))
        {
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
            if (!r.enabled) continue;

            Material[] originals = r.sharedMaterials;
            if (originals == null || originals.Length == 0) continue;

            Material[] copies = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
                if (originals[i] != null) copies[i] = BuildGhostMaterial(originals[i]);

            _ghosted.Add(new GhostedRenderer
            {
                Target            = r,
                OriginalMaterials = originals,
                OriginalShadows   = r.shadowCastingMode,
                Copies            = copies
            });

            r.materials = copies;
            // Un fantasma que proyecta una sombra sólida delata que sigue ahí, y encima
            // se ve mal: la sombra es del modelo entero, opaca.
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // La copia fantasma de un material, SIEMPRE sobre el shader Lit de URP.
    //
    // POR QUÉ SE CAMBIA EL SHADER Y NO SE TOCA EL ORIGINAL: porque no todos saben ser
    // transparentes. El cuerpo del personaje usa un material del pack de Kevin Iglesias
    // con un shader HEREDADO de Unity (no el de URP): no tiene _BaseColor ni _Surface, y
    // bajarle el alfa a su _Color no hace absolutamente nada. Por eso en la primera
    // versión solo se volvían fantasma las armas —esas sí traen materiales de URP— y el
    // cuerpo se quedaba opaco.
    //
    // Pasando la copia a URP/Lit el resultado es el mismo para cualquier material de
    // origen, venga del pack que venga. Se le lleva la textura y el color, que es lo que
    // hace reconocible al personaje; el resto (normales, brillos) no se extraña con el
    // modelo al 35%.
    private Material BuildGhostMaterial(Material source)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");

        // Sin ese shader a mano (no debería pasar en este proyecto, pero una build puede
        // dejarlo afuera si nada lo usa), se hace lo que se pueda con el original.
        if (lit == null)
        {
            Material fallback = new Material(source);
            MakeTransparent(fallback, GhostAlpha);
            return fallback;
        }

        Material ghost = new Material(lit);

        Texture tex = null;
        if      (source.HasProperty("_BaseMap")) tex = source.GetTexture("_BaseMap");
        else if (source.HasProperty("_MainTex")) tex = source.GetTexture("_MainTex");
        if (tex != null) ghost.SetTexture("_BaseMap", tex);

        Color tint = Color.white;
        if      (source.HasProperty("_BaseColor")) tint = source.GetColor("_BaseColor");
        else if (source.HasProperty("_Color"))     tint = source.GetColor("_Color");

        tint.a = GhostAlpha;
        ghost.SetColor("_BaseColor", tint);

        MakeTransparent(ghost, GhostAlpha);
        return ghost;
    }

    // Pasa un material de URP a transparente. No alcanza con bajarle el alfa al color:
    // si el material está compilado como OPACO, el alfa no se mira siquiera. Hay que
    // cambiarle el modo de mezcla, apagarle la escritura de profundidad y mandarlo a la
    // cola de transparentes — que es exactamente lo que hace el desplegable "Surface
    // Type" del inspector.
    private static void MakeTransparent(Material mat, float alpha)
    {
        if (mat == null) return;

        // Cada propiedad se pregunta antes de escribirla: los packs comprados traen
        // shaders que no son el Lit de URP, y pedirle una propiedad que no tiene llena
        // la consola de errores en cada frame.
        SetIfPresent(mat, "_Surface", 1f);   // 0 = opaco, 1 = transparente
        SetIfPresent(mat, "_Blend",   0f);   // mezcla por alfa
        SetIfPresent(mat, "_ZWrite",  0f);
        SetIfPresent(mat, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetIfPresent(mat, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");   // URP
        mat.EnableKeyword("_ALPHABLEND_ON");              // shaders heredados de Unity
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        // El "Rendering Mode" de los shaders viejos. 3 = Transparent.
        SetIfPresent(mat, "_Mode", 3f);

        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        // El color va en _BaseColor en URP; se escribe también _Color por si el material
        // usa un shader viejo.
        if (mat.HasProperty("_BaseColor"))
        {
            Color c = mat.GetColor("_BaseColor");
            c.a = alpha;
            mat.SetColor("_BaseColor", c);
        }

        if (mat.HasProperty("_Color"))
        {
            Color tint = mat.GetColor("_Color");
            tint.a = alpha;
            mat.SetColor("_Color", tint);
        }
    }

    private static void SetIfPresent(Material mat, string property, float value)
    {
        if (mat.HasProperty(property)) mat.SetFloat(property, value);
    }

    // =========================================================
    // VOLVER A LA NORMALIDAD
    // =========================================================

    private void Restore()
    {
        foreach (Renderer r in _hiddenRenderers)
            if (r != null) r.enabled = true;
        _hiddenRenderers.Clear();

        foreach (GhostedRenderer g in _ghosted)
        {
            if (g.Target != null)
            {
                g.Target.sharedMaterials   = g.OriginalMaterials;
                g.Target.shadowCastingMode = g.OriginalShadows;
            }

            // Las copias son nuestras: si no las destruimos, cada vez que alguien se
            // vuelve invisible quedan materiales huérfanos en memoria hasta cerrar.
            if (g.Copies == null) continue;
            foreach (Material m in g.Copies)
                if (m != null) Destroy(m);
        }
        _ghosted.Clear();
    }
}
