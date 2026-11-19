using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PanelUniversalRobotUI : MonoBehaviour
{
    public static PanelUniversalRobotUI Instancia { get; private set; }

    [Header("Contenedor Visual del Panel")]
    public GameObject contenedorVisual; // El Panel hijo que se oculta/muestra
    public TMP_Text textoNombreRobot;

    [Header("Filas de Articulaciones (J1 a J6)")]
    public GameObject[] filasArticulaciones = new GameObject[6]; // Contenedor de cada fila (Label + Slider + Valor)
    public Slider[] slidersJ = new Slider[6];
    public TMP_Text[] textosValorJ = new TMP_Text[6];

    [Header("Control de Garra")]
    public GameObject filaGarra;
    public Slider sliderGarra;
    public TMP_Text textoValorGarra;

    [Header("Botones de Acción")]
    public Button botonPosicionOriginal;
    public Button botonCerrar;

    private RobotEstandarAdapter robotActual;

    void Awake()
    {
        Instancia = this;

        // Inicia oculto al darle Play para que se vea la escena normal
        if (contenedorVisual != null)
            contenedorVisual.SetActive(false);

        // Configurar eventos de los 6 sliders
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

        // Configurar evento de la garra
        if (sliderGarra != null)
        {
            sliderGarra.minValue = 0f;
            sliderGarra.maxValue = 1f;
            sliderGarra.onValueChanged.AddListener(valor =>
            {
                if (robotActual != null)
                {
                    robotActual.EstablecerGarra(valor);
                    textoValorGarra.text = $"{valor * 100f:0}%";
                }
            });
        }

        if (botonPosicionOriginal != null)
            botonPosicionOriginal.onClick.AddListener(RestaurarHome);

        if (botonCerrar != null)
            botonCerrar.onClick.AddListener(CerrarPanel);
    }

    public void AbrirParaRobot(RobotEstandarAdapter robot)
    {
        // Si se vuelve a hacer clic sobre el mismo robot ya abierto, cierra el panel
        if (robotActual == robot && contenedorVisual.activeSelf)
        {
            CerrarPanel();
            return;
        }

        robotActual = robot;
        textoNombreRobot.text = robot.nombreMostrarEnPanel;

        // Activar solo las filas según los grados de libertad (5 o 6)
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

        // Mostrar u ocultar fila de garra
        if (filaGarra != null)
            filaGarra.SetActive(robot.tieneGarra);

        if (robot.tieneGarra && sliderGarra != null)
        {
            float apertura = robot.ObtenerGarra();
            sliderGarra.SetValueWithoutNotify(apertura);
            textoValorGarra.text = $"{apertura * 100f:0}%";
        }

        contenedorVisual.SetActive(true);
    }

    public void RestaurarHome()
    {
        if (robotActual == null) return;

        robotActual.VolverAPosicionOriginal();

        // Actualizar visualmente los sliders y textos
        for (int i = 0; i < robotActual.gradosDeLibertad; i++)
        {
            float ang = robotActual.ObtenerAngulo(i);
            slidersJ[i].SetValueWithoutNotify(ang);
            textosValorJ[i].text = $"{ang:0}°";
        }

        if (robotActual.tieneGarra && sliderGarra != null)
        {
            float ap = robotActual.ObtenerGarra();
            sliderGarra.SetValueWithoutNotify(ap);
            textoValorGarra.text = $"{ap * 100f:0}%";
        }
    }

    public void CerrarPanel()
    {
        robotActual = null;
        if (contenedorVisual != null)
            contenedorVisual.SetActive(false);
    }
}