
using UnityEngine;

public class Gripper_Controller : MonoBehaviour
{
    [Header("Pivotes de las pinzas")]
    public Transform pivoteIzquierdo;
    public Transform pivoteDerecho;

    [Header("Apertura de la pinza")]
    [Range(0f, 1f)]
    public float apertura = 0f;

    [Header("Angulos de referencia")]
    public float izquierdoCerrado = 25.455f;
    public float izquierdoAbierto = -30f;

    public float derechoCerrado = -25.062f;
    public float derechoAbierto = 30f;

    void Update()
    {
        if (pivoteIzquierdo == null || pivoteDerecho == null)
            return;

        float anguloIzquierdo = Mathf.Lerp(
            izquierdoCerrado, izquierdoAbierto, apertura);

        float anguloDerecho = Mathf.Lerp(
            derechoCerrado, derechoAbierto, apertura);

        Vector3 rotIzquierda = pivoteIzquierdo.localEulerAngles;
        Vector3 rotDerecha = pivoteDerecho.localEulerAngles;

        rotIzquierda.z = anguloIzquierdo;
        rotDerecha.z = anguloDerecho;

        pivoteIzquierdo.localEulerAngles = rotIzquierda;
        pivoteDerecho.localEulerAngles = rotDerecha;
    }
}


