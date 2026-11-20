using UnityEngine;
using UnityEngine.EventSystems;

public class CamaraCeldaController : MonoBehaviour
{
    [Header("Velocidades")]
    public float velocidadMovimiento = 6f;
    public float velocidadRapida = 12f;
    public float sensibilidadRotacion = 2.5f;
    public float velocidadZoom = 8f;

    [Header("Altura Fija (Evita volar)")]
    public bool mantenerAlturaFija = true;
    public float alturaOjos = 1.7f;

    [Header("Límites Amplios de la Celda (Opcional)")]
    public bool usarLimites = false;
    public Vector2 limiteX = new Vector2(-20f, 20f);
    public Vector2 limiteZ = new Vector2(-20f, 20f);

    private float rotacionX;
    private float rotacionY;

    void Start()
    {
        Vector3 rot = transform.eulerAngles;
        rotacionY = rot.y;
        rotacionX = rot.x;
        if (rotacionX > 180f) rotacionX -= 360f;

        if (mantenerAlturaFija)
            alturaOjos = transform.position.y;
    }

    void Update()
    {
        // 1. Rotar la vista solo mientras mantienes presionado el clic derecho
        if (Input.GetMouseButton(1))
        {
            rotacionY += Input.GetAxis("Mouse X") * sensibilidadRotacion;
            rotacionX -= Input.GetAxis("Mouse Y") * sensibilidadRotacion;
            rotacionX = Mathf.Clamp(rotacionX, -80f, 80f);
            transform.rotation = Quaternion.Euler(rotacionX, rotacionY, 0f);
        }

        // 2. Movimiento horizontal con WASD o Flechas (proyectado al plano del suelo, sin volar)
        float h = Input.GetAxisRaw("Horizontal"); // A / D
        float v = Input.GetAxisRaw("Vertical");   // W / S

        Vector3 adelantePlano = transform.forward;
        adelantePlano.y = 0f;
        adelantePlano.Normalize();

        Vector3 derechaPlano = transform.right;
        derechaPlano.y = 0f;
        derechaPlano.Normalize();

        float vel = Input.GetKey(KeyCode.LeftShift) ? velocidadRapida : velocidadMovimiento;
        Vector3 desplazamiento = (adelantePlano * v + derechaPlano * h).normalized * vel * Time.deltaTime;

        // Zoom hacia adelante/atrás con la rueda del mouse (sin bloquear el cursor)
        if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                desplazamiento += adelantePlano * (scroll * velocidadZoom);
            }
        }

        Vector3 nuevaPos = transform.position + desplazamiento;

        if (mantenerAlturaFija)
            nuevaPos.y = alturaOjos;

        if (usarLimites)
        {
            nuevaPos.x = Mathf.Clamp(nuevaPos.x, limiteX.x, limiteX.y);
            nuevaPos.z = Mathf.Clamp(nuevaPos.z, limiteZ.x, limiteZ.y);
        }

        transform.position = nuevaPos;
    }
}