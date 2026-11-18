using UnityEngine;
using UnityEngine.UI;

public class RobotJointController : MonoBehaviour
{
    [Header("Configuración de la Articulación")]
    public Transform targetJoint; // El eslabón o pieza del robot que va a girar
    public Vector3 rotationAxis = Vector3.up; // Eje de giro (Right, Up o Forward)
    
    [Header("Límites en Grados (SCORBOT-ER IX)")]
    public float minAngle = -135f; // Límite mínimo físico
    public float maxAngle = 135f;  // Límite máximo físico

    private Vector3 initialLocalEuler;

    void Start()
    {
        // Guarda la rotación inicial de la pieza para respetar la postura base del modelo
        if (targetJoint != null)
        {
            initialLocalEuler = targetJoint.localEulerAngles;
        }
    }

    // Esta es la función que llamará el Slider cada vez que lo muevas
    public void OnSliderValueChanged(float sliderValue)
    {
        if (targetJoint == null) return;

        // 1. Mapea el valor del slider (0 a 1) al rango de ángulos reales permitidos
        float currentAngle = Mathf.Lerp(minAngle, maxAngle, sliderValue);

        // 2. Restricción física estricta (Clamp) para asegurar que nunca rebase los topes
        currentAngle = Mathf.Clamp(currentAngle, minAngle, maxAngle);

        // 3. Aplica la rotación absoluta sumada a la posición inicial (usando '=')
        Vector3 newRotation = initialLocalEuler;
        
        if (rotationAxis == Vector3.right) 
            newRotation.x = initialLocalEuler.x + currentAngle;
        else if (rotationAxis == Vector3.up) 
            newRotation.y = initialLocalEuler.y + currentAngle;
        else if (rotationAxis == Vector3.forward) 
            newRotation.z = initialLocalEuler.z + currentAngle;

        targetJoint.localEulerAngles = newRotation;
    }
}