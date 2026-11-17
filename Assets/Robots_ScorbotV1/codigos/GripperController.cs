using UnityEngine;

public class GripperController : MonoBehaviour
{
    public Transform leftFinger;   // Arrastra gripper_left aquí
    public Transform rightFinger;  // Arrastra gripper_right aquí
    
    public float openPosition = 0.09f;   // Amplitud de apertura
    public float closePosition = 0.002f; // Posición de cierre
    
    private bool isOpen = true;

    public void ToggleGripper()
    {
        isOpen = !isOpen;
        float targetPos = isOpen ? openPosition : closePosition;

        if (leftFinger != null)
        {
            // Invertimos el signo para que el dedo izquierdo vaya en dirección contraria
            Vector3 posLeft = leftFinger.localPosition;
            posLeft.x = -targetPos; 
            leftFinger.localPosition = posLeft;
        }

        if (rightFinger != null)
        {
            // Invertimos el signo para que el dedo derecho vaya hacia el otro lado
            Vector3 posRight = rightFinger.localPosition;
            posRight.x = targetPos; 
            rightFinger.localPosition = posRight;
        }
    }
}