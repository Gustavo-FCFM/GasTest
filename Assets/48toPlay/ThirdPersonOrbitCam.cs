using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonOrbitCam : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform Target; 

    [Header("Configuración de Hombro")]
    public Vector3 PivotOffset = new Vector3(0, 1.5f, 0); 
    public Vector3 CamOffset = new Vector3(0.8f, 0f, -2.5f); 

    [Header("Sensibilidad")]
    public float SensitivityX = 0.5f; // Baje estos valores en el inspector si es muy rápido
    public float SensitivityY = 0.5f;
    public float MinY = -30f; 
    public float MaxY = 60f;  

    private float rotX = 0f;
    private float rotY = 0f;
    
    private PlayerInput playerInput;

    void Start()
    {
        // Al iniciar, la cámara busca automáticamente el componente de Input en su "Padre" (El jugador)
        playerInput = GetComponentInParent<PlayerInput>();

        Vector3 angles = transform.eulerAngles;
        rotX = angles.x;
        rotY = angles.y;
    }

    void LateUpdate()
    {
        if (Target == null || playerInput == null) return;

        if (Time.timeScale > 0)
        {
            // Leemos la acción "Aim" ESPECÍFICA de este jugador (ignora el mouse global)
            Vector2 aimInput = playerInput.actions["Aim"].ReadValue<Vector2>();

            rotY += aimInput.x * SensitivityX;
            rotX -= aimInput.y * SensitivityY;
            rotX = Mathf.Clamp(rotX, MinY, MaxY);
        }

        Quaternion targetRotation = Quaternion.Euler(rotX, rotY, 0);
        Vector3 focusPoint = Target.position + PivotOffset;
        Vector3 finalPosition = focusPoint + (targetRotation * CamOffset);

        transform.position = finalPosition;
        transform.rotation = targetRotation;
    }
}