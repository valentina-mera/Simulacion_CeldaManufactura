
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SCORBOT_UIController : MonoBehaviour
{
    [Header("Controladores del robot")]
    public SCORBOTController robot;
    public Gripper_Controller gripper;

    [Header("Sliders")]
    public Slider sliderJ1;
    public Slider sliderJ2;
    public Slider sliderJ3;
    public Slider sliderJ4;
    public Slider sliderJ5;
    public Slider sliderJ6;

    [Header("Indicadores de angulo")]
    public TMP_Text valorJ1;
    public TMP_Text valorJ2;
    public TMP_Text valorJ3;
    public TMP_Text valorJ4;
    public TMP_Text valorJ5;

    [Header("Indicador de apertura")]
    public TMP_Text valorJ6;

    void Start()
    {
        // Inicializar sliders de articulaciones
        sliderJ1.SetValueWithoutNotify(robot.anguloJ1);
        sliderJ2.SetValueWithoutNotify(robot.anguloJ2);
        sliderJ3.SetValueWithoutNotify(robot.anguloJ3);
        sliderJ4.SetValueWithoutNotify(robot.anguloJ4);
        sliderJ5.SetValueWithoutNotify(robot.anguloJ5);

        // Inicializar slider de pinza
        sliderJ6.minValue = 0f;
        sliderJ6.maxValue = 1f;
        sliderJ6.wholeNumbers = false;
        sliderJ6.SetValueWithoutNotify(gripper.apertura);

        // Control de articulaciones
        sliderJ1.onValueChanged.AddListener(v => robot.anguloJ1 = v);
        sliderJ2.onValueChanged.AddListener(v => robot.anguloJ2 = v);
        sliderJ3.onValueChanged.AddListener(v => robot.anguloJ3 = v);
        sliderJ4.onValueChanged.AddListener(v => robot.anguloJ4 = v);
        sliderJ5.onValueChanged.AddListener(v => robot.anguloJ5 = v);

        // Control de apertura de la pinza
        sliderJ6.onValueChanged.AddListener(v => gripper.apertura = v);
    }

    void Update()
    {
        valorJ1.text = $"{robot.anguloJ1:0}°";
        valorJ2.text = $"{robot.anguloJ2:0}°";
        valorJ3.text = $"{robot.anguloJ3:0}°";
        valorJ4.text = $"{robot.anguloJ4:0}°";
        valorJ5.text = $"{robot.anguloJ5:0}°";

        // Porcentaje de apertura
        valorJ6.text = $"{gripper.apertura * 100f:0}%";
    }
}
