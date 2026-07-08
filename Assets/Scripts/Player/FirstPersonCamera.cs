using UnityEngine;

// ============================================================
// FirstPersonCamera
//
// Control de cámara en primera persona clásico: el mouse horizontal
// rota el cuerpo del personaje (playerBody) y el vertical rota solo
// esta cámara, con límites de inclinación. No usado por el
// PlayerController actual (que usa ThirdPersonOrbitCam) — queda
// disponible como alternativa en primera persona.
// ============================================================
public class FirstPersonCamera : MonoBehaviour
{
    [Header("Sensibilidad")]
    public float sensitivityX = 2f; // Sensibilidad horizontal del mouse
    public float sensitivityY = 2f; // Sensibilidad vertical del mouse

    [Header("Límites de Mirada")]
    public float minimumY = -60f; // Cuánto se puede inclinar hacia abajo
    public float maximumY = 60f;  // Cuánto se puede inclinar hacia arriba

    // Transform del cuerpo del personaje, que rota en el eje horizontal.
    public Transform playerBody;

    private float rotationX = 0f; // Rotación vertical acumulada (cámara)
    private float rotationY = 0f; // Rotación horizontal acumulada (cuerpo)

    // Bloquea y oculta el cursor al arrancar.
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Cada frame, traduce el movimiento del mouse en rotación: horizontal
    // al cuerpo del personaje, vertical (con límites) a esta cámara.
    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivityX;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivityY;

        rotationY += mouseX;
        playerBody.Rotate(Vector3.up * mouseX);

        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, minimumY, maximumY);

        transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
    }
}
