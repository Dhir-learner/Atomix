using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns the floating measurement label's display mode and the small toast that confirms a change.
///
/// The label sits just above the vessel, which is exactly where the student looks while pouring.
/// <see cref="FreeHandTooltip"/> already lifts and fades it as the camera closes in; this adds the
/// escape hatch for the times that is still not enough - one key cycles
/// Detailed -> Compact -> Hidden and back.
///
/// It also owns the always-on measurement readout at the top of the screen. That is what makes
/// turning the label off free: the scene's own tracker text only appears while something is
/// actively being poured, so without this readout, hiding the label would leave the student with
/// no reading at all between pours.
/// </summary>
public class LabHudController : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Cycles the floating measurement label: Detailed -> Compact -> Hidden.")]
    public KeyCode cycleLabelKey = KeyCode.L;

    [Header("Toast")]
    [Tooltip("Seconds the confirmation stays on screen.")]
    public float toastSeconds = 2.0f;

    public float toastFontSize = 26.0f;

    [Header("Measurement readout")]
    [Tooltip("A small always-on readout at the top of the screen showing the live amounts.\n\n" +
             "This is what makes hiding the floating label free: the numbers never disappear, " +
             "they just stop sitting on top of the glassware.")]
    public bool showMeasurementHud = true;

    public float measurementFontSize = 24.0f;

    [Header("Pour gauge")]
    [Tooltip("A small bar just under the crosshair while something is being poured, showing the " +
             "amount against the accepted range - so the student's eyes can stay on the vessel " +
             "instead of darting up to the readout at the top of the screen.")]
    public bool showPourGauge = true;

    [Tooltip("Where the gauge sits, in 1920x1080 pixels from the centre of the screen.")]
    public Vector2 gaugeOffset = new Vector2(0.0f, -70.0f);

    public float gaugeWidth = 300.0f;

    [Tooltip("Seconds the gauge stays up after a pour stops, so the student can see where they stopped.")]
    public float gaugeLingerSeconds = 1.5f;

    [Header("Scenes")]
    [Tooltip("The key only does anything in these scenes.")]
    public string[] enabledScenes = { "LabScene", "TestingPhaseLab" };

    // One always-active canvas holds both pieces of HUD; each plate is shown independently, so
    // the toast fading out can never take the measurement readout with it.
    private Canvas hudCanvas;

    private RectTransform toastPlate;
    private TMP_Text toastText;
    private CanvasGroup toastGroup;
    private float toastRemaining;

    private RectTransform measurementPlate;
    private TMP_Text measurementText;

    void Update()
    {
        // Only the key read is gated: the toast and the measurement strip are display work, and
        // freezing them while the student types a question would look like the game had hung.
        if (!LabTextInput.IsCapturing &&
            Input.GetKeyDown(cycleLabelKey) && IsEnabledScene())
        {
            CycleLabelMode();
        }

        UpdateToast();
        UpdateMeasurementHud();
    }

    // =========================================================
    // MEASUREMENT READOUT
    // =========================================================

    /// <summary>
    /// Mirrors the live quantities to a small strip at the top of the screen.
    ///
    /// Without this, turning the floating label off would leave the student with no reading at
    /// all between pours - the scene's own tracker text only appears while something is actually
    /// being poured, and shows guidance the rest of the time.
    /// </summary>
    private void UpdateMeasurementHud()
    {
        if (!showMeasurementHud || !IsEnabledScene())
        {
            SetMeasurementVisible(false);
            SetGaugeVisible(false);
            return;
        }

        // Prefer the experiment currently on the bench. ReactionHistoryRecorder.Active is the most
        // recent one and is kept after a verdict for the AI assistant's benefit, so reading it
        // directly left the last task's reagents - and its FAILED banner - on screen through the
        // whole of the next one. Fall back to Active only while that attempt is still running.
        ReactionHistoryRecorder active = ReactionHistoryRecorder.Selected;
        if (active == null)
        {
            ReactionHistoryRecorder recent = ReactionHistoryRecorder.Active;
            if (recent != null && !recent.IsFinished)
            {
                active = recent;
            }
        }

        FreeHandReactionEngine engine = active != null ? active.Engine : null;

        UpdatePourGauge(engine);

        // No engine means no experiment is selected.
        if (engine == null || engine.Substances.Count == 0)
        {
            SetMeasurementVisible(false);
            return;
        }

        EnsureMeasurementBuilt();
        SetMeasurementVisible(true);

        if (measurementText == null)
        {
            return;
        }

        // Assigning TMP_Text.text re-lays-out the mesh, so only write when the readout actually
        // changed. Pouring changes it several times a second; standing still, never.
        string line = engine.GetHudText(active.ReactionName);
        if (line != lastMeasurementLine)
        {
            lastMeasurementLine = line;
            measurementText.text = line;
        }
    }

    private string lastMeasurementLine;

    private void SetMeasurementVisible(bool visible)
    {
        if (measurementPlate != null &&
            measurementPlate.gameObject.activeSelf != visible)
        {
            measurementPlate.gameObject.SetActive(visible);
        }
    }

    private void EnsureMeasurementBuilt()
    {
        if (measurementPlate != null)
        {
            return;
        }

        EnsureHudCanvas();
        if (hudCanvas == null)
        {
            return;
        }

        GameObject plate = new GameObject("MeasurementReadout",
            typeof(RectTransform), typeof(Image));
        plate.transform.SetParent(hudCanvas.transform, false);

        measurementPlate = plate.GetComponent<RectTransform>();
        measurementPlate.anchorMin = new Vector2(0.5f, 1.0f);
        measurementPlate.anchorMax = new Vector2(0.5f, 1.0f);
        measurementPlate.pivot = new Vector2(0.5f, 1.0f);
        measurementPlate.anchoredPosition = new Vector2(0.0f, -18.0f);
        measurementPlate.sizeDelta = new Vector2(980.0f, 46.0f);

        Image plateImage = plate.GetComponent<Image>();
        plateImage.color = new Color(0.04f, 0.05f, 0.08f, 0.72f);
        plateImage.raycastTarget = false;

        GameObject textObject = new GameObject("Text",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(plate.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14.0f, 6.0f);
        textRect.offsetMax = new Vector2(-14.0f, -6.0f);

        measurementText = textObject.GetComponent<TextMeshProUGUI>();
        measurementText.fontSize = measurementFontSize;
        measurementText.alignment = TextAlignmentOptions.Center;
        measurementText.color = Color.white;
        measurementText.textWrappingMode = TextWrappingModes.NoWrap;
        measurementText.overflowMode = TextOverflowModes.Overflow;
        measurementText.richText = true;
        measurementText.raycastTarget = false;

        plate.SetActive(false);
    }

    // Scene.name allocates a string on every access, and this is consulted every frame, so the
    // answer is cached and only recomputed when the active scene actually changes.
    private bool sceneEnabled;
    private bool sceneChecked;

    void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleSceneChanged;
        sceneChecked = false;
    }

    void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleSceneChanged;
    }

    private void HandleSceneChanged(Scene previous, Scene next)
    {
        sceneChecked = false;

        // The readout belongs to the scene it was measuring; do not carry it across.
        SetMeasurementVisible(false);
        lastMeasurementLine = null;
        SetGaugeVisible(false);
        gaugeSubstance = null;
        gaugeEngine = null;
        gaugeLinger = 0.0f;
    }

    // =========================================================
    // POUR GAUGE
    // =========================================================
    //
    // While pouring, the student is looking at the vessel - but the numbers were only ever at the
    // top edge of the screen, so their eyes had to jump between the two at exactly the moment
    // that matters most. The gauge sits just under the crosshair instead.
    //
    // It reads the same engine the top strip does. With the targets hidden (the testing scene,
    // the Expert level, a stoichiometry challenge) it shows the amount and the flow only: no bar
    // against a range, because the range is the answer.

    private RectTransform gaugeRoot;
    private CanvasGroup gaugeGroup;
    private TMP_Text gaugeLabel;
    private RectTransform gaugeTrack;
    private RectTransform gaugeBand;
    private RectTransform gaugeMarker;
    private RectTransform gaugeFill;
    private Image gaugeFillImage;
    private RectTransform gaugeFlowTrack;
    private RectTransform gaugeFlowFill;

    private FreeHandReactionEngine gaugeEngine;
    private string gaugeSubstance;
    private float gaugeLinger;
    private string lastGaugeLabel;

    private const float GaugeBarHeight = 12.0f;
    private const float GaugeFlowHeight = 4.0f;

    private void UpdatePourGauge(FreeHandReactionEngine engine)
    {
        if (!showPourGauge || engine == null || !FirstPersonController.IsCursorLocked)
        {
            SetGaugeVisible(false);
            gaugeLinger = 0.0f;
            return;
        }

        // Whatever is being poured right now; failing that, keep the last one up for a moment.
        string pouring = null;
        List<string> substances = engine.Substances;
        for (int i = 0; i < substances.Count; i++)
        {
            if (engine.IsPouring(substances[i]))
            {
                pouring = substances[i];
                break;
            }
        }

        if (pouring != null)
        {
            gaugeEngine = engine;
            gaugeSubstance = pouring;
            gaugeLinger = gaugeLingerSeconds;
        }
        else if (gaugeEngine != engine)
        {
            gaugeLinger = 0.0f;    // a different experiment is on the bench now
        }
        else
        {
            gaugeLinger -= Time.unscaledDeltaTime;
        }

        if (gaugeLinger <= 0.0f || string.IsNullOrEmpty(gaugeSubstance) ||
            !engine.targetQuantities.ContainsKey(gaugeSubstance))
        {
            SetGaugeVisible(false);
            return;
        }

        EnsureGaugeBuilt();
        if (gaugeRoot == null)
        {
            return;
        }

        SetGaugeVisible(true);

        // Hold solid while pouring, then fade over the last half second of the linger.
        gaugeGroup.alpha = pouring != null ? 1.0f : Mathf.Clamp01(gaugeLinger / 0.5f);

        RenderGauge(engine, gaugeSubstance);
    }

    private void RenderGauge(FreeHandReactionEngine engine, string substance)
    {
        float current = engine.GetCurrent(substance);
        string unit = engine.UnitFor(substance);
        bool showTargets = engine.ShowTargetsNow;

        string label = showTargets
            ? string.Format("{0}  {1:F1} / {2:F1} {3}", substance, current,
                engine.targetQuantities[substance], unit)
            : string.Format("{0}  {1:F1} {2}", substance, current, unit);

        if (label != lastGaugeLabel)
        {
            lastGaugeLabel = label;
            gaugeLabel.text = label;
        }

        gaugeTrack.gameObject.SetActive(showTargets);
        if (showTargets)
        {
            float min = engine.MinAllowed(substance);
            float max = engine.MaxAllowed(substance);
            float target = engine.targetQuantities[substance];

            // A little headroom past the top of the range, so an overshoot is visible as one.
            float scale = Mathf.Max(max * 1.25f, current * 1.05f, 0.001f);

            SetSpan(gaugeBand, min / scale, max / scale);
            SetSpan(gaugeFill, 0.0f, Mathf.Clamp01(current / scale));

            // A zero-width span, widened to a 2 px line either side of the target.
            SetSpan(gaugeMarker, target / scale, target / scale);
            gaugeMarker.offsetMin = new Vector2(-1.0f, -3.0f);
            gaugeMarker.offsetMax = new Vector2(1.0f, 3.0f);

            gaugeFillImage.color = current > max
                ? AtomixSettings.FailureColour
                : (current >= min ? AtomixSettings.SuccessColour : FreeHandTooltip.ProgressColor);
        }

        float flow;
        bool hasFlow = engine.TryGetFlowFraction(substance, out flow) && engine.IsPouring(substance);
        gaugeFlowTrack.gameObject.SetActive(hasFlow);
        if (hasFlow)
        {
            SetSpan(gaugeFlowFill, 0.0f, Mathf.Clamp01(flow));
        }
    }

    /// <summary>Stretches a child across part of its parent's width, 0..1 from the left edge.</summary>
    private static void SetSpan(RectTransform rect, float from, float to)
    {
        rect.anchorMin = new Vector2(Mathf.Clamp01(from), 0.0f);
        rect.anchorMax = new Vector2(Mathf.Clamp01(to), 1.0f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void SetGaugeVisible(bool visible)
    {
        if (gaugeRoot != null && gaugeRoot.gameObject.activeSelf != visible)
        {
            gaugeRoot.gameObject.SetActive(visible);
        }
    }

    private void EnsureGaugeBuilt()
    {
        if (gaugeRoot != null)
        {
            return;
        }

        EnsureHudCanvas();
        if (hudCanvas == null)
        {
            return;
        }

        GameObject root = new GameObject("PourGauge", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(hudCanvas.transform, false);

        gaugeRoot = root.GetComponent<RectTransform>();
        gaugeRoot.anchorMin = new Vector2(0.5f, 0.5f);
        gaugeRoot.anchorMax = new Vector2(0.5f, 0.5f);
        gaugeRoot.pivot = new Vector2(0.5f, 1.0f);
        gaugeRoot.anchoredPosition = gaugeOffset;
        gaugeRoot.sizeDelta = new Vector2(gaugeWidth, 52.0f);

        gaugeGroup = root.GetComponent<CanvasGroup>();
        gaugeGroup.interactable = false;
        gaugeGroup.blocksRaycasts = false;

        Image plate = CreateGaugeImage("Plate", gaugeRoot, new Color(0.04f, 0.05f, 0.08f, 0.66f));
        Stretch(plate.rectTransform, Vector2.zero, Vector2.one, new Vector2(-10.0f, -6.0f), new Vector2(10.0f, 4.0f));

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(gaugeRoot, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        Stretch(labelRect, new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f),
            new Vector2(0.0f, -26.0f), new Vector2(0.0f, 0.0f));

        gaugeLabel = labelObject.GetComponent<TextMeshProUGUI>();
        gaugeLabel.fontSize = 20.0f;
        gaugeLabel.alignment = TextAlignmentOptions.Center;
        gaugeLabel.color = Color.white;
        gaugeLabel.textWrappingMode = TextWrappingModes.NoWrap;
        gaugeLabel.raycastTarget = false;

        // The amount against the range.
        Image track = CreateGaugeImage("Track", gaugeRoot, new Color(1.0f, 1.0f, 1.0f, 0.14f));
        gaugeTrack = track.rectTransform;
        Stretch(gaugeTrack, new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f),
            new Vector2(0.0f, -30.0f - GaugeBarHeight), new Vector2(0.0f, -30.0f));

        Color band = AtomixSettings.SuccessColour;
        band.a = 0.35f;
        gaugeBand = CreateGaugeImage("AcceptedRange", gaugeTrack, band).rectTransform;

        gaugeFillImage = CreateGaugeImage("Amount", gaugeTrack, FreeHandTooltip.ProgressColor);
        gaugeFill = gaugeFillImage.rectTransform;

        gaugeMarker = CreateGaugeImage("Target", gaugeTrack, Color.white).rectTransform;
        gaugeMarker.pivot = new Vector2(0.5f, 0.5f);

        // How hard it is pouring - the tilt, made visible.
        Image flowTrack = CreateGaugeImage("FlowTrack", gaugeRoot, new Color(1.0f, 1.0f, 1.0f, 0.10f));
        gaugeFlowTrack = flowTrack.rectTransform;
        Stretch(gaugeFlowTrack, new Vector2(0.25f, 1.0f), new Vector2(0.75f, 1.0f),
            new Vector2(0.0f, -48.0f - GaugeFlowHeight), new Vector2(0.0f, -48.0f));

        gaugeFlowFill = CreateGaugeImage("Flow", gaugeFlowTrack, new Color(1.0f, 0.84f, 0.36f, 0.9f)).rectTransform;

        root.SetActive(false);
    }

    private static Image CreateGaugeImage(string name, Transform parent, Color colour)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
                                Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private bool IsEnabledScene()
    {
        if (sceneChecked)
        {
            return sceneEnabled;
        }

        sceneChecked = true;

        if (enabledScenes == null || enabledScenes.Length == 0)
        {
            sceneEnabled = true;
            return sceneEnabled;
        }

        string active = SceneManager.GetActiveScene().name;
        sceneEnabled = false;
        for (int i = 0; i < enabledScenes.Length; i++)
        {
            if (enabledScenes[i] == active)
            {
                sceneEnabled = true;
                break;
            }
        }
        return sceneEnabled;
    }

    /// <summary>Steps the shared label mode on and tells the student what just happened.</summary>
    public void CycleLabelMode()
    {
        switch (FreeHandTooltip.DisplayMode)
        {
            case LabelDisplayMode.Detailed:
                FreeHandTooltip.DisplayMode = LabelDisplayMode.Compact;
                ShowToast("Label above the vessel: COMPACT\nJust the reagent you are pouring.");
                break;

            case LabelDisplayMode.Compact:
                FreeHandTooltip.DisplayMode = LabelDisplayMode.Hidden;
                ShowToast("Label above the vessel: OFF\nYour amounts stay on the readout at the top of the screen.");
                break;

            default:
                FreeHandTooltip.DisplayMode = LabelDisplayMode.Detailed;
                ShowToast("Label above the vessel: FULL\nEvery reagent listed above the glassware.");
                break;
        }
    }

    // =========================================================
    // TOAST
    // =========================================================

    /// <summary>
    /// Posts a short message to the HUD. Public so other systems - the achievement unlocks, for
    /// one - can reuse the toast rather than each building their own.
    /// </summary>
    public static void Toast(string message)
    {
        if (Instance != null)
        {
            Instance.ShowToast(message);
        }
    }

    /// <summary>The live HUD, set on Awake. There is only ever one, on the persistent object.</summary>
    public static LabHudController Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ShowToast(string message)
    {
        EnsureToastBuilt();

        if (toastText != null)
        {
            toastText.text = message;
        }

        toastRemaining = Mathf.Max(0.1f, toastSeconds);

        if (toastPlate != null)
        {
            toastPlate.gameObject.SetActive(true);
        }
        if (toastGroup != null)
        {
            toastGroup.alpha = 1.0f;
        }
    }

    private void UpdateToast()
    {
        if (toastRemaining <= 0.0f || toastGroup == null)
        {
            return;
        }

        toastRemaining -= Time.unscaledDeltaTime;

        // Hold solid, then fade over the last half second.
        toastGroup.alpha = Mathf.Clamp01(toastRemaining / 0.5f);

        if (toastRemaining <= 0.0f && toastPlate != null)
        {
            toastPlate.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Builds the shared HUD canvas the first time anything needs it. Deliberately has no
    /// GraphicRaycaster and nothing raycastable inside, so it can never eat a crosshair click
    /// meant for a beaker.
    /// </summary>
    private void EnsureHudCanvas()
    {
        if (hudCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            "LabHudCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        hudCanvas = canvasObject.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 560; // above the graph panel

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);
    }

    private void EnsureToastBuilt()
    {
        if (toastPlate != null)
        {
            return;
        }

        EnsureHudCanvas();
        if (hudCanvas == null)
        {
            return;
        }

        GameObject plate = new GameObject("ToastPlate",
            typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        plate.transform.SetParent(hudCanvas.transform, false);

        toastPlate = plate.GetComponent<RectTransform>();
        toastPlate.anchorMin = new Vector2(0.5f, 0.0f);
        toastPlate.anchorMax = new Vector2(0.5f, 0.0f);
        toastPlate.pivot = new Vector2(0.5f, 0.0f);
        toastPlate.anchoredPosition = new Vector2(0.0f, 150.0f);
        toastPlate.sizeDelta = new Vector2(760.0f, 96.0f);

        toastGroup = plate.GetComponent<CanvasGroup>();
        toastGroup.interactable = false;
        toastGroup.blocksRaycasts = false;

        Image plateImage = plate.GetComponent<Image>();
        plateImage.color = new Color(0.04f, 0.05f, 0.08f, 0.88f);
        plateImage.raycastTarget = false;

        GameObject textObject = new GameObject("Text",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(plate.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16.0f, 10.0f);
        textRect.offsetMax = new Vector2(-16.0f, -10.0f);

        toastText = textObject.GetComponent<TextMeshProUGUI>();
        toastText.fontSize = toastFontSize;
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.color = new Color(0.85f, 0.93f, 1.0f, 1.0f);
        toastText.textWrappingMode = TextWrappingModes.Normal;
        toastText.raycastTarget = false;

        plate.SetActive(false);
    }
}
