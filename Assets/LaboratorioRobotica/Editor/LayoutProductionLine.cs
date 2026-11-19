#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LaboratorioRobotica.EditorTools
{
    /// <summary>
    /// Herramienta de Editor para organizar y distribuir con precisin milimtrica
    /// los 6 robots industriales y sus respectivos soportes/mesas alrededor de la
    /// banda transportadora, reproduciendo exactamente el plano de referencia:
    /// 
    /// - Robot 1 (Curva Izquierda, Sur): Scorbot ER-IX a 30 cm de la curva apex (Z = -2.75 m), mirando +Z.
    /// - Robot 2 (Tramo Inferior): Scorbot ER-IX a 30 cm de la banda, a 110 cm del extremo izq (Z = -0.63 m), mirando +X.
    /// - Robot 3 (Tramo Superior Izq): Scorbot V2 a 50 cm de la banda, a 140 cm de ref (Z = -0.40 m), mirando -X.
    /// - Robot 4 (Tramo Superior Medio): KUKA KR4 a 35 cm de la banda, altura 85 cm (Z = +0.35 m), mirando -X.
    /// - Robot 5 (Tramo Superior Riel Lineal): KUKA KR4 sobre mesa 150x80cm con riel 130cm a 30 cm de la banda (Z = +1.25 m), mirando -X.
    /// - Robot 6 (Curva Derecha, Norte): KUKA KR4 a 33 cm de la curva apex (Z = +2.75 m), mirando -Z.
    /// </summary>
    public static class LayoutProductionLine
    {
        private const string SceneLaboratorioPath = "Assets/LaboratorioRobotica/Scenes/LaboratorioRoboticaFMS.unity";
        private const string SceneCeldaPath = "Assets/_MainCell/CeldaManufactura.unity";

        [MenuItem("Robtica Industrial/2. Distribuir 6 Robots y Soportes (Segn Plano de Referencia)", false, 20)]
        public static void MenuAlignRobotsCurrentScene()
        {
            AlignActiveScene(saveScene: true);
        }

        public static void AlignActiveScene(bool saveScene = true)
        {
            // 1. Asegurar la presencia de la banda transportadora
            ConveyorSimulationManager manager = Object.FindObjectOfType<ConveyorSimulationManager>();
            GameObject beltGo = manager != null ? manager.gameObject : GameObject.Find("BandaTransportadora_FMS");
            if (beltGo == null)
            {
                string prefabPath = "Assets/LaboratorioRobotica/Prefabs/BandaTransportadora_FMS.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    beltGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                }
                else
                {
                    Debug.LogError("[LayoutProductionLine] No se encontr 'BandaTransportadora_FMS' en la escena ni su Prefab.");
                    return;
                }
            }

            // Asegurar posicin base de la banda en el origen (0, 0, 0)
            beltGo.transform.position = Vector3.zero;
            beltGo.transform.rotation = Quaternion.identity;

            // 2. Localizar o instanciar los 6 robots disponibles
            GameObject scorbot1 = FindOrInstantiateRobot("scorboterix (1)", "Assets/Robots_ScorbotV1/scorboterix.fbx");
            GameObject scorbot2 = FindOrInstantiateRobot("scorboterix", "Assets/Robots_ScorbotV1/scorboterix.fbx");
            GameObject scorbot3 = FindOrInstantiateRobot("scorbotv2", "Assets/Robots_ScorbotV2/Models/scorbotv2.fbx");

            GameObject kuka1 = FindOrInstantiateRobot("kukakr4_garra", "Assets/Robots_Kuka/kukakr4_garra.prefab");
            GameObject kuka2 = FindOrInstantiateRobot("kukakr4_garra (1)", "Assets/Robots_Kuka/kukakr4_garra.prefab");
            GameObject kuka3 = FindOrInstantiateRobot("kukakr4_garra (2)", "Assets/Robots_Kuka/kukakr4_garra.prefab");

            // 3. Crear/organizar el contenedor de soportes y estaciones
            Transform stationsRoot = beltGo.transform.Find("TresEstaciones_Y_SensoresMagneticos");
            if (stationsRoot == null)
            {
                GameObject rootGo = new GameObject("TresEstaciones_Y_SensoresMagneticos");
                rootGo.transform.SetParent(beltGo.transform, false);
                stationsRoot = rootGo.transform;
            }

            // =========================================================================================
            // MEDIDAS DERIVADAS DEL PLANO DE REFERENCIA (Exactitud geomtrica en metros)
            // =========================================================================================
            // Eje central de la banda: X = 0
            // Carril superior (+X): centro en +0.606 m, borde exterior en +0.693 m
            // Carril inferior (-X): centro en -0.606 m, borde exterior en -0.693 m
            // Curva izquierda (-Z): centro en Z = -1.730 m, pice exterior en Z = -2.423 m
            // Curva derecha (+Z): centro en Z = +1.730 m, pice exterior en Z = +2.423 m
            // Altura de superficie de trabajo: 0.85 m (altura 85 cm segn el plano)
            // Altura de la cinta transportadora: 0.96 m

            // -----------------------------------------------------------------------------------------
            // ROBOT 1: SCORBOT - CURVA IZQUIERDA (SUR)
            // Plano: scortbot, 30 cm de separacin a la curva, centrado en el eje
            // -----------------------------------------------------------------------------------------
            Vector3 posRobot1 = new Vector3(0.000f, 0.850f, -2.750f); // -2.423 - 0.30 - offset
            Quaternion rotRobot1 = Quaternion.Euler(0f, 0f, 0f); // Mira hacia la banda (+Z)
            ApplyTransform(scorbot1, posRobot1, rotRobot1, "Robot_1_Scorbot_CurvaIzquierda");

            // -----------------------------------------------------------------------------------------
            // ROBOT 2: SCORBOT - TRAMO INFERIOR
            // Plano: scortbot, 30 cm de la banda, a 110 cm del extremo/tangente izquierda
            // Tangente izq: -1.730 m => Z = -1.730 + 1.100 = -0.630 m
            // Borde inferior: -0.693 m => Base robot: -0.693 - 0.30 - 0.15 = -1.143 m (~ -1.15 m)
            // -----------------------------------------------------------------------------------------
            Vector3 posRobot2 = new Vector3(-1.150f, 0.850f, -0.630f);
            Quaternion rotRobot2 = Quaternion.Euler(0f, 90f, 0f); // Mira hacia la banda (+X)
            ApplyTransform(scorbot2, posRobot2, rotRobot2, "Robot_2_Scorbot_Inferior");

            // -----------------------------------------------------------------------------------------
            // ROBOT 3: SCORBOT - TRAMO SUPERIOR IZQUIERDO
            // Plano: scortbot, 50 cm de la banda, a 140 cm de la referencia izquierda
            // Borde superior: +0.693 m => Base robot: +0.693 + 0.50 + 0.06 = +1.250 m
            // Posicin longitudinal: Z = -0.400 m
            // -----------------------------------------------------------------------------------------
            Vector3 posRobot3 = new Vector3(1.250f, 0.850f, -0.400f);
            Quaternion rotRobot3 = Quaternion.Euler(0f, -90f, 0f); // Mira hacia la banda (-X)
            ApplyTransform(scorbot3, posRobot3, rotRobot3, "Robot_3_Scorbot_SuperiorIzquierda");

            // -----------------------------------------------------------------------------------------
            // ROBOT 4: KUKA KR4 - TRAMO SUPERIOR MEDIO
            // Plano: KUKA, altura 85 cm, 35 cm de la banda, separacin de 40 cm a la mesa del riel
            // Borde superior: +0.693 m => Base robot: +0.693 + 0.35 + 0.08 = +1.120 m
            // Posicin longitudinal: Z = +0.350 m
            // -----------------------------------------------------------------------------------------
            Vector3 posRobot4 = new Vector3(1.120f, 0.850f, 0.350f);
            Quaternion rotRobot4 = Quaternion.Euler(-90f, -90f, 0f); // Base KUKA orientada hacia la banda (-X)
            ApplyTransform(kuka1, posRobot4, rotRobot4, "Robot_4_Kuka_SuperiorMedio");

            // -----------------------------------------------------------------------------------------
            // ROBOT 5: KUKA KR4 - TRAMO SUPERIOR DERECHO (SOBRE RIEL LINEAL)
            // Plano: KUKA, mesa 150 cm x 80 cm, riel 130 cm con 10 cm margen, altura 85 cm, a 30 cm de la banda
            // Borde superior: +0.693 m => Base robot: +0.693 + 0.30 + 0.12 = +1.120 m
            // Posicin longitudinal: Z = +1.250 m (centro de la mesa de 1.50 m: +0.50 m a +2.00 m)
            // -----------------------------------------------------------------------------------------
            Vector3 posRobot5 = new Vector3(1.120f, 0.850f, 1.250f);
            Quaternion rotRobot5 = Quaternion.Euler(-90f, -90f, 0f); // Base KUKA orientada hacia la banda (-X)
            ApplyTransform(kuka2, posRobot5, rotRobot5, "Robot_5_Kuka_RielLineal");

            // -----------------------------------------------------------------------------------------
            // ROBOT 6: KUKA KR4 - CURVA DERECHA (NORTE)
            // Plano: KUKA, 33 cm de separacin al pice de la curva, centrado en el eje
            // pice derecho: +2.423 m => Base robot: +2.423 + 0.33 = +2.753 m
            // -----------------------------------------------------------------------------------------
            Vector3 posRobot6 = new Vector3(0.000f, 0.850f, 2.753f);
            Quaternion rotRobot6 = Quaternion.Euler(-90f, 180f, 0f); // Base KUKA mirando hacia la banda (-Z)
            ApplyTransform(kuka3, posRobot6, rotRobot6, "Robot_6_Kuka_CurvaDerecha");

            // 4. Ubicar y duplicar soportes (mesas, pedestales, riel lineal) bajo cada robot
            OrganizeSupportStructures(beltGo, posRobot1, posRobot2, posRobot3, posRobot4, posRobot5, posRobot6);

            // 5. Ajustar cmara y guardar
            SetupCameraView();

            if (saveScene && !Application.isPlaying)
            {
                Scene activeScene = SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[Robtica Industrial] ? Distribucin de 6 robots completada con xito en la escena '{activeScene.path}'.");
            }
        }

        private static GameObject FindOrInstantiateRobot(string preferredName, string fallbackAssetPath)
        {
            // 1. Buscar en la escena por nombre exacto
            GameObject go = GameObject.Find(preferredName);
            if (go != null) return go;

            // 2. Buscar por coincidencia parcial
            GameObject[] all = Object.FindObjectsOfType<GameObject>();
            foreach (var item in all)
            {
                if (item.name.Equals(preferredName, System.StringComparison.OrdinalIgnoreCase))
                    return item;
            }

            // 3. Cargar desde Prefab o Modelo FBX
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(fallbackAssetPath);
            if (asset != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                instance.name = preferredName;
                return instance;
            }

            Debug.LogWarning($"[LayoutProductionLine] No se encontr el robot '{preferredName}' ni el archivo '{fallbackAssetPath}'.");
            return null;
        }

        private static void ApplyTransform(GameObject go, Vector3 worldPos, Quaternion worldRot, string finalName)
        {
            if (go == null) return;
            Undo.RecordObject(go.transform, "Posicionar Robot");
            go.transform.position = worldPos;
            go.transform.rotation = worldRot;
            go.name = finalName;
        }

        private static void OrganizeSupportStructures(
            GameObject beltGo,
            Vector3 posR1, Vector3 posR2, Vector3 posR3, Vector3 posR4, Vector3 posR5, Vector3 posR6)
        {
            Material pedestalGrey = ConveyorDimensions.GetOrCreateMaterial("PedestalGrey", new Color(0.25f, 0.28f, 0.31f), 0.5f, 0.5f);
            Material tableWhiteMat = ConveyorDimensions.GetOrCreateMaterial("TableWhiteGloss", new Color(0.92f, 0.92f, 0.90f), 0.1f, 0.7f);

            GameObject supportsGroup = GameObject.Find("Soportes_6_Robots");
            if (supportsGroup == null)
            {
                supportsGroup = new GameObject("Soportes_6_Robots");
                supportsGroup.transform.SetParent(beltGo.transform, false);
            }

            // 1. Soporte Robot 1 (Pedestal Curva Izquierda en Z = -2.75 m)
            EnsurePedestalSupport(supportsGroup.transform, "Soporte_Robot1_CurvaIzq", new Vector3(posR1.x, 0f, posR1.z), pedestalGrey);

            // 2. Soporte Robot 2 (Mesa de trabajo inferior en Z = -0.63 m)
            GameObject mesa2 = GameObject.Find("MesaTrabajo_ConMatricesPerforadas");
            if (mesa2 != null)
            {
                mesa2.transform.position = new Vector3(posR2.x - 0.10f, 0f, posR2.z);
                mesa2.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else
            {
                EnsureTableSupport(supportsGroup.transform, "MesaTrabajo_Robot2", new Vector3(posR2.x - 0.10f, 0f, posR2.z), new Vector3(0.65f, 0.85f, 0.95f), tableWhiteMat, pedestalGrey);
            }

            // 3. Soporte Robot 3 (Mesa superior izquierda en Z = -0.40 m)
            EnsureTableSupport(supportsGroup.transform, "MesaSoporte_Robot3", new Vector3(posR3.x + 0.10f, 0f, posR3.z), new Vector3(0.65f, 0.85f, 0.95f), tableWhiteMat, pedestalGrey);

            // 4. Soporte Robot 4 (Mesa KUKA superior medio en Z = +0.35 m, altura 85 cm)
            EnsureTableSupport(supportsGroup.transform, "MesaKuka_Robot4", new Vector3(posR4.x + 0.10f, 0f, posR4.z), new Vector3(0.65f, 0.85f, 0.95f), tableWhiteMat, pedestalGrey);

            // 5. Soporte Robot 5 (Mesa de aluminio 150x80cm con Riel Lineal 130cm en Z = +1.25 m)
            GameObject mesaAlu = GameObject.Find("MesaAluminio_Almacen_ASRS");
            if (mesaAlu != null)
            {
                mesaAlu.transform.position = new Vector3(posR5.x + 0.08f, 0f, posR5.z);
                mesaAlu.transform.rotation = Quaternion.identity;
            }
            else
            {
                EnsureTableSupport(supportsGroup.transform, "MesaRiel_Robot5", new Vector3(posR5.x + 0.08f, 0f, posR5.z), new Vector3(0.80f, 0.85f, 1.50f), tableWhiteMat, pedestalGrey);
            }

            // 6. Soporte Robot 6 (Pedestal Curva Derecha en Z = +2.753 m)
            EnsurePedestalSupport(supportsGroup.transform, "Soporte_Robot6_CurvaDer", new Vector3(posR6.x, 0f, posR6.z), pedestalGrey);
        }

        private static void EnsurePedestalSupport(Transform parent, string name, Vector3 pos, Material mat)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.position = pos;
                return;
            }

            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            // Placa inferior piso
            CreateBox(root.transform, "PlacaPiso", new Vector3(0f, 0.01f, 0f), new Vector3(0.38f, 0.02f, 0.38f), mat);
            // Columna cilndrica (altura 85 cm)
            CreateCylinder(root.transform, "ColumnaCilindrica", new Vector3(0f, 0.425f, 0f), new Vector3(0.24f, 0.425f, 0.24f), mat);
            // Placa superior de montaje
            CreateBox(root.transform, "PlacaSuperior", new Vector3(0f, 0.85f, 0f), new Vector3(0.38f, 0.02f, 0.38f), mat);
        }

        private static void EnsureTableSupport(Transform parent, string name, Vector3 pos, Vector3 size, Material tableMat, Material legMat)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.position = pos;
                return;
            }

            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            float topThick = 0.03f;
            float legHeight = size.y - topThick;
            float legThick = 0.04f;

            // Superficie / tablero
            CreateBox(root.transform, "TableroMesa", new Vector3(0f, size.y - topThick * 0.5f, 0f), new Vector3(size.x, topThick, size.z), tableMat);

            // 4 patas
            float hx = (size.x - legThick) * 0.5f;
            float hz = (size.z - legThick) * 0.5f;
            float legY = legHeight * 0.5f;

            CreateBox(root.transform, "Pata1", new Vector3(-hx, legY, -hz), new Vector3(legThick, legHeight, legThick), legMat);
            CreateBox(root.transform, "Pata2", new Vector3( hx, legY, -hz), new Vector3(legThick, legHeight, legThick), legMat);
            CreateBox(root.transform, "Pata3", new Vector3(-hx, legY,  hz), new Vector3(legThick, legHeight, legThick), legMat);
            CreateBox(root.transform, "Pata4", new Vector3( hx, legY,  hz), new Vector3(legThick, legHeight, legThick), legMat);
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            return go;
        }

        private static GameObject CreateCylinder(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            return go;
        }

        private static void SetupCameraView()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(3.6f, 3.8f, 0.0f);
                cam.transform.rotation = Quaternion.Euler(38f, -90f, 0f);
            }
        }
    }
}
#endif
