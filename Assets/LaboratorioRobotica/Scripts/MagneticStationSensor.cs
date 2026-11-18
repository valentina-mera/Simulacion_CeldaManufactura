using UnityEngine;

namespace LaboratorioRobotica
{
    public enum StationSensorState
    {
        IdleWaiting = 0,
        WorkingRobot = 1,
        WarningEmptyTray = 2,
        ErrorBadTrayOrientation = 3,
        StationStandbyDisabled = 4
    }

    /// <summary>
    /// Representa el conjunto de sensores magnéticos / inductivos ubicados por debajo de la banda
    /// en cada estación de robot (placa "PALLET I.D. -> ID2 | ID1 | ID0 | IN PLACE"), junto con su
    /// torreta luminosa (Rojo/Verde) y bandeja lateral de transferencia.
    /// </summary>
    public class MagneticStationSensor : MonoBehaviour
    {
        [Header("Configuración de la Estación")]
        public int stationNumber = 1;
        public string stationName = "Estación 1 - Inventario (PLC)";
        public string robotTypeDescription = "KUKA KR 4 R600";
        public float distanceOnLoop = 0.85f;
        public bool isWhiteKukaStation = true;

        [Tooltip("Si está activo, el sensor magnético detiene la banda cuando llega una bandeja a esta estación.")]
        public bool sensorActive = true;

        [Tooltip("Duración del ciclo de trabajo del robot mientras la banda está detenida (segundos)")]
        public float robotWorkDuration = 3.8f;

        [Header("Estado en Tiempo Real del Sensor Magnético")]
        public StationSensorState currentState = StationSensorState.IdleWaiting;
        public bool inPlaceSignal = false;
        public bool bitID0 = false;
        public bool bitID1 = false;
        public bool bitID2 = false;
        public float workTimer = 0f;

        public TrayController currentDetectedTray;
        public RobotStationAnimator linkedRobot;

        // Referencias visuales de los LEDs de los 4 sensores magnéticos y la baliza luminosa
        [SerializeField] private Renderer _ledInPlace;
        [SerializeField] private Renderer _ledID0;
        [SerializeField] private Renderer _ledID1;
        [SerializeField] private Renderer _ledID2;
        [SerializeField] private Renderer _stackLightRed;
        [SerializeField] private Renderer _stackLightGreen;
        [SerializeField] private Light _beaconPointLight;

        // Evita re-detener inmediatamente la misma bandeja al salir del sensor
        private TrayController _lastReleasedTray;
        private float _releaseCooldown = 0f;

        public void BuildSensorVisuals(int index, string name, string robotDesc, float loopDist, bool whiteKuka, RobotStationAnimator robot)
        {
            stationNumber = index;
            stationName = name;
            robotTypeDescription = robotDesc;
            distanceOnLoop = loopDist;
            isWhiteKukaStation = whiteKuka;
            linkedRobot = robot;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            ConveyorDimensions.EvaluateLoopPath(distanceOnLoop, out Vector3 pos, out Vector3 tangent, out Vector3 outwardNormal);
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            float outwardSign = Vector3.Dot(transform.right, outwardNormal) >= 0f ? 1f : -1f;

            Material sensorPlateMat = ConveyorDimensions.GetOrCreateMaterial("SensorPlateBlack", new Color(0.10f, 0.11f, 0.13f), 0.3f, 0.6f);
            Material whiteStencil = ConveyorDimensions.GetOrCreateMaterial("SensorWhite", new Color(0.92f, 0.94f, 0.96f), 0.1f, 0.4f);
            Material coilCopperMat = ConveyorDimensions.GetOrCreateMaterial("MagneticCoil", new Color(0.78f, 0.48f, 0.20f), 0.7f, 0.6f);
            Material bracketBlack = ConveyorDimensions.GetOrCreateMaterial("BracketBlack", new Color(0.12f, 0.13f, 0.15f), 0.45f, 0.5f);

            // =================================================================================
            // 1. PLACA SUPERIOR SERIGRAFIADA EN EL CARRIL ("PALLET I.D.: ID2 | ID1 | ID0 | IN PLACE")
            // Exactamente como se observa en el primer plano de WhatsApp Image 5.41.01 PM.jpeg
            // =================================================================================
            GameObject topPlate = CreateBox(transform, "PlacaSensor_PALLET_ID",
                new Vector3(0f, 0.001f, 0f),
                new Vector3(0.088f, 0.002f, 0.24f),
                sensorPlateMat);

            // Flecha blanca indicadora en la placa del sensor
            CreateBox(topPlate.transform, "FlechaSensor",
                new Vector3(-outwardSign * 0.22f, 0.6f, 0.32f),
                new Vector3(0.18f, 0.2f, 0.16f),
                whiteStencil);

            // 4 Ventanas de lectura magnética (IN PLACE, ID0, ID1, ID2) con sus LEDs indicadores
            _ledInPlace = CreateBox(transform, "Sensor_IN_PLACE", new Vector3(outwardSign * 0.015f, 0.0025f, 0.075f), new Vector3(0.024f, 0.002f, 0.032f), whiteStencil).GetComponent<Renderer>();
            _ledID0     = CreateBox(transform, "Sensor_ID0",      new Vector3(outwardSign * 0.015f, 0.0025f, 0.025f), new Vector3(0.024f, 0.002f, 0.032f), whiteStencil).GetComponent<Renderer>();
            _ledID1     = CreateBox(transform, "Sensor_ID1",      new Vector3(outwardSign * 0.015f, 0.0025f, -0.025f), new Vector3(0.024f, 0.002f, 0.032f), whiteStencil).GetComponent<Renderer>();
            _ledID2     = CreateBox(transform, "Sensor_ID2",      new Vector3(outwardSign * 0.015f, 0.0025f, -0.075f), new Vector3(0.024f, 0.002f, 0.032f), whiteStencil).GetComponent<Renderer>();

            // =================================================================================
            // 2. SENSORES MAGNÉTICOS / IMANES POR DEBAJO DE LA BANDA (Visibles bajo el riel)
            // =================================================================================
            GameObject underBeltHousing = new GameObject("SensoresMagneticos_BajoBanda");
            underBeltHousing.transform.SetParent(transform, false);
            underBeltHousing.transform.localPosition = new Vector3(0f, -0.055f, 0f);

            CreateBox(underBeltHousing.transform, "CajaSensoresMagneticos",
                Vector3.zero,
                new Vector3(0.11f, 0.042f, 0.22f),
                bracketBlack);

            for (int s = 0; s < 4; s++)
            {
                float zOff = 0.075f - s * 0.050f;
                CreateCylinder(underBeltHousing.transform, $"BobinaIman_{s}",
                    new Vector3(0f, 0.018f, zOff),
                    new Vector3(0.024f, 0.018f, 0.024f),
                    coilCopperMat);
            }

            // =================================================================================
            // 3. PLATAFORMA / REPISA LATERAL DE ESTACIÓN (Lado exterior hacia donde están los robots)
            // =================================================================================
            GameObject sideBuffer = new GameObject("PlataformaTransferenciaLateral");
            sideBuffer.transform.SetParent(transform, false);
            sideBuffer.transform.localPosition = new Vector3(outwardSign * (ConveyorDimensions.TrackWidth * 0.5f + 0.105f), -0.012f, 0f);

            CreateBox(sideBuffer.transform, "BandejaEstacionLateral",
                Vector3.zero,
                new Vector3(0.20f, 0.016f, 0.24f),
                bracketBlack);
            CreateBox(sideBuffer.transform, "TopeGuia1", new Vector3(outwardSign * 0.08f, 0.016f, 0.10f), new Vector3(0.03f, 0.018f, 0.015f), bracketBlack);
            CreateBox(sideBuffer.transform, "TopeGuia2", new Vector3(outwardSign * 0.08f, 0.016f, -0.10f), new Vector3(0.03f, 0.018f, 0.015f), bracketBlack);

            // =================================================================================
            // 4. TORRETA / BALIZA LUMINOSA (ROJA / VERDE) EN EL BORDE INTERIOR DE LA BANDA
            // Exactamente como las balizas de cada estación en las fotos del laboratorio
            // =================================================================================
            GameObject stackLightGroup = new GameObject("BalizaLuminosa_Estacion");
            stackLightGroup.transform.SetParent(transform, false);
            stackLightGroup.transform.localPosition = new Vector3(-outwardSign * (ConveyorDimensions.TrackWidth * 0.5f + 0.09f), -0.02f, 0.08f);

            // Brazo soporte horizontal
            CreateBox(stackLightGroup.transform, "BrazoSoporteBaliza", new Vector3(outwardSign * 0.045f, 0f, 0f), new Vector3(0.10f, 0.018f, 0.025f), bracketBlack);
            // Tubo vertical plateado
            CreateCylinder(stackLightGroup.transform, "PosteBaliza", new Vector3(0f, 0.06f, 0f), new Vector3(0.022f, 0.06f, 0.022f), whiteStencil);
            // Base negra de la torreta
            CreateCylinder(stackLightGroup.transform, "BaseTorreta", new Vector3(0f, 0.12f, 0f), new Vector3(0.052f, 0.022f, 0.052f), bracketBlack);
            // Lente Verde (inferior)
            _stackLightGreen = CreateCylinder(stackLightGroup.transform, "LenteVerde", new Vector3(0f, 0.165f, 0f), new Vector3(0.052f, 0.026f, 0.052f), bracketBlack).GetComponent<Renderer>();
            // Lente Rojo (superior)
            _stackLightRed = CreateCylinder(stackLightGroup.transform, "LenteRojo", new Vector3(0f, 0.215f, 0f), new Vector3(0.052f, 0.026f, 0.052f), bracketBlack).GetComponent<Renderer>();
            // Tapa superior negra
            CreateCylinder(stackLightGroup.transform, "TapaTorreta", new Vector3(0f, 0.245f, 0f), new Vector3(0.053f, 0.006f, 0.053f), bracketBlack);

            // Luz puntual suave para que se note el encendido de la baliza
            GameObject lightGo = new GameObject("LuzBaliza");
            lightGo.transform.SetParent(stackLightGroup.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            _beaconPointLight = lightGo.AddComponent<Light>();
            _beaconPointLight.type = LightType.Point;
            _beaconPointLight.range = 0.85f;
            _beaconPointLight.intensity = 1.2f;

            UpdateVisualIndicators();
        }

        public void TickCooldown(float dt)
        {
            if (_releaseCooldown > 0f)
            {
                _releaseCooldown -= dt;
                if (_releaseCooldown <= 0f)
                {
                    _lastReleasedTray = null;
                }
            }
            UpdateVisualIndicators();
        }

        public bool CanTriggerForTray(TrayController tray)
        {
            if (!sensorActive || tray == null) return false;
            if (tray == _lastReleasedTray && _releaseCooldown > 0f) return false;

            float perimeter = ConveyorDimensions.TotalLoopPerimeter;
            float diff = Mathf.Abs(tray.distanceOnLoop - distanceOnLoop);
            if (diff > perimeter * 0.5f) diff = perimeter - diff;

            // Ventana magnética de detección del sensor IN PLACE (~2.2 cm)
            return diff <= 0.022f;
        }

        /// <summary>
        /// Se invoca cuando el imán inferior de la bandeja activa el sensor magnético de esta estación.
        /// Evalúa si la bandeja viene normal, sin pieza (ADVERTENCIA) o volteada/mal posicionada (ERROR).
        /// </summary>
        public void LatchTrayAtSensor(TrayController tray, out StationSensorState resultingState, out string eventMessage)
        {
            currentDetectedTray = tray;
            // Alinear exactamente la bandeja con el sensor magnético IN PLACE
            tray.distanceOnLoop = distanceOnLoop;
            tray.UpdateTransformOnBelt();

            inPlaceSignal = true;
            bitID0 = tray.BitID0;
            bitID1 = tray.BitID1;
            bitID2 = tray.BitID2;
            workTimer = 0f;

            // 1. Verificar primero si la bandeja está VOLTEADA o en POSICIÓN INCORRECTA -> ERROR CRÍTICO
            if (tray.HasOrientationError)
            {
                currentState = StationSensorState.ErrorBadTrayOrientation;
                resultingState = currentState;
                string cause = tray.orientationState == TrayOrientationState.VolteadaBocaAbajo
                    ? "BANDEJA VOLTEADA BOCA ABAJO (180° - ranuras de 2.3 y 1.7 cm hacia arriba)"
                    : "BANDEJA MAL POSICIONADA / GIRADA 90° (fuera de alcance del gripper)";

                eventMessage = $"[ERROR EN SENSOR #{stationNumber} - {stationName}] Banda detenida de emergencia: {cause}. El robot ({robotTypeDescription}) NO puede tomar la bandeja #{tray.palletId}.";
                if (linkedRobot != null) linkedRobot.SetWorkingState(false, 0f);
                UpdateVisualIndicators();
                return;
            }

            // 2. Verificar si la bandeja va SOLA SIN PIEZA a trabajar -> ADVERTENCIA (WARNING)
            if (tray.HasMissingPieceWarning)
            {
                currentState = StationSensorState.WarningEmptyTray;
                resultingState = currentState;
                string warnCause = tray.loadState == TrayLoadState.SoloBandejaInferior
                    ? "Solo viaja la bandeja inferior (20x16 cm) sin bandeja superior 010004 ni pieza"
                    : "La bandeja viaja sola SIN PIEZA de trabajo sobre la matriz de 17.5x12.5 cm";

                eventMessage = $"[ADVERTENCIA EN SENSOR #{stationNumber} - {stationName}] Sensor magnético detectó Bandeja #{tray.palletId} (ID={tray.GetBinaryIdString()}), pero {warnCause}.";
                if (linkedRobot != null) linkedRobot.SetWorkingState(false, 0f);
                UpdateVisualIndicators();
                return;
            }

            // 3. Bandeja en posición correcta y con pieza -> La banda se detiene para que el robot trabaje
            currentState = StationSensorState.WorkingRobot;
            resultingState = currentState;
            eventMessage = $"[PARADA EN SENSOR #{stationNumber} - {stationName}] Sensor magnético IN PLACE activado (Bandeja #{tray.palletId}, ID={tray.GetBinaryIdString()}). Banda detenida; {robotTypeDescription} trabajando...";
            if (linkedRobot != null) linkedRobot.SetWorkingState(true, 0f);
            UpdateVisualIndicators();
        }

        /// <summary>
        /// Libera la bandeja actual después de que el robot termina su trabajo o tras resolver un error/advertencia.
        /// </summary>
        public void ReleaseCurrentTray()
        {
            if (currentDetectedTray != null)
            {
                _lastReleasedTray = currentDetectedTray;
                _releaseCooldown = 1.6f;
            }
            currentDetectedTray = null;
            inPlaceSignal = false;
            bitID0 = false;
            bitID1 = false;
            bitID2 = false;
            workTimer = 0f;
            currentState = sensorActive ? StationSensorState.IdleWaiting : StationSensorState.StationStandbyDisabled;
            if (linkedRobot != null) linkedRobot.SetWorkingState(false, 0f);
            UpdateVisualIndicators();
        }

        public void UpdateVisualIndicators()
        {
            if (!sensorActive && currentDetectedTray == null)
            {
                currentState = StationSensorState.StationStandbyDisabled;
            }
            else if (sensorActive && currentDetectedTray == null)
            {
                currentState = StationSensorState.IdleWaiting;
            }

            Material ledOff = ConveyorDimensions.GetOrCreateMaterial("SensorLedOff", new Color(0.22f, 0.24f, 0.26f), 0.2f, 0.4f);
            Material ledOnCyan = ConveyorDimensions.GetOrCreateMaterial("SensorLedOnCyan", new Color(0.1f, 0.95f, 1.0f), 0.1f, 0.9f, true, new Color(0.1f, 0.9f, 1f) * 2.2f);
            Material ledOnYellow = ConveyorDimensions.GetOrCreateMaterial("SensorLedOnYellow", new Color(1.0f, 0.85f, 0.1f), 0.1f, 0.9f, true, new Color(1f, 0.85f, 0.1f) * 2.0f);

            if (_ledInPlace != null) _ledInPlace.sharedMaterial = inPlaceSignal ? ledOnCyan : ledOff;
            if (_ledID0 != null) _ledID0.sharedMaterial = bitID0 ? ledOnYellow : ledOff;
            if (_ledID1 != null) _ledID1.sharedMaterial = bitID1 ? ledOnYellow : ledOff;
            if (_ledID2 != null) _ledID2.sharedMaterial = bitID2 ? ledOnYellow : ledOff;

            Material redOff = ConveyorDimensions.GetOrCreateMaterial("BeaconRedOff", new Color(0.32f, 0.08f, 0.08f), 0.2f, 0.7f);
            Material greenOff = ConveyorDimensions.GetOrCreateMaterial("BeaconGreenOff", new Color(0.08f, 0.28f, 0.10f), 0.2f, 0.7f);
            Material redOn = ConveyorDimensions.GetOrCreateMaterial("BeaconRedOn", new Color(1.0f, 0.12f, 0.12f), 0.1f, 0.9f, true, new Color(1f, 0.1f, 0.1f) * 2.6f);
            Material amberOn = ConveyorDimensions.GetOrCreateMaterial("BeaconAmberOn", new Color(1.0f, 0.68f, 0.05f), 0.1f, 0.9f, true, new Color(1f, 0.65f, 0.0f) * 2.4f);
            Material greenOn = ConveyorDimensions.GetOrCreateMaterial("BeaconGreenOn", new Color(0.12f, 0.98f, 0.28f), 0.1f, 0.9f, true, new Color(0.1f, 0.95f, 0.25f) * 2.2f);

            bool blink = Mathf.Sin(Time.time * 10f) > 0f;

            if (_stackLightRed != null && _stackLightGreen != null)
            {
                switch (currentState)
                {
                    case StationSensorState.ErrorBadTrayOrientation:
                        _stackLightRed.sharedMaterial = blink ? redOn : redOff;
                        _stackLightGreen.sharedMaterial = greenOff;
                        if (_beaconPointLight != null) { _beaconPointLight.color = Color.red; _beaconPointLight.intensity = blink ? 2.2f : 0.2f; }
                        break;

                    case StationSensorState.WarningEmptyTray:
                        _stackLightRed.sharedMaterial = blink ? amberOn : redOff;
                        _stackLightGreen.sharedMaterial = greenOn;
                        if (_beaconPointLight != null) { _beaconPointLight.color = new Color(1f, 0.7f, 0.1f); _beaconPointLight.intensity = 1.6f; }
                        break;

                    case StationSensorState.WorkingRobot:
                        _stackLightRed.sharedMaterial = redOn;
                        _stackLightGreen.sharedMaterial = greenOn;
                        if (_beaconPointLight != null) { _beaconPointLight.color = Color.cyan; _beaconPointLight.intensity = 1.5f; }
                        break;

                    case StationSensorState.IdleWaiting:
                        _stackLightRed.sharedMaterial = redOff;
                        _stackLightGreen.sharedMaterial = greenOn;
                        if (_beaconPointLight != null) { _beaconPointLight.color = Color.green; _beaconPointLight.intensity = 0.8f; }
                        break;

                    case StationSensorState.StationStandbyDisabled:
                        _stackLightRed.sharedMaterial = redOff;
                        _stackLightGreen.sharedMaterial = greenOff;
                        if (_beaconPointLight != null) { _beaconPointLight.intensity = 0f; }
                        break;
                }
            }
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

