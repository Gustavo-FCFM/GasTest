using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    [Header("Sensibilidad")]
    public float sensitivityX = 2f; // Sensibilidad horizontal
    public float sensitivityY = 2f; // Sensibilidad vertical

    [Header("Límites de Mirada")]
    public float minimumY = -60f; // Límite inferior (mirar hacia abajo)
    public float maximumY = 60f;  // Límite superior (mirar hacia arriba)

    // El transform del personaje (PlayerController) que rota horizontalmente
    public Transform playerBody; 

    private float rotationX = 0f; // Almacena la rotación vertical actual
    private float rotationY = 0f; // Almacena la rotación horizontal actual (del cuerpo)


    void Start()
    {
        // Oculta el cursor y lo bloquea en el centro de la pantalla
        Cursor.lockState = CursorLockMode.Locked; 
    }

    void Update()
    {
        // 1. Obtener la entrada del mouse
        float mouseX = Input.GetAxis("Mouse X") * sensitivityX;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivityY;

        // 2. Rotación Horizontal (Controla el cuerpo del personaje)
        // Se aplica al cuerpo del personaje (playerBody)
        rotationY += mouseX;
        playerBody.Rotate(Vector3.up * mouseX); // Rota el cuerpo del personaje en el eje Y global

        // 3. Rotación Vertical (Controla la cámara)
        // La rotación vertical se acumula y se sujeta a los límites (clamp)
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, minimumY, maximumY);

        // Aplicar rotación vertical a la cámara (transform.localRotation)
        // Usamos Quaternion.Euler para convertir el float de ángulo a una rotación de Unity
        transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
    }
}