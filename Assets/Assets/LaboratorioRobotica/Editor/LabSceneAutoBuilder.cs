#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace LaboratorioRobotica.EditorTools
{
    /// <summary>
    /// Script de Editor que construye y guarda automáticamente como Prefabs:
    /// 1. BandaTransportadora_FMS.prefab (únicamente la banda, estaciones/sensores y bandejas, sin piso).
    /// 2. Canvas_PanelControlBanda.prefab (Panel hecho en Unity UI / Canvas sencillo con botón Simular Sensor).
    /// </summary>
    [InitializeOnLoad]
    public static class LabSceneAutoBuilder
    {
        public const string PrefabsDir = "Assets/LaboratorioRobotica/Prefabs";
        public const string ConveyorPrefabPath = "Assets/LaboratorioRobotica/Prefabs/BandaTransportadora_FMS.prefab";
        public const string UIPanelPrefabPath = "Assets/LaboratorioRobotica/Prefabs/Canvas_PanelControlBanda.prefab";

        static LabSceneAutoBuilder()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            ConveyorSimulationManager manager = Object.FindObjectOfType<ConveyorSimulationManager>();
            ConveyorUIPanel uiPanel = Object.FindObjectOfType<ConveyorUIPanel>();
            bool hasOldFloor = manager != null && manager.transform.Find("EntornoLaboratorio") != null;
            bool missingPrefabs = !File.Exists(ConveyorPrefabPath) || !File.Exists(UIPanelPrefabPath);
            bool outdatedUI = uiPanel != null && uiPanel.builtLayoutVersion != ConveyorUIPanel.LayoutVersion;

            if (manager == null || uiPanel == null || hasOldFloor || missingPrefabs || outdatedUI)
            {
                BuildWhenTextMeshProReady();
            }
        }

        [MenuItem("Robótica Industrial/1. Generar Prefabs (Banda Sin Piso + Panel UI) y Escena", false, 10)]
        public static void MenuGenerateLab()
        {
            BuildWhenTextMeshProReady();
        }

        // =========================================================================================
        // TEXTMESHPRO (Unity 6: incluido en com.unity.ugui 2.0) - Importa la fuente SDF si falta
        // =========================================================================================
        private static bool TextMeshProEssentialsPresent()
        {
            return Resources.Load<TMPro.TMP_Settings>("TMP Settings") != null;
        }

        /// <summary>
        /// Si los recursos esenciales de TextMeshPro (fuente LiberationSans SDF y TMP Settings) no están
        /// importados, los importa automáticamente desde el paquete uGUI y luego construye los prefabs.
        /// </summary>
        private static void BuildWhenTextMeshProReady()
        {
            if (TextMeshProEssentialsPresent())
            {
                BuildOrRefreshLaboratoryInCurrentScene(saveScene: true);
                return;
            }

            string packageRoot = Path.GetFullPath("Packages/com.unity.ugui");
            string essentialsPkg = Path.Combine(packageRoot, "Package Resources", "TMP Essential Resources.unitypackage");
            if (!File.Exists(essentialsPkg))
            {
                Debug.LogWarning("[Robótica Industrial] No se encontró 'TMP Essential Resources'. Impórtalo desde " +
                                 "Window > TextMeshPro > Import TMP Essential Resources y vuelve a usar el menú Robótica Industrial > 1.");
                BuildOrRefreshLaboratoryInCurrentScene(saveScene: true);
                return;
            }

            AssetDatabase.importPackageCompleted += OnTmpEssentialsImported;
            AssetDatabase.importPackageFailed += OnTmpEssentialsFailed;
            AssetDatabase.ImportPackage(essentialsPkg, false);
        }

        private static void OnTmpEssentialsImported(string packageName)
        {
            if (!packageName.Contains("TMP Essential")) return;
            UnsubscribeImportCallbacks();
            AssetDatabase.Refresh();
            EditorApplication.delayCall += () => BuildOrRefreshLaboratoryInCurrentScene(saveScene: true);
        }

        private static void OnTmpEssentialsFailed(string packageName, string error)
        {
            if (!packageName.Contains("TMP Essential")) return;
            UnsubscribeImportCallbacks();
            Debug.LogError($"[Robótica Industrial] Falló la importación de TMP Essential Resources: {error}");
        }

        private static void UnsubscribeImportCallbacks()
        {
            AssetDatabase.importPackageCompleted -= OnTmpEssentialsImported;
            AssetDatabase.importPackageFailed -= OnTmpEssentialsFailed;
        }

        [MenuItem("Robótica Industrial/2. Simular Sensor en Escena", false, 30)]
        public static void MenuSimulateSensor()
        {
            ConveyorSimulationManager mgr = Object.FindObjectOfType<ConveyorSimulationManager>();
            if (mgr != null)
            {
                mgr.SimulateSensorFromUIButton();
                SceneView.RepaintAll();
            }
        }

        public static void BuildOrRefreshLaboratoryInCurrentScene(bool saveScene)
        {
            if (!Directory.Exists(PrefabsDir))
            {
                Directory.CreateDirectory(PrefabsDir);
            }

            // 1. Configurar cámara principal
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

            // 2. Configurar iluminación
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

            // 3. Construir la Banda y Objetos Esenciales (SIN PISO) y guardarla como Prefab
            ConveyorSimulationManager existingManager = Object.FindObjectOfType<ConveyorSimulationManager>();
            if (existingManager != null)
            {
                Object.DestroyImmediate(existingManager.gameObject);
            }

            GameObject conveyorRoot = new GameObject("BandaTransportadora_FMS");
            ConveyorSimulationManager manager = conveyorRoot.AddComponent<ConveyorSimulationManager>();
            manager.RebuildEntireLaboratoryScene();

            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAssetAndConnect(conveyorRoot, ConveyorPrefabPath, InteractionMode.AutomatedAction);

            // 4. Construir el Panel UI en Canvas (UnityEngine.UI) y guardarlo como Prefab
            ConveyorUIPanel existingUI = Object.FindObjectOfType<ConveyorUIPanel>();
            if (existingUI != null)
            {
                Object.DestroyImmediate(existingUI.gameObject);
            }

            Sprite roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            ConveyorUIPanel uiPanel = ConveyorUIPanel.CreateSimpleCanvasHierarchy(roundedSprite, knobSprite);
            uiPanel.simulationManager = manager;
            if (uiPanel.toggleBeltButton != null)
                UnityEditor.Events.UnityEventTools.AddPersistentListener(uiPanel.toggleBeltButton.onClick, uiPanel.OnToggleBeltClicked);
            if (uiPanel.simulateSensorButton != null)
                UnityEditor.Events.UnityEventTools.AddPersistentListener(uiPanel.simulateSensorButton.onClick, uiPanel.OnSimulateSensorClicked);
            if (uiPanel.speedSlider != null)
                UnityEditor.Events.UnityEventTools.AddPersistentListener(uiPanel.speedSlider.onValueChanged, uiPanel.OnSpeedSliderChanged);

            PrefabUtility.SaveAsPrefabAssetAndConnect(uiPanel.gameObject, UIPanelPrefabPath, InteractionMode.AutomatedAction);

            // Registrar la referencia de la escena en la instancia del Prefab UI
            SerializedObject so = new SerializedObject(uiPanel);
            SerializedProperty mgrProp = so.FindProperty("simulationManager");
            if (mgrProp != null)
            {
                mgrProp.objectReferenceValue = manager;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 5. Asegurar EventSystem en la escena para interacción con botones/slider UI
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            // Enfocar la vista de Escena hacia la banda transportadora
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
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Selection.activeGameObject = uiPanel.gameObject;
            Debug.Log("[Robótica Industrial] Prefabs creados en Assets/LaboratorioRobotica/Prefabs: 'BandaTransportadora_FMS.prefab' (sin piso) y 'Canvas_PanelControlBanda.prefab' (Unity UI).");
        }
    }
}
#endif
