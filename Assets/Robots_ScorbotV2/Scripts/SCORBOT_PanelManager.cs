
using UnityEngine;
using UnityEngine.EventSystems;

public class SCORBOT_PanelManager : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject robot;
    public GameObject panelSCORBOT;
    public Camera camaraPrincipal;

    void Start()
    {
        if (camaraPrincipal == null)
            camaraPrincipal = Camera.main;

        if (panelSCORBOT != null)
            panelSCORBOT.SetActive(false);
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        // Evitar detectar clics sobre la interfaz.
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        if (camaraPrincipal == null || robot == null)
            return;

        Ray rayo = camaraPrincipal.ScreenPointToRay(
            Input.mousePosition
        );

        if (Physics.Raycast(rayo, out RaycastHit impacto))
        {
            // Comprobar si la pieza pertenece al robot.
            if (impacto.transform == robot.transform ||
                impacto.transform.IsChildOf(robot.transform))
            {
                AbrirPanel();
            }
        }
    }

    public void AbrirPanel()
    {
        if (panelSCORBOT != null)
            panelSCORBOT.SetActive(true);
    }

    public void CerrarPanel()
    {
        if (panelSCORBOT != null)
            panelSCORBOT.SetActive(false);
    }
}

