#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LaboratorioRobotica.EditorTools
{
    /// <summary>
    /// Script de Editor que:
    /// 1. Agrega el menú superior "Robótica Industrial" en Unity para crear/reconstruir el laboratorio con 1 clic.
    /// 2. Al abrir el proyecto por primera vez en Unity, construye automáticamente los modelos 3D en la escena
    ///    y enfoca la cámara del Editor para que todo se vea de inmediato sin pasos manuales complicados.
    /// </summary>
    [InitializeOnLoad]
    public static class LabSceneAutoBuilder
    {
        private const string FirstLoadSessionKey = "LabRobotica_AutoBuiltOnce";

        static LabSceneAutoBuilder()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            ConveyorSimulationManager manager = Object.FindObjectOfType<ConveyorSimulationManager>();
            bool hasOldRobots = manager != null && manager.transform.Find("EstacionesRobots_Y_SensoresMagneticos") != null;

            if (hasOldRobots || (manager == null && !SessionState.GetBool(FirstLoadSessionKey, false)))
            {
                SessionState.SetBool(FirstLoadSessionKey, true);
                BuildOrRefreshLaboratoryInCurrentScene(saveScene: true);
            }
        }

        [MenuItem("Robótica Industrial/1. Generar Laboratorio Completo en Escena (Escala 1:1)", false, 10)]
        public static void MenuGenerateLab()
        {
            BuildOrRefreshLaboratoryInCurrentScene(saveScene: true);
        }

        [MenuItem("Robótica Industrial/2. Simular Bandeja #1 VOLTEADA (Error 180°)", false, 30)]
        public static void MenuFlipTray1()
        {
            ConveyorSimulationManager mgr = Object.FindObjectOfType<ConveyorSimulationManager>();
            if (mgr != null && mgr.trays.Count > 0)
            {
                mgr.trays[0].orientationState = TrayOrientationState.VolteadaBocaAbajo;
                mgr.trays[0].RefreshVisualState();
                SceneView.RepaintAll();
            }
        }

        [MenuItem("Robótica Industrial/3. Simular Bandeja #1 SIN PIEZA (Advertencia)", false, 31)]
        public static void MenuEmptyTray1()
        {
            ConveyorSimulationManager mgr = Object.FindObjectOfType<ConveyorSimulationManager>();
            if (mgr != null && mgr.trays.Count > 0)
            {
                mgr.trays[0].orientationState = TrayOrientationState.Correcta;
                mgr.trays[0].loadState = TrayLoadState.SinPieza;
                mgr.trays[0].RefreshVisualState();
                SceneView.RepaintAll();
            }
        }

        [MenuItem("Robótica Industrial/4. Restaurar Todas las Bandejas Normales", false, 32)]
        public static void MenuResetAllTrays()
        {
            ConveyorSimulationManager mgr = Object.FindObjectOfType<ConveyorSimulationManager>();
            if (mgr != null)
            {
                foreach (var t in mgr.trays)
                {
                    if (t == null) continue;
                    t.orientationState = TrayOrientationState.Correcta;
                    t.loadState = TrayLoadState.ConPieza;
                    t.RefreshVisualState();
                }
                SceneView.RepaintAll();
            }
        }

        public static void BuildOrRefreshLaboratoryInCurrentScene(bool saveScene)
        {
            // Configurar cámara principal
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

            CameraController camCtrl = mainCam.GetComponent<CameraController>();
            if (camCtrl == null) camCtrl = mainCam.gameObject.AddComponent<CameraController>();
            camCtrl.targetPivot = new Vector3(0f, 0.90f, 0f);
            camCtrl.distance = 4.6f;
            camCtrl.yaw = -128f;
            camCtrl.pitch = 30f;
            camCtrl.ApplyCameraTransformImmediate();

            // Configurar iluminación del laboratorio
            Light dirLight = Object.FindObjectOfType<Light>();
            if (dirLight == null)
            {
                GameObject lightGo = new GameObject("Directional Light_Laboratorio");
                dirLight = lightGo.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }
            dirLight.intensity = 1.15f;
            dirLight.color = new Color(1.0f, 0.98f, 0.94f);
            dirLight.shadows = LightShadows.Soft;
            dirLight.transform.rotation = Quaternion.Euler(48f, -38f, 0f);

            // Crear o reconstruir el gestor de simulación del laboratorio
            ConveyorSimulationManager manager = Object.FindObjectOfType<ConveyorSimulationManager>();
            if (manager == null)
            {
                GameObject root = new GameObject("Simulacion_Laboratorio_Robotica_FMS");
                manager = root.AddComponent<ConveyorSimulationManager>();
            }

            manager.RebuildEntireLaboratoryScene();

            // Enfocar la vista de Escena (SceneView) hacia la banda transportadora
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.LookAt(
                    new Vector3(0f, 0.96f, 0f),
                    Quaternion.Euler(30f, -128f, 0f),
                    4.8f);
                SceneView.lastActiveSceneView.Repaint();
            }

            if (saveScene && !Application.isPlaying)
            {
                string scenesDir = "Assets/LaboratorioRobotica/Scenes";
                if (!Directory.Exists(scenesDir))
                {
                    Directory.CreateDirectory(scenesDir);
                }
                string scenePath = $"{scenesDir}/LaboratorioRoboticaFMS.unity";
                Scene activeScene = SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene, scenePath);
                AssetDatabase.Refresh();
            }

            Selection.activeGameObject = manager.gameObject;
            Debug.Log("[Laboratorio Robótica Industrial] Escena 3D generada a escala 1:1 (484.5 cm x 138.5 cm, altura 96 cm). Presiona PLAY (▶) para iniciar la simulación interactiva.");
        }
    }
}
#endif

