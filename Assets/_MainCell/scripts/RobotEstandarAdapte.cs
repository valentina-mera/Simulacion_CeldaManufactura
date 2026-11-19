using UnityEngine;
using UnityEngine.EventSystems;

public class RobotEstandarAdapter : MonoBehaviour
{
    public enum TipoRobot
    {
        Kuka_6GDL,              // Usa KukaRobotController
        Scorbot_5GDL_Directo,   // Usa SCORBOTController + Gripper_Controller
        Scorbot_ERIX_PorEjes    // Usa 5 RobotJointController + GripperController
    }

    [Header("1. Identidad del Robot en el Panel")]
    public string nombreMostrarEnPanel = "KUKA KR - Robot 1";
    public TipoRobot tipoRobot = TipoRobot.Kuka_6GDL;
    [Range(5, 6)]
    public int gradosDeLibertad = 6;
    public bool tieneGarra = true;

    [Header("2A. Si es KUKA (Arrastra KukaRobotController)")]
    public KukaRobotController kukaController;

    [Header("2B. Si es SCORBOT Directo (Arrastra ambos scripts)")]
    public SCORBOTController scorbotController;
    public Gripper_Controller gripperControllerV1;

    [Header("2C. Si es SCORBOT ER-IX (Arrastra los 5 ejes y la garra)")]
    public RobotJointController[] ejesScorbotERIX = new RobotJointController[5];
    public GripperController gripperControllerERIX;

    [Header("3. Límites de cada articulación (Grados)")]
    public Vector2[] limitesJ = new Vector2[6]
    {
        new Vector2(-170f, 170f), // J1
        new Vector2(-90f, 90f),   // J2
        new Vector2(-110f, 110f), // J3
        new Vector2(-180f, 180f), // J4
        new Vector2(-120f, 120f), // J5
        new Vector2(-360f, 360f)  // J6
    };

    // Estado interno en grados y apertura (0 = cerrada, 1 = abierta)
    private float[] angulosActuales = new float[6];
    private float aperturaActual = 0f;

    private float[] valoresHome = new float[6];
    private float garraHome = 0f;

    // Respaldo para mover Kuka directo si no tiene sliders viejos asignados
    private Transform[] kukaArticulaciones;
    private Quaternion[] kukaRotacionesIniciales;
    private Vector3[] kukaEjes;
    private float[] kukaSentidos;
    private Vector3 kukaGarraPos1;
    private Vector3 kukaGarraPos2;
    private bool kukaModoDirecto = false;

    void Awake()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        AjustarCollider(col);
    }

    void Start()
    {
        InicializarSegunTipo();

        // Guardar posición original (Home)
        for (int i = 0; i < gradosDeLibertad; i++)
        {
            valoresHome[i] = angulosActuales[i];
        }
        garraHome = aperturaActual;
    }

    private void InicializarSegunTipo()
    {
        if (tipoRobot == TipoRobot.Kuka_6GDL && kukaController != null)
        {
            gradosDeLibertad = 6;
            // Si el compañero no dejó los sliders viejos asignados, tomamos control directo de sus Transforms
            if (kukaController.sliderA1 == null || kukaController.sliderGarra == null)
            {
                kukaModoDirecto = true;
                kukaController.enabled = false; // Evita el Debug.LogError de su Start()

                kukaArticulaciones = new Transform[]
                {
                    kukaController.armAxis1, kukaController.armAxis2, kukaController.armAxis3,
                    kukaController.armAxis4, kukaController.armAxis5, kukaController.armAxis6
                };
                kukaEjes = new Vector3[]
                {
                    kukaController.ejeA1, kukaController.ejeA2, kukaController.ejeA3,
                    kukaController.ejeA4, kukaController.ejeA5, kukaController.ejeA6
                };
                kukaSentidos = new float[]
                {
                    kukaController.sentidoA1, kukaController.sentidoA2, kukaController.sentidoA3,
                    kukaController.sentidoA4, kukaController.sentidoA5, kukaController.sentidoA6
                };

                kukaRotacionesIniciales = new Quaternion[6];
                for (int i = 0; i < 6; i++)
                {
                    if (kukaArticulaciones[i] != null)
                        kukaRotacionesIniciales[i] = kukaArticulaciones[i].localRotation;
                    angulosActuales[i] = 0f;
                }

                if (kukaController.fingerGarra != null)
                    kukaGarraPos1 = kukaController.fingerGarra.localPosition;
                if (kukaController.fingerGarra2 != null)
                    kukaGarraPos2 = kukaController.fingerGarra2.localPosition;
            }
            aperturaActual = 1f; // Kuka inicia con garra abierta (0% cierre)
        }
        else if (tipoRobot == TipoRobot.Scorbot_5GDL_Directo && scorbotController != null)
        {
            gradosDeLibertad = 5;
            limitesJ[0] = new Vector2(-155f, 155f);
            limitesJ[1] = new Vector2(-35f, 130f);
            limitesJ[2] = new Vector2(-130f, 90f);
            limitesJ[3] = new Vector2(-90f, 90f);
            limitesJ[4] = new Vector2(-570f, 570f);

            angulosActuales[0] = scorbotController.anguloJ1;
            angulosActuales[1] = scorbotController.anguloJ2;
            angulosActuales[2] = scorbotController.anguloJ3;
            angulosActuales[3] = scorbotController.anguloJ4;
            angulosActuales[4] = scorbotController.anguloJ5;

            aperturaActual = (gripperControllerV1 != null) ? gripperControllerV1.apertura : 0f;
        }
        else if (tipoRobot == TipoRobot.Scorbot_ERIX_PorEjes)
        {
            gradosDeLibertad = 5;
            for (int i = 0; i < 5; i++)
            {
                if (ejesScorbotERIX[i] != null)
                {
                    limitesJ[i] = new Vector2(ejesScorbotERIX[i].minAngle, ejesScorbotERIX[i].maxAngle);
                    // La posición inicial del modelo corresponde a 0° relativos
                    angulosActuales[i] = 0f;
                }
            }
            aperturaActual = 1f; // GripperController inicia con isOpen = true
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Ray rayo = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(rayo, out RaycastHit impacto, 1000f))
            {
                if (impacto.transform == transform || impacto.transform.IsChildOf(transform))
                {
                    if (PanelUniversalRobotUI.Instancia != null)
                    {
                        PanelUniversalRobotUI.Instancia.AbrirParaRobot(this);
                    }
                }
            }
        }
    }

    // --- LECTURA UNIFICADA ---
    public float ObtenerAngulo(int indice)
    {
        if (indice < 0 || indice >= gradosDeLibertad) return 0f;
        return angulosActuales[indice];
    }

    public float ObtenerGarra()
    {
        return aperturaActual;
    }

    // --- ESCRITURA UNIFICADA ---
    public void EstablecerAngulo(int indice, float angulo)
    {
        if (indice < 0 || indice >= gradosDeLibertad) return;
        angulosActuales[indice] = angulo;

        if (tipoRobot == TipoRobot.Kuka_6GDL && kukaController != null)
        {
            if (!kukaModoDirecto)
            {
                kukaController.MoverEje(indice, angulo);
            }
            else if (kukaArticulaciones[indice] != null)
            {
                Vector3 eje = kukaEjes[indice].normalized;
                float giro = angulo * kukaSentidos[indice];
                kukaArticulaciones[indice].localRotation =
                    kukaRotacionesIniciales[indice] * Quaternion.AngleAxis(giro, eje);
            }
        }
        else if (tipoRobot == TipoRobot.Scorbot_5GDL_Directo && scorbotController != null)
        {
            switch (indice)
            {
                case 0: scorbotController.anguloJ1 = angulo; break;
                case 1: scorbotController.anguloJ2 = angulo; break;
                case 2: scorbotController.anguloJ3 = angulo; break;
                case 3: scorbotController.anguloJ4 = angulo; break;
                case 4: scorbotController.anguloJ5 = angulo; break;
            }
        }
        else if (tipoRobot == TipoRobot.Scorbot_ERIX_PorEjes)
        {
            if (indice < ejesScorbotERIX.Length && ejesScorbotERIX[indice] != null)
            {
                // RobotJointController espera un valor normalizado entre 0 y 1
                float t = Mathf.InverseLerp(ejesScorbotERIX[indice].minAngle, ejesScorbotERIX[indice].maxAngle, angulo);
                ejesScorbotERIX[indice].OnSliderValueChanged(t);
            }
        }
    }

    public void EstablecerGarra(float apertura) // 0 = cerrada, 1 = abierta
    {
        aperturaActual = Mathf.Clamp01(apertura);

        if (tipoRobot == TipoRobot.Kuka_6GDL && kukaController != null)
        {
            // En Kuka: 0 = abierta, 100 = cerrada (inverso a apertura)
            float porcentajeCierre = (1f - aperturaActual) * 100f;
            if (!kukaModoDirecto)
            {
                kukaController.MoverGarra(porcentajeCierre);
            }
            else
            {
                float cierre = porcentajeCierre / 100f;
                if (kukaController.fingerGarra != null)
                    kukaController.fingerGarra.localPosition = kukaGarraPos1 + Vector3.right * (-kukaController.recorridoGarra * cierre);
                if (kukaController.fingerGarra2 != null)
                    kukaController.fingerGarra2.localPosition = kukaGarraPos2 + Vector3.right * (kukaController.recorridoGarra * cierre);
            }
        }
        else if (tipoRobot == TipoRobot.Scorbot_5GDL_Directo && gripperControllerV1 != null)
        {
            gripperControllerV1.apertura = aperturaActual;
        }
        else if (tipoRobot == TipoRobot.Scorbot_ERIX_PorEjes && gripperControllerERIX != null)
        {
            float targetPos = Mathf.Lerp(gripperControllerERIX.closePosition, gripperControllerERIX.openPosition, aperturaActual);
            if (gripperControllerERIX.leftFinger != null)
            {
                Vector3 posL = gripperControllerERIX.leftFinger.localPosition;
                posL.x = -targetPos;
                gripperControllerERIX.leftFinger.localPosition = posL;
            }
            if (gripperControllerERIX.rightFinger != null)
            {
                Vector3 posR = gripperControllerERIX.rightFinger.localPosition;
                posR.x = targetPos;
                gripperControllerERIX.rightFinger.localPosition = posR;
            }
        }
    }

    // --- BOTÓN POSICIÓN ORIGINAL (HOME) ---
    public void VolverAPosicionOriginal()
    {
        for (int i = 0; i < gradosDeLibertad; i++)
        {
            EstablecerAngulo(i, valoresHome[i]);
        }
        if (tieneGarra)
        {
            EstablecerGarra(garraHome);
        }
    }

    private void AjustarCollider(BoxCollider colliderRobot)
    {
        Renderer[] renderizadores = GetComponentsInChildren<Renderer>();
        if (renderizadores.Length == 0) return;

        Bounds limites = renderizadores[0].bounds;
        foreach (Renderer r in renderizadores) limites.Encapsulate(r.bounds);

        Vector3 min = limites.min;
        Vector3 max = limites.max;
        Vector3[] esquinas = {
            new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
        };

        Vector3 minLocal = Vector3.one * float.MaxValue;
        Vector3 maxLocal = Vector3.one * float.MinValue;
        foreach (Vector3 e in esquinas)
        {
            Vector3 local = transform.InverseTransformPoint(e);
            minLocal = Vector3.Min(minLocal, local);
            maxLocal = Vector3.Max(maxLocal, local);
        }

        colliderRobot.center = (minLocal + maxLocal) * 0.5f;
        colliderRobot.size = maxLocal - minLocal + Vector3.one * 0.05f;
    }
}