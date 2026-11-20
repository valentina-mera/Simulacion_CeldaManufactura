
using UnityEngine;

public class TCP_Coordinates : MonoBehaviour
{
    [Header("Punto de control del robot")]
    public Transform tcp;

    [Header("Coordenadas cartesianas globales")]
    public Vector3 posicionTCP;

    void Update()
    {
        if (tcp != null)
        {
            posicionTCP = tcp.position;
        }
    }
}

