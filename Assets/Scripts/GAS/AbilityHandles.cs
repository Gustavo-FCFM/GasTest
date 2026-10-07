#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

// ============================================================
// AbilityHandles  (SOLO EDITOR)
//
// Las manijas que usan las habilidades en GameplayAbility.DrawSceneHandles para que su
// forma se ajuste ARRASTRANDO en la Scene view, sobre el muñeco de prueba (AbilityPreview):
// el alcance de un cono, el radio de un área, el ángulo, desde dónde sale el golpe...
//
// Cada una dibuja la manija con su etiqueta ("Range 2.5 m") y, si se movió, escribe el
// valor nuevo en el asset con Deshacer (Ctrl+Z) y lo marca para guardar. Devuelven true
// si cambió algo.
//
// Vive en la carpeta del GAS (y no en Editor/) porque la llaman las habilidades; todo el
// archivo está dentro de #if UNITY_EDITOR, así que no existe en la build.
// ============================================================
public static class AbilityHandles
{
    // Colores de las manijas por tipo de dato, iguales en todas las habilidades.
    public static readonly Color DistanceColor = new Color(1f, 0.6f, 0.15f);
    public static readonly Color RadiusColor   = new Color(0.25f, 0.9f, 0.5f);
    public static readonly Color AngleColor    = new Color(1f, 0.85f, 0.2f);
    public static readonly Color OffsetColor   = new Color(0.35f, 0.75f, 1f);
    public static readonly Color BoxColor      = new Color(0.4f, 0.65f, 1f);

    // Los números se redondean a 5 cm (y los ángulos a medio grado): arrastrando, un
    // 2.4973 no le sirve a nadie.
    private const float DistanceStep = 0.05f;
    private const float AngleStep    = 0.5f;

    // Una DISTANCIA medida desde 'from' hacia 'dir' (el alcance de un cono, el largo de una
    // estocada, a dónde llega un salto).
    public static bool Distance(Object owner, string label, Vector3 from, Vector3 dir, ref float value,
                                Color color, float min = 0f)
    {
        if (dir.sqrMagnitude < 0.0001f) return false;
        dir.Normalize();

        Vector3 tip = from + dir * value;

        using (new Handles.DrawingScope(color))
        {
            Handles.DrawDottedLine(from, tip, 3f);

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(tip, dir, HandleUtility.GetHandleSize(tip) * 0.09f,
                                           Handles.DotHandleCap, 0f);
            Label(tip, $"{label} {value:0.##} m", color);

            if (!EditorGUI.EndChangeCheck()) return false;

            float newValue = Mathf.Max(min, Snap(Vector3.Dot(moved - from, dir), DistanceStep));
            return Write(owner, label, ref value, newValue);
        }
    }

    // Un RADIO alrededor de 'center' (un área, el golpe de un salto). La manija va en el
    // borde, del lado de 'handleDir' (normalmente a la derecha, para no tapar a las demás).
    public static bool Radius(Object owner, string label, Vector3 center, ref float value, Color color,
                              Vector3 handleDir = default)
    {
        Vector3 dir = handleDir.sqrMagnitude > 0.0001f ? handleDir.normalized : Vector3.right;
        Vector3 edge = center + dir * value;

        using (new Handles.DrawingScope(color))
        {
            Handles.DrawWireDisc(center, Vector3.up, value);

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(edge, dir, HandleUtility.GetHandleSize(edge) * 0.09f,
                                           Handles.DotHandleCap, 0f);
            Label(edge, $"{label} {value:0.##} m", color);

            if (!EditorGUI.EndChangeCheck()) return false;

            float newValue = Mathf.Max(0f, Snap(Vector3.Dot(moved - center, dir), DistanceStep));
            return Write(owner, label, ref value, newValue);
        }
    }

    // La APERTURA de un cono, en grados. Por defecto el ángulo COMPLETO (el de un cono de
    // golpe); con halfAngle, la MITAD (el de selección de objetivo: "hasta X° de la mira").
    // La manija va en el borde derecho del arco, a 'radius' del centro.
    public static bool Angle(Object owner, string label, Vector3 center, Vector3 forward, float radius,
                             ref float angle, Color color, float max = 360f, bool halfAngle = false)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        radius = Mathf.Max(0.5f, radius);

        float full = halfAngle ? angle * 2f : angle;
        Vector3 from = Quaternion.AngleAxis(-full * 0.5f, Vector3.up) * forward;
        Vector3 edge = center + Quaternion.AngleAxis(full * 0.5f, Vector3.up) * forward * radius;

        using (new Handles.DrawingScope(color))
        {
            Handles.DrawWireArc(center, Vector3.up, from, full, radius);
            Handles.DrawLine(center, center + from * radius);
            Handles.DrawLine(center, edge);

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(edge, HandleUtility.GetHandleSize(edge) * 0.09f,
                                                   Vector3.zero, Handles.DotHandleCap);
            Label(edge + Vector3.up * 0.3f, halfAngle ? $"{label} ±{angle:0.#}°" : $"{label} {angle:0.#}°", color);

            if (!EditorGUI.EndChangeCheck()) return false;

            Vector3 toHandle = moved - center;
            toHandle.y = 0f;
            if (toHandle.sqrMagnitude < 0.0001f) return false;

            float half = Mathf.Abs(Vector3.SignedAngle(forward, toHandle, Vector3.up));
            float newValue = Mathf.Clamp(Snap(halfAngle ? half : half * 2f, AngleStep), 0f, max);
            return Write(owner, label, ref angle, newValue);
        }
    }

    // Las dos de una habilidad de OBJETIVO ÚNICO: hasta dónde busca (alcance) y qué tan
    // centrado en la mira tiene que estar (ángulo de selección, la mitad del cono).
    public static void Selection(Object owner, Transform origin, ref float maxRange, ref float selectionAngle)
    {
        if (origin == null) return;
        Vector3 chest = origin.position + Vector3.up;
        Distance(owner, "Max Range", chest, origin.forward, ref maxRange, DistanceColor);
        Angle(owner, "Selection Angle", chest, origin.forward, maxRange * 0.6f, ref selectionAngle,
              AngleColor, 90f, halfAngle: true);
    }

    // Un PUNTO en el espacio LOCAL de 'origin' (desde dónde sale un golpe, de qué mano sale
    // un proyectil, dónde aparece un VFX). Se arrastra libre en el plano de la cámara.
    public static bool Offset(Object owner, string label, Transform origin, ref Vector3 local, Color color)
    {
        if (origin == null) return false;

        Vector3 world = origin.TransformPoint(local);

        using (new Handles.DrawingScope(color))
        {
            Handles.DrawDottedLine(origin.position, world, 2f);

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(world, HandleUtility.GetHandleSize(world) * 0.07f,
                                                   Vector3.zero, Handles.SphereHandleCap);
            Label(world + Vector3.up * 0.15f, label, color);

            if (!EditorGUI.EndChangeCheck()) return false;

            Vector3 newLocal = origin.InverseTransformPoint(moved);
            newLocal = new Vector3(Snap(newLocal.x, DistanceStep), Snap(newLocal.y, DistanceStep),
                                   Snap(newLocal.z, DistanceStep));
            if (newLocal == local) return false;

            Undo.RecordObject(owner, "Ajustar " + label);
            local = newLocal;
            EditorUtility.SetDirty(owner);
            return true;
        }
    }

    // Una ALTURA sobre 'basePoint' (a qué altura sale un disparo).
    public static bool Height(Object owner, string label, Vector3 basePoint, ref float value, Color color)
        => Distance(owner, label, basePoint, Vector3.up, ref value, color, 0f);

    // Una CAJA orientada (la hitbox de un golpe). center y size en el espacio de 'rotation';
    // se arrastra cada cara por separado, como el BoxCollider de Unity.
    private static readonly BoxBoundsHandle _box = new BoxBoundsHandle();

    public static bool Box(Object owner, string label, Vector3 center, Quaternion rotation,
                           ref Vector3 size, out Vector3 newCenter, Color color)
    {
        newCenter = center;

        using (new Handles.DrawingScope(color, Matrix4x4.TRS(center, rotation, Vector3.one)))
        {
            _box.center = Vector3.zero;
            _box.size   = size;
            _box.SetColor(color);

            EditorGUI.BeginChangeCheck();
            _box.DrawHandle();
            if (!EditorGUI.EndChangeCheck()) return false;

            Vector3 newSize = new Vector3(Snap(_box.size.x, DistanceStep), Snap(_box.size.y, DistanceStep),
                                          Snap(_box.size.z, DistanceStep));
            newCenter = center + rotation * _box.center;

            Undo.RecordObject(owner, "Ajustar " + label);
            size = newSize;
            EditorUtility.SetDirty(owner);
            return true;
        }
    }

    // Texto con fondo, legible sobre cualquier escena.
    private static GUIStyle _labelStyle;

    public static void Label(Vector3 position, string text, Color color)
    {
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                padding   = new RectOffset(4, 4, 1, 1),
            };
            Texture2D bg = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
            bg.Apply();
            _labelStyle.normal.background = bg;
        }

        _labelStyle.normal.textColor = Color.Lerp(color, Color.white, 0.35f);
        Handles.Label(position, text, _labelStyle);
    }

    private static float Snap(float value, float step) => Mathf.Round(value / step) * step;

    private static bool Write(Object owner, string label, ref float value, float newValue)
    {
        if (Mathf.Approximately(value, newValue)) return false;

        Undo.RecordObject(owner, "Ajustar " + label);
        value = newValue;
        EditorUtility.SetDirty(owner);
        return true;
    }
}
#endif
