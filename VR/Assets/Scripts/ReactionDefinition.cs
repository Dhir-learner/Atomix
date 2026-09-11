using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What the engine is measuring for an experiment.</summary>
public enum ReactionKind
{
    /// <summary>Reagents poured or dropped in, measured in ml or g.</summary>
    Measured = 0,

    /// <summary>A sample held over a flame, measured in seconds.</summary>
    Heating = 1
}

/// <summary>How a reagent physically gets into the vessel.</summary>
public enum ReagentAddition
{
    /// <summary>Tipped out of a vessel; the rate follows the tilt.</summary>
    Pour = 0,

    /// <summary>One measured piece that counts as a full dose the moment it lands.</summary>
    Lump = 1,

    /// <summary>Time over the burner.</summary>
    Heat = 2,

    /// <summary>Drops from the pipette, at one fixed rate.</summary>
    Dropper = 3
}

/// <summary>How a reagent's amount responds when a stoichiometry challenge asks for more or less product.</summary>
public enum ChallengeRole
{
    /// <summary>Not consumed - a catalyst. The standard amount is used whatever is asked for.</summary>
    Fixed = 0,

    /// <summary>In the balanced equation. Worked out from moles and the coefficient.</summary>
    Reactant = 1,

    /// <summary>A medium used in proportion to the basis reagent, at the recipe's own ratio.</summary>
    Solvent = 2
}

/// <summary>One measured input of an experiment.</summary>
[Serializable]
public class ReagentDefinition
{
    [Tooltip("Shown in the tracker and stored in the history. Unique within the experiment.")]
    public string name;

    [Tooltip("ml, g or s.")]
    public string unit = "ml";

    [Tooltip("The amount the experiment needs.")]
    public float target;

    [Tooltip("Units per second at the vessel's full rate - fully tipped, for a pour.")]
    public float flowPerSecond;

    [Tooltip("This reagent's own tolerance in percent. -1 uses the experiment's tolerance.")]
    public float tolerancePercentOverride = -1.0f;

    [Tooltip("Off for a reagent that may go in at any point.")]
    public bool orderMatters = true;

    [Tooltip("Reagents sharing a group may go in either order among themselves. -1 uses the position in the list.")]
    public int orderGroup = -1;

    public ReagentAddition addition;

    [TextArea(2, 4)] public string overdoseMessage;
    [TextArea(2, 4)] public string underdoseMessage;

    [Tooltip("Lump only: seconds the container may stay tipped after the piece drops before more falls in.")]
    public float lumpGraceSeconds;

    [Tooltip("Lump only: extra units per second that fall in once the grace period is over.")]
    public float lumpOverflowPerSecond;

    [Tooltip("Chemical formula, for the stoichiometry challenge's molar masses. H2O, CuO, Ca(OH)2 ...")]
    public string formula;

    [Tooltip("Coefficient in the balanced equation. 0 when the reagent is not in it.")]
    public int coefficient;

    public ChallengeRole challengeRole;
}

/// <summary>
/// The measured recipe for one experiment - reagents, targets, units, tolerances, addition order
/// and the chemistry behind every way it can fail.
///
/// This used to be written out twice for every reaction, once in the Lab script and once in its
/// testing-scene twin, and the two copies were already drifting: the sodium experiment gave a
/// different explanation for too much water depending on which scene you were in. Both copies
/// now read the same asset from <c>Resources/ReactionDefinitions</c>, so they cannot disagree,
/// and a new experiment's numbers are a data file rather than code.
///
/// What stays in code is everything that is not a number: the explosions, the balloon, the
/// burner, the colour change - the scripts that stage each reaction.
/// </summary>
[CreateAssetMenu(fileName = "ReactionDefinition", menuName = "Atomix/Reaction Definition")]
public class ReactionDefinition : ScriptableObject
{
    /// <summary>Folder under Resources the definitions are loaded from.</summary>
    public const string ResourceFolder = "ReactionDefinitions";

    [Tooltip("The book / StartReaction number, 1-8.")]
    public int reactionId;

    [Tooltip("Name the Lab records attempts under.")]
    public string displayName;

    [Tooltip("Name the testing scene records attempts under.")]
    public string examDisplayName;

    public string equation;
    public ReactionKind kind;

    [Tooltip("Accepted deviation from each target, in percent, at the Standard level.")]
    public float tolerancePercent = 5.0f;

    [Tooltip("Seconds everything must sit still, all in, before the verdict.")]
    public float settleSeconds = 1.5f;

    public bool enforceOrder = true;

    [TextArea(2, 4)] public string wrongOrderMessage;

    [Tooltip("Reason given when the procedure itself goes wrong - e.g. heating before the balloon is on.")]
    [TextArea(2, 4)] public string procedureFailureReason;

    [Tooltip("Short banner for that failure. Use \\n for a second line.")]
    public string procedureFailureHeadline;

    public List<ReagentDefinition> reagents = new List<ReagentDefinition>();

    [Header("Stoichiometry challenge")]
    [Tooltip("Can the student be asked for a product amount instead? Pour and dropper reagents only.")]
    public bool challengeEligible;

    public string productName;
    public string productFormula;
    public int productCoefficient = 1;

    [Tooltip("The reactant, in grams, the recipe's standard yield is worked out from.")]
    public string challengeBasisReagent;

    public string LabTrackerTitle
    {
        get { return kind == ReactionKind.Heating ? "[Lab Heating Tracker]" : "[Lab Measurement Tracker]"; }
    }

    public string ExamTrackerTitle
    {
        get { return kind == ReactionKind.Heating ? "[Heating Time Used]" : "[Chemicals Used]"; }
    }

    // =========================================================
    // LOOKUP
    // =========================================================

    public ReagentDefinition Reagent(string reagentName)
    {
        if (reagents == null || string.IsNullOrEmpty(reagentName))
        {
            return null;
        }

        for (int i = 0; i < reagents.Count; i++)
        {
            if (reagents[i] != null && reagents[i].name == reagentName)
            {
                return reagents[i];
            }
        }
        return null;
    }

    public float TargetFor(string reagentName)
    {
        ReagentDefinition reagent = Reagent(reagentName);
        return reagent != null ? reagent.target : 0.0f;
    }

    public float FlowFor(string reagentName)
    {
        ReagentDefinition reagent = Reagent(reagentName);
        return reagent != null ? reagent.flowPerSecond : 0.0f;
    }

    /// <summary>The Standard-level tolerance for a reagent, in percent.</summary>
    public float ToleranceFor(string reagentName)
    {
        ReagentDefinition reagent = Reagent(reagentName);
        return reagent != null && reagent.tolerancePercentOverride > 0.0f
            ? reagent.tolerancePercentOverride
            : tolerancePercent;
    }

    public Dictionary<string, float> StandardTargets()
    {
        Dictionary<string, float> targets = new Dictionary<string, float>();
        if (reagents == null)
        {
            return targets;
        }

        for (int i = 0; i < reagents.Count; i++)
        {
            if (reagents[i] != null && !string.IsNullOrEmpty(reagents[i].name))
            {
                targets[reagents[i].name] = reagents[i].target;
            }
        }
        return targets;
    }

    // =========================================================
    // ENGINE SET-UP
    // =========================================================

    /// <summary>
    /// Registers every reagent on a fresh engine, exactly as the reaction scripts used to do one
    /// <c>AddSubstance</c> call at a time. Call once per engine.
    /// </summary>
    public void Configure(FreeHandReactionEngine engine)
    {
        if (engine == null)
        {
            return;
        }

        engine.tolerancePercent = tolerancePercent;
        engine.settleTimeRequired = settleSeconds;
        engine.enforceOrder = enforceOrder;

        if (!string.IsNullOrEmpty(wrongOrderMessage))
        {
            engine.wrongOrderMessage = wrongOrderMessage;
        }

        for (int i = 0; i < reagents.Count; i++)
        {
            ReagentDefinition reagent = reagents[i];
            if (reagent == null)
            {
                continue;
            }

            engine.AddSubstance(reagent.name, reagent.target, reagent.unit,
                tolerancePercentOverride: reagent.tolerancePercentOverride,
                overdose: reagent.overdoseMessage,
                underdose: reagent.underdoseMessage,
                orderMatters: reagent.orderMatters,
                orderGroup: reagent.orderGroup);
        }
    }

    /// <summary>Puts every target back to the recipe's own amount, undoing a challenge.</summary>
    public void ResetTargets(FreeHandReactionEngine engine)
    {
        if (engine == null)
        {
            return;
        }

        for (int i = 0; i < reagents.Count; i++)
        {
            if (reagents[i] != null)
            {
                engine.SetTarget(reagents[i].name, reagents[i].target);
            }
        }
    }

    /// <summary>Fails the experiment for the definition's procedure reason (e.g. no balloon fitted).</summary>
    public ReactionResult ForceProcedureFailure(FreeHandReactionEngine engine, string reagentName)
    {
        if (engine == null)
        {
            return ReactionResult.InProgress;
        }

        return engine.ForceFailure(reagentName, procedureFailureReason,
            ReactionResult.FailWrongOrder, procedureFailureHeadline);
    }

    /// <summary>Checks the asset is usable. A broken one is never handed to a reaction.</summary>
    public bool IsValid(out string problem)
    {
        problem = string.Empty;

        if (reactionId < 1)
        {
            problem = "reactionId is not set";
            return false;
        }
        if (string.IsNullOrEmpty(displayName))
        {
            problem = "displayName is empty";
            return false;
        }
        if (tolerancePercent <= 0.0f)
        {
            problem = "tolerancePercent must be above zero";
            return false;
        }
        if (reagents == null || reagents.Count == 0)
        {
            problem = "there are no reagents";
            return false;
        }

        HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < reagents.Count; i++)
        {
            ReagentDefinition reagent = reagents[i];
            if (reagent == null || string.IsNullOrEmpty(reagent.name))
            {
                problem = "reagent " + i + " has no name";
                return false;
            }
            if (!seen.Add(reagent.name))
            {
                problem = "reagent '" + reagent.name + "' is listed twice";
                return false;
            }
            if (reagent.target <= 0.0f)
            {
                problem = "reagent '" + reagent.name + "' has no target";
                return false;
            }
            if (string.IsNullOrEmpty(reagent.unit))
            {
                problem = "reagent '" + reagent.name + "' has no unit";
                return false;
            }
        }

        return true;
    }

    // =========================================================
    // LOADING
    // =========================================================

    private static readonly Dictionary<int, ReactionDefinition> cache = new Dictionary<int, ReactionDefinition>();

    /// <summary>
    /// The definition for a reaction: <c>Resources/ReactionDefinitions/ReactionDefinition_{id}</c>.
    ///
    /// Should that asset be missing or fail its checks, the recipe built into
    /// <see cref="ReactionDefinitionDefaults"/> is used instead and a warning is logged, so a
    /// damaged data file can never leave an experiment with no reagents at all. Returns null only
    /// for an id that has neither.
    /// </summary>
    public static ReactionDefinition Load(int reactionId)
    {
        ReactionDefinition definition;
        if (cache.TryGetValue(reactionId, out definition) && definition != null)
        {
            return definition;
        }

        definition = Resources.Load<ReactionDefinition>(ResourceFolder + "/ReactionDefinition_" + reactionId);

        string problem = string.Empty;
        if (definition != null && definition.reactionId != reactionId)
        {
            problem = "it is set up as reaction " + definition.reactionId;
            definition = null;
        }
        else if (definition != null && !definition.IsValid(out problem))
        {
            definition = null;
        }
        else if (definition == null)
        {
            problem = "the asset could not be found";
        }

        if (definition == null)
        {
            definition = ReactionDefinitionDefaults.Create(reactionId);
            if (definition != null)
            {
                Debug.LogWarning("[ReactionDefinition] Using the built-in recipe for reaction " + reactionId +
                                 " because " + problem + ".");
            }
        }

        if (definition != null)
        {
            cache[reactionId] = definition;
        }

        return definition;
    }
}
