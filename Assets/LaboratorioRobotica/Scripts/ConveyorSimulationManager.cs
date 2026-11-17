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

        public BeltSystemStatus systemStatus = BeltSystemStatus.Running;

        [Tooltip("Si está activo, cuando una bandeja va sola sin pieza el sensor detiene la banda en estado de ADVERTENCIA esperando confirmación o auto-continúa tras 3.5 s")]
        public bool autoResumeOnEmptyTrayWarning = false;

        [Tooltip("Mostrar u ocultar todos los paneles de control en pantalla (Atajo: Tecla H)")]
        public bool showHudPanels = true;

        [Tooltip("Mostrar u ocultar el panel inferior derecho de cámaras y registro de eventos")]
        public bool showRightLogPanel = true;

        [Tooltip("Mostrar etiquetas 3D en pantalla con las medidas del PDF (484.5 cm, 138.5 cm, 96 cm, 17.3 cm, bandejas, sensores)")]
        public bool show3DDimensionLabels = false;

        [Tooltip("Mostrar etiquetas 3D sobre cada estación de robot y sensor magnético")]
        public bool showStation3DLabels = false;

        [Header("Referencias Generadas")]
        public List<MagneticStationSensor> stationSensors = new List<MagneticStationSensor>();
        public List<TrayController> trays = new List<TrayController>();
        public MagneticStationSensor activeHoldingSensor;
        public int selectedTrayIndex = 0;

        // Animación visual de los eslabones crema de la banda (3 cm de paso, 4 cm de ancho)
        private readonly List<Transform> _animatedSlatPairs = new List<Transform>();
        private readonly List<float> _animatedSlatBaseDistances = new List<float>();
        private float _slatScrollOffset = 0f;

        // Registro de eventos tipo PLC / SCADA
        private readonly List<string> _eventLog = new List<string>();
        private Vector2 _logScrollPos;
        private string _activeBannerMessage = "Sistema listo. Cinta transportadora Eshed Robotec en operación.";
        private float _warningTimer = 0f;

        private CameraController _cameraController;
        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _tagStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _buttonStyle;

        private void OnEnable()
        {
            // Reconstruir automáticamente la escena si no tiene la versión limpia de 3 estaciones
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
                LogPlcEvent("INFO", "Simulación iniciada. Monitoreando los 3 sensores magnéticos (Inicio/Inventario, Manufactura y Almacenamiento).");
            }
        }

        private void RefreshReferencesFromHierarchy()
        {
            stationSensors.Clear();
             stationSensors.AddRange(GetComponentsInChildren<MagneticStationSensor>(true));
            trays.Clear();
            trays.AddRange(GetComponentsInChildren<TrayController>(true));
            _cameraController = FindObjectOfType<CameraController>();
        }

        // =========================================================================================
        // LÓGICA DE SIMULACIÓN EN TIEMPO REAL (SENSORES MAGNÉTICOS, PARADAS, ERRORES Y ADVERTENCIAS)
        // =========================================================================================
        private void Update()
        {
            if (!Application.isPlaying) return;

            float dt = Time.deltaTime;

            // Actualizar enfriamiento e indicadores de todos los sensores magnéticos
            for (int i = 0; i < stationSensors.Count; i++)
            {
                if (stationSensors[i] != null)
                {
                    stationSensors[i].TickCooldown(dt);
                }
            }

            // Atajos de teclado rápidos para demostración en clase
            HandleKeyboardShortcuts();

            // Evaluar máquina de estados de la banda
            switch (systemStatus)
            {
                case BeltSystemStatus.ManualPause:
                    break;

                case BeltSystemStatus.StoppedByErrorBadTray:
                    // Si el usuario corrigió la orientación de la bandeja detenida desde la interfaz, reanudar
                    if (activeHoldingSensor != null && activeHoldingSensor.currentDetectedTray != null)
                    {
                        if (!activeHoldingSensor.currentDetectedTray.HasOrientationError)
                        {
                            ResolveActiveSensorStop("Orientación de bandeja corregida por el operador.");
                        }
                    }
                    break;

                case BeltSystemStatus.PausedByWarningEmptyTray:
                    if (activeHoldingSensor != null && activeHoldingSensor.currentDetectedTray != null)
                    {
                        // Si el operador le colocó una pieza en vivo, pasar a trabajo del robot
                        if (!activeHoldingSensor.currentDetectedTray.HasMissingPieceWarning)
                        {
                            activeHoldingSensor.LatchTrayAtSensor(activeHoldingSensor.currentDetectedTray, out _, out string msg);
                            systemStatus = BeltSystemStatus.StoppedForRobotWork;
                            _activeBannerMessage = msg;
                            LogPlcEvent("TRABAJO", msg);
                            break;
                        }

                        if (autoResumeOnEmptyTrayWarning)
                        {
                            _warningTimer += dt;
                            if (_warningTimer >= 3.2f)
                            {
                                ResolveActiveSensorStop("Auto-continuación tras advertencia de bandeja sin pieza.");
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
                            string doneMsg = $"[{activeHoldingSensor.stationName}] Trabajo de robot completado sobre Bandeja #{(workedTray != null ? workedTray.palletId : 0)}. Reanudando banda.";
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
            float perimeter = ConveyorDimensions.TotalLoopPerimeter;
            float deltaDist = beltSpeedMetersPerSec * dt;

            // 1. Mover visualmente los eslabones crema de la banda (paso de 3 cm = 0.03 m)
            _slatScrollOffset = (_slatScrollOffset + deltaDist) % perimeter;
            for (int i = 0; i < _animatedSlatPairs.Count; i++)
            {
                if (_animatedSlatPairs[i] == null) continue;
                float d = (_animatedSlatBaseDistances[i] + _slatScrollOffset) % perimeter;
                ConveyorDimensions.EvaluateLoopPath(d, out Vector3 sPos, out Vector3 sTan, out _);
                _animatedSlatPairs[i].position = sPos;
                if (sTan.sqrMagnitude > 0.001f)
                {
                    _animatedSlatPairs[i].rotation = Quaternion.LookRotation(sTan, Vector3.up);
                }
            }

            // 2. Avanzar cada bandeja y verificar si llega a algún sensor magnético activo
            for (int t = 0; t < trays.Count; t++)
            {
                TrayController tray = trays[t];
                if (tray == null) continue;

                tray.distanceOnLoop = (tray.distanceOnLoop + deltaDist) % perimeter;
                tray.UpdateTransformOnBelt();

                // Comprobar sensores magnéticos de las estaciones
                for (int s = 0; s < stationSensors.Count; s++)
                {
                    MagneticStationSensor sensor = stationSensors[s];
                    if (sensor != null && sensor.CanTriggerForTray(tray))
                    {
                        TriggerSensorStop(sensor, tray);
                        return; // La banda se detiene inmediatamente al activarse el sensor magnético
                    }
                }
            }
        }

        private void TriggerSensorStop(MagneticStationSensor sensor, TrayController tray)
        {
            activeHoldingSensor = sensor;
            _warningTimer = 0f;

            sensor.LatchTrayAtSensor(tray, out StationSensorState sensorState, out string msg);
            _activeBannerMessage = msg;

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
            _activeBannerMessage = reasonMessage;
            LogPlcEvent("BANDA", reasonMessage);
        }

        public void SetWhiteKukaStationsActiveMode(bool enableWhiteKukaSensors)
        {
            for (int i = 0; i < stationSensors.Count; i++)
            {
                if (stationSensors[i] != null && stationSensors[i].isWhiteKukaStation)
                {
                    stationSensors[i].sensorActive = enableWhiteKukaSensors;
                    if (!enableWhiteKukaSensors && activeHoldingSensor == stationSensors[i])
                    {
                        ResolveActiveSensorStop("Sensores de robots KUKA blancos puestos en modo ubicación/espera.");
                    }
                    stationSensors[i].UpdateVisualIndicators();
                }
            }
            string modeDesc = enableWhiteKukaSensors
                ? "Modo Línea Completa: Los sensores magnéticos de los 3 KUKA blancos + 2 SCORBOT detienen la banda."
                : "Modo Estado Actual Lab: Robots blancos KUKA en espera (se conserva su ubicación y sensores visibles); paran los SCORBOT.";
            _activeBannerMessage = modeDesc;
            LogPlcEvent("MODO", modeDesc);
        }

        private void HandleKeyboardShortcuts()
        {
            // Tecla H: Mostrar / Ocultar todos los paneles y anuncios para ver la simulación limpia
            if (Input.GetKeyDown(KeyCode.H))
            {
                showHudPanels = !showHudPanels;
            }

            // Tecla T: Mostrar / Ocultar etiquetas 3D flotantes sobre las estaciones
            if (Input.GetKeyDown(KeyCode.T))
            {
                bool nextState = !(show3DDimensionLabels || showStation3DLabels);
                show3DDimensionLabels = nextState;
                showStation3DLabels = nextState;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (systemStatus == BeltSystemStatus.ManualPause)
                {
                    systemStatus = BeltSystemStatus.Running;
                    _activeBannerMessage = "Banda reanudada manualmente.";
                }
                else if (systemStatus == BeltSystemStatus.Running)
                {
                    systemStatus = BeltSystemStatus.ManualPause;
                    _activeBannerMessage = "Banda en pausa manual (Barra Espaciadora).";
                }
                else
                {
                    ResolveActiveSensorStop("Interbloqueo liberado manualmente por el operador.");
                }
            }
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
        // CONSTRUCCIÓN PROCEDURAL COMPLETA DEL LABORATORIO A ESCALA REAL 1:1
        // =========================================================================================
        [ContextMenu("Reconstruir Escena Completa del Laboratorio (Escala 1:1)")]
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

            BuildLaboratoryFloorAndEnvironment();
            BuildEshedConveyorStructure();
            BuildRobotStationsAndMagneticSensors();
            BuildInitialTrays();

            _cameraController = FindObjectOfType<CameraController>();
            if (_cameraController == null && Camera.main != null)
            {
                _cameraController = Camera.main.gameObject.AddComponent<CameraController>();
            }
        }

        private void BuildLaboratoryFloorAndEnvironment()
        {
            GameObject envRoot = new GameObject("EntornoLaboratorio");
            envRoot.transform.SetParent(transform, false);

            Material floorMat = ConveyorDimensions.GetOrCreateMaterial("LabFloorTile", new Color(0.84f, 0.85f, 0.83f), 0.08f, 0.35f);
            Material gridLineMat = ConveyorDimensions.GetOrCreateMaterial("FloorGridLine", new Color(0.72f, 0.74f, 0.72f), 0.05f, 0.2f);
            Material blueCurtainMat = ConveyorDimensions.GetOrCreateMaterial("BlueCurtain", new Color(0.08f, 0.22f, 0.78f), 0.05f, 0.25f);
            Material woodTopMat = ConveyorDimensions.GetOrCreateMaterial("CurtainWoodTop", new Color(0.78f, 0.56f, 0.36f), 0.1f, 0.35f);

            // Piso del laboratorio de robótica
            CreateBox(envRoot.transform, "PisoLaboratorio", new Vector3(0f, -0.025f, 0f), new Vector3(11.0f, 0.05f, 12.0f), floorMat);

            // Líneas de baldosas cada 1 metro para referencia visual de escala
            for (int x = -4; x <= 4; x++)
            {
                CreateBox(envRoot.transform, $"LineaPisoX_{x}", new Vector3(x, 0.001f, 0f), new Vector3(0.008f, 0.002f, 10f), gridLineMat);
            }
            for (int z = -4; z <= 4; z++)
            {
                CreateBox(envRoot.transform, $"LineaPisoZ_{z}", new Vector3(0f, 0.001f, z), new Vector3(8f, 0.002f, 0.008f), gridLineMat);
            }

            // Falda/cortina azul central característica del laboratorio (visible en las fotos 5.41.01 PM)
            CreateBox(envRoot.transform, "CortinaAzulInterior", new Vector3(0.18f, 0.48f, 0.1f), new Vector3(0.02f, 0.92f, 2.85f), blueCurtainMat);
            CreateBox(envRoot.transform, "ListonMaderaCortina", new Vector3(0.18f, 0.955f, 0.1f), new Vector3(0.075f, 0.025f, 2.85f), woodTopMat);
        }

        private void BuildEshedConveyorStructure()
        {
            GameObject beltRoot = new GameObject("EstructuraCinta_EshedRobotec");
            beltRoot.transform.SetParent(transform, false);

            Material blackFrameMat = ConveyorDimensions.GetOrCreateMaterial("BeltFrameBlack", new Color(0.11f, 0.12f, 0.14f), 0.45f, 0.55f);
            Material centralGrooveMat = ConveyorDimensions.GetOrCreateMaterial("CentralGrooveBlack", new Color(0.07f, 0.07f, 0.08f), 0.35f, 0.4f);
            Material creamSlatMat = ConveyorDimensions.GetOrCreateMaterial("CreamSlatPlastic", new Color(0.82f, 0.75f, 0.67f), 0.08f, 0.45f);
            Material orangeLogoMat = ConveyorDimensions.GetOrCreateMaterial("EshedOrange", new Color(0.95f, 0.42f, 0.10f), 0.2f, 0.5f);
            Material whiteControlMat = ConveyorDimensions.GetOrCreateMaterial("ControlBoxWhite", new Color(0.89f, 0.88f, 0.84f), 0.1f, 0.45f);
            Material motorDarkMat = ConveyorDimensions.GetOrCreateMaterial("MotorDark", new Color(0.14f, 0.15f, 0.17f), 0.6f, 0.5f);

            float R = ConveyorDimensions.CurveRadius;          // 0.606 m
            float L = ConveyorDimensions.StraightLength;       // 3.460 m
            float H = ConveyorDimensions.BeltHeight;           // 0.960 m
            float W = ConveyorDimensions.TrackWidth;           // 0.173 m
            float frameH = ConveyorDimensions.TrackFrameThickness; // 0.065 m

            // 1. Dos tramos rectos principales (Derecho X = +0.606 m, Izquierdo X = -0.606 m)
            float[] sideX = { R, -R };
            string[] sideNames = { "CarrilRecto_Derecho_KUKA", "CarrilRecto_Izquierdo_SCORBOT" };

            for (int i = 0; i < 2; i++)
            {
                GameObject rail = new GameObject(sideNames[i]);
                rail.transform.SetParent(beltRoot.transform, false);
                rail.transform.localPosition = new Vector3(sideX[i], H - frameH * 0.5f, 0f);

                // Bastidor exterior de 17.3 cm de ancho
                CreateBox(rail.transform, "BastidorAluminio_17.3cm", Vector3.zero, new Vector3(W, frameH, L), blackFrameMat);

                // Canal central negro ranurado (9.3 cm entre las dos hileras de 4 cm)
                CreateBox(rail.transform, "CanalCentralRanurado", new Vector3(0f, frameH * 0.5f + 0.001f, 0f),
                    new Vector3(W - 2f * ConveyorDimensions.SlatChainWidth, 0.003f, L), centralGrooveMat);
            }

            // 2. Dos curvas semicirculares en los extremos (Z = +1.730 m y Z = -1.730 m)
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

                    // Logo ESHED ROBOTEC en las curvas exteriores
                    if (s == 6 || s == 21)
                    {
                        CreateBox(seg.transform, "PlacaLogo_ESHED_ROBOTEC",
                            new Vector3(0.51f, 0f, 0f),
                            new Vector3(0.02f, 0.65f, 0.9f),
                            orangeLogoMat);
                    }
                }
            }

            // 3. Eslabones plásticos articulados color crema/beige (4 cm de ancho a cada lado, paso de 3 cm)
            // Generamos pares de eslabones alrededor de los 10.73 m del circuito que se animan con la banda
            GameObject slatsRoot = new GameObject("EslabonesPlasticos_4x3cm");
            slatsRoot.transform.SetParent(beltRoot.transform, false);

            float slatSpacing = 0.06f; // Cada 2 pasos (6 cm) para alto rendimiento manteniendo detalle visual idéntico
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

                // Eslabón izquierdo (4 cm ancho x 3 cm paso)
                CreateBox(pair.transform, "EslabonIzq_4x3cm",
                    new Vector3(-lateralCenterOffset, 0.002f, 0f),
                    new Vector3(ConveyorDimensions.SlatChainWidth, 0.004f, ConveyorDimensions.SlatChainPitch * 0.92f),
                    creamSlatMat);

                // Eslabón derecho (4 cm ancho x 3 cm paso)
                CreateBox(pair.transform, "EslabonDer_4x3cm",
                    new Vector3(lateralCenterOffset, 0.002f, 0f),
                    new Vector3(ConveyorDimensions.SlatChainWidth, 0.004f, ConveyorDimensions.SlatChainPitch * 0.92f),
                    creamSlatMat);

                _animatedSlatPairs.Add(pair.transform);
                _animatedSlatBaseDistances.Add(d);
            }

            // 4. Patas de soporte de la cinta (altura 96 cm desde el piso)
            GameObject legsRoot = new GameObject("PatasSoporte_Altura96cm");
            legsRoot.transform.SetParent(beltRoot.transform, false);

            float[] legZPositions = { -1.65f, -0.55f, 0.55f, 1.65f };
            float legHeight = H - frameH;
            foreach (float lz in legZPositions)
            {
                // Pata carril derecho y pata carril izquierdo + travesaño inferior
                CreateCylinder(legsRoot.transform, $"PataDer_{lz:F1}", new Vector3(R, legHeight * 0.5f, lz), new Vector3(0.045f, legHeight * 0.5f, 0.045f), blackFrameMat);
                CreateCylinder(legsRoot.transform, $"PataIzq_{lz:F1}", new Vector3(-R, legHeight * 0.5f, lz), new Vector3(0.045f, legHeight * 0.5f, 0.045f), blackFrameMat);
                CreateBox(legsRoot.transform, $"Travesano_{lz:F1}", new Vector3(0f, 0.12f, lz), new Vector3(ConveyorDimensions.CenterlineSpanX, 0.035f, 0.035f), blackFrameMat);
            }
        }

        private void BuildRobotStationsAndMagneticSensors()
        {
            GameObject stationsRoot = new GameObject("TresEstaciones_Y_SensoresMagneticos");
            stationsRoot.transform.SetParent(transform, false);

            // ÚNICAMENTE las 3 estaciones marcadas (con sus estantes, pedestales, repisas laterales en la banda y sensores magnéticos):
            // 1. d = 0.64 m  -> Estación 1: Inicio, Inventario (Carril delantero X=+0.606, Z=+1.09: Estante de 3 niveles + mesa + repisa y sensor)
            // 2. d = 7.87 m  -> Estación 2: Manufactura (Carril trasero X=-0.606, corrida hacia la derecha en Z=+0.78: Pedestal + estante/mesa con matrices + repisa y sensor)
            // 3. d = 9.776 m -> Estación 3: Almacenamiento (Curva derecha en Z=+2.336: Pedestal/estante + repisa y sensor)

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

                // Crear únicamente la Base / Pedestal / Estante de cada una de las 3 estaciones marcadas
                GameObject baseGo = new GameObject($"Estacion_{cfg.idx}_{cfg.name}");
                baseGo.transform.SetParent(stationsRoot.transform, false);
                Vector3 floorPos = new Vector3(
                    beltPos.x + outwardNormal.x * cfg.radialOffset,
                    0f,
                    beltPos.z + outwardNormal.z * cfg.radialOffset);
                baseGo.transform.position = floorPos;

                RobotStationAnimator stationBase = baseGo.AddComponent<RobotStationAnimator>();
                stationBase.BuildRobotModel(cfg.model, cfg.name, cfg.robotDesc, beltPos, outwardNormal);

                // Crear Sensor Magnético y la repisa lateral en la banda únicamente en estas 3 estaciones
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

            // 3 bandejas distribuidas en el nuevo sentido de giro:
            // Bandeja #1: Saliendo hacia Estación 1 (Inicio, Inventario)
            CreateTrayInstance(traysRoot.transform, 1, 0.20f, TrayOrientationState.Correcta, TrayLoadState.ConPieza);

            // Bandeja #2: En camino hacia Estación 2 (Manufactura)
            CreateTrayInstance(traysRoot.transform, 2, 5.60f, TrayOrientationState.Correcta, TrayLoadState.ConPieza);

            // Bandeja #3: Saliendo de Manufactura hacia Estación 3 (Almacenamiento)
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

        // =========================================================================================
        // INTERFAZ GRÁFICA SCADA / HMI Y ETIQUETAS 3D DE MEDIDAS EN TIEMPO REAL
        // =========================================================================================
        private void InitGuiStyles()
        {
            if (_boxStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 8, 8)
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.97f, 1f) }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.88f, 0.91f, 0.95f) }
            };

            _tagStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _bannerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(12, 12, 6, 6),
                normal = { textColor = Color.white }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            InitGuiStyles();

            // 1. Etiquetas 3D flotantes sobre la escena (solo si están activadas)
            if (showHudPanels)
            {
                Draw3DSceneOverlays();
            }

            // Si el usuario ocultó la interfaz (Tecla H o botón), mostrar únicamente un botón pequeño arriba a la izquierda
            // y un aviso compacto arriba solo cuando ocurra un ERROR o ADVERTENCIA
            if (!showHudPanels)
            {
                GUI.backgroundColor = new Color(0.15f, 0.55f, 0.95f, 0.92f);
                if (GUI.Button(new Rect(10f, 10f, 215f, 30f), "👁 Mostrar Paneles (Tecla H)", _buttonStyle))
                {
                    showHudPanels = true;
                }
                GUI.backgroundColor = Color.white;

                // Mostrar alerta compacta arriba solo si hay un Error o Advertencia activa para que se note en la demo
                if (systemStatus == BeltSystemStatus.StoppedByErrorBadTray || systemStatus == BeltSystemStatus.PausedByWarningEmptyTray)
                {
                    DrawTopStatusBanner(fullWidth: true);
                }
                return;
            }

            // 2. Banner superior de estado de la línea y alertas de Error / Advertencia
            DrawTopStatusBanner(fullWidth: false);

            // 3. Panel izquierdo de control SCADA / Simulación de Bandejas y Sensores
            DrawLeftControlPanel();

            // 4. Panel inferior derecho de Vistas de Cámara y Registro de Eventos PLC (si está visible)
            if (showRightLogPanel)
            {
                DrawRightCameraAndLogPanel();
            }
        }

        private void DrawTopStatusBanner(bool fullWidth)
        {
            Color bannerColor = new Color(0.10f, 0.45f, 0.22f, 0.92f);
            string statusTitle = "ESTADO LÍNEA: EN MARCHA (BANDA AVANZANDO)";

            switch (systemStatus)
            {
                case BeltSystemStatus.StoppedForRobotWork:
                    bannerColor = new Color(0.10f, 0.38f, 0.68f, 0.94f);
                    statusTitle = $"BANDA DETENIDA POR SENSOR MAGNÉTICO — ESTACIÓN ACTIVA ({activeHoldingSensor?.stationName})";
                    break;
                case BeltSystemStatus.PausedByWarningEmptyTray:
                    bannerColor = new Color(0.82f, 0.50f, 0.04f, 0.95f);
                    statusTitle = $"⚠ ADVERTENCIA: BANDEJA VA SOLA SIN PIEZA EN {activeHoldingSensor?.stationName}";
                    break;
                case BeltSystemStatus.StoppedByErrorBadTray:
                    bannerColor = new Color(0.80f, 0.12f, 0.12f, 0.96f);
                    statusTitle = $"✖ ERROR CRÍTICO: BANDEJA VOLTEADA O MAL POSICIONADA EN {activeHoldingSensor?.stationName}";
                    break;
                case BeltSystemStatus.ManualPause:
                    bannerColor = new Color(0.35f, 0.35f, 0.40f, 0.92f);
                    statusTitle = "ESTADO LÍNEA: PAUSA MANUAL DEL OPERADOR";
                    break;
            }

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = bannerColor;
            float startX = fullWidth ? 235f : 390f;
            Rect bannerRect = new Rect(startX, 10f, Screen.width - startX - 15f, 54f);
            GUI.Box(bannerRect, $"{statusTitle}\n{_activeBannerMessage}", _bannerStyle);
            GUI.backgroundColor = prevBg;
        }

        private void DrawLeftControlPanel()
        {
            Rect panelRect = new Rect(10f, 10f, 372f, Screen.height - 20f);
            GUI.Box(panelRect, "", _boxStyle);

            GUILayout.BeginArea(new Rect(20f, 18f, 352f, Screen.height - 36f));

            // Botones rápidos arriba del todo para limpiar la pantalla mientras simulas
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.20f, 0.65f, 1.0f);
            if (GUILayout.Button("👁 Ocultar Todo (Tecla H)", _buttonStyle, GUILayout.Height(24f)))
            {
                showHudPanels = false;
            }
            GUI.backgroundColor = showRightLogPanel ? new Color(0.75f, 0.78f, 0.85f) : new Color(0.45f, 0.50f, 0.58f);
            if (GUILayout.Button(showRightLogPanel ? "Ocultar Registro Der." : "Mostrar Registro Der.", _buttonStyle, GUILayout.Height(24f)))
            {
                showRightLogPanel = !showRightLogPanel;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label("LABORATORIO ROBÓTICA INDUSTRIAL (FMS)", _headerStyle);
            GUILayout.Label("Simulación de Banda Eshed Robotec, Sensores Magnéticos y Estaciones (Escala 1:1)", _labelStyle);
            GUILayout.Space(6f);

            // --- CONTROLES DE BANDA ---
            GUILayout.Label("1. CONTROL DE LA CINTA TRANSPORTADORA", _headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(systemStatus == BeltSystemStatus.ManualPause ? "▶ Reanudar Banda" : "⏸ Pausar Banda", _buttonStyle, GUILayout.Height(26f)))
            {
                if (systemStatus == BeltSystemStatus.ManualPause)
                    systemStatus = BeltSystemStatus.Running;
                else if (systemStatus == BeltSystemStatus.Running)
                    systemStatus = BeltSystemStatus.ManualPause;
                else
                    ResolveActiveSensorStop("Continuación forzada por el operador.");
            }
            if (activeHoldingSensor != null)
            {
                GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
                if (GUILayout.Button("Liberar Sensor Actual", _buttonStyle, GUILayout.Height(26f)))
                {
                    ResolveActiveSensorStop("Sensor liberado manualmente; banda reanudada.");
                }
                GUI.backgroundColor = Color.white;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Velocidad Cinta: {(beltSpeedMetersPerSec * 100f):F0} cm/s", _labelStyle, GUILayout.Width(145f));
            beltSpeedMetersPerSec = GUILayout.HorizontalSlider(beltSpeedMetersPerSec, 0.08f, 0.65f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            show3DDimensionLabels = GUILayout.Toggle(show3DDimensionLabels, " Cotas PDF (cm)");
            showStation3DLabels = GUILayout.Toggle(showStation3DLabels, " Etiquetas Estaciones");
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            // --- PRUEBAS RÁPIDAS DE ERROR Y ADVERTENCIA (REabastecimiento / Volteo) ---
            GUILayout.Label("2. SIMULAR CONDICIONES EN BANDEJA SELECCIONADA", _headerStyle);
            if (trays.Count > 0)
            {
                selectedTrayIndex = Mathf.Clamp(selectedTrayIndex, 0, trays.Count - 1);
                GUILayout.BeginHorizontal();
                for (int i = 0; i < trays.Count; i++)
                {
                    GUI.backgroundColor = (i == selectedTrayIndex) ? new Color(0.25f, 0.65f, 1f) : Color.white;
                    if (GUILayout.Button($"Bandeja #{trays[i].palletId}", _buttonStyle))
                    {
                        selectedTrayIndex = i;
                    }
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                TrayController sel = trays[selectedTrayIndex];
                GUILayout.Label($"Estado actual: {sel.GetDiagnosticDescription()}", _labelStyle);

                // Botones de acción directa para demostrar al profesor / compañeros:
                GUI.backgroundColor = new Color(0.25f, 0.85f, 0.35f);
                if (GUILayout.Button("✔ Poner Bandeja NORMAL (Con Pieza y Bien Ubicada)", _buttonStyle, GUILayout.Height(25f)))
                {
                    sel.orientationState = TrayOrientationState.Correcta;
                    sel.loadState = TrayLoadState.ConPieza;
                    sel.RefreshVisualState();
                    if (activeHoldingSensor != null && activeHoldingSensor.currentDetectedTray == sel)
                    {
                        TriggerSensorStop(activeHoldingSensor, sel);
                    }
                    LogPlcEvent("OPERADOR", $"Bandeja #{sel.palletId} normalizada (posición correcta + pieza de trabajo).");
                }

                GUI.backgroundColor = new Color(0.95f, 0.30f, 0.30f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("✖ Voltear 180° (ERROR)", _buttonStyle, GUILayout.Height(25f)))
                {
                    sel.orientationState = TrayOrientationState.VolteadaBocaAbajo;
                    sel.RefreshVisualState();
                    if (activeHoldingSensor != null && activeHoldingSensor.currentDetectedTray == sel)
                    {
                        TriggerSensorStop(activeHoldingSensor, sel);
                    }
                    LogPlcEvent("SIMULAR ERROR", $"Bandeja #{sel.palletId} volteada boca abajo (180°). Dará ERROR al llegar al sensor.");
                }
                if (GUILayout.Button("✖ Girar 90° Mal (ERROR)", _buttonStyle, GUILayout.Height(25f)))
                {
                    sel.orientationState = TrayOrientationState.MalPosicionada;
                    sel.RefreshVisualState();
                    if (activeHoldingSensor != null && activeHoldingSensor.currentDetectedTray == sel)
                    {
                        TriggerSensorStop(activeHoldingSensor, sel);
                    }
                    LogPlcEvent("SIMULAR ERROR", $"Bandeja #{sel.palletId} girada 90° fuera de agarre. Dará ERROR al llegar al sensor.");
                }
                GUILayout.EndHorizontal();

                GUI.backgroundColor = new Color(1.0f, 0.75f, 0.20f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("⚠ Quitar Pieza (ADVERTENCIA)", _buttonStyle, GUILayout.Height(25f)))
                {
                    sel.orientationState = TrayOrientationState.Correcta;
                    sel.loadState = TrayLoadState.SinPieza;
                    sel.RefreshVisualState();
                    if (activeHoldingSensor != null && activeHoldingSensor.currentDetectedTray == sel)
                    {
                        TriggerSensorStop(activeHoldingSensor, sel);
                    }
                    LogPlcEvent("SIMULAR ADVERTENCIA", $"Bandeja #{sel.palletId} va sola SIN pieza. Dará ADVERTENCIA en el sensor.");
                }
                if (GUILayout.Button("⚠ Solo Base 20x16cm", _buttonStyle, GUILayout.Height(25f)))
                {
                    sel.orientationState = TrayOrientationState.Correcta;
                    sel.loadState = TrayLoadState.SoloBandejaInferior;
                    sel.RefreshVisualState();
                    if (activeHoldingSensor != null && activeHoldingSensor.currentDetectedTray == sel)
                    {
                        TriggerSensorStop(activeHoldingSensor, sel);
                    }
                    LogPlcEvent("SIMULAR ADVERTENCIA", $"Bandeja #{sel.palletId} sin bandeja superior 010004 ni pieza.");
                }
                GUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
            }

            GUILayout.Space(8f);

            // --- SENSORES MAGNÉTICOS EN LAS 3 ESTACIONES DE LA LÍNEA ---
            GUILayout.Label("3. SENSORES MAGNÉTICOS (3 ESTACIONES)", _headerStyle);

            for (int i = 0; i < stationSensors.Count; i++)
            {
                MagneticStationSensor s = stationSensors[i];
                if (s == null) continue;

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                s.sensorActive = GUILayout.Toggle(s.sensorActive, $" {s.stationName}");
                GUILayout.EndHorizontal();

                string stateTxt = s.inPlaceSignal
                    ? $"IN PLACE=1 | ID={((s.bitID2 ? 1 : 0))}{((s.bitID1 ? 1 : 0))}{((s.bitID0 ? 1 : 0))} ({s.currentState})"
                    : (s.sensorActive ? "IN PLACE=0 (Esperando bandeja...)" : "Sensor desactivado temporalmente");
                GUILayout.Label($"   {s.robotTypeDescription} | {stateTxt}", _labelStyle);
                GUILayout.EndVertical();
            }

            GUILayout.EndArea();
        }

        private void DrawRightCameraAndLogPanel()
        {
            float panelW = 430f;
            float panelH = 235f;
            Rect rightRect = new Rect(Screen.width - panelW - 10f, Screen.height - panelH - 10f, panelW, panelH);
            GUI.Box(rightRect, "", _boxStyle);

            GUILayout.BeginArea(new Rect(rightRect.x + 10f, rightRect.y + 8f, panelW - 20f, panelH - 16f));

            GUILayout.Label("CÁMARAS RÁPIDAS (Clic Derecho: Rotar | Rueda: Zoom | WASD: Mover)", _headerStyle);
            if (_cameraController != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Vista General", _buttonStyle))
                    _cameraController.SetPresetView(new Vector3(0f, 0.90f, 0f), 4.6f, -128f, 30f);
                if (GUILayout.Button("1. Inicio / Inventario", _buttonStyle))
                    _cameraController.SetPresetView(new Vector3(0.75f, 1.10f, 1.09f), 1.85f, -55f, 24f);
                if (GUILayout.Button("2. Manufactura", _buttonStyle))
                    _cameraController.SetPresetView(new Vector3(-0.75f, 1.05f, 0.78f), 1.75f, 110f, 24f);
                if (GUILayout.Button("3. Almacenamiento", _buttonStyle))
                    _cameraController.SetPresetView(new Vector3(0.0f, 1.08f, 2.30f), 1.65f, 15f, 24f);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Zoom Sensor/Bandeja", _buttonStyle))
                    _cameraController.SetPresetView(new Vector3(0.606f, 0.98f, 1.09f), 0.65f, -45f, 35f);
                if (trays.Count > 0 && GUILayout.Button("Seguir Bandeja Seleccionada", _buttonStyle))
                {
                    TrayController sel = trays[Mathf.Clamp(selectedTrayIndex, 0, trays.Count - 1)];
                    _cameraController.SetPresetView(sel.transform.position, 0.95f, -45f, 32f, sel.transform);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
            GUILayout.Label("REGISTRO DE EVENTOS PLC / SENSORES MAGNÉTICOS:", _headerStyle);
            _logScrollPos = GUILayout.BeginScrollView(_logScrollPos, GUILayout.Height(110f));
            for (int i = 0; i < _eventLog.Count; i++)
            {
                GUILayout.Label(_eventLog[i], _labelStyle);
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void Draw3DSceneOverlays()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // A. Etiquetas de las medidas reales del PDF "Medida Cinta"
            if (show3DDimensionLabels)
            {
                DrawWorldTag(cam, new Vector3(0f, 0.96f, -2.55f), "Ancho Total Cinta: 138.5 cm (1.385 m)", new Color(0.12f, 0.45f, 0.78f, 0.88f));
                DrawWorldTag(cam, new Vector3(0.95f, 0.96f, 0f), "Largo Total Cinta: 484.5 cm (4.845 m)", new Color(0.12f, 0.45f, 0.78f, 0.88f));
                DrawWorldTag(cam, new Vector3(0.606f, 0.48f, -1.65f), "Altura Cinta: 96 cm", new Color(0.12f, 0.45f, 0.78f, 0.88f));
                DrawWorldTag(cam, new Vector3(0.606f, 1.03f, -1.55f), "Ancho Carril: 17.3 cm | Eslabones: 4x3 cm", new Color(0.12f, 0.45f, 0.78f, 0.88f));

                if (trays.Count > 0 && trays[0] != null)
                {
                    DrawWorldTag(cam, trays[0].transform.position + new Vector3(0f, 0.20f, 0f),
                        "Bandeja Inf: 20x16x1.9 cm (Ranuras 2.3 y 1.7 cm)\nBandeja Sup: 17.5x12.5 cm | Manija T: 3.5x4.5x2.3 cm",
                        new Color(0.18f, 0.22f, 0.30f, 0.90f), 285f, 38f);
                }
            }

            // B. Etiquetas de cada estación de robot y su sensor magnético
            if (showStation3DLabels)
            {
                for (int i = 0; i < stationSensors.Count; i++)
                {
                    MagneticStationSensor s = stationSensors[i];
                    if (s == null) continue;

                    Color c = new Color(0.15f, 0.20f, 0.26f, 0.86f);
                    if (s.currentState == StationSensorState.ErrorBadTrayOrientation) c = new Color(0.85f, 0.12f, 0.12f, 0.95f);
                    else if (s.currentState == StationSensorState.WarningEmptyTray) c = new Color(0.88f, 0.55f, 0.05f, 0.95f);
                    else if (s.currentState == StationSensorState.WorkingRobot) c = new Color(0.10f, 0.55f, 0.85f, 0.95f);

                    string sensorLine = s.inPlaceSignal
                        ? $"[SENSOR ACTIVO: IN PLACE | ID={((s.bitID2 ? 1 : 0))}{((s.bitID1 ? 1 : 0))}{((s.bitID0 ? 1 : 0))}]"
                        : "[Sensor Magnético Listo]";

                    DrawWorldTag(cam, s.transform.position + new Vector3(0f, 0.34f, 0f),
                        $"S{s.stationNumber}: {s.robotTypeDescription}\n{sensorLine}", c, 230f, 36f);
                }
            }
        }

        private void DrawWorldTag(Camera cam, Vector3 worldPos, string text, Color bgColor, float width = 230f, float height = 24f)
        {
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0.1f) return;

            float guiX = screenPos.x - width * 0.5f;
            float guiY = Screen.height - screenPos.y - height * 0.5f;

            // No superponer sobre el panel izquierdo de control
            if (guiX < 385f) return;

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;
            GUI.Box(new Rect(guiX, guiY, width, height), text, _tagStyle);
            GUI.backgroundColor = prev;
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

