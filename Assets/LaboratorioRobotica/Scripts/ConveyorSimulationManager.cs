using System;
using System.Collections.Generic;
using UnityEngine;

namespace LaboratorioRobotica
{
    public enum BeltSystemStatus
    {
        Running = 0,
        StoppedForRobotWork = 1,
        PausedByWarningEmptyTray = 2,
        StoppedByErrorBadTray = 3,
        ManualPause = 4
    }

    [ExecuteAlways]
    public class ConveyorSimulationManager : MonoBehaviour
    {
        [Header("Parámetros de Operación de la Banda")]
        [Tooltip("Velocidad lineal de la cinta transportadora en metros/segundo (0.28 m/s = 28 cm/s)")]
        [Range(0.05f, 0.80f)]
        public float beltSpeedMetersPerSec = 0.28f;

        [Tooltip("Sentido de giro de la banda: 1 = Normal (Horario), -1 = Reversa (Antihorario)")]
        public int beltDirection = 1;

        [Tooltip("Si está activo, la banda gira de forma continua ignorando las paradas en sensores")]
        public bool continuousFreeMode = false;

        public BeltSystemStatus systemStatus = BeltSystemStatus.Running;

        [Tooltip("Si está activo, cuando una bandeja va sola sin pieza el sensor detiene la banda en estado de ADVERTENCIA esperando confirmación o auto-continúa tras 3.5 s")]
        public bool autoResumeOnEmptyTrayWarning = false;

        [Header("Referencias Generadas")]
        public List<MagneticStationSensor> stationSensors = new List<MagneticStationSensor>();
        public List<TrayController> trays = new List<TrayController>();
        public MagneticStationSensor activeHoldingSensor;
        public int selectedTrayIndex = 0;

        // Animación visual de los eslabones crema de la banda (3 cm de paso, 4 cm de ancho)
        [SerializeField] private List<Transform> _animatedSlatPairs = new List<Transform>();
        [SerializeField] private List<float> _animatedSlatBaseDistances = new List<float>();
        private float _slatScrollOffset = 0f;

        private readonly List<string> _eventLog = new List<string>();
        private float _warningTimer = 0f;
        private int _targetStationFilter = 0;
        private CameraController _cameraController;

        private void OnEnable()
        {
            // Eliminar piso antiguo si existía en la escena
            Transform oldFloor = transform.Find("EntornoLaboratorio");
            if (oldFloor != null)
            {
                DestroyImmediate(oldFloor.gameObject);
            }

            if (transform.Find("EstructuraCinta_EshedRobotec") == null ||
                transform.Find("TresEstaciones_Y_SensoresMagneticos") == null)
            {
                RebuildEntireLaboratoryScene();
            }
            else
            {
                RefreshReferencesFromHierarchy();
            }
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                _cameraController = FindObjectOfType<CameraController>();
                LogPlcEvent("INFO", "Simulación iniciada.");
            }
        }

        public void RefreshReferencesFromHierarchy()
        {
            stationSensors.Clear();
            stationSensors.AddRange(GetComponentsInChildren<MagneticStationSensor>(true));
            trays.Clear();
            trays.AddRange(GetComponentsInChildren<TrayController>(true));
            _cameraController = FindObjectOfType<CameraController>();

            if (_animatedSlatPairs.Count == 0 || _animatedSlatPairs[0] == null)
            {
                _animatedSlatPairs.Clear();
                _animatedSlatBaseDistances.Clear();
                Transform slatsRoot = transform.Find("EstructuraCinta_EshedRobotec/EslabonesPlasticos_4x3cm");
                if (slatsRoot != null)
                {
                    float slatSpacing = 0.06f;
                    for (int i = 0; i < slatsRoot.childCount; i++)
                    {
                        _animatedSlatPairs.Add(slatsRoot.GetChild(i));
                        _animatedSlatBaseDistances.Add(i * slatSpacing);
                    }
                }
            }
        }

        // =========================================================================================
        // ACCIONES LLAMADAS POR EL PREFAB UI (Canvas_PanelControlBanda)
        // =========================================================================================
        public void ToggleBeltPlayPause()
        {
            if (systemStatus == BeltSystemStatus.ManualPause)
            {
                systemStatus = BeltSystemStatus.Running;
            }
            else if (systemStatus == BeltSystemStatus.Running)
            {
                systemStatus = BeltSystemStatus.ManualPause;
            }
            else
            {
                ResolveActiveSensorStop("Banda reanudada desde el panel UI.");
            }
        }

        /// <summary>
        /// Simula el sensor con un solo botón desde el Panel UI:
        /// - Si la banda ya está detenida en un sensor, lo libera y continúa.
        /// - Si la banda está en movimiento o en pausa, activa de inmediato el sensor sobre la bandeja.
        /// </summary>
        public void SimulateSensorFromUIButton()
        {
            if (activeHoldingSensor != null)
            {
                ResolveActiveSensorStop("Sensor liberado desde el botón UI.");
                return;
            }

            if (stationSensors.Count == 0 || trays.Count == 0)
            {
                RefreshReferencesFromHierarchy();
                if (stationSensors.Count == 0 || trays.Count == 0) return;
            }

            selectedTrayIndex = Mathf.Clamp(selectedTrayIndex, 0, trays.Count - 1);
            TrayController tray = trays[selectedTrayIndex];
            if (tray == null) tray = trays[0];

            // Buscar el siguiente sensor en el sentido de avance (o el primero disponible)
            float perimeter = ConveyorDimensions.TotalLoopPerimeter;
            MagneticStationSensor bestSensor = stationSensors[0];
            float bestDistAhead = float.MaxValue;

            for (int i = 0; i < stationSensors.Count; i++)
            {
                MagneticStationSensor s = stationSensors[i];
                if (s == null) continue;
                float distAhead = WrapLoopDistance(s.distanceOnLoop - tray.distanceOnLoop, perimeter);
                if (distAhead < bestDistAhead)
                {
                    bestDistAhead = distAhead;
                    bestSensor = s;
                }
            }

            if (bestSensor != null)
            {
                bestSensor.sensorActive = true;
                float shift = WrapLoopDistance(bestSensor.distanceOnLoop - tray.distanceOnLoop, perimeter);
                StepBeltByDistance(shift, checkSensors: false);
                TriggerSensorStop(bestSensor, tray);
            }
        }

        // =========================================================================================
        // LÓGICA DE SIMULACIÓN EN TIEMPO REAL
        // =========================================================================================
        private void Update()
        {
            if (!Application.isPlaying) return;

            float dt = Time.deltaTime;

            for (int i = 0; i < stationSensors.Count; i++)
            {
                if (stationSensors[i] != null)
                {
                    stationSensors[i].TickCooldown(dt);
                }
            }

            switch (systemStatus)
            {
                case BeltSystemStatus.ManualPause:
                    break;

                case BeltSystemStatus.StoppedByErrorBadTray:
                    if (activeHoldingSensor != null && activeHoldingSensor.currentDetectedTray != null)
                    {
                        if (!activeHoldingSensor.currentDetectedTray.HasOrientationError)
                        {
                            ResolveActiveSensorStop("Orientación de bandeja corregida.");
                        }
                    }
                    break;

                case BeltSystemStatus.PausedByWarningEmptyTray:
                    if (activeHoldingSensor != null && activeHoldingSensor.currentDetectedTray != null)
                    {
                        if (!activeHoldingSensor.currentDetectedTray.HasMissingPieceWarning)
                        {
                            activeHoldingSensor.LatchTrayAtSensor(activeHoldingSensor.currentDetectedTray, out _, out string msg);
                            systemStatus = BeltSystemStatus.StoppedForRobotWork;
                            LogPlcEvent("TRABAJO", msg);
                            break;
                        }

                        if (autoResumeOnEmptyTrayWarning)
                        {
                            _warningTimer += dt;
                            if (_warningTimer >= 3.2f)
                            {
                                ResolveActiveSensorStop("Auto-continuación tras advertencia.");
                            }
                        }
                    }
                    break;

                case BeltSystemStatus.StoppedForRobotWork:
                    if (activeHoldingSensor != null)
                    {
                        activeHoldingSensor.workTimer += dt;
                        float progress = activeHoldingSensor.workTimer / Mathf.Max(0.5f, activeHoldingSensor.robotWorkDuration);
                        if (activeHoldingSensor.linkedRobot != null)
                        {
                            activeHoldingSensor.linkedRobot.SetWorkingState(true, progress);
                        }

                        if (activeHoldingSensor.workTimer >= activeHoldingSensor.robotWorkDuration)
                        {
                            TrayController workedTray = activeHoldingSensor.currentDetectedTray;
                            if (workedTray != null)
                            {
                                workedTray.operationsCompleted++;
                                workedTray.lastStationProcessed = activeHoldingSensor.stationName;
                            }
                            string doneMsg = $"[{activeHoldingSensor.stationName}] Trabajo completado. Reanudando banda.";
                            ResolveActiveSensorStop(doneMsg);
                        }
                    }
                    else
                    {
                        systemStatus = BeltSystemStatus.Running;
                    }
                    break;

                case BeltSystemStatus.Running:
                    AdvanceBeltAndTrays(dt);
                    break;
            }
        }

        private void AdvanceBeltAndTrays(float dt)
        {
            float sign = beltDirection >= 0 ? 1f : -1f;
            float deltaDist = beltSpeedMetersPerSec * sign * dt;
            StepBeltByDistance(deltaDist, checkSensors: !continuousFreeMode);
        }

        public void StepBeltByDistance(float deltaDist, bool checkSensors)
        {
            float perimeter = ConveyorDimensions.TotalLoopPerimeter;

            if (!checkSensors && activeHoldingSensor != null && Mathf.Abs(deltaDist) > 0.0005f)
            {
                activeHoldingSensor.ReleaseCurrentTray();
                activeHoldingSensor = null;
            }

            _slatScrollOffset = WrapLoopDistance(_slatScrollOffset + deltaDist, perimeter);
            for (int i = 0; i < _animatedSlatPairs.Count; i++)
            {
                if (_animatedSlatPairs[i] == null) continue;
                float d = WrapLoopDistance(_animatedSlatBaseDistances[i] + _slatScrollOffset, perimeter);
                ConveyorDimensions.EvaluateLoopPath(d, out Vector3 sPos, out Vector3 sTan, out _);
                _animatedSlatPairs[i].position = sPos;
                if (sTan.sqrMagnitude > 0.001f)
                {
                    _animatedSlatPairs[i].rotation = Quaternion.LookRotation(sTan, Vector3.up);
                }
            }

            for (int t = 0; t < trays.Count; t++)
            {
                TrayController tray = trays[t];
                if (tray == null) continue;

                tray.distanceOnLoop = WrapLoopDistance(tray.distanceOnLoop + deltaDist, perimeter);
                tray.UpdateTransformOnBelt();

                if (!checkSensors) continue;

                for (int s = 0; s < stationSensors.Count; s++)
                {
                    MagneticStationSensor sensor = stationSensors[s];
                    if (sensor == null) continue;
                    if (_targetStationFilter > 0 && sensor.stationNumber != _targetStationFilter) continue;

                    if (sensor.CanTriggerForTray(tray))
                    {
                        TriggerSensorStop(sensor, tray);
                        return;
                    }
                }
            }
        }

        private void TriggerSensorStop(MagneticStationSensor sensor, TrayController tray)
        {
            activeHoldingSensor = sensor;
            _warningTimer = 0f;

            sensor.LatchTrayAtSensor(tray, out StationSensorState sensorState, out string msg);

            switch (sensorState)
            {
                case StationSensorState.ErrorBadTrayOrientation:
                    systemStatus = BeltSystemStatus.StoppedByErrorBadTray;
                    LogPlcEvent("ERROR", msg);
                    break;

                case StationSensorState.WarningEmptyTray:
                    systemStatus = BeltSystemStatus.PausedByWarningEmptyTray;
                    LogPlcEvent("ADVERTENCIA", msg);
                    break;

                case StationSensorState.WorkingRobot:
                    systemStatus = BeltSystemStatus.StoppedForRobotWork;
                    LogPlcEvent("SENSOR", msg);
                    break;
            }
        }

        public void ResolveActiveSensorStop(string reasonMessage)
        {
            if (activeHoldingSensor != null)
            {
                activeHoldingSensor.ReleaseCurrentTray();
                activeHoldingSensor = null;
            }
            systemStatus = BeltSystemStatus.Running;
            LogPlcEvent("BANDA", reasonMessage);
        }

        private static float WrapLoopDistance(float d, float perimeter)
        {
            float r = d % perimeter;
            return r < 0f ? r + perimeter : r;
        }

        private void LogPlcEvent(string tag, string message)
        {
            string timeStr = DateTime.Now.ToString("HH:mm:ss");
            _eventLog.Insert(0, $"[{timeStr}] [{tag}] {message}");
            if (_eventLog.Count > 35)
            {
                _eventLog.RemoveAt(_eventLog.Count - 1);
            }
        }

        // =========================================================================================
        // CONSTRUCCIÓN PROCEDURAL DE LO ESENCIAL (BANDA, ESTACIONES/SENSORES Y BANDEJAS - SIN PISO)
        // =========================================================================================
        [ContextMenu("Reconstruir Banda y Objetos Esenciales (Sin Piso)")]
        public void RebuildEntireLaboratoryScene()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            stationSensors.Clear();
            trays.Clear();
            _animatedSlatPairs.Clear();
            _animatedSlatBaseDistances.Clear();

            // Solo construimos la banda y los objetos esenciales de la escena (sin piso)
            BuildEshedConveyorStructure();
            BuildRobotStationsAndMagneticSensors();
            BuildInitialTrays();

            _cameraController = FindObjectOfType<CameraController>();
            if (_cameraController == null && Camera.main != null)
            {
                _cameraController = Camera.main.gameObject.AddComponent<CameraController>();
            }
        }

        private void BuildEshedConveyorStructure()
        {
            GameObject beltRoot = new GameObject("EstructuraCinta_EshedRobotec");
            beltRoot.transform.SetParent(transform, false);

            Material blackFrameMat = ConveyorDimensions.GetOrCreateMaterial("BeltFrameBlack", new Color(0.11f, 0.12f, 0.14f), 0.45f, 0.55f);
            Material centralGrooveMat = ConveyorDimensions.GetOrCreateMaterial("CentralGrooveBlack", new Color(0.07f, 0.07f, 0.08f), 0.35f, 0.4f);
            Material creamSlatMat = ConveyorDimensions.GetOrCreateMaterial("CreamSlatPlastic", new Color(0.82f, 0.75f, 0.67f), 0.08f, 0.45f);
            Material orangeLogoMat = ConveyorDimensions.GetOrCreateMaterial("EshedOrange", new Color(0.95f, 0.42f, 0.10f), 0.2f, 0.5f);

            float R = ConveyorDimensions.CurveRadius;          // 0.606 m
            float L = ConveyorDimensions.StraightLength;       // 3.460 m
            float H = ConveyorDimensions.BeltHeight;           // 0.960 m
            float W = ConveyorDimensions.TrackWidth;           // 0.173 m
            float frameH = ConveyorDimensions.TrackFrameThickness; // 0.065 m

            // 1. Dos tramos rectos principales
            float[] sideX = { R, -R };
            string[] sideNames = { "CarrilRecto_Derecho_KUKA", "CarrilRecto_Izquierdo_SCORBOT" };

            for (int i = 0; i < 2; i++)
            {
                GameObject rail = new GameObject(sideNames[i]);
                rail.transform.SetParent(beltRoot.transform, false);
                rail.transform.localPosition = new Vector3(sideX[i], H - frameH * 0.5f, 0f);

                CreateBox(rail.transform, "BastidorAluminio_17.3cm", Vector3.zero, new Vector3(W, frameH, L), blackFrameMat);
                CreateBox(rail.transform, "CanalCentralRanurado", new Vector3(0f, frameH * 0.5f + 0.001f, 0f),
                    new Vector3(W - 2f * ConveyorDimensions.SlatChainWidth, 0.003f, L), centralGrooveMat);
            }

            // 2. Dos curvas semicirculares en los extremos
            int curveSegments = 28;
            float[] endZ = { ConveyorDimensions.HalfStraightZ, -ConveyorDimensions.HalfStraightZ };
            float[] startAngles = { 0f, Mathf.PI };
            string[] curveNames = { "CurvaNorte_Cabecera", "CurvaSur_Retorno" };

            for (int c = 0; c < 2; c++)
            {
                GameObject curveGroup = new GameObject(curveNames[c]);
                curveGroup.transform.SetParent(beltRoot.transform, false);

                float segArc = ConveyorDimensions.CurveArcLength / curveSegments;
                for (int s = 0; s < curveSegments; s++)
                {
                    float angle = startAngles[c] + ((s + 0.5f) / curveSegments) * Mathf.PI;
                    float cx = R * Mathf.Cos(angle);
                    float cz = endZ[c] + R * Mathf.Sin(angle);
                    Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                    GameObject seg = CreateBox(curveGroup.transform, $"SegCurvo_{s}",
                        new Vector3(cx, H - frameH * 0.5f, cz),
                        new Vector3(W, frameH, segArc * 1.08f),
                        blackFrameMat);
                    seg.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);

                    if (s == 6 || s == 21)
                    {
                        CreateBox(seg.transform, "PlacaLogo_ESHED_ROBOTEC",
                            new Vector3(0.51f, 0f, 0f),
                            new Vector3(0.02f, 0.65f, 0.9f),
                            orangeLogoMat);
                    }
                }
            }

            // 3. Eslabones plásticos articulados color crema
            GameObject slatsRoot = new GameObject("EslabonesPlasticos_4x3cm");
            slatsRoot.transform.SetParent(beltRoot.transform, false);

            float slatSpacing = 0.06f;
            int totalSlatPairs = Mathf.FloorToInt(ConveyorDimensions.TotalLoopPerimeter / slatSpacing);
            float lateralCenterOffset = (W * 0.5f) - (ConveyorDimensions.SlatChainWidth * 0.5f) - 0.002f;

            for (int k = 0; k < totalSlatPairs; k++)
            {
                float d = k * slatSpacing;
                ConveyorDimensions.EvaluateLoopPath(d, out Vector3 sPos, out Vector3 sTan, out _);

                GameObject pair = new GameObject($"ParEslabon_{k}");
                pair.transform.SetParent(slatsRoot.transform, false);
                pair.transform.position = sPos;
                pair.transform.rotation = Quaternion.LookRotation(sTan, Vector3.up);

                CreateBox(pair.transform, "EslabonIzq_4x3cm",
                    new Vector3(-lateralCenterOffset, 0.002f, 0f),
                    new Vector3(ConveyorDimensions.SlatChainWidth, 0.004f, ConveyorDimensions.SlatChainPitch * 0.92f),
                    creamSlatMat);

                CreateBox(pair.transform, "EslabonDer_4x3cm",
                    new Vector3(lateralCenterOffset, 0.002f, 0f),
                    new Vector3(ConveyorDimensions.SlatChainWidth, 0.004f, ConveyorDimensions.SlatChainPitch * 0.92f),
                    creamSlatMat);

                _animatedSlatPairs.Add(pair.transform);
                _animatedSlatBaseDistances.Add(d);
            }

            // 4. Patas de soporte de la cinta (altura 96 cm)
            GameObject legsRoot = new GameObject("PatasSoporte_Altura96cm");
            legsRoot.transform.SetParent(beltRoot.transform, false);

            float[] legZPositions = { -1.65f, -0.55f, 0.55f, 1.65f };
            float legHeight = H - frameH;
            foreach (float lz in legZPositions)
            {
                CreateCylinder(legsRoot.transform, $"PataDer_{lz:F1}", new Vector3(R, legHeight * 0.5f, lz), new Vector3(0.045f, legHeight * 0.5f, 0.045f), blackFrameMat);
                CreateCylinder(legsRoot.transform, $"PataIzq_{lz:F1}", new Vector3(-R, legHeight * 0.5f, lz), new Vector3(0.045f, legHeight * 0.5f, 0.045f), blackFrameMat);
                CreateBox(legsRoot.transform, $"Travesano_{lz:F1}", new Vector3(0f, 0.12f, lz), new Vector3(ConveyorDimensions.CenterlineSpanX, 0.035f, 0.035f), blackFrameMat);
            }
        }

        private void BuildRobotStationsAndMagneticSensors()
        {
            GameObject stationsRoot = new GameObject("TresEstaciones_Y_SensoresMagneticos");
            stationsRoot.transform.SetParent(transform, false);

            var configs = new[]
            {
                new {
                    idx = 1,
                    name = "Estación 1: Inicio, Inventario",
                    robotDesc = "Estante Inventario + Repisa Robot",
                    dist = 0.64f,
                    whiteKuka = false,
                    model = RobotModelType.KukaKR4R600_LinearRailStorage,
                    radialOffset = 0.48f
                },
                new {
                    idx = 2,
                    name = "Estación 2: Manufactura",
                    robotDesc = "Pedestal + Estante Matrices + Repisa",
                    dist = 7.87f,
                    whiteKuka = false,
                    model = RobotModelType.ScorbotERVPlus_AssemblyCell,
                    radialOffset = 0.46f
                },
                new {
                    idx = 3,
                    name = "Estación 3: Almacenamiento",
                    robotDesc = "Base Almacenamiento + Repisa Robot",
                    dist = 9.776f,
                    whiteKuka = false,
                    model = RobotModelType.ScorbotERVPlus_CurveStation,
                    radialOffset = 0.48f
                }
            };

            foreach (var cfg in configs)
            {
                ConveyorDimensions.EvaluateLoopPath(cfg.dist, out Vector3 beltPos, out _, out Vector3 outwardNormal);

                GameObject baseGo = new GameObject($"Estacion_{cfg.idx}_{cfg.name}");
                baseGo.transform.SetParent(stationsRoot.transform, false);
                Vector3 floorPos = new Vector3(
                    beltPos.x + outwardNormal.x * cfg.radialOffset,
                    0f,
                    beltPos.z + outwardNormal.z * cfg.radialOffset);
                baseGo.transform.position = floorPos;

                RobotStationAnimator stationBase = baseGo.AddComponent<RobotStationAnimator>();
                stationBase.BuildRobotModel(cfg.model, cfg.name, cfg.robotDesc, beltPos, outwardNormal);

                GameObject sensorGo = new GameObject($"SensorMagnetico_Estacion_{cfg.idx}");
                sensorGo.transform.SetParent(stationsRoot.transform, false);
                MagneticStationSensor sensor = sensorGo.AddComponent<MagneticStationSensor>();
                sensor.BuildSensorVisuals(cfg.idx, cfg.name, cfg.robotDesc, cfg.dist, cfg.whiteKuka, stationBase);
                sensor.sensorActive = true;

                stationSensors.Add(sensor);
            }
        }

        private void BuildInitialTrays()
        {
            GameObject traysRoot = new GameObject("Bandejas_En_Circulacion");
            traysRoot.transform.SetParent(transform, false);

            CreateTrayInstance(traysRoot.transform, 1, 0.20f, TrayOrientationState.Correcta, TrayLoadState.ConPieza);
            CreateTrayInstance(traysRoot.transform, 2, 5.60f, TrayOrientationState.Correcta, TrayLoadState.ConPieza);
            CreateTrayInstance(traysRoot.transform, 3, 8.65f, TrayOrientationState.Correcta, TrayLoadState.ConPieza);
        }

        public TrayController CreateTrayInstance(Transform parent, int id, float distOnLoop, TrayOrientationState orient, TrayLoadState load)
        {
            GameObject trayGo = new GameObject($"Bandeja_Pallet_{id}_20x16cm");
            trayGo.transform.SetParent(parent, false);
            TrayController tc = trayGo.AddComponent<TrayController>();
            tc.BuildDetailedGeometry(id, distOnLoop, orient, load);
            trays.Add(tc);
            return tc;
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
