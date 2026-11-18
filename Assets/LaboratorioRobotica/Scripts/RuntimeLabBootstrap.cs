using UnityEngine;

namespace LaboratorioRobotica
{
    /// <summary>
    /// Garantiza que al presionar Play (▶) estén presentes tanto la banda transportadora
    /// como el Panel UI (Canvas) y el EventSystem.
    /// </summary>
    public static class RuntimeLabBootstrap
    {
        //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSimulationInScene()
        {
            ConveyorSimulationManager existing = Object.FindObjectOfType<ConveyorSimulationManager>();
            if (existing == null)
            {
                GameObject root = new GameObject("BandaTransportadora_FMS");
                existing = root.AddComponent<ConveyorSimulationManager>();
                existing.RebuildEntireLaboratoryScene();
            }

            ConveyorUIPanel uiPanel = Object.FindObjectOfType<ConveyorUIPanel>();
            if (uiPanel == null)
            {
                uiPanel = ConveyorUIPanel.CreateSimpleCanvasHierarchy();
            }
            uiPanel.simulationManager = existing;
            ConveyorUIPanel.EnsureEventSystemExists();

            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                mainCam = camGo.AddComponent<Camera>();
            }

            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.16f, 0.19f, 0.23f);
            mainCam.nearClipPlane = 0.03f;
            mainCam.farClipPlane = 60f;

            if (mainCam.GetComponent<CameraController>() == null)
            {
                CameraController cc = mainCam.gameObject.AddComponent<CameraController>();
                cc.ApplyCameraTransformImmediate();
            }

            Light dirLight = Object.FindObjectOfType<Light>();
            if (dirLight == null)
            {
                GameObject lightGo = new GameObject("Directional Light_Laboratorio");
                dirLight = lightGo.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.intensity = 1.15f;
                dirLight.color = new Color(1.0f, 0.98f, 0.94f);
                lightGo.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
            }
        }
    }
}
