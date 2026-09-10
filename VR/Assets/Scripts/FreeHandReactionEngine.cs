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

    /// <summary>
    /// Exam mode. Every reading still shows the student how much they have actually used, but
    /// the target quantity and the accepted range are never disclosed - not in the tracker, the
    /// tooltip, or the failure text. The judging is completely unchanged; only what is said out
    /// loud differs. Used by the testing scene, where the student is supposed to know the
    /// quantities rather than be told them.
    /// </summary>
    public bool hideTargets = false;

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

    /// <summary>
    /// Scratch buffer shared by all three readouts below.
    ///
    /// Each of them is rebuilt every frame, by every reaction script that is live, for as long as
    /// an experiment is on the bench. Built with <c>+=</c> and <c>string.Format</c> - as they
    /// were - a three-reagent experiment produced roughly a dozen short-lived strings per readout
    /// per frame, which at 60 fps is a steady several hundred kilobytes a minute of pure garbage
    /// feeding straight into the collector.
    ///
    /// Safe as a single shared instance: every caller is a MonoBehaviour <c>Update</c> on the
    /// main thread, and each finishes with the buffer before the next one starts.
    /// </summary>
    private static readonly System.Text.StringBuilder scratch = new System.Text.StringBuilder(256);

    /// <summary>Appends "name: current [/ target] unit" - the shape common to every readout.</summary>
    private void AppendReading(System.Text.StringBuilder builder, string substance, bool withTarget)
    {
        builder.Append(substance).Append(": ")
               .Append(currentQuantities[substance].ToString("F1"));

        if (withTarget)
        {
            builder.Append(" / ").Append(targetQuantities[substance].ToString("F1"));
        }

        builder.Append(' ').Append(UnitFor(substance));
    }

    /// <summary>Multi-line body used for the on-screen canvas text.</summary>
    public string GetTrackerText()
    {
        scratch.Length = 0;
        scratch.Append(trackerTitle).Append('\n');

        for (int i = 0; i < registrationOrder.Count; i++)
        {
            string substance = registrationOrder[i];
            AppendReading(scratch, substance, !hideTargets);

            if (!hideTargets)
            {
                scratch.Append("  (accept ").Append(MinAllowed(substance).ToString("F1"))
                       .Append('-').Append(MaxAllowed(substance).ToString("F1")).Append(')');
            }

            scratch.Append('\n');
        }

        if (IsAnyPouring)
        {
            foreach (string substance in pouringSubstances)
            {
                scratch.Append("\nAdding ").Append(substance).Append("...");
                if (!hideTargets)
                {
                    scratch.Append(" stop between ").Append(MinAllowed(substance).ToString("F1"))
                           .Append(" and ").Append(MaxAllowed(substance).ToString("F1"))
                           .Append(' ').Append(UnitFor(substance)).Append('.');
                }
                break;
            }
        }
        else if (reactionState == FreeHandReactionState.Settling)
        {
            scratch.Append("\nLet the mixture settle... ")
                   .Append(settleTimer.ToString("F1")).Append("s / ")
                   .Append(settleTimeRequired.ToString("F1")).Append('s');
        }

        return scratch.ToString();
    }

    /// <summary>Compact body for the floating world-space tooltip.</summary>
    public string GetTooltipText()
    {
        scratch.Length = 0;

        for (int i = 0; i < registrationOrder.Count; i++)
        {
            if (i > 0)
            {
                scratch.Append('\n');
            }

            AppendReading(scratch, registrationOrder[i], !hideTargets);
        }

        return scratch.ToString();
    }

    /// <summary>
    /// One rich-text line for the always-on readout at the top of the screen: the reaction, then
    /// every reagent, green once it is inside the accepted range.
    ///
    /// This is what lets the floating label be turned off entirely - the numbers move to the edge
    /// of the screen instead of disappearing. Honours <see cref="hideTargets"/>, so the testing
    /// scene never shows an answer here either.
    /// </summary>
    public string GetHudText(string reactionName)
    {
        System.Text.StringBuilder builder = scratch;
        builder.Length = 0;

        if (!string.IsNullOrEmpty(reactionName))
        {
            builder.Append("<color=#9FB4CC>").Append(reactionName).Append("</color>   ");
        }

        for (int i = 0; i < registrationOrder.Count; i++)
        {
            string substance = registrationOrder[i];

            if (i > 0)
            {
                builder.Append("    ");
            }

            bool inRange = IsWithinTolerance(substance);

            builder.Append("<color=").Append(inRange ? "#86F7A0" : "#7AECFF").Append('>');
            builder.Append(substance).Append(' ')
                   .Append(currentQuantities[substance].ToString("F1")).Append(' ')
                   .Append(UnitFor(substance));

            if (!hideTargets)
            {
                builder.Append(" / ").Append(targetQuantities[substance].ToString("F1"));
            }

            if (inRange)
            {
                builder.Append("  OK");
            }

            builder.Append("</color>");
        }

        // The settle wait is the one moment the lab looks like it has stopped responding - every
        // reagent is in, nothing is moving, and the verdict is still a second and a half away.
        // Saying so here removes the single most confusing pause in the whole experiment.
        if (HasSucceeded)
        {
            builder.Append("    <color=#86F7A0>SUCCESS</color>");
        }
        else if (HasFailed)
        {
            builder.Append("    <color=#FF9180>FAILED</color>");
        }
        else if (reactionState == FreeHandReactionState.Settling)
        {
            builder.AppendFormat("    <color=#FFCD5C>settling {0:F1}s / {1:F1}s</color>",
                settleTimer, settleTimeRequired);
        }

        return builder.ToString();
    }

    /// <summary>
    /// A single line rather than one per reagent, for when the full block would sit in front of
    /// the thing the student is actually pouring into. Prefers whatever is being poured right
    /// now, then the first reagent still missing, then the last one added.
    /// </summary>
    public string GetCompactTooltipText()
    {
        if (registrationOrder.Count == 0)
        {
            return string.Empty;
        }

        string focus = string.Empty;

        // 1. Whatever is actively pouring - that is what the student is watching.
        for (int i = 0; i < registrationOrder.Count; i++)
        {
            if (pouringSubstances.Contains(registrationOrder[i]))
            {
                focus = registrationOrder[i];
                break;
            }
        }

        // 2. Otherwise the first reagent that is not yet in range.
        if (string.IsNullOrEmpty(focus))
        {
            for (int i = 0; i < registrationOrder.Count; i++)
            {
                if (!IsWithinTolerance(registrationOrder[i]))
                {
                    focus = registrationOrder[i];
                    break;
                }
            }
        }

        // 3. Otherwise everything is in range - show the last one so the reading stays live.
        if (string.IsNullOrEmpty(focus))
        {
            focus = registrationOrder[registrationOrder.Count - 1];
        }

        return hideTargets
            ? string.Format("{0}: {1:F1} {2}", focus, currentQuantities[focus], UnitFor(focus))
            : string.Format("{0}: {1:F1} / {2:F1} {3}",
                focus, currentQuantities[focus], targetQuantities[focus], UnitFor(focus));
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
                return hideTargets
                    ? string.Format("FAILED: Too much {0}\n{1:F1} {2} used",
                        offendingSubstance, GetCurrent(offendingSubstance), UnitFor(offendingSubstance))
                    : string.Format("FAILED: Too much {0}\n{1:F1} {2} (max {3:F1})",
                        offendingSubstance, GetCurrent(offendingSubstance),
                        UnitFor(offendingSubstance), MaxAllowed(offendingSubstance));
            case ReactionResult.FailUnderdose:
                return hideTargets
                    ? string.Format("FAILED: Not enough {0}\n{1:F1} {2} used",
                        offendingSubstance, GetCurrent(offendingSubstance), UnitFor(offendingSubstance))
                    : string.Format("FAILED: Not enough {0}\n{1:F1} {2} (min {3:F1})",
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
            measurements += hideTargets
                ? string.Format("{0}: {1:F1} {2} used\n",
                    substance, currentQuantities[substance], UnitFor(substance))
                : ComposeMeasurementLine(substance, currentQuantities[substance],
                    UnitFor(substance), targetQuantities[substance]);
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

        return ComposeFailureExplanation(headline, measurements, failureReason,
            hideTargets ? ExamClosingLine : AssistantClosingLine);
    }

    /// <summary>Closing line for the lab, where the AI assistant is available to ask.</summary>
    /// <summary>
    /// Shown in the lab. The second line matters as much as the first: a failed experiment used to
    /// be a dead end, because re-selecting it did not reset anything and the only way out was to
    /// restart the game. <see cref="LabRetryController"/> is the way out, so the failure message is
    /// where it has to be advertised.
    /// </summary>
    public const string AssistantClosingLine =
        "Ask your AI Lab Assistant what went wrong and how to correct it.\n" +
        "Press F5 to reset the bench and try this experiment again.";

    /// <summary>
    /// Closing line for the testing scene. The assistant does not run there, and pointing the
    /// student back at the lab is the correction that does not give the answer away.
    /// </summary>
    public const string ExamClosingLine =
        "Revisit this experiment in the Lab to see the correct quantities.";

    /// <summary>
    /// The shared failure-text layout. Reaction 1 tracks its quantities inline rather than through
    /// this engine, so it calls this directly - that way all eight experiments show the student
    /// exactly the same shape of message instead of two near-miss variants.
    /// </summary>
    public static string ComposeFailureExplanation(string headline, string measurements, string reason)
    {
        return ComposeFailureExplanation(headline, measurements, reason, AssistantClosingLine);
    }

    public static string ComposeFailureExplanation(string headline, string measurements, string reason,
                                                   string closingLine)
    {
        return headline + "\n" + measurements + "\n" + reason + "\n\n" + closingLine;
    }

    /// <summary>One measurement line in the shared format: "Water: 22.4 ml (expected ~20.0)".</summary>
    public static string ComposeMeasurementLine(string substance, float current, string unit, float target)
    {
        return string.Format("{0}: {1:F1} {2} (expected ~{3:F1})\n", substance, current, unit, target);
    }

    /// <summary>The shared failure headline, so the wording matches across all eight reactions.</summary>
    public static string ComposeFailureHeadline(ReactionResult result, string substance)
    {
        switch (result)
        {
            case ReactionResult.FailOverdose:
                return "Experiment Failed - too much " + substance + ".";
            case ReactionResult.FailUnderdose:
                return "Experiment Failed - not enough " + substance + ".";
            case ReactionResult.FailWrongOrder:
                return "Experiment Failed - " + substance + " was added out of sequence.";
            default:
                return "Experiment Failed.";
        }
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
/// <summary>How much of the measurement label to show. Cycled with the L key at runtime.</summary>
public enum LabelDisplayMode
{
    /// <summary>Every reagent on its own line. The default.</summary>
    Detailed,

    /// <summary>One line - whatever is being poured right now. Keeps the vessel clear.</summary>
    Compact,

    /// <summary>Nothing above the bench. The canvas tracker still shows the numbers.</summary>
    Hidden
}

public class FreeHandTooltip
{
    /// <summary>Metres per line is about this times the font size.</summary>
    public const float DefaultFontSize = 0.55f;

    private const int IgnoreRaycastLayer = 2;

    /// <summary>
    /// Shared by every reaction, so one keypress changes whichever experiment is running rather
    /// than only the one that happens to own the label.
    /// </summary>
    public static LabelDisplayMode DisplayMode = LabelDisplayMode.Detailed;

    /// <summary>Below this camera distance the label stops growing on screen.</summary>
    public float maxApparentSizeDistance = 1.0f;

    // --- Leaning-in behaviour ---------------------------------------------------------
    // The label sits just above the container, which is exactly where the student looks while
    // pouring. Standing back that is fine; leaning in, an opaque plate 20 cm above the beaker
    // covers the beaker. Rather than move the label somewhere it cannot be found, it lifts out
    // of the way and turns translucent the closer the camera gets.

    /// <summary>Distance at which the label starts getting out of the way.</summary>
    public float comfortDistance = 0.95f;

    /// <summary>Distance at which it is fully lifted and at its faintest.</summary>
    public float nearDistance = 0.35f;

    /// <summary>Extra metres the label rises when the camera is right over the vessel.</summary>
    public float extraLiftWhenClose = 0.12f;

    /// <summary>Opacity multiplier at <see cref="nearDistance"/>. 1 = no fade.</summary>
    public float minProximityAlpha = 0.35f;

    private Color baseTextColor = Color.white;
    private float basePanelAlpha = 1.0f;

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
        basePanelAlpha = panelColor.a;

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

        if (DisplayMode == LabelDisplayMode.Hidden)
        {
            SetVisible(false);
            return;
        }
        SetVisible(true);

        Camera camera = Camera.main;
        if (camera == null)
        {
            tooltipObject.transform.position = anchor.position + Vector3.up * heightOffset;
            return;
        }

        // Measured to the anchor, not to the label: the label's own position depends on this
        // value, and feeding it back would make the placement chase itself.
        float distance = Vector3.Distance(anchor.position, camera.transform.position);

        // 0 when standing back, 1 when leaning right over the vessel.
        float span = Mathf.Max(0.01f, comfortDistance - nearDistance);
        float closeness = Mathf.Clamp01((comfortDistance - distance) / span);

        tooltipObject.transform.position =
            anchor.position + Vector3.up * (heightOffset + closeness * extraLiftWhenClose);

        tooltipObject.transform.rotation =
            Quaternion.LookRotation(tooltipObject.transform.position - camera.transform.position);

        // Hold a roughly constant apparent size once the player is closer than the reference
        // distance, so leaning over the bench does not blow the label up across the screen.
        float scale = Mathf.Clamp(distance / Mathf.Max(0.01f, maxApparentSizeDistance), 0.45f, 1.0f);
        tooltipObject.transform.localScale = Vector3.one * scale;

        // Go translucent up close so the vessel stays visible straight through the label.
        ApplyAlpha(Mathf.Lerp(1.0f, Mathf.Clamp01(minProximityAlpha), closeness));
    }

    private void SetVisible(bool visible)
    {
        if (tooltipObject != null && tooltipObject.activeSelf != visible)
        {
            tooltipObject.SetActive(visible);
        }
    }

    // Last proximity alpha UpdatePose worked out. Show() needs it too: it rewrites the text
    // colour, and without this the label would flash back to full opacity for a frame every time
    // the reading changed.
    private float currentAlpha = 1.0f;
    private float appliedAlpha = -1.0f;

    private void ApplyAlpha(float alpha)
    {
        currentAlpha = alpha;

        // Assigning TMP_Text.color marks the mesh dirty and rebuilds its vertex colours, so only
        // pay for it when the value has actually moved.
        if (Mathf.Abs(alpha - appliedAlpha) < 0.01f)
        {
            return;
        }
        appliedAlpha = alpha;

        if (tooltipText != null)
        {
            Color color = baseTextColor;
            color.a = baseTextColor.a * alpha;
            tooltipText.color = color;
        }

        if (panelMaterial != null)
        {
            Color color = panelMaterial.color;
            color.a = basePanelAlpha * alpha;
            panelMaterial.color = color;
        }
    }

    public void Show(Color color, string text)
    {
        if (tooltipText == null)
        {
            return;
        }

        // Remembered unfaded: UpdatePose scales alpha off these every frame, so writing the
        // faded colour back here would make the label darken a little more each frame.
        baseTextColor = color;

        // Written at the current proximity alpha, not at full opacity, so a changing reading
        // does not un-fade the label for a frame.
        Color faded = color;
        faded.a = color.a * currentAlpha;
        tooltipText.color = faded;

        // The reaction scripts call this every frame, and ResizePanel does a ForceMeshUpdate plus
        // a GetRenderedValues - a full text re-layout. Only pay for it when the string actually
        // changed, which for a "20.4 / 20.0 ml" readout is a few times a second at most.
        if (text == lastRenderedText)
        {
            return;
        }

        lastRenderedText = text;
        tooltipText.text = text;
        ResizePanel();
    }

    private string lastRenderedText;

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

        if (DisplayMode == LabelDisplayMode.Hidden)
        {
            return;
        }

        // Success and failure are the payoff of the whole experiment - they are shown in full
        // whatever the mode. Only the live running measurements get compacted.
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
            Show(ProgressColor, DisplayMode == LabelDisplayMode.Compact
                ? engine.GetCompactTooltipText()
                : engine.GetTooltipText());
        }
    }
}
