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

    private float rotX = 0f;
    private float rotY = 0f;

    void Start()
    {
        // Bloquear cursor para que girar sea cómodo
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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