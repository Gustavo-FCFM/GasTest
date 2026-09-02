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
// Se arma solo, igual que el marcador: componente en la escena y ya.
// ============================================================
public class UI_MatchAnnouncer : MonoBehaviour
{
    [Header("Colocación")]
    [Tooltip("Altura del aviso, medida desde el centro de la pantalla hacia arriba (1080p).")]
    public float VerticalOffset = 180f;

    [Header("Tiempos")]
    public float MessageDuration = 3.5f;
    public float FadeDuration    = 0.45f;

    [Tooltip("Cuántos avisos se apilan a la vez. Los más viejos suben y se desvanecen.")]
    public int MaxVisible = 3;

    private class Message
    {
        public TextMeshProUGUI Text;
        public float Born;
    }

    private readonly List<Message> _messages = new List<Message>();
    private RectTransform _root;

    private void Start()
    {
        Transform parent = ResolveCanvas();

        _root = MercUIFactory.CreateRect(parent, "AvisosDePartida",
            anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
            pivot:     new Vector2(0.5f, 0.5f),
            anchoredPos: new Vector2(0f, VerticalOffset),
            size:        new Vector2(1200f, 200f));

        MercenariesGameMode.OnAnnouncement += HandleAnnouncement;
    }

    private void OnDestroy()
    {
        MercenariesGameMode.OnAnnouncement -= HandleAnnouncement;
    }

    private Transform ResolveCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) return canvas.transform;

        canvas = MercUIFactory.CreateCanvas("Canvas_Avisos", 51);
        canvas.transform.SetParent(transform, false);
        return canvas.transform;
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

    private string BuildText(EMatchAnnouncement type, int team, int extra)
    {
        string teamName = MercenariesGameMode.TeamName(team);

        switch (type)
        {
            case EMatchAnnouncement.MatchStarted:
                return "¡QUE EMPIECE LA CACERÍA!";

            case EMatchAnnouncement.ObjectiveSpawned:
                return "¡EL OBJETIVO APARECIÓ EN EL CENTRO!";

            case EMatchAnnouncement.ObjectiveTaken:
                return team == MercUIFactory.LocalTeam()
                    ? "¡TU EQUIPO LEVANTÓ EL OBJETIVO!"
                    : $"{teamName} levantó el Objetivo";

            case EMatchAnnouncement.ObjectiveDropped:
                return $"{teamName} soltó el Objetivo";

            case EMatchAnnouncement.ObjectiveDelivered:
                return $"¡{teamName} ENTREGÓ EL OBJETIVO!  ({extra}/{ResolvePointsToWin()})";

            case EMatchAnnouncement.ObjectiveReturning:
                return extra > 0 ? $"Próximo Objetivo en {extra}s" : "";

            case EMatchAnnouncement.TeamWiped:
                return team == MercUIFactory.LocalTeam()
                    ? "¡TU EQUIPO FUE ANIQUILADO!"
                    : $"¡{teamName} FUE ANIQUILADO!";

            case EMatchAnnouncement.TeamLevelUp:
                // El nivel de los OTROS equipos ya se ve en el marcador; no lo gritamos.
                if (team != MercUIFactory.LocalTeam()) return "";

                // Al llegar al tope se agrega la tecla: es el unico momento en que el
                // jugador PUEDE hacer algo con el nivel, y si no se lo decimos acá no se
                // entera — el menu ya no se abre solo, justamente para no plantarlo en
                // medio de una pelea.
                int maxLevel = MercenariesGameMode.Instance != null
                    ? MercenariesGameMode.Instance.MaxTeamLevel : 0;

                return (maxLevel > 0 && extra >= maxLevel)
                    ? $"¡TU EQUIPO SUBIÓ A NIVEL {extra}! — Presiona V para elegir una Subclase"
                    : $"¡TU EQUIPO SUBIÓ A NIVEL {extra}!";

            case EMatchAnnouncement.MatchEnded:
                return team > 0 ? $"GANA {teamName}" : "EMPATE";
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

    private static float ResolveSize(EMatchAnnouncement type)
    {
        switch (type)
        {
            case EMatchAnnouncement.MatchEnded:
            case EMatchAnnouncement.ObjectiveDelivered:
            case EMatchAnnouncement.ObjectiveSpawned:
                return 44f;
            case EMatchAnnouncement.TeamWiped:
            case EMatchAnnouncement.MatchStarted:
                return 38f;
            default:
                return 30f;
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
        if (_root == null) return;

        TextMeshProUGUI tmp = MercUIFactory.CreateText(_root, "Aviso", text, fontSize, color,
            TextAlignmentOptions.Center,
            anchoredPos: Vector2.zero, size: new Vector2(1200f, 50f),
            anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
            pivot: new Vector2(0.5f, 0.5f));
        tmp.fontStyle = FontStyles.Bold;
        MercUIFactory.AddShadow(tmp, 3f);

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
            Vector2 target = new Vector2(0f, fromBottom * 52f);
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
