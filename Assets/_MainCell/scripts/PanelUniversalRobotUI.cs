using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PanelUniversalRobotUI : MonoBehaviour
{
    public static PanelUniversalRobotUI Instancia { get; private set; }

    [Header("Contenedor Visual del Panel")]
    public GameObject contenedorVisual;
    public TMP_Text textoNombreRobot;

    [Header("Filas de Articulaciones (J1 a J6)")]
    public GameObject[] filasArticulaciones = new GameObject[6];
    public Slider[] slidersJ = new Slider[6];
    public TMP_Text[] textosValorJ = new TMP_Text[6];

    [Header("Control de Garra (Botón Abrir/Cerrar)")]
    public GameObject filaGarra;
    public Button botonGarra;
    public TMP_Text textoBotonGarra;

    [Header("Botones de Acción")]
    public Button botonPosicionOriginal;
    public Button botonCerrar;

    private RobotEstandarAdapter robotActual;
    private bool garraAbierta = true;

    void Awake()
    {
        Instancia = this;

        if (contenedorVisual != null)
            contenedorVisual.SetActive(false);

        for (int i = 0; i < 6; i++)
        {
            int indice = i;
            slidersJ[indice].onValueChanged.AddListener(valor =>
            {
                if (robotActual != null)
                {
                    robotActual.EstablecerAngulo(indice, valor);
                    textosValorJ[indice].text = $"{valor:0}°";
                }
            });
        }

        if (botonGarra != null)
            botonGarra.onClick.AddListener(AlternarGarra);

        if (botonPosicionOriginal != null)
            botonPosicionOriginal.onClick.AddListener(RestaurarHome);

        if (botonCerrar != null)
            botonCerrar.onClick.AddListener(CerrarPanel);
    }

    public void AbrirParaRobot(RobotEstandarAdapter robot)
    {
        if (robotActual == robot && contenedorVisual.activeSelf)
        {
            CerrarPanel();
            return;
        }

        robotActual = robot;
        textoNombreRobot.text = robot.nombreMostrarEnPanel;

        for (int i = 0; i < 6; i++)
        {
            bool activo = i < robot.gradosDeLibertad;
            if (filasArticulaciones[i] != null)
                filasArticulaciones[i].SetActive(activo);

            if (activo)
            {
                slidersJ[i].minValue = robot.limitesJ[i].x;
                slidersJ[i].maxValue = robot.limitesJ[i].y;
                float anguloActual = robot.ObtenerAngulo(i);
                slidersJ[i].SetValueWithoutNotify(anguloActual);
                textosValorJ[i].text = $"{anguloActual:0}°";
            }
        }

        if (filaGarra != null)
            filaGarra.SetActive(robot.tieneGarra);

        if (robot.tieneGarra)
        {
            garraAbierta = robot.ObtenerGarra() >= 0.5f;
            ActualizarVisualBotonGarra();
        }

        contenedorVisual.SetActive(true);
    }

    public void AlternarGarra()
    {
        if (robotActual == null) return;

        garraAbierta = !garraAbierta;
        robotActual.EstablecerGarra(garraAbierta ? 1f : 0f);
        ActualizarVisualBotonGarra();
    }

    private void ActualizarVisualBotonGarra()
    {
        if (textoBotonGarra != null)
        {
            // Muestra la acción disponible al hacer clic (o el estado actual)
            textoBotonGarra.text = garraAbierta ? "Cerrar Garra" : "Abrir Garra";
        }
    }

    public void RestaurarHome()
    {
        if (robotActual == null) return;

        robotActual.VolverAPosicionOriginal();

        for (int i = 0; i < robotActual.gradosDeLibertad; i++)
        {
            float ang = robotActual.ObtenerAngulo(i);
            slidersJ[i].SetValueWithoutNotify(ang);
            textosValorJ[i].text = $"{ang:0}°";
        }

        if (robotActual.tieneGarra)
        {
            garraAbierta = robotActual.ObtenerGarra() >= 0.5f;
            ActualizarVisualBotonGarra();
        }
    }

    public void CerrarPanel()
    {
        robotActual = null;
        if (contenedorVisual != null)
            contenedorVisual.SetActive(false);
    }
}