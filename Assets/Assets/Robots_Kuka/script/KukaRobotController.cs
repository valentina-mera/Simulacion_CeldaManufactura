
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KukaRobotController : MonoBehaviour
{
    [Header("ARTICULACIONES KUKA")]
    public Transform armAxis1;
    public Transform armAxis2;
    public Transform armAxis3;
    public Transform armAxis4;
    public Transform armAxis5;
    public Transform armAxis6;

    [Header("GARRA")]
    public Transform fingerGarra;
    public Transform fingerGarra2;

    [Header("SLIDERS")]
    public Slider sliderA1;
    public Slider sliderA2;
    public Slider sliderA3;
    public Slider sliderA4;
    public Slider sliderA5;
    public Slider sliderA6;
    public Slider sliderGarra;

    [Header("INDICADORES")]
    public TMP_Text textoA1;
    public TMP_Text textoA2;
    public TMP_Text textoA3;
    public TMP_Text textoA4;
    public TMP_Text textoA5;
    public TMP_Text textoA6;
    public TMP_Text textoGarra;
    public TMP_Text estadoGarra;

    [Header("EJES LOCALES")]
    public Vector3 ejeA1 = Vector3.up;
    public Vector3 ejeA2 = Vector3.up;
    public Vector3 ejeA3 = Vector3.up;
    public Vector3 ejeA4 = Vector3.up;
    public Vector3 ejeA5 = Vector3.up;
    public Vector3 ejeA6 = Vector3.up;

    [Header("SENTIDOS")]
    public float sentidoA1 = 1f;
    public float sentidoA2 = 1f;
    public float sentidoA3 = 1f;
    public float sentidoA4 = 1f;
    public float sentidoA5 = 1f;
    public float sentidoA6 = 1f;

    [Header("RECORRIDO DE GARRA")]
    [Range(0.001f, 0.05f)]
    public float recorridoGarra = 0.01f;

    private Transform[] articulaciones;
    private Slider[] sliders;
    private TMP_Text[] textos;
    private Vector3[] ejes;
    private float[] sentidos;

    private Quaternion[] rotacionesOriginales;
    private float[] valoresHome;

    private Vector3 posicionOriginalGarra1;
    private Vector3 posicionOriginalGarra2;

    void Start()
    {
        articulaciones = new Transform[]
        {
            armAxis1, armAxis2, armAxis3,
            armAxis4, armAxis5, armAxis6
        };

        sliders = new Slider[]
        {
            sliderA1, sliderA2, sliderA3,
            sliderA4, sliderA5, sliderA6
        };

        textos = new TMP_Text[]
        {
            textoA1, textoA2, textoA3,
            textoA4, textoA5, textoA6
        };

        ejes = new Vector3[]
        {
            ejeA1, ejeA2, ejeA3,
            ejeA4, ejeA5, ejeA6
        };

        sentidos = new float[]
        {
            sentidoA1, sentidoA2, sentidoA3,
            sentidoA4, sentidoA5, sentidoA6
        };

        rotacionesOriginales = new Quaternion[6];
        valoresHome = new float[6];

        for (int i = 0; i < 6; i++)
        {
            if (articulaciones[i] == null || sliders[i] == null)
            {
                Debug.LogError("Falta asignar A" + (i + 1));
                enabled = false;
                return;
            }

            rotacionesOriginales[i] =
                articulaciones[i].localRotation;

            valoresHome[i] = sliders[i].value;

            int indice = i;

            sliders[i].onValueChanged.AddListener(
                valor => MoverEje(indice, valor)
            );

            MoverEje(i, sliders[i].value);
        }

        if (fingerGarra == null || fingerGarra2 == null ||
            sliderGarra == null)
        {
            Debug.LogError("Falta asignar alguna pieza de la garra");
            enabled = false;
            return;
        }

        posicionOriginalGarra1 = fingerGarra.localPosition;
        posicionOriginalGarra2 = fingerGarra2.localPosition;

        sliderGarra.minValue = 0;
        sliderGarra.maxValue = 100;

        sliderGarra.onValueChanged.AddListener(MoverGarra);

        MoverGarra(sliderGarra.value);
    }

    public void MoverEje(int indice, float angulo)
    {
        angulo = Mathf.Clamp(
            angulo,
            sliders[indice].minValue,
            sliders[indice].maxValue);
    Vector3 eje = ejes[indice].normalized;

        if (eje.sqrMagnitude < 0.001f)
            return;

        float giro = angulo * sentidos[indice];

        articulaciones[indice].localRotation =
            rotacionesOriginales[indice] *
            Quaternion.AngleAxis(giro, eje);

        if (textos[indice] != null)
        {
            textos[indice].text =
                "A" + (indice + 1) + ": " +
                angulo.ToString("F1") + "°";
        }
    }

    public void MoverGarra(float valor)
    {
        float cierre = valor / 100f;

        fingerGarra.localPosition =
            posicionOriginalGarra1 +
            Vector3.right * (-recorridoGarra * cierre);

        fingerGarra2.localPosition =
            posicionOriginalGarra2 +
            Vector3.right * (recorridoGarra * cierre);

        if (textoGarra != null)
        {
            textoGarra.text =
                "GARRA: " + valor.ToString("F0") + "%";
        }

        if (estadoGarra != null)
        {
            if (valor <= 1f)
            {
                estadoGarra.text = "ESTADO: ABIERTA";
            }
            else if (valor >= 99f)
            {
                estadoGarra.text = "ESTADO: CIERRE MAXIMO";
            }
            else
            {
                estadoGarra.text = "ESTADO: CIERRE PARCIAL";
            }
        }
    }

    public void IrHome()
    {
        for (int i = 0; i < 6; i++)
        {
            sliders[i].value = valoresHome[i];
        }

        sliderGarra.value = 0;
    }

    public void ResetearRobot()
    {
        for (int i = 0; i < 6; i++)
        {
            sliders[i].value = 0;
        }

        sliderGarra.value = 0;
    }
}
