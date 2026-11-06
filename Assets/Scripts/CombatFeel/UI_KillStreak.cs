using UnityEngine;

// ============================================================
// UI_KillStreak
//
// Las rachas de bajas: si haces varias bajas seguidas, sale "¡DOBLE BAJA!", "¡TRIPLE BAJA!"… en
// el centro de TU pantalla. Cuentan los personajes (jugadores y bots), no los NPCs.
//
// No viaja nada nuevo por la red: la baja ya les llega a todos por el registro de bajas
// (PlayerController.ObserversKillFeed, que avisa por CombatFeel.CharacterKilled), y cada pantalla
// cuenta las de su jugador. El aviso sale por UI_MatchAnnouncer, el de los avisos de partida.
//
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ CombatFeel). Sus números, en el asset CombatFeel. Los
// textos, en Localization.csv (announce.streak2 a announce.streak5).
// ============================================================
public class UI_KillStreak : MonoBehaviour
{
    private const int LongestStreakText = 5;   // de acá en adelante repite el último texto

    private int   _streak;
    private float _lastKillAt;

    private void OnEnable()  => CombatFeel.CharacterKilled += OnCharacterKilled;
    private void OnDisable() => CombatFeel.CharacterKilled -= OnCharacterKilled;

    private void OnCharacterKilled(int killerObjectId)
    {
        PlayerController local = PlayerController.LocalPlayer;
        if (local == null || local.ObjectId != killerObjectId) return;

        CombatFeel feel = CombatFeel.Config;

        if (_streak > 0 && Time.time - _lastKillAt > feel.KillStreakSeconds) _streak = 0;
        _streak++;
        _lastKillAt = Time.time;

        if (_streak < 2 || !CombatFeel.KillStreakOn) return;

        UI_MatchAnnouncer announcer = FindFirstObjectByType<UI_MatchAnnouncer>(FindObjectsInactive.Include);
        if (announcer == null) return;

        string key = "announce.streak" + Mathf.Min(_streak, LongestStreakText);
        announcer.Push(Loc.T(key), feel.KillStreakColor, feel.KillStreakFontSize);
    }
}
