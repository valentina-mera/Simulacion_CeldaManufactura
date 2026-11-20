
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(100)]
public class SCORBOT_CartesianController : MonoBehaviour
{
    [Header("Referencias del robot")]
    public SCORBOTController robot;
    public Transform tcp;

    [Header("Botones cartesianos")]
    public Button btnXPositivo;
    public Button btnXNegativo;
    public Button btnYPositivo;
    public Button btnYNegativo;
    public Button btnZPositivo;
    public Button btnZNegativo;

    [Header("Posicion cartesiana")]
    public Vector3 posicionObjetivo;
    public Vector3 posicionActual;
    public float errorCartesiano;

    [Header("Configuracion del movimiento")]
    [Min(0.0001f)]
    public float velocidadCartesiana = 0.02f;

    [Min(0.001f)]
    public float velocidadArticular = 30f;

    [Min(0.00001f)]
    public float tolerancia = 0.0005f;

    [Min(0.0001f)]
    public float amortiguamiento = 0.01f;

    [Header("Estado")]
    public bool movimientoActivo;
    public Vector3 direccionMovimiento;

    private const int NUM_ARTICULACIONES = 5;

    private readonly float[] limitesMinimos =
    {
        -155f, -35f, -130f, -90f, -570f
    };

    private readonly float[] limitesMaximos =
    {
        155f, 130f, 90f, 90f, 570f
    };

    private Transform[] articulaciones;
    private Vector3[] ejesLocales;

    private void Start()
    {
        if (robot == null || tcp == null)
        {
            Debug.LogError(
                "SCORBOT Cartesian: faltan referencias."
            );

            enabled = false;
            return;
        }

        articulaciones = new Transform[]
        {
            robot.j1,
            robot.j2,
            robot.j3,
            robot.j4,
            robot.j5
        };

        ejesLocales = new Vector3[]
        {
            Vector3.up,
            Vector3.right,
            Vector3.right,
            Vector3.right,
            Vector3.forward
        };

        foreach (Transform articulacion in articulaciones)
        {
            if (articulacion == null)
            {
                Debug.LogError(
                    "SCORBOT Cartesian: falta una articulacion."
                );

                enabled = false;
                return;
            }
        }

        ConfigurarBoton(btnXPositivo, Vector3.right);
        ConfigurarBoton(btnXNegativo, Vector3.left);
        ConfigurarBoton(btnYPositivo, Vector3.up);
        ConfigurarBoton(btnYNegativo, Vector3.down);
        ConfigurarBoton(btnZPositivo, Vector3.forward);
        ConfigurarBoton(btnZNegativo, Vector3.back);

        posicionActual = tcp.position;
        posicionObjetivo = posicionActual;

        movimientoActivo = false;
        direccionMovimiento = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (robot == null || tcp == null)
            return;

        posicionActual = tcp.position;

        if (!movimientoActivo)
        {
            posicionObjetivo = posicionActual;
            errorCartesiano = 0f;
            return;
        }

        // Seguridad: detener al soltar el clic.
        if (!Input.GetMouseButton(0))
        {
            DetenerMovimiento();
            return;
        }

        // El objetivo avanza con el tiempo.
        // No existe limite de duracion.
        posicionObjetivo +=
            direccionMovimiento *
            velocidadCartesiana *
            Time.deltaTime;

        errorCartesiano = Vector3.Distance(
            posicionActual,
            posicionObjetivo
        );

        // Evitar que el objetivo se aleje demasiado
        // si el robot no puede seguirlo.
        float errorMaximo = 0.02f;

        if (errorCartesiano > errorMaximo)
        {
            posicionObjetivo =
                posicionActual +
                (posicionObjetivo - posicionActual)
                .normalized * errorMaximo;
        }

        ResolverCinematicaInversa();
    }

    // ==========================================
    // CONFIGURACION DE BOTONES
    // ==========================================

    private void ConfigurarBoton(
        Button boton,
        Vector3 direccion)
    {
        if (boton == null)
            return;

        EventTrigger trigger =
            boton.GetComponent<EventTrigger>();

        if (trigger == null)
        {
            trigger =
                boton.gameObject.AddComponent<EventTrigger>();
        }

        if (trigger.triggers == null)
        {
            trigger.triggers =
                new System.Collections.Generic.List<
                    EventTrigger.Entry>();
        }

        EventTrigger.Entry presionar =
            new EventTrigger.Entry();

        presionar.eventID =
            EventTriggerType.PointerDown;

        presionar.callback.AddListener(
            (datos) => IniciarMovimiento(direccion)
        );

        trigger.triggers.Add(presionar);

        EventTrigger.Entry soltar =
            new EventTrigger.Entry();

        soltar.eventID =
            EventTriggerType.PointerUp;

        soltar.callback.AddListener(
            (datos) => DetenerMovimiento()
        );

        trigger.triggers.Add(soltar);

        EventTrigger.Entry salir =
            new EventTrigger.Entry();

        salir.eventID =
            EventTriggerType.PointerExit;

        salir.callback.AddListener(
            (datos) => DetenerMovimiento()
        );

        trigger.triggers.Add(salir);
    }

    private void IniciarMovimiento(Vector3 direccion)
    {
        if (!enabled || robot == null || tcp == null)
            return;

        direccionMovimiento = direccion.normalized;

        posicionActual = tcp.position;
        posicionObjetivo = posicionActual;

        movimientoActivo = true;
    }

    public void DetenerMovimiento()
    {
        movimientoActivo = false;
        direccionMovimiento = Vector3.zero;

        if (tcp != null)
        {
            posicionActual = tcp.position;
            posicionObjetivo = posicionActual;
        }

        errorCartesiano = 0f;
    }

    private void OnDisable()
    {
        DetenerMovimiento();
    }

    // ==========================================
    // ANGULOS ARTICULARES
    // ==========================================

    private float[] ObtenerAngulos()
    {
        return new float[]
        {
            robot.anguloJ1,
            robot.anguloJ2,
            robot.anguloJ3,
            robot.anguloJ4,
            robot.anguloJ5
        };
    }

    private void AplicarAngulos(float[] angulos)
    {
        robot.anguloJ1 = Mathf.Clamp(
            angulos[0], limitesMinimos[0], limitesMaximos[0]
        );

        robot.anguloJ2 = Mathf.Clamp(
            angulos[1], limitesMinimos[1], limitesMaximos[1]
        );

        robot.anguloJ3 = Mathf.Clamp(
            angulos[2], limitesMinimos[2], limitesMaximos[2]
        );

        robot.anguloJ4 = Mathf.Clamp(
            angulos[3], limitesMinimos[3], limitesMaximos[3]
        );

        robot.anguloJ5 = Mathf.Clamp(
            angulos[4], limitesMinimos[4], limitesMaximos[4]
        );
    }

    // ==========================================
    // JACOBIANO
    // ==========================================

    private Vector3[] CalcularJacobiano()
    {
        Vector3[] jacobiano =
            new Vector3[NUM_ARTICULACIONES];

        Vector3 posicionTCP = tcp.position;

        for (int i = 0; i < NUM_ARTICULACIONES; i++)
        {
            Transform articulacion = articulaciones[i];

            Vector3 ejeGlobal =
                articulacion.TransformDirection(
                    ejesLocales[i]
                ).normalized;

            Vector3 distancia =
                posicionTCP - articulacion.position;

            jacobiano[i] =
                Vector3.Cross(ejeGlobal, distancia)
                * Mathf.Deg2Rad;
        }

        return jacobiano;
    }

    // ==========================================
    // CINEMATICA INVERSA
    // ==========================================

    private void ResolverCinematicaInversa()
    {
        Vector3 error =
            posicionObjetivo - tcp.position;

        Vector3[] J = CalcularJacobiano();

        float[,] matriz = new float[3, 3];

        for (int i = 0; i < NUM_ARTICULACIONES; i++)
        {
            Vector3 columna = J[i];

            matriz[0, 0] += columna.x * columna.x;
            matriz[0, 1] += columna.x * columna.y;
            matriz[0, 2] += columna.x * columna.z;

            matriz[1, 0] += columna.y * columna.x;
            matriz[1, 1] += columna.y * columna.y;
            matriz[1, 2] += columna.y * columna.z;

            matriz[2, 0] += columna.z * columna.x;
            matriz[2, 1] += columna.z * columna.y;
            matriz[2, 2] += columna.z * columna.z;
        }

        float lambda2 =
            amortiguamiento * amortiguamiento;

        matriz[0, 0] += lambda2;
        matriz[1, 1] += lambda2;
        matriz[2, 2] += lambda2;

        Vector3 resultado;

        if (!ResolverSistema3x3(
            matriz, error, out resultado))
        {
            DetenerMovimiento();
            return;
        }

        float[] angulos = ObtenerAngulos();

        float maximoCambio =
            velocidadArticular * Time.deltaTime;

        bool huboCambio = false;

        for (int i = 0; i < NUM_ARTICULACIONES; i++)
        {
            float incremento =
                Vector3.Dot(J[i], resultado);

            incremento = Mathf.Clamp(
                incremento,
                -maximoCambio,
                maximoCambio
            );

            float nuevoAngulo = Mathf.Clamp(
                angulos[i] + incremento,
                limitesMinimos[i],
                limitesMaximos[i]
            );

            if (Mathf.Abs(nuevoAngulo - angulos[i])
                > 0.000001f)
            {
                huboCambio = true;
            }

            angulos[i] = nuevoAngulo;
        }

        if (!huboCambio)
        {
            // Detener si no existe avance articular.
            DetenerMovimiento();
            return;
        }

        AplicarAngulos(angulos);
    }

    // ==========================================
    // SISTEMA LINEAL 3x3
    // ==========================================

    private bool ResolverSistema3x3(
        float[,] A,
        Vector3 b,
        out Vector3 resultado)
    {
        resultado = Vector3.zero;

        float[,] m = new float[3, 4];

        for (int fila = 0; fila < 3; fila++)
        {
            for (int columna = 0; columna < 3; columna++)
                m[fila, columna] = A[fila, columna];
        }

        m[0, 3] = b.x;
        m[1, 3] = b.y;
        m[2, 3] = b.z;

        for (int i = 0; i < 3; i++)
        {
            int pivote = i;

            for (int fila = i + 1; fila < 3; fila++)
            {
                if (Mathf.Abs(m[fila, i]) >
                    Mathf.Abs(m[pivote, i]))
                {
                    pivote = fila;
                }
            }

            if (Mathf.Abs(m[pivote, i]) < 0.0000000001f)
                return false;

            if (pivote != i)
            {
                for (int columna = 0; columna < 4; columna++)
                {
                    float temporal = m[i, columna];
                    m[i, columna] = m[pivote, columna];
                    m[pivote, columna] = temporal;
                }
            }

            float divisor = m[i, i];

            for (int columna = i; columna < 4; columna++)
                m[i, columna] /= divisor;

            for (int fila = 0; fila < 3; fila++)
            {
                if (fila == i)
                    continue;

                float factor = m[fila, i];

                for (int columna = i; columna < 4; columna++)
                {
                    m[fila, columna] -=
                        factor * m[i, columna];
                }
            }
        }

        resultado = new Vector3(
            m[0, 3],
            m[1, 3],
            m[2, 3]
        );

        return
            !float.IsNaN(resultado.x) &&
            !float.IsNaN(resultado.y) &&
            !float.IsNaN(resultado.z) &&
            !float.IsInfinity(resultado.x) &&
            !float.IsInfinity(resultado.y) &&
            !float.IsInfinity(resultado.z);
    }
}
