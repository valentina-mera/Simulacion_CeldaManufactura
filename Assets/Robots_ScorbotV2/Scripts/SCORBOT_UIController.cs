
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SCORBOT_UIController : MonoBehaviour
{
    [Header("Controladores del robot")]
    public SCORBOTController robot;
    public Gripper_Controller gripper;

    [Header("Sliders de articulaciones")]
    public Slider sliderJ1;
    public Slider sliderJ2;
    public Slider sliderJ3;
    public Slider sliderJ4;
    public Slider sliderJ5;

    [Header("Indicadores de angulo")]
    public TMP_Text valorJ1;
    public TMP_Text valorJ2;
    public TMP_Text valorJ3;
    public TMP_Text valorJ4;
    public TMP_Text valorJ5;

    [Header("Control de pinza")]
    public Button btnPinza;
    public TMP_Text textoBtnPinza;

    void Start()
    {
        // Comprobar las referencias necesarias
        if (robot == null || gripper == null)
        {
            Debug.LogWarning("Faltan referencias del robot o la pinza.");
            return;
        }

        // Inicializar sliders de articulaciones
        sliderJ1.SetValueWithoutNotify(robot.anguloJ1);
        sliderJ2.SetValueWithoutNotify(robot.anguloJ2);
        sliderJ3.SetValueWithoutNotify(robot.anguloJ3);
        sliderJ4.SetValueWithoutNotify(robot.anguloJ4);
        sliderJ5.SetValueWithoutNotify(robot.anguloJ5);

        // Control de articulaciones
        sliderJ1.onValueChanged.AddListener(v => robot.anguloJ1 = v);
        sliderJ2.onValueChanged.AddListener(v => robot.anguloJ2 = v);
        sliderJ3.onValueChanged.AddListener(v => robot.anguloJ3 = v);
        sliderJ4.onValueChanged.AddListener(v => robot.anguloJ4 = v);
        sliderJ5.onValueChanged.AddListener(v => robot.anguloJ5 = v);

        // Control de apertura y cierre
        btnPinza.onClick.AddListener(AlternarPinza);

        // Inicializar el texto del boton
        ActualizarTextoPinza();
    }

    void Update()
    {
        if (robot == null)
            return;

        // Mostrar angulos de articulaciones
        valorJ1.text = $"{robot.anguloJ1:0}°";
        valorJ2.text = $"{robot.anguloJ2:0}°";
        valorJ3.text = $"{robot.anguloJ3:0}°";
        valorJ4.text = $"{robot.anguloJ4:0}°";
        valorJ5.text = $"{robot.anguloJ5:0}°";
    }

    public void AlternarPinza()
    {
        if (gripper == null)
            return;

        // Alternar entre cerrada (0) y abierta (1)
        if (gripper.apertura < 0.5f)
        {
            gripper.apertura = 1f;
        }
        else
        {
            gripper.apertura = 0f;
        }

        ActualizarTextoPinza();
    }

    void ActualizarTextoPinza()
    {
        if (gripper == null || textoBtnPinza == null)
            return;

        if (gripper.apertura >= 0.5f)
        {
            textoBtnPinza.text = "Cerrar Pinza";
        }
        else
        {
            textoBtnPinza.text = "Abrir Pinza";
        }
    }
}
