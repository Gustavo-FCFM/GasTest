using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_CombatFeedback
//
// Las tres respuestas que el juego le debe al jugador cuando pasa algo en combate:
//
//   · PEGUÉ           → una X roja en la retícula. Cuanto MENOS vida le queda al que
//                       recibió el golpe, más opaca aparece: de un vistazo se sabe si
//                       le estás haciendo cosquillas o está por caer.
//   · MATÉ A ALGUIEN  → una calavera en el centro, a plena opacidad, que se apaga
//                       rápido. Solo por matar a un PERSONAJE (jugador o bot), no por
//                       un monstruo: una baja es un evento del partido.
//   · ME ESTÁN PEGANDO→ un arco rojo en un círculo grande alrededor de la mira, del
//                       lado por donde vino el golpe. Arriba = de frente, abajo = por
//                       la espalda, y los costados en su ángulo real.
//
// POR QUÉ IMPORTA EL DE RECIBIR: sin él, morir por la espalda se siente injusto — no
// llegaste a saber que te estaban pegando. Con el arco, te das vuelta.
//
// VIVE EN EL PREFAB DE LA CÁMARA (Player Camera ▸ UI ▸ CombatFeedback), con sus piezas
// armadas ahí y a la vista para acomodarlas: la X (cuatro puntas), la calavera y el círculo
// de pedacitos. Al empezar el juego este script las apaga, y las prende cuando toca.
//   · El TAMAÑO y la FORMA se cambian en las piezas mismas. Las cuatro puntas de la X están
//     giradas en sus soportes, así que seleccionándolas juntas se les cambia el largo, el
//     grosor y el hueco del centro (su posición X) a las cuatro a la vez.
//   · El arco usa el COLOR que tiene cada pedacito en el prefab, y el ángulo de cada uno sale
//     de dónde está: se pueden mover, sacar o agregar pedacitos.
//
// QUIÉN LO LLAMA: el servidor, que es el único que sabe cuánto daño entró y a quién. Se
// lo manda SOLO al dueño que corresponde por TargetRpc — ver
// NetworkAbilitySystemComponent.ServerReportDamage.
// ============================================================
public class UI_CombatFeedback : MonoBehaviour
{
    [Header("X de golpe")]
    [Tooltip("El CanvasGroup de la X: la prende y la apaga.")]
    [Label("X: grupo")]
    public CanvasGroup HitMarkGroup;

    [Tooltip("Las puntas de la X, para pintarlas (rojas, o amarillas en un crítico).")]
    [Label("X: puntas")]
    public Image[] HitMarkArms;

    [Tooltip("Segundos que tarda en apagarse la X.")]
    [Label("X: se apaga en (s)")]
    public float HitFadeSeconds = 0.45f;

    [Tooltip("Opacidad MÍNIMA de la X (0 a 1). Sin esto, pegarle a alguien con la vida llena " +
             "no se vería casi nada y parecería que el golpe no entró.")]
    [Range(0f, 1f)]
    [Label("X: opacidad mínima")]
    public float HitMinAlpha = 0.25f;

    [Label("X: color")]
    public Color HitColor = new Color(0.95f, 0.15f, 0.15f, 1f);

    [Tooltip("Color de la X cuando el golpe fue CRÍTICO (el mismo amarillo del número).")]
    [Label("X: color de crítico")]
    public Color HitCriticalColor = new Color(1f, 0.85f, 0.1f, 1f);

    [Tooltip("Opacidad mínima de la X de crítico (0 a 1): un crítico siempre se tiene que " +
             "notar, aunque el golpeado tenga la vida llena.")]
    [Range(0f, 1f)]
    [Label("X: opacidad mínima de crítico")]
    public float HitCriticalMinAlpha = 0.85f;

    [Header("Calavera de baja")]
    [Tooltip("El CanvasGroup de la calavera: la prende y la apaga. Lo de adentro (las piezas, " +
             "o una Image con un sprite de calavera) se arma en el prefab.")]
    [Label("Calavera: grupo")]
    public CanvasGroup KillMarkGroup;

    [Tooltip("Segundos que tarda en apagarse. Corto a propósito: es un premio, no un cartel.")]
    [Label("Calavera: se apaga en (s)")]
    public float KillFadeSeconds = 0.9f;

    [Header("Arco de daño recibido")]
    [Tooltip("El círculo de pedacitos alrededor de la mira: cada hijo con Image es un pedacito. " +
             "Su ángulo sale de dónde está (arriba = de frente, a la derecha = por la derecha) y " +
             "al encenderse usa el color que tiene en el prefab.")]
    [Label("Daño recibido: círculo")]
    public RectTransform DamageRing;

    [Tooltip("Cuántos grados a cada lado de por donde vino el golpe se encienden. Los del borde " +
             "se encienden menos.")]
    [Label("Daño recibido: grados a cada lado")]
    public float DamageArcHalfWidth = 38f;

    [Tooltip("Segundos que tarda en apagarse el arco.")]
    [Label("Daño recibido: se apaga en (s)")]
    public float DamageFadeSeconds = 1.1f;

    // =========================================================
    // ACCESO
    // =========================================================

    // El del jugador local (en su cámara). Null si todavía no hay jugador.
    private static UI_CombatFeedback _instance;

    public static UI_CombatFeedback Get() => _instance;

    // Cada pedacito del círculo, con su ángulo y su color del prefab.
    private struct RingSegment
    {
        public Image Image;
        public float Angle;
        public Color Color;
    }

    private RingSegment[] _ring = new RingSegment[0];
    private float[] _segmentAlpha = new float[0];

    private float _hitAlpha, _hitFrom;
    private float _killAlpha;

    private void Awake()
    {
        _instance = this;

        if (HitMarkGroup != null)  HitMarkGroup.alpha  = 0f;
        if (KillMarkGroup != null) KillMarkGroup.alpha = 0f;
        CollectRing();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // Los pedacitos del círculo y a qué ángulo mira cada uno, medido desde el centro del
    // círculo: 0 grados arriba y creciendo hacia la derecha, que es como se lee una brújula
    // — y como devuelve el ángulo SignedAngle de ShowDamageFrom.
    private void CollectRing()
    {
        if (DamageRing == null) return;

        var found = new List<RingSegment>();
        foreach (Image image in DamageRing.GetComponentsInChildren<Image>(true))
        {
            if (image.transform == DamageRing) continue;

            Vector3 local = DamageRing.InverseTransformPoint(image.rectTransform.position);
            found.Add(new RingSegment
            {
                Image = image,
                Angle = Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg,
                Color = image.color,
            });

            Color off = image.color; off.a = 0f;
            image.color = off;
        }

        _ring         = found.ToArray();
        _segmentAlpha = new float[_ring.Length];
    }

    // =========================================================
    // LO QUE SE LE PIDE DESDE AFUERA
    // =========================================================

    // Pegaste. 'victimHealthFraction' es la vida que le QUEDA al golpeado, de 0 a 1:
    // cuanto más baja, más opaca la X.
    // critical: el golpe fue crítico → la X sale amarilla y bien visible.
    public void ShowHit(float victimHealthFraction, bool critical = false)
    {
        float health = Mathf.Clamp01(victimHealthFraction);

        // La opacidad es lo que le falta de vida, con un piso para que un golpe a
        // alguien entero igual se note.
        _hitFrom  = Mathf.Max(critical ? HitCriticalMinAlpha : HitMinAlpha, 1f - health);
        _hitAlpha = _hitFrom;

        if (HitMarkArms == null) return;
        Color color = critical ? HitCriticalColor : HitColor;
        foreach (Image arm in HitMarkArms)
            if (arm != null) arm.color = color;
    }

    // Mataste a un personaje.
    public void ShowKill() => _killAlpha = 1f;

    // Te pegaron desde 'worldPosition'. El arco se enciende en ese ángulo, medido
    // respecto de hacia dónde estás MIRANDO (no hacia dónde apunta el cuerpo): lo que
    // el jugador tiene que corregir es la cámara.
    public void ShowDamageFrom(Vector3 worldPosition, Transform victim)
    {
        if (_ring.Length == 0 || victim == null) return;

        Vector3 toAttacker = worldPosition - victim.position;
        toAttacker.y = 0f;
        if (toAttacker.sqrMagnitude < 0.0001f)
        {
            // Sin dirección utilizable (te pegaste a vos mismo, o el atacante está justo
            // encima): se enciende el círculo entero. Algo pasó, y eso ya es información.
            for (int i = 0; i < _segmentAlpha.Length; i++) _segmentAlpha[i] = 1f;
            return;
        }

        Camera cam = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : victim.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = victim.forward;

        // Ángulo con signo entre "hacia dónde miro" y "de dónde vino": 0 = de frente,
        // 180 = por la espalda, positivo = por la derecha.
        float angle = Vector3.SignedAngle(forward.normalized, toAttacker.normalized, Vector3.up);
        float half  = Mathf.Max(1f, DamageArcHalfWidth);

        for (int i = 0; i < _ring.Length; i++)
        {
            float delta = Mathf.Abs(Mathf.DeltaAngle(_ring[i].Angle, angle));
            if (delta > half) continue;

            // Los del borde del arco encienden menos: así el arco se ve como una mancha
            // y no como un bloque recortado.
            float strength = 1f - delta / half;
            _segmentAlpha[i] = Mathf.Max(_segmentAlpha[i], strength);
        }
    }

    // =========================================================
    // APAGARSE
    // =========================================================

    private void Update()
    {
        if (HitMarkGroup != null)
        {
            if (_hitAlpha > 0f && HitFadeSeconds > 0f)
                _hitAlpha = Mathf.Max(0f, _hitAlpha - _hitFrom * Time.deltaTime / HitFadeSeconds);
            HitMarkGroup.alpha = _hitAlpha;
        }

        if (KillMarkGroup != null)
        {
            if (_killAlpha > 0f && KillFadeSeconds > 0f)
                _killAlpha = Mathf.Max(0f, _killAlpha - Time.deltaTime / KillFadeSeconds);
            KillMarkGroup.alpha = _killAlpha;
        }

        float step = DamageFadeSeconds > 0f ? Time.deltaTime / DamageFadeSeconds : 1f;
        for (int i = 0; i < _ring.Length; i++)
        {
            if (_ring[i].Image == null) continue;
            if (_segmentAlpha[i] <= 0f && _ring[i].Image.color.a <= 0f) continue;

            if (_segmentAlpha[i] > 0f) _segmentAlpha[i] = Mathf.Max(0f, _segmentAlpha[i] - step);

            Color c = _ring[i].Color;
            c.a *= _segmentAlpha[i];
            _ring[i].Image.color = c;
        }
    }
}
