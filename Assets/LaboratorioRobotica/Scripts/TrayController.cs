using UnityEngine;

namespace LaboratorioRobotica
{
    /// <summary>
    /// Estado de orientación física de la bandeja sobre la cinta transportadora.
    /// </summary>
    public enum TrayOrientationState
    {
        /// <summary>Posición correcta: cara superior hacia arriba, flecha blanca en sentido de la banda, manija T accesible.</summary>
        Correcta = 0,

        /// <summary>ERROR: Bandeja volteada boca abajo (180° en Roll), dejando expuestas las ranuras guía de 2.3 cm y 1.7 cm. El robot no puede tomarla.</summary>
        VolteadaBocaAbajo = 1,

        /// <summary>ERROR: Bandeja rotada/atravesada (90° o 180° respecto al eje de agarre), el robot no puede sujetar la manija T ni la pieza.</summary>
        MalPosicionada = 2
    }

    /// <summary>
    /// Estado de carga de la bandeja al llegar a un sensor magnético de estación.
    /// </summary>
    public enum TrayLoadState
    {
        /// <summary>Normal: Lleva bandeja superior perforada (010004) + pieza/cubo de trabajo lista para el robot.</summary>
        ConPieza = 0,

        /// <summary>ADVERTENCIA: La bandeja va sola sin pieza a trabajar sobre la matriz perforada.</summary>
        SinPieza = 1,

        /// <summary>ADVERTENCIA: Solo viaja la bandeja inferior (20x16 cm) sin la bandeja portapiezas superior ni pieza.</summary>
        SoloBandejaInferior = 2
    }

    /// <summary>
    /// Controla el modelado 3D a escala real (PDF Medida Cinta) y el movimiento/estado de cada bandeja (Pallet) en la banda.
    /// - Bandeja Inferior: 20 cm x 16 cm x 1.9 cm (ranuras inferiores de 2.3 cm y 1.7 cm, imanes codificadores ID0, ID1, ID2, IN PLACE)
    /// - Bandeja Superior: 17.5 cm x 12.5 cm con manija en T (3.5 cm alto, 4.5 cm ancho superior, 2.3 cm base)
    /// </summary>
    public class TrayController : MonoBehaviour
    {
        [Header("Identificación Magnética (PALLET I.D.)")]
        [Tooltip("Número identificador de la bandeja (1..7) leído por los sensores magnéticos ID0, ID1, ID2")]
        [Range(1, 7)]
        public int palletId = 1;

        public string trayCode = "010004";

        [Header("Posición en la Cinta (metros)")]
        public float distanceOnLoop = 0f;

        [Header("Estados de Simulación (Condiciones de Error / Advertencia)")]
        public TrayOrientationState orientationState = TrayOrientationState.Correcta;
        public TrayLoadState loadState = TrayLoadState.ConPieza;

        [Header("Estado de Procesamiento")]
        public int operationsCompleted = 0;
        public string lastStationProcessed = "Ninguna";

        // Referencias visuales internas para animar volteo/rotación/carga en tiempo real
        private Transform _visualPivot;
        private GameObject _upperTrayGroup;
        private GameObject _workpieceObject;
        private GameObject _statusBadgeObject;
        private Renderer _statusBadgeRenderer;
        private Renderer _workpieceRenderer;

        public bool HasOrientationError => orientationState != TrayOrientationState.Correcta;
        public bool HasMissingPieceWarning => loadState != TrayLoadState.ConPieza;

        /// <summary>Bits binarios leídos por los sensores Hall/magnéticos bajo la banda</summary>
        public bool BitID0 => (palletId & 1) != 0;
        public bool BitID1 => (palletId & 2) != 0;
        public bool BitID2 => (palletId & 4) != 0;

        public string GetBinaryIdString()
        {
            return $"{(BitID2 ? 1 : 0)}{(BitID1 ? 1 : 0)}{(BitID0 ? 1 : 0)}";
        }

        public string GetDiagnosticDescription()
        {
            if (orientationState == TrayOrientationState.VolteadaBocaAbajo)
            {
                return $"ERROR CRÍTICO: Bandeja #{palletId} VOLTEADA (180°). Ranuras inferiores (2.3 cm / 1.7 cm) hacia arriba; el robot no puede tomar la manija T.";
            }
            if (orientationState == TrayOrientationState.MalPosicionada)
            {
                return $"ERROR DE POSICIÓN: Bandeja #{palletId} DESALINEADA / ROTADA (90°). Fuera de geometría de agarre del gripper del robot.";
            }
            if (loadState == TrayLoadState.SinPieza)
            {
                return $"ADVERTENCIA: Bandeja #{palletId} VIAJA SOLA SIN PIEZA a trabajar sobre la bandeja perforada (17.5x12.5 cm).";
            }
            if (loadState == TrayLoadState.SoloBandejaInferior)
            {
                return $"ADVERTENCIA: Bandeja #{palletId} INCOMPLETA (solo base inferior de 20x16 cm, sin bandeja superior 010004 ni pieza).";
            }
            return $"OK: Bandeja #{palletId} (ID binario {GetBinaryIdString()}) en posición correcta y con pieza lista.";
        }

        /// <summary>
        /// Construye la geometría 3D detallada a escala exacta 1:1 según el PDF "Medida Cinta".
        /// </summary>
        public void BuildDetailedGeometry(int id, float initialDistance, TrayOrientationState initOrientation, TrayLoadState initLoad)
        {
            palletId = id;
            trayCode = $"01000{id + 3}";
            distanceOnLoop = initialDistance;
            orientationState = initOrientation;
            loadState = initLoad;

            // Limpiar hijos previos si se reconstruye
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            _visualPivot = new GameObject("VisualPivot").transform;
            _visualPivot.SetParent(transform, false);
            _visualPivot.localPosition = Vector3.zero;

            Material blackAnodized = ConveyorDimensions.GetOrCreateMaterial("TrayBlackAnodized", new Color(0.13f, 0.14f, 0.16f), 0.45f, 0.65f);
            Material darkGrooveMat = ConveyorDimensions.GetOrCreateMaterial("TrayGrooveDark", new Color(0.06f, 0.06f, 0.07f), 0.3f, 0.3f);
            Material whiteStencilMat = ConveyorDimensions.GetOrCreateMaterial("WhiteStencil", new Color(0.92f, 0.94f, 0.96f), 0.1f, 0.4f);
            Material steelPinMat = ConveyorDimensions.GetOrCreateMaterial("SteelPin", new Color(0.76f, 0.79f, 0.83f), 0.85f, 0.85f);
            Material magnetSensorMat = ConveyorDimensions.GetOrCreateMaterial("MagnetPin", new Color(0.85f, 0.62f, 0.15f), 0.7f, 0.7f);
            Material woodPieceMat = ConveyorDimensions.GetOrCreateMaterial("WoodWorkpiece", new Color(0.82f, 0.67f, 0.44f), 0.05f, 0.35f);
            Material barcodeMat = ConveyorDimensions.GetOrCreateMaterial("BarcodeSticker", new Color(0.95f, 0.93f, 0.86f), 0.05f, 0.3f);

            // =================================================================================
            // A. BANDEJA INFERIOR (20 cm largo x 16 cm ancho x 1.9 cm alto) - Página 1 del PDF
            // En nuestro sistema local de la bandeja:
            // Eje Z local = sentido de avance sobre la cinta (Largo = 20 cm = 0.20 m)
            // Eje X local = transversal al carril (Ancho = 16 cm = 0.16 m)
            // Eje Y local = espesor vertical (Alto = 1.9 cm = 0.019 m)
            // =================================================================================
            GameObject lowerTray = new GameObject("BandejaInferior_20x16x1.9cm");
            lowerTray.transform.SetParent(_visualPivot, false);

            // Placa principal de 20 cm x 16 cm x 1.9 cm
            CreateBox(lowerTray.transform, "PlacaBase_20x16x1.9cm",
                new Vector3(0f, ConveyorDimensions.LowerTrayHeight * 0.5f, 0f),
                new Vector3(ConveyorDimensions.LowerTrayWidth, ConveyorDimensions.LowerTrayHeight, ConveyorDimensions.LowerTrayLength),
                blackAnodized);

            // Flecha blanca direccional superior (indica sentido correcto de la bandeja)
            CreateBox(lowerTray.transform, "FlechaBlanca_Tallo",
                new Vector3(-0.015f, ConveyorDimensions.LowerTrayHeight + 0.0006f, -0.008f),
                new Vector3(0.008f, 0.001f, 0.036f),
                whiteStencilMat);
            CreateBox(lowerTray.transform, "FlechaBlanca_Punta",
                new Vector3(-0.015f, ConveyorDimensions.LowerTrayHeight + 0.0006f, 0.016f),
                new Vector3(0.022f, 0.001f, 0.014f),
                whiteStencilMat);

            // 2 Esquineros en "L" en esquinas opuestas (como se ve en Página 1 del PDF y fotos)
            float cornerHeight = 0.018f;
            float cornerThick = 0.008f;
            float cornerArmLen = 0.045f;
            float halfW = ConveyorDimensions.LowerTrayWidth * 0.5f;   // 0.08 m
            float halfL = ConveyorDimensions.LowerTrayLength * 0.5f;  // 0.10 m

            // Esquinero L delantero derecho (+X, +Z)
            CreateBox(lowerTray.transform, "EsquineroL_1A",
                new Vector3(halfW - cornerThick * 0.5f, ConveyorDimensions.LowerTrayHeight + cornerHeight * 0.5f, halfL - cornerArmLen * 0.5f),
                new Vector3(cornerThick, cornerHeight, cornerArmLen), blackAnodized);
            CreateBox(lowerTray.transform, "EsquineroL_1B",
                new Vector3(halfW - cornerArmLen * 0.5f, ConveyorDimensions.LowerTrayHeight + cornerHeight * 0.5f, halfL - cornerThick * 0.5f),
                new Vector3(cornerArmLen, cornerHeight, cornerThick), blackAnodized);

            // Esquinero L trasero izquierdo (-X, -Z)
            CreateBox(lowerTray.transform, "EsquineroL_2A",
                new Vector3(-halfW + cornerThick * 0.5f, ConveyorDimensions.LowerTrayHeight + cornerHeight * 0.5f, -halfL + cornerArmLen * 0.5f),
                new Vector3(cornerThick, cornerHeight, cornerArmLen), blackAnodized);
            CreateBox(lowerTray.transform, "EsquineroL_2B",
                new Vector3(-halfW + cornerArmLen * 0.5f, ConveyorDimensions.LowerTrayHeight + cornerHeight * 0.5f, -halfL + cornerThick * 0.5f),
                new Vector3(cornerArmLen, cornerHeight, cornerThick), blackAnodized);

            // Cara Inferior de la Bandeja (Ranuras de 2.3 cm y 1.7 cm + Imanes de sensor)
            // Estas ranuras quedan visibles hacia arriba cuando la bandeja está VOLTEADA (Error)
            CreateBox(lowerTray.transform, "RanuraInferiorCentral_2.3cm",
                new Vector3(0f, 0.0015f, 0f),
                new Vector3(ConveyorDimensions.LowerTrayMainGrooveWidth, 0.004f, ConveyorDimensions.LowerTrayLength * 0.98f),
                darkGrooveMat);

            CreateBox(lowerTray.transform, "RanuraInferiorSecundaria_1.7cm_Izq",
                new Vector3(-0.035f, 0.0015f, 0f),
                new Vector3(ConveyorDimensions.LowerTraySecondaryGrooveWidth, 0.004f, ConveyorDimensions.LowerTrayLength * 0.96f),
                darkGrooveMat);

            CreateBox(lowerTray.transform, "RanuraInferiorSecundaria_1.7cm_Der",
                new Vector3(0.035f, 0.0015f, 0f),
                new Vector3(ConveyorDimensions.LowerTraySecondaryGrooveWidth, 0.004f, ConveyorDimensions.LowerTrayLength * 0.96f),
                darkGrooveMat);

            // Imanes/Pines metálicos inferiores que activan los sensores magnéticos bajo la banda (IN PLACE, ID0, ID1, ID2)
            for (int m = 0; m < 4; m++)
            {
                float xPos = -0.045f + m * 0.030f;
                CreateCylinder(lowerTray.transform, $"ImanSensor_Pin_{m}",
                    new Vector3(xPos, 0.002f, 0.055f),
                    new Vector3(0.012f, 0.003f, 0.012f),
                    magnetSensorMat);
            }

            // =================================================================================
            // B. BANDEJA SUPERIOR PERFORADA ESHED ROBOTEC 010004 (17.5 cm x 12.5 cm) - Pág. 3 PDF
            // =================================================================================
            _upperTrayGroup = new GameObject("BandejaSuperior_17.5x12.5cm_010004");
            _upperTrayGroup.transform.SetParent(_visualPivot, false);
            _upperTrayGroup.transform.localPosition = new Vector3(0f, ConveyorDimensions.LowerTrayHeight, 0f);

            // Cuerpo de la bandeja superior (17.5 cm x 12.5 cm x 1.6 cm)
            CreateBox(_upperTrayGroup.transform, "CuerpoPerforado_17.5x12.5cm",
                new Vector3(0f, ConveyorDimensions.UpperTrayHeight * 0.5f, 0f),
                new Vector3(ConveyorDimensions.UpperTrayWidth, ConveyorDimensions.UpperTrayHeight, ConveyorDimensions.UpperTrayLength),
                blackAnodized);

            // Etiqueta de código de barras lateral "010004 ESHED ROBOTEC" (foto WhatsApp 7.46.15 AM)
            CreateBox(_upperTrayGroup.transform, "EtiquetaCodigoBarras_010004",
                new Vector3(ConveyorDimensions.UpperTrayWidth * 0.5f + 0.0005f, ConveyorDimensions.UpperTrayHeight * 0.5f, 0f),
                new Vector3(0.001f, 0.010f, 0.095f),
                barcodeMat);

            // Matriz de perforaciones (representada visualmente en la cara superior)
            int cols = 6;
            int rows = 9;
            float spacingX = 0.016f;
            float spacingZ = 0.016f;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float hx = (c - (cols - 1) * 0.5f) * spacingX;
                    float hz = (r - (rows - 1) * 0.5f) * spacingZ;
                    // Dejar espacio central para la manija T
                    if (Mathf.Abs(hx) < 0.018f && hz > 0.02f) continue;

                    CreateCylinder(_upperTrayGroup.transform, $"Hole_{r}_{c}",
                        new Vector3(hx, ConveyorDimensions.UpperTrayHeight + 0.0003f, hz),
                        new Vector3(0.0055f, 0.0006f, 0.0055f),
                        darkGrooveMat);
                }
            }

            // 4 Pines metálicos cónicos de posicionamiento sobre la bandeja perforada
            Vector3[] pinPositions =
            {
                new Vector3(-0.042f, ConveyorDimensions.UpperTrayHeight + 0.010f, -0.032f),
                new Vector3( 0.042f, ConveyorDimensions.UpperTrayHeight + 0.010f, -0.032f),
                new Vector3(-0.042f, ConveyorDimensions.UpperTrayHeight + 0.010f,  0.032f),
                new Vector3( 0.042f, ConveyorDimensions.UpperTrayHeight + 0.010f,  0.032f)
            };
            for (int p = 0; p < pinPositions.Length; p++)
            {
                CreateCylinder(_upperTrayGroup.transform, $"PinMetalico_{p}",
                    pinPositions[p],
                    new Vector3(0.007f, 0.010f, 0.007f),
                    steelPinMat);
            }

            // Manija en "T" de agarre robótico (3.5 cm alto, 4.5 cm travesaño superior, 2.3 cm cuello) - Pág. 3 PDF
            GameObject tHandle = new GameObject("Manija_T_3.5x4.5x2.3cm");
            tHandle.transform.SetParent(_upperTrayGroup.transform, false);
            tHandle.transform.localPosition = new Vector3(0f, ConveyorDimensions.UpperTrayHeight, 0.048f);

            // Cuello vertical de 2.3 cm de ancho x 3.5 cm de alto
            CreateBox(tHandle.transform, "CuelloManija_2.3cm",
                new Vector3(0f, ConveyorDimensions.THandleHeight * 0.5f, 0f),
                new Vector3(ConveyorDimensions.THandleStemWidth, ConveyorDimensions.THandleHeight, 0.014f),
                blackAnodized);

            // Travesaño superior de 4.5 cm de ancho
            CreateBox(tHandle.transform, "TravesanoSuperior_4.5cm",
                new Vector3(0f, ConveyorDimensions.THandleHeight - 0.004f, 0f),
                new Vector3(ConveyorDimensions.THandleTopWidth, 0.009f, 0.020f),
                blackAnodized);

            // =================================================================================
            // C. PIEZA DE TRABAJO (Bloque/Cubo de madera con marcador fiducial)
            // =================================================================================
            _workpieceObject = new GameObject("PiezaDeTrabajo_CuboMadera");
            _workpieceObject.transform.SetParent(_upperTrayGroup.transform, false);
            _workpieceObject.transform.localPosition = new Vector3(0f, ConveyorDimensions.UpperTrayHeight, -0.018f);

            GameObject cubeMesh = CreateBox(_workpieceObject.transform, "BloqueMadera_5x5x3.8cm",
                new Vector3(0f, 0.019f, 0f),
                new Vector3(0.052f, 0.038f, 0.052f),
                woodPieceMat);
            _workpieceRenderer = cubeMesh.GetComponent<Renderer>();

            // Marcador fiducial ArUco blanco/negro sobre la pieza (como en los videos 5.41.00 y 5.41.02)
            CreateBox(_workpieceObject.transform, "MarcadorArUco_FondoBlanco",
                new Vector3(0f, 0.0385f, 0f),
                new Vector3(0.034f, 0.001f, 0.034f),
                whiteStencilMat);
            CreateBox(_workpieceObject.transform, "MarcadorArUco_PatronNegro",
                new Vector3(0.003f, 0.0391f, -0.002f),
                new Vector3(0.020f, 0.001f, 0.020f),
                darkGrooveMat);

            // =================================================================================
            // D. INDICADOR FLOTANTE DISCRETO DE ESTADO SOBRE LA BANDEJA
            // =================================================================================
            _statusBadgeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _statusBadgeObject.name = "IndicadorEstadoBandeja";
            _statusBadgeObject.transform.SetParent(transform, false);
            _statusBadgeObject.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            _statusBadgeObject.transform.localScale = new Vector3(0.022f, 0.022f, 0.022f);
            Collider col = _statusBadgeObject.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            _statusBadgeRenderer = _statusBadgeObject.GetComponent<Renderer>();

            RefreshVisualState();
            UpdateTransformOnBelt();
        }

        /// <summary>
        /// Actualiza la orientación visual (normal, volteada 180°, o mal posicionada 90°)
        /// y la presencia o ausencia de la pieza de trabajo.
        /// </summary>
        public void RefreshVisualState()
        {
            if (_visualPivot == null) return;

            // 1. Orientación de la bandeja
            switch (orientationState)
            {
                case TrayOrientationState.Correcta:
                    _visualPivot.localPosition = Vector3.zero;
                    _visualPivot.localRotation = Quaternion.identity;
                    break;

                case TrayOrientationState.VolteadaBocaAbajo:
                    // Volteada 180 grados: la cara inferior con ranuras de 2.3 cm y 1.7 cm queda hacia arriba
                    _visualPivot.localPosition = new Vector3(0f, ConveyorDimensions.LowerTrayHeight + 0.015f, 0f);
                    _visualPivot.localRotation = Quaternion.Euler(0f, 0f, 180f);
                    break;

                case TrayOrientationState.MalPosicionada:
                    // Girada 90 grados + desplazada: el robot no puede tomar la manija T en la posición esperada
                    _visualPivot.localPosition = new Vector3(0.01f, 0f, 0f);
                    _visualPivot.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    break;
            }

            // 2. Presencia de pieza / bandeja superior
            if (_upperTrayGroup != null)
            {
                _upperTrayGroup.SetActive(loadState != TrayLoadState.SoloBandejaInferior);
            }
            if (_workpieceObject != null)
            {
                _workpieceObject.SetActive(loadState == TrayLoadState.ConPieza);
            }

            // 3. Color del indicador de diagnóstico
            if (_statusBadgeRenderer != null)
            {
                if (HasOrientationError)
                {
                    _statusBadgeRenderer.sharedMaterial = ConveyorDimensions.GetOrCreateMaterial(
                        "BadgeErrorRed", new Color(0.95f, 0.15f, 0.15f), 0.1f, 0.8f, true, new Color(1f, 0.1f, 0.1f) * 2f);
                }
                else if (HasMissingPieceWarning)
                {
                    _statusBadgeRenderer.sharedMaterial = ConveyorDimensions.GetOrCreateMaterial(
                        "BadgeWarnAmber", new Color(1.0f, 0.72f, 0.05f), 0.1f, 0.8f, true, new Color(1f, 0.65f, 0.0f) * 1.8f);
                }
                else
                {
                    _statusBadgeRenderer.sharedMaterial = ConveyorDimensions.GetOrCreateMaterial(
                        "BadgeOkGreen", new Color(0.15f, 0.90f, 0.30f), 0.1f, 0.8f, true, new Color(0.1f, 0.85f, 0.25f) * 1.5f);
                }
            }
        }

        /// <summary>
        /// Posiciona y orienta la bandeja siguiendo exactamente el circuito cerrado de 484.5 cm x 138.5 cm.
        /// </summary>
        public void UpdateTransformOnBelt()
        {
            ConveyorDimensions.EvaluateLoopPath(distanceOnLoop, out Vector3 pos, out Vector3 tangent, out _);
            transform.position = pos;
            if (tangent.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            }
        }

        public void SetPieceWorkedColor(Color tint)
        {
            if (_workpieceRenderer != null)
            {
                _workpieceRenderer.sharedMaterial = ConveyorDimensions.GetOrCreateMaterial(
                    $"WorkedPiece_{tint}", tint, 0.15f, 0.45f);
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

