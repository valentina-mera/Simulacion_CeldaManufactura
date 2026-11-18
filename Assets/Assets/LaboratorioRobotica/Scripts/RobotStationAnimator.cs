using UnityEngine;

namespace LaboratorioRobotica
{
    public enum RobotModelType
    {
        KukaKR4R600_PLCInventory = 0,
        KukaKR4R600_PedestalManufacturing = 1,
        KukaKR4R600_LinearRailStorage = 2,
        ScorbotERVPlus_AssemblyCell = 3,
        ScorbotERVPlus_CurveStation = 4,
        MotomanBlue_Reference = 5
    }

    /// <summary>
    /// Construye ÚNICAMENTE las bases, pedestales, mesas de trabajo, gabinete del PLC, riel lineal y estantería
    /// donde van ubicados los robots alrededor de la cinta transportadora (sin incluir los brazos robóticos),
    /// dejando marcado el punto exacto de montaje en cada estación de la banda.
    /// </summary>
    public class RobotStationAnimator : MonoBehaviour
    {
        public string stationName = "Estación en Banda";
        public string stationRole = "Manufactura";
        public RobotModelType modelType = RobotModelType.KukaKR4R600_PedestalManufacturing;

        public bool isOperationalInSimulation = true;

        // Punto de montaje y luz/indicador de estación activa mientras la banda se detiene en el sensor
        [SerializeField] private Transform _mountingPoint;
        [SerializeField] private Renderer _mountingPadRenderer;
        private bool _isWorking = false;
        private float _workNormalizedTime = 0f;

        public void BuildRobotModel(RobotModelType type, string name, string role, Vector3 beltStopPoint, Vector3 outwardDir)
        {
            modelType = type;
            stationName = name;
            stationRole = role;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            Vector3 toBelt = -outwardDir;
            toBelt.y = 0f;
            if (toBelt.sqrMagnitude < 0.001f) toBelt = Vector3.left;

            switch (type)
            {
                case RobotModelType.KukaKR4R600_PLCInventory:
                    BuildPLCInventoryBaseAndCabinet(toBelt);
                    break;

                case RobotModelType.KukaKR4R600_PedestalManufacturing:
                    BuildPedestalAndTableStation(toBelt);
                    break;

                case RobotModelType.KukaKR4R600_LinearRailStorage:
                    BuildLinearRailAndStorageTableStation(toBelt);
                    break;

                case RobotModelType.ScorbotERVPlus_AssemblyCell:
                    BuildScorbotPedestalAndTables(toBelt, includeWoodenWorkTables: true);
                    break;

                case RobotModelType.ScorbotERVPlus_CurveStation:
                    BuildScorbotPedestalAndTables(toBelt, includeWoodenWorkTables: false);
                    break;

                case RobotModelType.MotomanBlue_Reference:
                    BuildReferencePedestalOnly();
                    break;
            }

            UpdateStationPadColor();
        }

        public void SetWorkingState(bool working, float normalizedProgress)
        {
            _isWorking = working;
            _workNormalizedTime = Mathf.Clamp01(normalizedProgress);
            UpdateStationPadColor();
        }

        private void UpdateStationPadColor()
        {
            if (_mountingPadRenderer == null) return;

            if (_isWorking)
            {
                _mountingPadRenderer.sharedMaterial = ConveyorDimensions.GetOrCreateMaterial(
                    "MountPadActiveCyan", new Color(0.10f, 0.85f, 0.95f), 0.3f, 0.8f, true, new Color(0.1f, 0.75f, 0.95f) * 1.6f);
            }
            else
            {
                _mountingPadRenderer.sharedMaterial = ConveyorDimensions.GetOrCreateMaterial(
                    "MountPadIdle", new Color(0.38f, 0.41f, 0.45f), 0.6f, 0.6f);
            }
        }

        // =========================================================================================
        // CONSTRUCTORES 3D DE LAS BASES, PEDESTALES Y MESAS DE CADA ESTACIÓN (SIN ROBOTS)
        // =========================================================================================

        /// <summary>
        /// Estación 1: Caja del PLC beige + Pedestal/Base de ubicación del puesto de Inventario + Pila de bloques.
        /// </summary>
        private void BuildPLCInventoryBaseAndCabinet(Vector3 lookToBelt)
        {
            Material plcBeige = ConveyorDimensions.GetOrCreateMaterial("PLCBeige", new Color(0.84f, 0.83f, 0.76f), 0.15f, 0.45f);
            Material darkGrey = ConveyorDimensions.GetOrCreateMaterial("PedestalGrey", new Color(0.24f, 0.26f, 0.29f), 0.5f, 0.5f);
            Material woodPlank = ConveyorDimensions.GetOrCreateMaterial("WoodPlank", new Color(0.76f, 0.54f, 0.35f), 0.05f, 0.4f);
            Material cubeStackMat = ConveyorDimensions.GetOrCreateMaterial("CubeStackMat", new Color(0.85f, 0.87f, 0.82f), 0.1f, 0.4f);

            // 1. Gabinete / Caja del PLC Beige junto a la banda
            GameObject plcCabinet = new GameObject("Caja_PLC_GabineteControl");
            plcCabinet.transform.SetParent(transform, false);
            plcCabinet.transform.localPosition = new Vector3(0f, 0f, -0.38f);

            CreateBox(plcCabinet.transform, "GabinetePLC_Cuerpo", new Vector3(0f, 0.46f, 0f), new Vector3(0.42f, 0.92f, 0.58f), plcBeige);
            CreateBox(plcCabinet.transform, "PuertaPLC_Panel", new Vector3(0.212f, 0.48f, 0f), new Vector3(0.01f, 0.78f, 0.48f), darkGrey);
            CreateBox(plcCabinet.transform, "ListonMaderaSuperior", new Vector3(-0.08f, 0.935f, 0.15f), new Vector3(0.09f, 0.025f, 0.65f), woodPlank);

            // 2. Base / Pedestal de soporte del puesto de Inventario
            CreateBox(transform, "BaseSoporte_Estacion1_Inventario", new Vector3(0f, 0.47f, 0f), new Vector3(0.36f, 0.94f, 0.36f), darkGrey);
            CreateBox(transform, "PlacaSuperiorBase_Estacion1", new Vector3(0f, 0.95f, 0f), new Vector3(0.42f, 0.02f, 0.42f), darkGrey);

            // 3. Pila de piezas de inventario sobre la mesa/base
            GameObject inventoryPile = new GameObject("Inventario_PilaDePiezas");
            inventoryPile.transform.SetParent(transform, false);
            inventoryPile.transform.localPosition = new Vector3(-0.12f, 0.96f, 0.25f);
            for (int layer = 0; layer < 3; layer++)
            {
                for (int ix = 0; ix < 2; ix++)
                {
                    for (int iz = 0; iz < 2; iz++)
                    {
                        CreateBox(inventoryPile.transform, $"CuboInventario_{layer}_{ix}_{iz}",
                            new Vector3((ix - 0.5f) * 0.048f, 0.018f + layer * 0.036f, (iz - 0.5f) * 0.048f),
                            new Vector3(0.044f, 0.034f, 0.044f), cubeStackMat);
                    }
                }
            }

            // Brida / Punto de montaje donde se ubica el robot de la Estación 1
            CreateMountingFlange(transform, new Vector3(0f, 0.965f, 0f), lookToBelt, "PuntoUbicacion_Estacion1_Inventario");
        }

        /// <summary>
        /// Estación 2: Pedestal cilíndrico gris + Mesa auxiliar de madera (sin el brazo del robot).
        /// </summary>
        private void BuildPedestalAndTableStation(Vector3 lookToBelt)
        {
            Material pedestalGrey = ConveyorDimensions.GetOrCreateMaterial("PedestalGrey", new Color(0.25f, 0.28f, 0.31f), 0.55f, 0.5f);
            Material woodTableMat = ConveyorDimensions.GetOrCreateMaterial("SmartPadTableWood", new Color(0.78f, 0.52f, 0.28f), 0.1f, 0.45f);

            // Pedestal cilíndrico gris (altura ~94 cm)
            CreateCylinder(transform, "ColumnaPedestalCilindrica", new Vector3(0f, 0.47f, 0f), new Vector3(0.22f, 0.47f, 0.22f), pedestalGrey);
            CreateBox(transform, "PlacaInferiorPiso", new Vector3(0f, 0.01f, 0f), new Vector3(0.42f, 0.02f, 0.42f), pedestalGrey);
            CreateBox(transform, "PlacaSuperiorPedestal", new Vector3(0f, 0.95f, 0f), new Vector3(0.40f, 0.02f, 0.40f), pedestalGrey);

            // Mesa auxiliar de madera al lado del pedestal
            GameObject table = new GameObject("MesaAuxiliar_Estacion2");
            table.transform.SetParent(transform, false);
            table.transform.localPosition = new Vector3(0.28f, 0f, -0.46f);

            CreateBox(table.transform, "TableroMadera", new Vector3(0f, 0.74f, 0f), new Vector3(0.60f, 0.03f, 0.55f), woodTableMat);
            CreateCylinder(table.transform, "Pata1", new Vector3(-0.24f, 0.36f, -0.22f), new Vector3(0.045f, 0.36f, 0.045f), pedestalGrey);
            CreateCylinder(table.transform, "Pata2", new Vector3( 0.24f, 0.36f, -0.22f), new Vector3(0.045f, 0.36f, 0.045f), pedestalGrey);
            CreateCylinder(table.transform, "Pata3", new Vector3(-0.24f, 0.36f,  0.22f), new Vector3(0.045f, 0.36f, 0.045f), pedestalGrey);
            CreateCylinder(table.transform, "Pata4", new Vector3( 0.24f, 0.36f,  0.22f), new Vector3(0.045f, 0.36f, 0.045f), pedestalGrey);

            // Brida / Punto de montaje sobre el pedestal
            CreateMountingFlange(transform, new Vector3(0f, 0.965f, 0f), lookToBelt, "PuntoUbicacion_Estacion2_Manufactura");
        }

        /// <summary>
        /// Estación 3: Mesa de perfiles de aluminio + Riel lineal + Estantería vertical de Almacén (sin el brazo del robot).
        /// </summary>
        private void BuildLinearRailAndStorageTableStation(Vector3 lookToBelt)
        {
            Material aluMat = ConveyorDimensions.GetOrCreateMaterial("AluminumExtrusion", new Color(0.78f, 0.80f, 0.83f), 0.75f, 0.7f);
            Material darkMat = ConveyorDimensions.GetOrCreateMaterial("RailDark", new Color(0.14f, 0.15f, 0.17f), 0.5f, 0.5f);
            Material yellowCaution = ConveyorDimensions.GetOrCreateMaterial("YellowCaution", new Color(0.95f, 0.80f, 0.10f), 0.2f, 0.6f);

            // Mesa industrial de perfiles de aluminio ranurados
            GameObject aluTable = new GameObject("MesaAluminio_Almacen_ASRS");
            aluTable.transform.SetParent(transform, false);
            aluTable.transform.localPosition = new Vector3(0.18f, 0f, 0f);

            CreateBox(aluTable.transform, "SuperficiePerfilesAluminio", new Vector3(0f, 0.86f, 0f), new Vector3(0.75f, 0.045f, 1.15f), aluMat);
            CreateBox(aluTable.transform, "PataAlu1", new Vector3(-0.34f, 0.43f, -0.54f), new Vector3(0.045f, 0.86f, 0.045f), aluMat);
            CreateBox(aluTable.transform, "PataAlu2", new Vector3( 0.34f, 0.43f, -0.54f), new Vector3(0.045f, 0.86f, 0.045f), aluMat);
            CreateBox(aluTable.transform, "PataAlu3", new Vector3(-0.34f, 0.43f,  0.54f), new Vector3(0.045f, 0.86f, 0.045f), aluMat);
            CreateBox(aluTable.transform, "PataAlu4", new Vector3( 0.34f, 0.43f,  0.54f), new Vector3(0.045f, 0.86f, 0.045f), aluMat);

            // Guía lineal sobre la mesa
            CreateBox(transform, "RielLineal_Base", new Vector3(0f, 0.91f, 0f), new Vector3(0.18f, 0.06f, 0.98f), aluMat);
            CreateBox(transform, "FranjaAmarillaCaution", new Vector3(0.092f, 0.91f, 0f), new Vector3(0.005f, 0.035f, 0.92f), yellowCaution);

            // Estantería vertical de Almacenamiento (Rack AS/RS de 3 niveles)
            GameObject storageRack = new GameObject("Estanteria_Almacenamiento_ASRS");
            storageRack.transform.SetParent(aluTable.transform, false);
            storageRack.transform.localPosition = new Vector3(0.24f, 0.88f, 0.05f);

            CreateBox(storageRack.transform, "ColumnaRack_Izq", new Vector3(0f, 0.38f, -0.35f), new Vector3(0.04f, 0.76f, 0.04f), aluMat);
            CreateBox(storageRack.transform, "ColumnaRack_Der", new Vector3(0f, 0.38f,  0.35f), new Vector3(0.04f, 0.76f, 0.04f), aluMat);
            for (int level = 1; level <= 3; level++)
            {
                float y = level * 0.22f;
                CreateBox(storageRack.transform, $"NivelAlmacen_{level}", new Vector3(-0.04f, y, 0f), new Vector3(0.16f, 0.02f, 0.72f), darkMat);
            }

            // Base del carro sobre el riel lineal
            CreateBox(transform, "BaseCarroRielLineal", new Vector3(0f, 0.945f, 0f), new Vector3(0.24f, 0.04f, 0.24f), darkMat);

            // Brida / Punto de montaje sobre el carro lineal
            CreateMountingFlange(transform, new Vector3(0f, 0.97f, 0f), lookToBelt, "PuntoUbicacion_Estacion3_Almacen");
        }

        /// <summary>
        /// Estaciones 4 y 5: Pedestal cilíndrico gris + Mesas de trabajo con soportes de madera y matrices perforadas (sin el brazo del robot).
        /// </summary>
        private void BuildScorbotPedestalAndTables(Vector3 lookToBelt, bool includeWoodenWorkTables)
        {
            Material pedestalGrey = ConveyorDimensions.GetOrCreateMaterial("ScorbotPedestal", new Color(0.26f, 0.29f, 0.33f), 0.5f, 0.5f);
            Material scorbotBlack = ConveyorDimensions.GetOrCreateMaterial("ScorbotBlack", new Color(0.11f, 0.12f, 0.14f), 0.4f, 0.55f);
            Material woodBoxMat = ConveyorDimensions.GetOrCreateMaterial("ScorbotWoodBox", new Color(0.80f, 0.64f, 0.38f), 0.08f, 0.4f);
            Material tableWhiteMat = ConveyorDimensions.GetOrCreateMaterial("LabDeskWhite", new Color(0.88f, 0.89f, 0.90f), 0.1f, 0.5f);

            // Pedestal cilíndrico gris (código 93411)
            CreateCylinder(transform, "PedestalCilindrico_Estacion", new Vector3(0f, 0.45f, 0f), new Vector3(0.20f, 0.45f, 0.20f), pedestalGrey);
            CreateBox(transform, "PlacaInferiorPiso", new Vector3(0f, 0.01f, 0f), new Vector3(0.38f, 0.02f, 0.38f), pedestalGrey);
            CreateBox(transform, "PlacaSuperiorPedestal", new Vector3(0f, 0.91f, 0f), new Vector3(0.38f, 0.02f, 0.38f), pedestalGrey);

            if (includeWoodenWorkTables)
            {
                // Mesa de trabajo con los 2 soportes de madera y matrices negras perforadas (Video 5.41.00 PM)
                GameObject workBench = new GameObject("MesaTrabajo_ConMatricesPerforadas");
                workBench.transform.SetParent(transform, false);
                workBench.transform.localPosition = new Vector3(-0.32f, 0f, -0.52f);

                CreateBox(workBench.transform, "EscritorioBlanco", new Vector3(0f, 0.72f, 0f), new Vector3(0.95f, 0.03f, 0.65f), tableWhiteMat);
                CreateBox(workBench.transform, "PataMesa1", new Vector3(-0.42f, 0.36f, -0.28f), new Vector3(0.04f, 0.72f, 0.04f), pedestalGrey);
                CreateBox(workBench.transform, "PataMesa2", new Vector3( 0.42f, 0.36f, -0.28f), new Vector3(0.04f, 0.72f, 0.04f), pedestalGrey);
                CreateBox(workBench.transform, "PataMesa3", new Vector3(-0.42f, 0.36f,  0.28f), new Vector3(0.04f, 0.72f, 0.04f), pedestalGrey);
                CreateBox(workBench.transform, "PataMesa4", new Vector3( 0.42f, 0.36f,  0.28f), new Vector3(0.04f, 0.72f, 0.04f), pedestalGrey);

                for (int b = 0; b < 2; b++)
                {
                    float bx = -0.18f + b * 0.34f;
                    CreateBox(workBench.transform, $"SoporteMadera_{b}", new Vector3(bx, 0.85f, 0.08f), new Vector3(0.26f, 0.23f, 0.26f), woodBoxMat);
                    CreateBox(workBench.transform, $"MatrizPerforadaNegra_{b}", new Vector3(bx, 0.972f, 0.08f), new Vector3(0.27f, 0.015f, 0.27f), scorbotBlack);
                }
            }

            // Brida / Punto de montaje sobre el pedestal
            CreateMountingFlange(transform, new Vector3(0f, 0.925f, 0f), lookToBelt, "PuntoUbicacion_Estacion");
        }

        private void BuildReferencePedestalOnly()
        {
            Material pedestalGrey = ConveyorDimensions.GetOrCreateMaterial("PedestalGrey", new Color(0.25f, 0.28f, 0.31f), 0.5f, 0.5f);
            CreateCylinder(transform, "Pedestal_Auxiliar_JuntoAPLC", new Vector3(0f, 0.38f, 0f), new Vector3(0.26f, 0.38f, 0.26f), pedestalGrey);
            CreateBox(transform, "PlacaSuperior_PedestalAuxiliar", new Vector3(0f, 0.77f, 0f), new Vector3(0.38f, 0.02f, 0.38f), pedestalGrey);
        }

        /// <summary>
        /// Crea un disco/brida metálica sobre el pedestal o mesa que indica el punto exacto donde se ancla el robot de la estación.
        /// </summary>
        private void CreateMountingFlange(Transform parent, Vector3 localPos, Vector3 lookToBelt, string flangeName)
        {
            _mountingPoint = new GameObject(flangeName).transform;
            _mountingPoint.SetParent(parent, false);
            _mountingPoint.localPosition = localPos;

            float yaw = Mathf.Atan2(lookToBelt.x, lookToBelt.z) * Mathf.Rad2Deg;
            _mountingPoint.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Material padMat = ConveyorDimensions.GetOrCreateMaterial("MountPadIdle", new Color(0.38f, 0.41f, 0.45f), 0.6f, 0.6f);
            GameObject disc = CreateCylinder(_mountingPoint, "BridaAnclajeEstacion", new Vector3(0f, 0.008f, 0f), new Vector3(0.20f, 0.008f, 0.20f), padMat);
            _mountingPadRenderer = disc.GetComponent<Renderer>();
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            Collider c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
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
            if (c != null) DestroyImmediate(c);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            return go;
        }
    }
}
