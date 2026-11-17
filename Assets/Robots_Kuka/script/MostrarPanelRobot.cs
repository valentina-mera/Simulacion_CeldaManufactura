
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class MostrarPanelRobot : MonoBehaviour
{
    [Header("PANEL DE CONTROL")]
    public GameObject panelControl;

    [Header("CAMARA PRINCIPAL")]
    public Camera camaraPrincipal;

    [Header("DETECCION")]
    public float distanciaMaxima = 1000f;

    private BoxCollider colliderRobot;

    void Awake()
    {
        // Obtener o crear el collider del robot
        colliderRobot = GetComponent<BoxCollider>();

        if (colliderRobot == null)
        {
            colliderRobot = gameObject.AddComponent<BoxCollider>();
        }

        // Ajustar el collider al modelo completo
        AjustarCollider();

        // Ocultar el panel al iniciar
        if (panelControl != null)
        {
            panelControl.SetActive(false);
        }
    }

    void Start()
    {
        if (camaraPrincipal == null)
        {
            camaraPrincipal = Camera.main;
        }

        if (camaraPrincipal == null)
        {
            Debug.LogError("No se encontro Main Camera.");
        }

        if (panelControl == null)
        {
            Debug.LogError("No se asigno PanelControlKuka.");
        }
    }

    void Update()
    {
        if (Mouse.current == null ||
            camaraPrincipal == null ||
            panelControl == null)
        {
            return;
        }

        // Detectar clic izquierdo
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // No interferir con los botones y sliders
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 posicionMouse =
                Mouse.current.position.ReadValue();

            Ray rayo = camaraPrincipal.ScreenPointToRay(
                posicionMouse
            );

            RaycastHit impacto;

            if (Physics.Raycast(
                rayo,
                out impacto,
                distanciaMaxima))
            {
                // Comprobar si se selecciono el robot
                if (impacto.transform == transform ||
                    impacto.transform.IsChildOf(transform))
                {
                    panelControl.SetActive(
                        !panelControl.activeSelf
                    );
                }
            }
        }
    }

    void AjustarCollider()
    {
        Renderer[] renderizadores =
            GetComponentsInChildren<Renderer>();

        if (renderizadores.Length == 0)
        {
            Debug.LogWarning(
                "No se encontraron renderizadores en el robot."
            );
            return;
        }

        Bounds limites = renderizadores[0].bounds;

        foreach (Renderer r in renderizadores)
        {
            limites.Encapsulate(r.bounds);
        }

        Vector3 min = limites.min;
        Vector3 max = limites.max;

        Vector3[] esquinas = new Vector3[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        Vector3 minimoLocal = Vector3.one * float.MaxValue;
        Vector3 maximoLocal = Vector3.one * float.MinValue;

        foreach (Vector3 esquina in esquinas)
        {
            Vector3 local =
                transform.InverseTransformPoint(esquina);

            minimoLocal = Vector3.Min(minimoLocal, local);
            maximoLocal = Vector3.Max(maximoLocal, local);
        }

        colliderRobot.center =
            (minimoLocal + maximoLocal) * 0.5f;

        colliderRobot.size =
            maximoLocal - minimoLocal + Vector3.one * 0.05f;
    }
}
