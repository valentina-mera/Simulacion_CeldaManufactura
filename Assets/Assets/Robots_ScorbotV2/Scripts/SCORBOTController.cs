
using UnityEngine;

public class SCORBOTController : MonoBehaviour
{
    [Header("Articulaciones")]
    public Transform j1;
    public Transform j2;
    public Transform j3;
    public Transform j4;
    public Transform j5;

    [Header("Angulos de control")]
    [Range(-155f, 155f)] public float anguloJ1;
    [Range(-35f, 130f)] public float anguloJ2;
    [Range(-130f, 90f)] public float anguloJ3;
    [Range(-90f, 90f)] public float anguloJ4;
    [Range(-570f, 570f)] public float anguloJ5;

    private Quaternion[] rotacionesIniciales;

    void Start()
    {
        rotacionesIniciales = new Quaternion[]
        {
            j1.localRotation,
            j2.localRotation,
            j3.localRotation,
            j4.localRotation,
            j5.localRotation
        };
    }

    void Update()
    {
        // Limites angulares
        anguloJ1 = Mathf.Clamp(anguloJ1, -155f, 155f);
        anguloJ2 = Mathf.Clamp(anguloJ2, -35f, 130f);
        anguloJ3 = Mathf.Clamp(anguloJ3, -130f, 90f);
        anguloJ4 = Mathf.Clamp(anguloJ4, -90f, 90f);
        anguloJ5 = Mathf.Clamp(anguloJ5, -570f, 570f);

        // Movimiento de articulaciones
        j1.localRotation = rotacionesIniciales[0] *
            Quaternion.AngleAxis(anguloJ1, Vector3.up);

        j2.localRotation = rotacionesIniciales[1] *
            Quaternion.AngleAxis(anguloJ2, Vector3.right);

        j3.localRotation = rotacionesIniciales[2] *
            Quaternion.AngleAxis(anguloJ3, Vector3.right);

        j4.localRotation = rotacionesIniciales[3] *
            Quaternion.AngleAxis(anguloJ4, Vector3.right);

        j5.localRotation = rotacionesIniciales[4] *
            Quaternion.AngleAxis(anguloJ5, Vector3.forward);
    }
}
