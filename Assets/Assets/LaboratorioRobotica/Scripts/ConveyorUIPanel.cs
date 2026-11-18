using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LaboratorioRobotica
{
    /// <summary>
    /// Controlador del Panel UI (Unity UI / Canvas + TextMeshPro) sencillo para la banda transportadora.
    /// Diseñado para resolución de referencia Full HD (1920x1080).
    /// Se guarda como Prefab en Assets/LaboratorioRobotica/Prefabs/Canvas_PanelControlBanda.prefab.
    /// </summary>
    public class ConveyorUIPanel : MonoBehaviour
    {
        /// <summary>Versión del diseño del panel. Si cambia, el editor regenera el Prefab automáticamente.</summary>
        public const int LayoutVersion = 2;

        [Header("Referencia al Gestor de la Banda")]
        public ConveyorSimulationManager simulationManager;

        [Header("Referencias UI (Canvas + TextMeshPro)")]
        public TMP_Text statusText;
        public Image statusIndicator;
        public Button toggleBeltButton;
        public TMP_Text toggleBeltButtonText;
        public Button simulateSensorButton;
        public TMP_Text simulateSensorButtonText;
        public Slider speedSlider;
        public TMP_Text speedValueText;

        [HideInInspector] public int builtLayoutVersion = 0;

        // Colores del panel
        private static readonly Color ColorPanel = new Color(0.10f, 0.12f, 0.16f, 0.94f);
        private static readonly Color ColorTitle = new Color(1f, 1f, 1f, 1f);
        private static readonly Color ColorText = new Color(0.88f, 0.92f, 0.97f, 1f);
        private static readonly Color ColorRunning = new Color(0.20f, 0.85f, 0.40f, 1f);
        private static readonly Color ColorPaused = new Color(0.65f, 0.68f, 0.72f, 1f);
        private static readonly Color ColorSensor = new Color(1.00f, 0.70f, 0.15f, 1f);
        private static readonly Color ColorBtnBlue = new Color(0.18f, 0.50f, 0.88f, 1f);
        private static readonly Color ColorBtnGreen = new Color(0.16f, 0.66f, 0.38f, 1f);

        private void Awake()
        {
            AutoResolveReferences();
            RegisterUIEvents();
        }

        private void Start()
        {
            EnsureEventSystemExists();
            AutoResolveReferences();
            if (simulationManager != null && speedSlider != null)
            {
                speedSlider.SetValueWithoutNotify(simulationManager.beltSpeedMetersPerSec);
            }
            RefreshUIState();
        }

        private void Update()
        {
            RefreshUIState();
        }

        public void AutoResolveReferences()
        {
            if (simulationManager == null)
            {
                simulationManager = FindAnyObjectByType<ConveyorSimulationManager>();
            }
        }

        private void RegisterUIEvents()
        {
            if (toggleBeltButton != null && toggleBeltButton.onClick.GetPersistentEventCount() == 0)
            {
                toggleBeltButton.onClick.RemoveListener(OnToggleBeltClicked);
                toggleBeltButton.onClick.AddListener(OnToggleBeltClicked);
            }

            if (simulateSensorButton != null && simulateSensorButton.onClick.GetPersistentEventCount() == 0)
            {
                simulateSensorButton.onClick.RemoveListener(OnSimulateSensorClicked);
                simulateSensorButton.onClick.AddListener(OnSimulateSensorClicked);
            }

            if (speedSlider != null && speedSlider.onValueChanged.GetPersistentEventCount() == 0)
            {
                speedSlider.onValueChanged.RemoveListener(OnSpeedSliderChanged);
                speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
            }
        }

        public void OnToggleBeltClicked()
        {
            AutoResolveReferences();
            if (simulationManager == null) return;

            simulationManager.ToggleBeltPlayPause();
            RefreshUIState();
        }

        public void OnSimulateSensorClicked()
        {
            AutoResolveReferences();
            if (simulationManager == null) return;

            simulationManager.SimulateSensorFromUIButton();
            RefreshUIState();
        }

        public void OnSpeedSliderChanged(float newSpeed)
        {
            AutoResolveReferences();
            if (simulationManager != null)
            {
                simulationManager.beltSpeedMetersPerSec = newSpeed;
            }
            if (speedValueText != null)
            {
                speedValueText.text = $"Velocidad: <b>{(newSpeed * 100f):F0} cm/s</b>";
            }
        }

        public void RefreshUIState()
        {
            if (simulationManager == null) return;

            bool isStoppedAtSensor = simulationManager.activeHoldingSensor != null &&
                                     simulationManager.systemStatus != BeltSystemStatus.Running &&
                                     simulationManager.systemStatus != BeltSystemStatus.ManualPause;

            bool isPaused = simulationManager.systemStatus == BeltSystemStatus.ManualPause;

            Color stateColor;
            string stateLabel;
            if (isStoppedAtSensor)
            {
                stateColor = ColorSensor;
                stateLabel = $"Sensor activo\n<size=80%>{simulationManager.activeHoldingSensor.stationName}</size>";
            }
            else if (isPaused)
            {
                stateColor = ColorPaused;
                stateLabel = "Banda en pausa";
            }
            else
            {
                stateColor = ColorRunning;
                stateLabel = "Banda en marcha";
            }

            if (statusText != null) statusText.text = stateLabel;
            if (statusIndicator != null) statusIndicator.color = stateColor;

            if (toggleBeltButtonText != null)
            {
                toggleBeltButtonText.text = (isPaused || isStoppedAtSensor) ? "Reanudar Banda" : "Pausar Banda";
            }

            if (simulateSensorButtonText != null)
            {
                simulateSensorButtonText.text = isStoppedAtSensor ? "Liberar Sensor" : "Simular Sensor";
            }

            if (speedValueText != null)
            {
                speedValueText.text = $"Velocidad: <b>{(simulationManager.beltSpeedMetersPerSec * 100f):F0} cm/s</b>";
            }
        }

        public static void EnsureEventSystemExists()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }
        }

        // =========================================================================================
        // CONSTRUCCIÓN DE LA JERARQUÍA UI (Canvas Full HD + Panel + TextMeshPro)
        // =========================================================================================

        /// <summary>
        /// Construye la jerarquía Unity UI (Canvas + Panel + Botones + Slider) con TextMeshPro,
        /// escalada para Full HD (1920x1080) y lista para guardarse como Prefab.
        /// </summary>
        /// <param name="roundedSprite">Sprite con bordes redondeados (opcional, el editor pasa el UISprite integrado).</param>
        /// <param name="knobSprite">Sprite circular para la manija del slider (opcional).</param>
        public static ConveyorUIPanel CreateSimpleCanvasHierarchy(Sprite roundedSprite = null, Sprite knobSprite = null)
        {
            // 1. Canvas raíz escalado para Full HD
            GameObject canvasGo = new GameObject("Canvas_PanelControlBanda", typeof(RectTransform));
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvas.pixelPerfect = true;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            canvasGo.AddComponent<GraphicRaycaster>();
            ConveyorUIPanel ui = canvasGo.AddComponent<ConveyorUIPanel>();
            ui.builtLayoutVersion = LayoutVersion;

            // 2. Panel contenedor (esquina superior izquierda, tamaño cómodo en Full HD)
            GameObject panelGo = CreateUIObject("Panel_ControlBanda", canvasGo.transform);
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(32f, -32f);
            panelRect.sizeDelta = new Vector2(440f, 0f);

            Image panelImg = panelGo.AddComponent<Image>();
            ApplySprite(panelImg, roundedSprite);
            panelImg.color = ColorPanel;

            VerticalLayoutGroup layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 28);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // El alto del panel se ajusta solo a su contenido
            ContentSizeFitter fitter = panelGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // 3. Título
            CreateTextElement("Titulo_Text", panelGo.transform, "CONTROL BANDA",
                34f, FontStyles.Bold, TextAlignmentOptions.Center, ColorTitle, 44f);

            // Separador
            GameObject sep = CreateUIObject("Separador", panelGo.transform);
            Image sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(1f, 1f, 1f, 0.12f);
            AddLayoutHeight(sep, 2f);

            // 4. Fila de estado (indicador circular + texto)
            GameObject statusRow = CreateUIObject("Fila_Estado", panelGo.transform);
            HorizontalLayoutGroup rowLayout = statusRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 14f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            AddLayoutHeight(statusRow, 64f);

            GameObject dotGo = CreateUIObject("Indicador_Estado", statusRow.transform);
            ui.statusIndicator = dotGo.AddComponent<Image>();
            ApplySprite(ui.statusIndicator, knobSprite, sliced: false);
            ui.statusIndicator.color = ColorRunning;
            LayoutElement dotLe = dotGo.AddComponent<LayoutElement>();
            dotLe.minWidth = dotLe.preferredWidth = 22f;
            dotLe.minHeight = dotLe.preferredHeight = 22f;

            ui.statusText = CreateTextElement("Estado_Text", statusRow.transform, "Banda en marcha",
                26f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, ColorText, 64f);
            ui.statusText.GetComponent<LayoutElement>().flexibleWidth = 1f;

            // 5. Botón Pausar / Reanudar
            ui.toggleBeltButton = CreateButtonElement("Btn_MarchaPausa", panelGo.transform, "Pausar Banda",
                ColorBtnBlue, 68f, roundedSprite, out ui.toggleBeltButtonText);

            // 6. Botón Simular Sensor
            ui.simulateSensorButton = CreateButtonElement("Btn_SimularSensor", panelGo.transform, "Simular Sensor",
                ColorBtnGreen, 68f, roundedSprite, out ui.simulateSensorButtonText);

            // 7. Velocidad (texto + slider)
            ui.speedValueText = CreateTextElement("Velocidad_Text", panelGo.transform, "Velocidad: <b>28 cm/s</b>",
                24f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, ColorText, 34f);

            ui.speedSlider = CreateSliderElement("Slider_Velocidad", panelGo.transform, 0.05f, 0.80f, 0.28f, 36f,
                roundedSprite, knobSprite);

            return ui;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void AddLayoutHeight(GameObject go, float height)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
        }

        private static void ApplySprite(Image img, Sprite sprite, bool sliced = true)
        {
            if (sprite == null) return;
            img.sprite = sprite;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = 1f;
        }

        private static TextMeshProUGUI CreateTextElement(string name, Transform parent, string content,
            float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color, float height)
        {
            GameObject go = CreateUIObject(name, parent);
            AddLayoutHeight(go, height);

            TextMeshProUGUI txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = alignment;
            txt.color = color;
            txt.richText = true;
            txt.textWrappingMode = TextWrappingModes.Normal;
            txt.overflowMode = TextOverflowModes.Ellipsis;
            txt.raycastTarget = false;
            return txt;
        }

        private static Button CreateButtonElement(string name, Transform parent, string label,
            Color bgColor, float height, Sprite sprite, out TMP_Text labelText)
        {
            GameObject btnGo = CreateUIObject(name, parent);
            AddLayoutHeight(btnGo, height);

            Image img = btnGo.AddComponent<Image>();
            ApplySprite(img, sprite);
            img.color = Color.white;

            Button btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = bgColor;
            cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.18f);
            cb.pressedColor = Color.Lerp(bgColor, Color.black, 0.22f);
            cb.selectedColor = bgColor;
            cb.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            GameObject txtGo = CreateUIObject("Text (TMP)", btnGo.transform);
            RectTransform txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(12f, 4f);
            txtRt.offsetMax = new Vector2(-12f, -4f);

            TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 28f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            labelText = tmp;

            return btn;
        }

        private static Slider CreateSliderElement(string name, Transform parent, float min, float max, float val,
            float height, Sprite roundedSprite, Sprite knobSprite)
        {
            GameObject sliderGo = CreateUIObject(name, parent);
            AddLayoutHeight(sliderGo, height);

            // Fondo del slider
            GameObject bgGo = CreateUIObject("Background", sliderGo.transform);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.35f);
            bgRt.anchorMax = new Vector2(1f, 0.65f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            Image bgImg = bgGo.AddComponent<Image>();
            ApplySprite(bgImg, roundedSprite);
            bgImg.color = new Color(0.26f, 0.29f, 0.35f, 1f);

            // Área de relleno
            GameObject fillArea = CreateUIObject("Fill Area", sliderGo.transform);
            RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.35f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.65f);
            fillAreaRt.offsetMin = new Vector2(6f, 0f);
            fillAreaRt.offsetMax = new Vector2(-14f, 0f);

            GameObject fillGo = CreateUIObject("Fill", fillArea.transform);
            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.sizeDelta = new Vector2(10f, 0f);
            Image fillImg = fillGo.AddComponent<Image>();
            ApplySprite(fillImg, roundedSprite);
            fillImg.color = new Color(0.25f, 0.65f, 1.0f, 1f);

            // Manija del slider
            GameObject handleArea = CreateUIObject("Handle Slide Area", sliderGo.transform);
            RectTransform handleAreaRt = handleArea.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(14f, 0f);
            handleAreaRt.offsetMax = new Vector2(-14f, 0f);

            GameObject handleGo = CreateUIObject("Handle", handleArea.transform);
            RectTransform handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(30f, 0f);
            Image handleImg = handleGo.AddComponent<Image>();
            ApplySprite(handleImg, knobSprite, sliced: false);
            handleImg.color = Color.white;

            Slider slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = val;

            return slider;
        }
    }
}
