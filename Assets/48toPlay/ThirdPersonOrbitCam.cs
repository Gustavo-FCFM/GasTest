using UnityEngine;

public class ThirdPersonOrbitCam : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform Target; // Tu Player

    [Header("Configuración de Hombro")]
    // (0, 1.5, 0) es la altura de la cabeza/pivote
    public Vector3 PivotOffset = new Vector3(0, 1.5f, 0); 
    // (0.8, 0, -2.5) lo mueve a la derecha y atrás (Shoulder View)
    public Vector3 CamOffset = new Vector3(0.8f, 0f, -2.5f); 

    [Header("Input")]
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

        // 1. Leer Mouse
        // (Si estás en el menú de pausa, deberías bloquear esto)
        if (Time.timeScale > 0)
        {
            rotY += Input.GetAxis("Mouse X") * SensitivityX;
            rotX -= Input.GetAxis("Mouse Y") * SensitivityY;

            // Stick derecho del control (LookX/LookY, ver ProjectSettings/Input
            // Manager). Es un valor sostenido, no un delta, así que se escala
            // por Time.deltaTime para que gire a una velocidad constante en
            // grados/segundo sin importar el framerate.
            rotY += Input.GetAxis("LookX") * GamepadSensitivity * Time.deltaTime;
            rotX -= Input.GetAxis("LookY") * GamepadSensitivity * Time.deltaTime;

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