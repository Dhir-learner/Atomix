using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Lifecycle of a free-hand experiment. The user is never forced through a fixed sequence -
/// the engine only observes what was poured, how much of it, and when.
/// </summary>
public enum FreeHandReactionState
{
    WaitingForInputs,
    Pouring,
    Settling,
    Success,
    Failed
}

/// <summary>Outcome returned by <see cref="FreeHandReactionEngine.CheckReactionOutcome"/>.</summary>
public enum ReactionResult
{
    InProgress,
    Success,
    FailOverdose,
    FailUnderdose,
    FailWrongOrder
}

/// <summary>
/// Reusable quantity / timing tracker shared by every reaction script. Extracted from the
/// pattern originally written inline in <see cref="Reaction"/> so that all eight experiments
/// behave the same way: quantity matters, order matters and settling time matters.
///
/// Usage from a reaction MonoBehaviour:
///   engine = new FreeHandReactionEngine { tolerancePercent = 8f };
///   engine.AddSubstance("H2SO4", 20f, "ml", overdose: "...", underdose: "...");
///   engine.AddSubstance("CuO", 8f, "g", overdose: "...", underdose: "...");
///   ...
///   engine.UpdatePouringQuantity("H2SO4", 10f, h2so4.IsPouring);
///   ReactionResult result = engine.CheckReactionOutcome();
/// </summary>
public class FreeHandReactionEngine
{
    // --- Public state required by the free-hand spec --------------------------------
    public readonly Dictionary<string, float> targetQuantities = new Dictionary<string, float>();
    public readonly Dictionary<string, float> currentQuantities = new Dictionary<string, float>();
    public float tolerancePercent = 5.0f;
    public float settleTimeRequired = 1.5f;
    public FreeHandReactionState reactionState = FreeHandReactionState.WaitingForInputs;
    public string failureReason = string.Empty;

    // --- Presentation / chemistry copy ----------------------------------------------
    public string trackerTitle = "[Lab Measurement Tracker]";
    public string wrongOrderMessage = "The reagents were added in the wrong order, so the intermediate step never formed.";
    public bool enforceOrder = true;

    // --- Internal bookkeeping --------------------------------------------------------
    private readonly List<string> registrationOrder = new List<string>();
    private readonly Dictionary<string, string> units = new Dictionary<string, string>();
    private readonly Dictionary<string, float> perSubstanceTolerance = new Dictionary<string, float>();
    private readonly Dictionary<string, string> overdoseMessages = new Dictionary<string, string>();
    private readonly Dictionary<string, string> underdoseMessages = new Dictionary<string, string>();
    private readonly HashSet<string> orderFreeSubstances = new HashSet<string>();
    private readonly Dictionary<string, int> orderGroups = new Dictionary<string, int>();
    private readonly HashSet<string> pouringSubstances = new HashSet<string>();
    private readonly List<string> additionOrder = new List<string>();

    private float settleTimer = 0.0f;
    private ReactionResult lastResult = ReactionResult.InProgress;
    private string offendingSubstance = string.Empty;
    private string failureHeadlineOverride = string.Empty;

    public bool IsAnyPouring { get { return pouringSubstances.Count > 0; } }
    public bool HasFailed { get { return reactionState == FreeHandReactionState.Failed; } }
    public bool HasSucceeded { get { return reactionState == FreeHandReactionState.Success; } }
    public bool IsResolved { get { return HasFailed || HasSucceeded; } }
    public float SettleTimer { get { return settleTimer; } }
    public ReactionResult LastResult { get { return lastResult; } }
    public List<string> Substances { get { return registrationOrder; } }

    /// <summary>
    /// Registers one measurable input. Registration order is the expected procedure order
    /// (pass orderMatters: false for reagents that may be added at any point). Reagents that
    /// are interchangeable with each other but must come before/after others share an
    /// <paramref name="orderGroup"/>; leave it at -1 to use the registration index.
    /// </summary>
    public void AddSubstance(string substanceName, float target, string unit = "ml",
                             float tolerancePercentOverride = -1.0f,
                             string overdose = null, string underdose = null,
                             bool orderMatters = true, int orderGroup = -1)
    {
        if (string.IsNullOrEmpty(substanceName) || targetQuantities.ContainsKey(substanceName))
        {
            return;
        }

        orderGroups[substanceName] = orderGroup >= 0 ? orderGroup : registrationOrder.Count;
        registrationOrder.Add(substanceName);
        targetQuantities[substanceName] = target;
        currentQuantities[substanceName] = 0.0f;
        units[substanceName] = unit;

        if (tolerancePercentOverride > 0.0f)
        {
            perSubstanceTolerance[substanceName] = tolerancePercentOverride;
        }
        if (!string.IsNullOrEmpty(overdose))
        {
            overdoseMessages[substanceName] = overdose;
        }
        if (!string.IsNullOrEmpty(underdose))
        {
            underdoseMessages[substanceName] = underdose;
        }
        if (!orderMatters)
        {
            orderFreeSubstances.Add(substanceName);
        }
    }

    /// <summary>
    /// Call every frame for every input, passing the pour script IsPouring flag. Accumulates
    /// quantity at mlPerSecond (the same parameter drives g/s for powders and s/s for heating).
    /// </summary>
    public void UpdatePouringQuantity(string substanceName, float mlPerSecond, bool isPouring)
    {
        if (!targetQuantities.ContainsKey(substanceName))
        {
            return;
        }

        if (IsResolved)
        {
            pouringSubstances.Remove(substanceName);
            return;
        }

        if (isPouring)
        {
            pouringSubstances.Add(substanceName);
            currentQuantities[substanceName] += mlPerSecond * Time.deltaTime;
            RecordFirstAddition(substanceName);
        }
        else
        {
            pouringSubstances.Remove(substanceName);
        }
    }

    /// <summary>Adds a one-shot amount (a solid block dropped in, a measured spoon).</summary>
    public void AddDiscreteQuantity(string substanceName, float amount)
    {
        if (!targetQuantities.ContainsKey(substanceName) || IsResolved)
        {
            return;
        }

        currentQuantities[substanceName] += amount;
        RecordFirstAddition(substanceName);
    }

    /// <summary>Forces an exact amount (used when a discrete object counts as one full dose).</summary>
    public void SetQuantity(string substanceName, float amount)
    {
        if (!targetQuantities.ContainsKey(substanceName) || IsResolved)
        {
            return;
        }

        currentQuantities[substanceName] = amount;
        if (amount > 0.0f)
        {
            RecordFirstAddition(substanceName);
        }
    }

    private void RecordFirstAddition(string substanceName)
    {
        if (!additionOrder.Contains(substanceName))
        {
            additionOrder.Add(substanceName);
        }
    }

    /// <summary>Evaluates the experiment. Meant to be called once per frame from Update().</summary>
    public ReactionResult CheckReactionOutcome()
    {
        if (IsResolved)
        {
            return lastResult;
        }

        if (targetQuantities.Count == 0)
        {
            return ReactionResult.InProgress;
        }

        string outOfPlace;
        if (enforceOrder && IsOrderViolated(out outOfPlace))
        {
            offendingSubstance = outOfPlace;
            failureReason = wrongOrderMessage;
            return Fail(ReactionResult.FailWrongOrder);
        }

        // Overdose is detected the moment it happens, even mid-pour.
        for (int i = 0; i < registrationOrder.Count; i++)
        {
            string substance = registrationOrder[i];
            if (currentQuantities[substance] > MaxAllowed(substance))
            {
                offendingSubstance = substance;
                string message;
                failureReason = overdoseMessages.TryGetValue(substance, out message)
                    ? message
                    : "Too much " + substance + " pushes the mixture away from the balanced equation.";
                return Fail(ReactionResult.FailOverdose);
            }
        }

        bool everythingAdded = true;
        bool nothingAdded = true;
        for (int i = 0; i < registrationOrder.Count; i++)
        {
            if (currentQuantities[registrationOrder[i]] > 0.0f)
            {
                nothingAdded = false;
            }
            else
            {
                everythingAdded = false;
            }
        }

        if (IsAnyPouring)
        {
            settleTimer = 0.0f;
            reactionState = FreeHandReactionState.Pouring;
            return ReactionResult.InProgress;
        }

        // Underdose is only judged once every reagent is actually in the vessel, otherwise
        // a user who pauses between reagents would be failed unfairly.
        if (nothingAdded || !everythingAdded)
        {
            settleTimer = 0.0f;
            reactionState = FreeHandReactionState.WaitingForInputs;
            return ReactionResult.InProgress;
        }

        settleTimer += Time.deltaTime;
        reactionState = FreeHandReactionState.Settling;
        if (settleTimer < settleTimeRequired)
        {
            return ReactionResult.InProgress;
        }

        for (int i = 0; i < registrationOrder.Count; i++)
        {
            string substance = registrationOrder[i];
            if (currentQuantities[substance] < MinAllowed(substance))
            {
                offendingSubstance = substance;
                string message;
                failureReason = underdoseMessages.TryGetValue(substance, out message)
                    ? message
                    : "Not enough " + substance + " was added, so the reagents could not react completely.";
                return Fail(ReactionResult.FailUnderdose);
            }
        }

        reactionState = FreeHandReactionState.Success;
        lastResult = ReactionResult.Success;
        return ReactionResult.Success;
    }

    /// <summary>
    /// Fails the experiment for a reason the engine cannot observe on its own (apparatus not
    /// assembled, burner left open, sample heated before it was sealed, ...).
    /// </summary>
    public ReactionResult ForceFailure(string substanceName, string reason,
                                       ReactionResult result = ReactionResult.FailWrongOrder,
                                       string tooltipHeadline = null)
    {
        if (IsResolved)
        {
            return lastResult;
        }

        offendingSubstance = substanceName;
        failureReason = reason;
        failureHeadlineOverride = string.IsNullOrEmpty(tooltipHeadline) ? string.Empty : tooltipHeadline;
        return Fail(result);
    }

    private ReactionResult Fail(ReactionResult result)
    {
        reactionState = FreeHandReactionState.Failed;
        lastResult = result;
        pouringSubstances.Clear();
        return result;
    }

    private bool IsOrderViolated(out string outOfPlaceSubstance)
    {
        outOfPlaceSubstance = string.Empty;
        int highestExpectedIndexSoFar = -1;

        for (int i = 0; i < additionOrder.Count; i++)
        {
            string added = additionOrder[i];
            if (orderFreeSubstances.Contains(added))
            {
                continue;
            }

            int expectedIndex;
            if (!orderGroups.TryGetValue(added, out expectedIndex))
            {
                expectedIndex = registrationOrder.IndexOf(added);
            }

            if (expectedIndex < highestExpectedIndexSoFar)
            {
                outOfPlaceSubstance = added;
                return true;
            }

            highestExpectedIndexSoFar = expectedIndex;
        }

        return false;
    }

    public float ToleranceFor(string substanceName)
    {
        float value;
        return perSubstanceTolerance.TryGetValue(substanceName, out value) ? value : tolerancePercent;
    }

    public float MinAllowed(string substanceName)
    {
        return targetQuantities[substanceName] * (1.0f - ToleranceFor(substanceName) / 100.0f);
    }

    public float MaxAllowed(string substanceName)
    {
        return targetQuantities[substanceName] * (1.0f + ToleranceFor(substanceName) / 100.0f);
    }

    public float GetCurrent(string substanceName)
    {
        float value;
        return currentQuantities.TryGetValue(substanceName, out value) ? value : 0.0f;
    }

    /// <summary>Is this specific input being added right now? Used by the history recorder to
    /// log a step when a pour starts and again when it stops.</summary>
    public bool IsPouring(string substanceName)
    {
        return pouringSubstances.Contains(substanceName);
    }

    /// <summary>True when the measured amount is inside the accepted window.</summary>
    public bool IsWithinTolerance(string substanceName)
    {
        if (!targetQuantities.ContainsKey(substanceName))
        {
            return false;
        }

        float current = currentQuantities[substanceName];
        return current >= MinAllowed(substanceName) && current <= MaxAllowed(substanceName);
    }

    /// <summary>Snapshot of what has actually been added, for the experiment history.</summary>
    public Dictionary<string, float> GetQuantitiesSnapshot()
    {
        return new Dictionary<string, float>(currentQuantities);
    }

    /// <summary>Snapshot of what should have been added, for the experiment history.</summary>
    public Dictionary<string, float> GetTargetsSnapshot()
    {
        return new Dictionary<string, float>(targetQuantities);
    }

    public string UnitFor(string substanceName)
    {
        string unit;
        return units.TryGetValue(substanceName, out unit) ? unit : "ml";
    }

    /// <summary>First registered substance that is still missing, or empty when all are in.</summary>
    public string GetPendingSubstance()
    {
        for (int i = 0; i < registrationOrder.Count; i++)
        {
            if (currentQuantities[registrationOrder[i]] <= 0.0f)
            {
                return registrationOrder[i];
            }
        }
        return string.Empty;
    }

    /// <summary>Multi-line body used for the on-screen canvas text.</summary>
    public string GetTrackerText()
    {
        string text = trackerTitle + "\n";
        for (int i = 0; i < registrationOrder.Count; i++)
        {
            string substance = registrationOrder[i];
            text += string.Format("{0}: {1:F1} / {2:F1} {3}  (accept {4:F1}-{5:F1})\n",
                substance, currentQuantities[substance], targetQuantities[substance],
                UnitFor(substance), MinAllowed(substance), MaxAllowed(substance));
        }

        if (IsAnyPouring)
        {
            foreach (string substance in pouringSubstances)
            {
                text += string.Format("\nAdding {0}... stop between {1:F1} and {2:F1} {3}.",
                    substance, MinAllowed(substance), MaxAllowed(substance), UnitFor(substance));
                break;
            }
        }
        else if (reactionState == FreeHandReactionState.Settling)
        {
            text += string.Format("\nLet the mixture settle... {0:F1}s / {1:F1}s", settleTimer, settleTimeRequired);
        }

        return text;
    }

    /// <summary>Compact body for the floating world-space tooltip.</summary>
    public string GetTooltipText()
    {
        string text = string.Empty;
        for (int i = 0; i < registrationOrder.Count; i++)
        {
            string substance = registrationOrder[i];
            text += string.Format("{0}: {1:F1} / {2:F1} {3}\n",
                substance, currentQuantities[substance], targetQuantities[substance], UnitFor(substance));
        }
        return text.TrimEnd('\n');
    }

    /// <summary>Short red banner shown on the tooltip after a failure.</summary>
    public string GetFailureHeadline()
    {
        if (!string.IsNullOrEmpty(failureHeadlineOverride))
        {
            return failureHeadlineOverride;
        }

        switch (lastResult)
        {
            case ReactionResult.FailOverdose:
                return string.Format("FAILED: Too much {0}\n{1:F1} {2} (max {3:F1})",
                    offendingSubstance, GetCurrent(offendingSubstance),
                    UnitFor(offendingSubstance), MaxAllowed(offendingSubstance));
            case ReactionResult.FailUnderdose:
                return string.Format("FAILED: Not enough {0}\n{1:F1} {2} (min {3:F1})",
                    offendingSubstance, GetCurrent(offendingSubstance),
                    UnitFor(offendingSubstance), MinAllowed(offendingSubstance));
            case ReactionResult.FailWrongOrder:
                return string.Format("FAILED: Wrong procedure order\n{0} was added too early", offendingSubstance);
            default:
                return "FAILED";
        }
    }

    /// <summary>Chemistry-accurate explanation for the canvas and for the AI assistant.</summary>
    public string GetFailureExplanation()
    {
        if (!HasFailed)
        {
            return string.Empty;
        }

        string measurements = string.Empty;
        for (int i = 0; i < registrationOrder.Count; i++)
        {
            string substance = registrationOrder[i];
            measurements += string.Format("{0}: {1:F1} {2} (expected ~{3:F1})\n",
                substance, currentQuantities[substance], UnitFor(substance), targetQuantities[substance]);
        }

        string headline;
        switch (lastResult)
        {
            case ReactionResult.FailOverdose:
                headline = "Experiment Failed - too much " + offendingSubstance + ".";
                break;
            case ReactionResult.FailUnderdose:
                headline = "Experiment Failed - not enough " + offendingSubstance + ".";
                break;
            case ReactionResult.FailWrongOrder:
                headline = string.IsNullOrEmpty(failureHeadlineOverride)
                    ? "Experiment Failed - " + offendingSubstance + " was added out of sequence."
                    : "Experiment Failed - " + failureHeadlineOverride.Replace('\n', ' ') + ".";
                break;
            default:
                headline = "Experiment Failed.";
                break;
        }

        return headline + "\n" + measurements + "\n" + failureReason +
               "\n\nAsk your AI Lab Assistant what went wrong and how to correct it.";
    }

    public void Reset()
    {
        for (int i = 0; i < registrationOrder.Count; i++)
        {
            currentQuantities[registrationOrder[i]] = 0.0f;
        }
        additionOrder.Clear();
        pouringSubstances.Clear();
        settleTimer = 0.0f;
        lastResult = ReactionResult.InProgress;
        offendingSubstance = string.Empty;
        failureReason = string.Empty;
        failureHeadlineOverride = string.Empty;
        reactionState = FreeHandReactionState.WaitingForInputs;
    }
}

/// <summary>
/// World-space measurement label that floats just above the target container and always faces
/// the camera. Built entirely from code so no scene or prefab edits are required.
///
/// Sizing note: TextMeshPro world-space text renders at roughly (fontSize * 0.12) metres per
/// line. The label is drawn as bold text on a solid dark backing quad, because thin bright text
/// on its own is unreadable against the white lab benches.
/// </summary>
public class FreeHandTooltip
{
    /// <summary>Metres per line is about this times the font size.</summary>
    public const float DefaultFontSize = 0.55f;

    private const int IgnoreRaycastLayer = 2;

    /// <summary>Below this camera distance the label stops growing on screen.</summary>
    public float maxApparentSizeDistance = 1.0f;

    /// <summary>Dark plate drawn behind the glyphs. RGBA hex, no leading '#'.</summary>
    public string backgroundHex = "0A1020F0";

    public static readonly Color ProgressColor = new Color32(0x7A, 0xEC, 0xFF, 0xFF); // bright cyan
    public static readonly Color SuccessColor = new Color32(0x86, 0xF7, 0xA0, 0xFF);  // bright green
    public static readonly Color FailureColor = new Color32(0xFF, 0x91, 0x80, 0xFF);  // bright salmon

    private GameObject tooltipObject;
    private TextMeshPro tooltipText;
    private Transform panel;
    private Material panelMaterial;
    private Vector2 panelPadding;

    public bool Exists { get { return tooltipObject != null; } }

    public void Create(string objectName, TMP_Text fontSource, float fontSize = DefaultFontSize)
    {
        if (tooltipObject != null)
        {
            return;
        }

        tooltipObject = new GameObject(objectName);
        tooltipObject.layer = IgnoreRaycastLayer; // Ignore Raycast - must never block pointer grabs.

        tooltipText = tooltipObject.AddComponent<TextMeshPro>();
        if (fontSource != null && fontSource.font != null)
        {
            tooltipText.font = fontSource.font;
        }

        // Horizontally centred, vertically BOTTOM. Combined with the bottom pivot below this
        // makes the anchor point the bottom edge of the text block, so the label sits directly
        // above the container instead of floating in the middle of the (much taller) rect.
        tooltipText.alignment = TextAlignmentOptions.Bottom;
        tooltipText.fontSize = fontSize;
        tooltipText.fontStyle = FontStyles.Bold;
        tooltipText.color = ProgressColor;
        tooltipText.richText = true;
        tooltipText.textWrappingMode = TextWrappingModes.NoWrap;
        tooltipText.lineSpacing = 8.0f;
        tooltipText.raycastTarget = false;

        // A hard black outline keeps the glyphs legible even where the plate is clipped.
        tooltipText.outlineColor = new Color32(0, 0, 0, 255);
        tooltipText.outlineWidth = 0.22f;

        // Kept tight on purpose: with NoWrap the width only has to avoid clipping, and a tall
        // rect would push the text away from the anchor. Bottom pivot + Bottom alignment means
        // the text block starts exactly at the transform position and grows upwards.
        RectTransform rect = tooltipText.rectTransform;
        rect.sizeDelta = new Vector2(fontSize * 24.0f, fontSize * 2.0f);
        rect.pivot = new Vector2(0.5f, 0.0f);

        panelPadding = new Vector2(fontSize * 0.80f, fontSize * 0.50f);
        CreatePanel();
    }

    /// <summary>
    /// Builds the solid backing quad. An earlier version used TMP's rich-text mark tag for this,
    /// but that renders through the font atlas material and came out washed-out pale rather than
    /// the dark plate it was given, so the panel is now a real object whose colour we control.
    /// </summary>
    private void CreatePanel()
    {
        Shader shader = FindPanelShader();
        if (shader == null)
        {
            // No usable shader in this build - the outline alone still keeps the text readable.
            return;
        }

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Backing";
        quad.layer = IgnoreRaycastLayer;

        Collider quadCollider = quad.GetComponent<Collider>();
        if (quadCollider != null)
        {
            Object.Destroy(quadCollider); // primitives ship with a collider; it would block grabs
        }

        panel = quad.transform;
        panel.SetParent(tooltipObject.transform, false);

        Color panelColor;
        if (!ColorUtility.TryParseHtmlString("#" + backgroundHex, out panelColor))
        {
            panelColor = new Color(0.04f, 0.06f, 0.13f, 0.94f);
        }

        panelMaterial = new Material(shader);
        panelMaterial.color = panelColor;

        Renderer panelRenderer = quad.GetComponent<Renderer>();
        panelRenderer.material = panelMaterial;
        panelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        panelRenderer.receiveShadows = false;
    }

    /// <summary>First shader that exists here. Sprites/Default is preferred: it alpha-blends and
    /// is Cull Off, so the quad shows no matter which way round it faces.</summary>
    private static Shader FindPanelShader()
    {
        string[] candidates =
        {
            "Sprites/Default",
            "UI/Default",
            "Unlit/Transparent",
            "Unlit/Color",
            "Legacy Shaders/Transparent/Diffuse"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            Shader shader = Shader.Find(candidates[i]);
            if (shader != null)
            {
                return shader;
            }
        }
        return null;
    }

    public void SetActive(bool isActive)
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(isActive);
        }
    }

    public void Destroy()
    {
        if (panelMaterial != null)
        {
            Object.Destroy(panelMaterial);
            panelMaterial = null;
        }
        if (tooltipObject != null)
        {
            Object.Destroy(tooltipObject); // takes the panel child with it
            tooltipObject = null;
            tooltipText = null;
            panel = null;
        }
    }

    /// <summary>
    /// Places the label above the anchor, billboards it towards the camera, and caps how large
    /// it is allowed to appear on screen.
    /// </summary>
    public void UpdatePose(Transform anchor, float heightOffset)
    {
        if (tooltipObject == null || anchor == null)
        {
            return;
        }

        tooltipObject.transform.position = anchor.position + Vector3.up * heightOffset;

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        tooltipObject.transform.rotation =
            Quaternion.LookRotation(tooltipObject.transform.position - camera.transform.position);

        // Hold a roughly constant apparent size once the player is closer than the reference
        // distance, so leaning over the bench does not blow the label up across the screen.
        float distance = Vector3.Distance(tooltipObject.transform.position, camera.transform.position);
        float scale = Mathf.Clamp(distance / Mathf.Max(0.01f, maxApparentSizeDistance), 0.45f, 1.0f);
        tooltipObject.transform.localScale = Vector3.one * scale;
    }

    public void Show(Color color, string text)
    {
        if (tooltipText == null)
        {
            return;
        }

        tooltipText.color = color;
        tooltipText.text = text;
        ResizePanel();
    }

    /// <summary>Fits the backing quad to whatever the text currently measures.</summary>
    private void ResizePanel()
    {
        if (panel == null || tooltipText == null)
        {
            return;
        }

        tooltipText.ForceMeshUpdate();
        Vector2 size = tooltipText.GetRenderedValues(false);

        if (size.x <= 0.0f || size.y <= 0.0f)
        {
            panel.gameObject.SetActive(false);
            return;
        }

        panel.gameObject.SetActive(true);
        panel.localScale = new Vector3(size.x + panelPadding.x, size.y + panelPadding.y, 1.0f);

        // Text runs from y = 0 upwards (bottom pivot), so the plate centres on half its height.
        // The small +Z pushes the plate away from the camera, behind the glyphs.
        panel.localPosition = new Vector3(0.0f, size.y * 0.5f, 0.012f);
    }

    /// <summary>Convenience: renders whichever state the engine is currently in.</summary>
    public void RenderEngineState(FreeHandReactionEngine engine, string successText)
    {
        if (engine == null || tooltipText == null)
        {
            return;
        }

        if (engine.HasSucceeded)
        {
            Show(SuccessColor, string.IsNullOrEmpty(successText) ? "Reaction Success!" : successText);
        }
        else if (engine.HasFailed)
        {
            Show(FailureColor, engine.GetFailureHeadline());
        }
        else
        {
            Show(ProgressColor, engine.GetTooltipText());
        }
    }
}
