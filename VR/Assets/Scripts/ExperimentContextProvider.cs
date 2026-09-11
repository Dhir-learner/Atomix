using System.Collections.Generic;
using System.Text;

/// <summary>
/// Turns the live state of whatever experiment the student is doing into a short briefing for the
/// AI lab assistant, so "why did it go wrong?" can be answered specifically instead of generically.
///
/// Reads from <see cref="ReactionHistoryRecorder.Active"/> (live quantities, tolerances, failure
/// reason) and falls back to <see cref="ExperimentHistoryManager"/> for attempt counts and for the
/// last finished experiment once the current one has been cleared.
///
/// Deliberately a plain static class: nothing to place in a scene, and every reaction script
/// already feeds it through the recorder it owns.
/// </summary>
public static class ExperimentContextProvider
{
    /// <summary>Prefixed to the briefing so the character knows this is stage direction.</summary>
    public const string ContextHeader = "[LAB CONTEXT - do not read this aloud]";

    /// <summary>True when there is an experiment worth telling the assistant about.</summary>
    public static bool HasContext
    {
        get
        {
            return ReactionHistoryRecorder.Active != null ||
                   ExperimentHistoryManager.Instance.GetMostRecentAttempt() != null;
        }
    }

    /// <summary>Reaction id the student is on, or -1.</summary>
    public static int CurrentReactionId
    {
        get
        {
            ReactionHistoryRecorder active = ReactionHistoryRecorder.Active;
            if (active != null)
            {
                return active.ReactionId;
            }

            ExperimentAttempt latest = ExperimentHistoryManager.Instance.GetMostRecentAttempt();
            return latest != null ? latest.reactionId : -1;
        }
    }

    public static string CurrentReactionName
    {
        get
        {
            ReactionHistoryRecorder active = ReactionHistoryRecorder.Active;
            if (active != null)
            {
                return active.ReactionName;
            }

            ExperimentAttempt latest = ExperimentHistoryManager.Instance.GetMostRecentAttempt();
            return latest != null ? latest.reactionName : string.Empty;
        }
    }

    /// <summary>
    /// A short string that changes whenever anything the assistant should know about changes.
    /// Lets the caller avoid re-sending an identical briefing every time the student speaks.
    /// </summary>
    public static string BuildDigest()
    {
        ReactionHistoryRecorder active = ReactionHistoryRecorder.Active;
        if (active == null)
        {
            ExperimentAttempt latest = ExperimentHistoryManager.Instance.GetMostRecentAttempt();
            return latest == null ? "none" : latest.attemptId + ":" + (int)latest.outcome;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append(active.ReactionId).Append(':').Append(active.AttemptId).Append(':');

        FreeHandReactionEngine engine = active.Engine;
        if (engine != null)
        {
            builder.Append((int)engine.reactionState).Append(':');
            List<string> substances = engine.Substances;
            for (int i = 0; i < substances.Count; i++)
            {
                // One decimal place is plenty - it stops tiny per-frame drift counting as a change.
                builder.Append(substances[i]).Append('=')
                       .Append(engine.GetCurrent(substances[i]).ToString("F1")).Append(',');
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The full briefing sent to the assistant as a narrative action before the student's question.
    /// </summary>
    public static string BuildContext()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(ContextHeader).Append(' ');

        ReactionHistoryRecorder active = ReactionHistoryRecorder.Active;
        ExperimentHistoryManager manager = ExperimentHistoryManager.Instance;

        if (active == null)
        {
            ExperimentAttempt latest = manager.GetMostRecentAttempt();
            if (latest == null)
            {
                builder.Append("The student has not started an experiment yet. ");
                builder.Append("Answer their chemistry questions generally and encourage them to pick an experiment from the book.");
                return builder.ToString();
            }

            builder.AppendFormat("The student's most recent experiment was {0} ({1}). ",
                latest.reactionName, latest.OutcomeLabel);
            AppendQuantitiesFromAttempt(builder, latest);
            AppendFailureFromAttempt(builder, latest);
            AppendAttemptTally(builder, manager, latest.reactionId);
            AppendClosingInstruction(builder);
            return builder.ToString();
        }

        builder.AppendFormat("The student is performing experiment {0}: {1}. ",
            active.ReactionId, active.ReactionName);

        FreeHandReactionEngine engine = active.Engine;
        if (engine == null)
        {
            // Only a reaction run with its free-hand judging switched off has no engine.
            ExperimentAttempt attempt = manager.FindAttempt(active.AttemptId);
            if (attempt != null)
            {
                AppendQuantitiesFromAttempt(builder, attempt);
                AppendFailureFromAttempt(builder, attempt);
            }
            AppendAttemptTally(builder, manager, active.ReactionId);
            AppendClosingInstruction(builder);
            return builder.ToString();
        }

        AppendLiveQuantities(builder, engine);
        AppendLiveStatus(builder, engine);
        AppendRunMode(builder, active, engine);
        AppendAttemptTally(builder, manager, active.ReactionId);
        AppendClosingInstruction(builder);
        return builder.ToString();
    }

    /// <summary>
    /// Tells the assistant how this run is set, and - while the targets are hidden - that it must
    /// not hand them over. A cloud model that is simply told the answer will say it if asked.
    /// </summary>
    private static void AppendRunMode(StringBuilder builder, ReactionHistoryRecorder active,
                                      FreeHandReactionEngine engine)
    {
        StoichiometryChallenge challenge = active.Challenge;
        if (challenge != null)
        {
            builder.Append("This is a stoichiometry challenge. The student's brief: ")
                   .Append(challenge.Brief.Replace('\n', ' ')).Append(' ');

            if (engine.IsResolved)
            {
                builder.Append("It has been judged, so walk them through the calculation: ")
                       .Append(challenge.WorkedSolution.Replace('\n', ' ')).Append(' ');
            }
            else
            {
                builder.Append("Do NOT tell them the amounts of any reagent; coach them through the " +
                               "method (moles of product, mole ratio, then mass or volume) instead. ");
            }
            return;
        }

        if (active.Difficulty == LabDifficulty.Expert && !engine.IsResolved)
        {
            builder.Append("The student chose the Expert level, where the target amounts are hidden. " +
                           "Do NOT tell them the target amounts or the accepted range. ");
        }
        else if (active.Difficulty == LabDifficulty.Guided)
        {
            builder.Append("The student is on the Guided level, which allows a wider margin for error. ");
        }
    }

    /// <summary>
    /// Briefing for the graph panel's "Ask AI to Explain" button. Hands the assistant the exact
    /// numbers on screen plus the prepared explanation, so the answer is about the graph the
    /// student is looking at rather than the reaction in general.
    /// </summary>
    public static string BuildGraphContext(ReactionGraphData data, ReactionGraphType graphType)
    {
        if (data == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append(ContextHeader).Append(' ');

        builder.AppendFormat(
            "The student has just finished experiment {0} ({1}) and is looking at its {2} graph. ",
            data.reactionId, data.reactionName, GraphTypeLabel(graphType));

        builder.AppendFormat(
            "The values on screen are: enthalpy change dH = {0:+0.0;-0.0} kJ/mol, " +
            "activation energy Ea = {1:F0} kJ/mol, entropy change dS = {2:+0.0;-0.0} J/(mol K), " +
            "and Gibbs free energy at 25 C dG = {3:+0.0;-0.0} kJ/mol. The reaction is {4}. ",
            data.enthalpyChange, data.activationEnergy, data.entropyChange,
            data.StandardGibbsFreeEnergy,
            data.IsExothermic ? "EXOTHERMIC" : "ENDOTHERMIC");

        builder.Append(data.SpontaneitySummary()).Append(' ');

        if (!string.IsNullOrEmpty(data.graphExplanationText))
        {
            builder.Append("Explain it along these lines, in your own words: ")
                   .Append(data.graphExplanationText).Append(' ');
        }

        // The student's own run is what makes this personal rather than a textbook recital.
        ExperimentHistoryManager manager = ExperimentHistoryManager.Instance;
        if (manager != null)
        {
            AppendAttemptTally(builder, manager, data.reactionId);
        }

        builder.Append("Speak it as a short spoken explanation - about four or five sentences. ");
        builder.Append("Use the numbers above, and do not read out this instruction.");

        return builder.ToString();
    }

    private static string GraphTypeLabel(ReactionGraphType graphType)
    {
        switch (graphType)
        {
            case ReactionGraphType.EnergyProfile:
                return "energy vs reaction progress";
            case ReactionGraphType.EnthalpyLevels:
                return "exothermic/endothermic enthalpy level";
            case ReactionGraphType.Entropy:
                return "entropy";
            default:
                return "reaction";
        }
    }

    private static void AppendLiveQuantities(StringBuilder builder, FreeHandReactionEngine engine)
    {
        List<string> substances = engine.Substances;
        if (substances.Count == 0)
        {
            return;
        }

        // With the targets hidden, neither the target nor the verdict on each amount goes in:
        // "TOO MUCH" gives the answer away as surely as the number does.
        bool showTargets = engine.ShowTargetsNow;

        builder.Append("Measured so far: ");
        for (int i = 0; i < substances.Count; i++)
        {
            string substance = substances[i];
            float current = engine.GetCurrent(substance);
            float target = engine.targetQuantities[substance];
            string unit = engine.UnitFor(substance);

            if (i > 0)
            {
                builder.Append("; ");
            }

            if (!showTargets)
            {
                if (current <= 0.0f)
                {
                    builder.AppendFormat("{0} not added yet", substance);
                }
                else
                {
                    builder.AppendFormat("{0} {1:F1} {2}", substance, current, unit);
                }
                continue;
            }

            if (current <= 0.0f)
            {
                builder.AppendFormat("{0} not added yet (needs about {1:F1} {2})", substance, target, unit);
                continue;
            }

            string verdict = engine.IsWithinTolerance(substance) ? "correct"
                           : current > engine.MaxAllowed(substance) ? "TOO MUCH"
                           : "TOO LITTLE";

            builder.AppendFormat("{0} {1:F1} {2} against a target of {3:F1} {2} (accepted {4:F1}-{5:F1}) - {6}",
                substance, current, unit, target,
                engine.MinAllowed(substance), engine.MaxAllowed(substance), verdict);
        }
        builder.Append(". ");
    }

    private static void AppendLiveStatus(StringBuilder builder, FreeHandReactionEngine engine)
    {
        if (engine.HasSucceeded)
        {
            builder.Append("The experiment SUCCEEDED. ");
            return;
        }

        if (engine.HasFailed)
        {
            builder.Append("The experiment FAILED. ");
            if (!string.IsNullOrEmpty(engine.failureReason))
            {
                builder.Append("The chemical reason is: ").Append(engine.failureReason).Append(' ');
            }
            return;
        }

        string pending = engine.GetPendingSubstance();
        if (!string.IsNullOrEmpty(pending))
        {
            builder.AppendFormat("Still to add: {0}. ", pending);
        }
        else
        {
            builder.Append("Everything is in the vessel and the mixture is settling. ");
        }
    }

    private static void AppendQuantitiesFromAttempt(StringBuilder builder, ExperimentAttempt attempt)
    {
        if (attempt.quantitiesUsed.Count == 0)
        {
            return;
        }

        builder.Append("They used: ");
        bool first = true;
        foreach (KeyValuePair<string, float> pair in attempt.quantitiesUsed)
        {
            if (!first)
            {
                builder.Append("; ");
            }
            first = false;

            float target;
            if (attempt.targetQuantities.TryGetValue(pair.Key, out target))
            {
                builder.AppendFormat("{0} {1:F1} against a target of {2:F1}", pair.Key, pair.Value, target);
            }
            else
            {
                builder.AppendFormat("{0} {1:F1}", pair.Key, pair.Value);
            }
        }
        builder.Append(". ");
    }

    private static void AppendFailureFromAttempt(StringBuilder builder, ExperimentAttempt attempt)
    {
        if (!attempt.IsFailure)
        {
            return;
        }

        // The recorder writes the engine's chemistry explanation in as a step.
        for (int i = attempt.steps.Count - 1; i >= 0; i--)
        {
            string action = attempt.steps[i].action;
            if (!string.IsNullOrEmpty(action) && action.StartsWith("Why it failed: "))
            {
                builder.Append("The chemical reason is: ")
                       .Append(action.Substring("Why it failed: ".Length))
                       .Append(' ');
                return;
            }
        }
    }

    private static void AppendAttemptTally(StringBuilder builder, ExperimentHistoryManager manager, int reactionId)
    {
        if (reactionId < 0)
        {
            return;
        }

        List<ExperimentAttempt> previous = manager.GetHistoryForReaction(reactionId);
        int successes = 0;
        for (int i = 0; i < previous.Count; i++)
        {
            if (previous[i].outcome == ExperimentOutcome.Success)
            {
                successes++;
            }
        }

        builder.AppendFormat("This is attempt {0} at this experiment; {1} of the previous ones succeeded. ",
            previous.Count, successes);
    }

    private static void AppendClosingInstruction(StringBuilder builder)
    {
        builder.Append("Use these exact numbers when explaining what the student did. ");
        builder.Append("Keep the answer short, specific and encouraging.");
    }
}
