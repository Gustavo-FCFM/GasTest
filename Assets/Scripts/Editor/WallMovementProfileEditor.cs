using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================
// WallMovementProfileEditor
//
// Inspector de los perfiles de movimiento en paredes (WallMove_*), sobre la base común
// por secciones (SectionedInspector). Arriba, el perfil en una línea ("pegado 1 s,
// resbala 0.5 s · salto 7 afuera y 7 arriba") y qué clases lo usan; lo del otro modo se
// esconde (lo de correr en uno de pegarse, y al revés).
// ============================================================
[CustomEditor(typeof(WallMovementProfile))]
[CanEditMultipleObjects]
public class WallMovementProfileEditor : SectionedInspector
{
    protected override string[] SectionOrder =>
        new[] { "General", "Engancharse", "Pegarse", "Correr", "Saltar de la pared", "Cómo se ve", "*" };

    private WallMovementProfile Profile => (WallMovementProfile)target;

    // Qué clases lo usan. Se busca una vez al abrir el inspector, no en cada repintado.
    private readonly List<string> _users = new List<string>();

    protected override void OnEnable()
    {
        base.OnEnable();
        _users.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterClassDefinition"))
        {
            var c = AssetDatabase.LoadAssetAtPath<CharacterClassDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && c.WallMovement == target) _users.Add(c.name);
        }
    }

    protected override void DrawHeaderCard()
    {
        WallMovementProfile p = Profile;

        string what = p.Mode == WallMovementProfile.EWallMode.Cling
            ? $"Pegado {p.ClingTime:0.##} s, resbala {p.SlideTime:0.##} s a {p.SlideSpeed:0.##} m/s y se suelta"
            : $"Corre a lo largo a ×{p.RunSpeedMultiplier:0.##} de su velocidad, bajando hasta {p.MaxSlideSpeed:0.##} m/s";

        HeaderCard(null, p.name,
                   p.Mode == WallMovementProfile.EWallMode.Cling ? "Pegarse a las paredes" : "Correr por las paredes",
                   what,
                   $"Salto: {p.JumpOffSpeed:0.##} m/s hacia afuera y {p.JumpOffUp:0.##} hacia arriba",
                   _users.Count > 0 ? "Lo usan: " + string.Join(", ", _users) : "No lo usa ninguna clase.");
    }

    protected override void CollectWarnings(List<string> into)
    {
        WallMovementProfile p = Profile;

        if (p.WallLayers.value == 0)
            into.Add("• No tiene capas de pared: nunca se va a enganchar.");
        if ((p.WallLayers.value & (1 << 7)) != 0)
            into.Add("• Marca la capa Character (7): los jugadores no cuentan como pared igual, pero sobra.");
        if (p.JumpOffSpeed <= 0f && p.JumpOffUp <= 0f)
            into.Add("• El salto de la pared no tiene impulso: Espacio solo lo suelta.");
        if (p.Mode == WallMovementProfile.EWallMode.Cling && p.ClingTime + p.SlideTime <= 0f)
            into.Add("• Pegado 0 s y resbala 0 s: se suelta en el mismo frame en que se engancha.");
    }
}
