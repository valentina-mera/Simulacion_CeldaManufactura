using UnityEngine;
using UnityEngine.InputSystem;

public class PanelToggleController : MonoBehaviour
{
    public GameObject uiPanel;
    public Camera targetCamera; // Espacio para arrastrar la cámara directamente

    private void Start()
    {
        // Si no se asignó manualmente, intenta buscar la cámara principal
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (uiPanel != null)
        {
            uiPanel.SetActive(false); // Ocultar el panel al iniciar la escena
        }
    }

    private void Update()
    {
        if (targetCamera == null) return;

        // Comprueba si el ratón existe y se presionó el botón izquierdo este fotograma
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = targetCamera.ScreenPointToRay(mousePosition);
            RaycastHit hit;

            // Lanza un rayo desde la cámara hacia el cursor para detectar colisiones 3D
            if (Physics.Raycast(ray, out hit))
            {
                PanelToggleController robotController = hit.collider.GetComponentInParent<PanelToggleController>();
                if (robotController == this)
                {
                    TogglePanel();
                }
            }
        }
    }

    public void TogglePanel()
    {
        if (uiPanel != null)
        {
            bool isActive = uiPanel.activeSelf;
            uiPanel.SetActive(!isActive);
        }
    }
}