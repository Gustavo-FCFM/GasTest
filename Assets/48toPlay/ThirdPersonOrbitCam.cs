using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonOrbitCam : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform Target; // Tu Player

    [Header("Configuración de Hombro")]
    // (0, 1.5, 0) es la altura de la cabeza/pivote
    public Vector3 PivotOffset = new Vector3(0, 1.5f, 0); 
    // (0.8, 0, -2.5) lo mueve a la derecha y atrás (Shoulder View)
    public Vector3 CamOffset = new Vector3(0.8f, 0f, -2.5f); 

    [Header("Input - Mouse")]
    // Sensibilidad del mouse. Se aplica al delta puntual del mouse (acción Look,
    // Input System). El *0.1 interno compensa la escala del viejo eje "Mouse X"
    // (que tenía sensibilidad 0.1) para que estos valores se sientan igual que antes.
    public float SensitivityX = 2.0f;
    public float SensitivityY = 2.0f;
    public float MinY = -30f; // Límite mirar abajo
    public float MaxY = 60f;  // Límite mirar arriba

    [Header("Input - Control (stick derecho)")]
    // El stick manda un valor constante (-1 a 1) mientras se mantiene
    // inclinado, a diferencia del mouse que manda un delta puntual — por
    // eso necesita su propia sensibilidad y se multiplica por
    // Time.deltaTime (grados por segundo), no se suma directo como el mouse.
    public float GamepadSensitivity = 120f;

    private float rotX = 0f;
    private float rotY = 0f;

    void Start()
    {
        /*
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;*/

        // Inicializar rotación con la actual
        Vector3 angles = transform.eulerAngles;
        rotX = angles.x;
        rotY = angles.y;
    }

    void LateUpdate()
    {
        if (Target == null) return;

        // 1. Leer la mira (acción "Look" del Input System: mouse o stick derecho).
        // (Si estás en el menú de pausa, deberías bloquear esto)
        PlayerInputProvider input = PlayerInputProvider.Local;
        if (Time.timeScale > 0 && input != null && input.IsReady)
        {
            Vector2 look = input.LookValue;

            // El mouse y el stick tienen semánticas distintas: el mouse manda un
            // DELTA por frame (se suma directo), el stick manda una inclinación
            // SOSTENIDA (-1 a 1) que se escala por Time.deltaTime para girar a
            // grados/segundo constantes. Detectamos cuál está moviendo la acción
            // por el dispositivo de su control activo.
            bool fromGamepad = input.Look.activeControl != null &&
                               input.Look.activeControl.device is Gamepad;

            if (fromGamepad)
            {
                rotY += look.x * GamepadSensitivity * Time.deltaTime;
                rotX -= look.y * GamepadSensitivity * Time.deltaTime;
            }
            else
            {
                // *0.1 para replicar la escala del viejo eje "Mouse X" (sensibilidad 0.1).
                rotY += look.x * SensitivityX * 0.1f;
                rotX -= look.y * SensitivityY * 0.1f;
            }

            rotX = Mathf.Clamp(rotX, MinY, MaxY);
        }

        // 2. Calcular Rotación (Orbita)
        Quaternion targetRotation = Quaternion.Euler(rotX, rotY, 0);

        // 3. Calcular Posición
        // La posición es: PosiciónJugador + AlturaPivote + (Rotación * DistanciaHombro)
        Vector3 focusPoint = Target.position + PivotOffset;
        Vector3 finalPosition = focusPoint + (targetRotation * CamOffset);

        // 4. Aplicar
        transform.position = finalPosition;
        transform.rotation = targetRotation;
    }
}