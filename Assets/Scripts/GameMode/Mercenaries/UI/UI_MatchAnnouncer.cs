using System.Collections.Generic;
using TMPro;
using UnityEngine;

// ============================================================
// UI_MatchAnnouncer
//
// Los avisos grandes del centro de la pantalla: "¡Apareció el Objetivo!", "EQUIPO 2
// entregó el Objetivo", "Aniquilaron al EQUIPO 3". Son lo que hace que se ENTIENDA
// qué está pasando en la partida sin tener que mirar el marcador.
//
// El servidor manda el aviso como enum + números (ver MercenariesGameMode.Announce) y
// acá se arma el texto. Eso quiere decir que cambiar la redacción o traducir el juego
// no toca la red para nada.
//
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ Announcer), desde el 14 de octubre de 2026:
// "Messages" es dónde salen (su posición es la altura del aviso) y "MessageTemplate" el MOLDE
// apagado de un aviso (su letra, sombra y ancho se cambian ahí). El tamaño de letra según el
// tipo de aviso está en este componente.
// ============================================================
public class UI_MatchAnnouncer : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("Donde salen los avisos (el más nuevo en su centro, los anteriores suben).")]
    [Label("Contenedor de los avisos")]
    public RectTransform Messages;

    [Tooltip("El MOLDE de un aviso (apagado): un texto centrado.")]
    [Label("Molde de un aviso")]
    public TMP_Text MessageTemplate;

    [Header("Disposición")]
    [Tooltip("Cuánto sube cada aviso anterior cuando llega uno nuevo, en px del canvas.")]
    [Label("Separación entre avisos (px)")]
    public float LineSpacing = 52f;

    [Header("Tamaños de letra")]
    [Tooltip("Fin de partida, entrega y aparición del Objetivo.")]
    [Label("Tamaño de letra: lo más importante")]
    public float BigFontSize = 44f;

    [Tooltip("Equipo aniquilado y comienzo de partida.")]
    [Label("Tamaño de letra: importante")]
    public float MediumFontSize = 38f;

    [Label("Tamaño de letra: el resto")]
    public float SmallFontSize = 30f;

    [Header("Tiempos")]
    [Label("Duración del aviso (s)")]
    public float MessageDuration = 3.5f;
    [Label("Desvanecerse (s)")]
    public float FadeDuration    = 0.45f;

    [Tooltip("Cuántos avisos se apilan a la vez. Los más viejos suben y se desvanecen.")]
    [Label("Máximo a la vez")]
    public int MaxVisible = 3;

    private class Message
    {
        public TMP_Text Text;
        public float Born;
    }

    private readonly List<Message> _messages = new List<Message>();

    private void Start()
    {
        if (MessageTemplate != null) MessageTemplate.gameObject.SetActive(false);
        MercenariesGameMode.OnAnnouncement += HandleAnnouncement;
    }

    private void OnDestroy()
    {
        MercenariesGameMode.OnAnnouncement -= HandleAnnouncement;
    }

    // =========================================================
    // TEXTOS
    // =========================================================

    private void HandleAnnouncement(EMatchAnnouncement type, int team, int extra)
    {
        string text  = BuildText(type, team, extra);
        if (string.IsNullOrEmpty(text)) return;

        Color color = team > 0 ? MercUIFactory.TeamColor(team) : ResolveNeutralColor(type);
        Push(text, color, ResolveSize(type));
    }

    // El aviso llega como QUÉ pasó (el enum, el equipo y un número), nunca como texto: así
    // cada pantalla lo escribe en su idioma (ver Loc, claves announce.*).
    private string BuildText(EMatchAnnouncement type, int team, int extra)
    {
        string teamName = MercenariesGameMode.TeamName(team);

        switch (type)
        {
            case EMatchAnnouncement.MatchStarted:
                return Loc.T("announce.match_started");

            case EMatchAnnouncement.ObjectiveSpawned:
                return Loc.T("announce.objective_spawned");

            case EMatchAnnouncement.ObjectiveTaken:
                return team == MercUIFactory.LocalTeam()
                    ? Loc.T("announce.objective_taken_yours")
                    : Loc.T("announce.objective_taken", ("team", teamName));

            case EMatchAnnouncement.ObjectiveDropped:
                return Loc.T("announce.objective_dropped", ("team", teamName));

            case EMatchAnnouncement.ObjectiveDelivered:
                return Loc.T("announce.objective_delivered", ("team", teamName), ("points", extra),
                             ("goal", ResolvePointsToWin()));

            case EMatchAnnouncement.ObjectiveReturning:
                return extra > 0 ? Loc.T("announce.next_objective", ("seconds", extra)) : "";

            case EMatchAnnouncement.TeamWiped:
                return team == MercUIFactory.LocalTeam()
                    ? Loc.T("announce.wiped_yours")
                    : Loc.T("announce.wiped", ("team", teamName));

            case EMatchAnnouncement.TeamLevelUp:
                // El nivel de los OTROS equipos ya se ve en el marcador; no lo gritamos.
                if (team != MercUIFactory.LocalTeam()) return "";

                // Al llegar al tope se agrega el boton: es el unico momento en que el
                // jugador PUEDE hacer algo con el nivel, y si no se lo decimos acá no se
                // entera — el menu ya no se abre solo, justamente para no plantarlo en
                // medio de una pelea. Cuál botón nombrar lo decide InputGlyphs, según lo
                // que el jugador haya elegido en Ajustes.
                int maxLevel = MercenariesGameMode.Instance != null
                    ? MercenariesGameMode.Instance.MaxTeamLevel : 0;

                return (maxLevel > 0 && extra >= maxLevel)
                    ? Loc.T("announce.level_up_max", ("level", extra), ("key", InputGlyphs.Subclass))
                    : Loc.T("announce.level_up", ("level", extra));

            case EMatchAnnouncement.MatchEnded:
                return team > 0 ? Loc.T("match.wins", ("team", teamName)) : Loc.T("match.draw");
        }
        return "";
    }

    private static int ResolvePointsToWin()
    {
        MercenariesGameMode gm = MercenariesGameMode.Instance;
        return gm != null ? gm.PointsToWin : 2;
    }

    private static Color ResolveNeutralColor(EMatchAnnouncement type)
    {
        switch (type)
        {
            case EMatchAnnouncement.ObjectiveSpawned: return new Color(1f, 0.85f, 0.35f);
            case EMatchAnnouncement.MatchStarted:     return Color.white;
            default:                                  return new Color(1f, 1f, 1f, 0.9f);
        }
    }

    private float ResolveSize(EMatchAnnouncement type)
    {
        switch (type)
        {
            case EMatchAnnouncement.MatchEnded:
            case EMatchAnnouncement.ObjectiveDelivered:
            case EMatchAnnouncement.ObjectiveSpawned:
                return BigFontSize;
            case EMatchAnnouncement.TeamWiped:
            case EMatchAnnouncement.MatchStarted:
                return MediumFontSize;
            default:
                return SmallFontSize;
        }
    }

    // =========================================================
    // PILA DE AVISOS
    // =========================================================

    // Empuja un aviso nuevo abajo del todo; los anteriores suben. Es la disposición
    // clásica de los kill-feed/avisos de shooter: lo último que pasó siempre está en
    // el mismo lugar, así el ojo no lo tiene que buscar.
    public void Push(string text, Color color, float fontSize = 32f)
    {
        if (Messages == null || MessageTemplate == null) return;

        TMP_Text tmp = Instantiate(MessageTemplate, Messages);
        tmp.name = "Aviso";
        tmp.gameObject.SetActive(true);
        tmp.text     = text;
        tmp.color    = color;
        tmp.fontSize = fontSize;
        tmp.rectTransform.anchoredPosition = Vector2.zero;

        _messages.Add(new Message { Text = tmp, Born = Time.time });

        while (_messages.Count > MaxVisible) Retire(_messages[0]);
    }

    private void Retire(Message msg)
    {
        _messages.Remove(msg);
        if (msg.Text != null) Destroy(msg.Text.gameObject);
    }

    private void Update()
    {
        for (int i = _messages.Count - 1; i >= 0; i--)
        {
            Message msg = _messages[i];
            if (msg.Text == null) { _messages.RemoveAt(i); continue; }

            float age = Time.time - msg.Born;
            if (age > MessageDuration) { Retire(msg); continue; }

            // Los más viejos quedan arriba (el índice 0 es el más viejo).
            int fromBottom = _messages.Count - 1 - i;
            Vector2 target = new Vector2(0f, fromBottom * LineSpacing);
            msg.Text.rectTransform.anchoredPosition = Vector2.Lerp(
                msg.Text.rectTransform.anchoredPosition, target, Time.deltaTime * 12f);

            // Entra rápido y se va desvaneciendo.
            float alpha = 1f;
            if (age < FadeDuration)                          alpha = age / FadeDuration;
            else if (age > MessageDuration - FadeDuration)   alpha = (MessageDuration - age) / FadeDuration;

            Color c = msg.Text.color;
            c.a = Mathf.Clamp01(alpha);
            msg.Text.color = c;
        }
    }
}
