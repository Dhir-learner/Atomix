using System.Collections.Generic;

/// <summary>
/// How the next Lab run of each experiment is set up: its difficulty level, and whether it is a
/// stoichiometry challenge. Chosen on the book's LEARN/PERFORM page, applied by each Lab reaction
/// script where it sets up its engine - on first selection and on every re-selection.
///
/// The testing scene never reads any of this: its eight scripts go through
/// <see cref="ExamReactionRunner"/>, which always judges at the recipe's own tolerance with the
/// targets hidden.
///
/// Static on purpose. The F5 retry reloads the whole lab scene, and a challenge the student is
/// retrying has to survive that reload to be set again.
/// </summary>
public static class LabRunOptions
{
    private static StoichiometryChallenge queuedChallenge;

    // What is actually on the bench right now, as last applied by a reaction script. Lets the book
    // tell "pick the same experiment again, same settings" (nothing to do, as always) apart from
    // "same experiment, different settings" (the bench must be reset for them to apply).
    private static int benchReactionId = -1;
    private static LabDifficulty benchLevel = LabDifficulty.Standard;
    private static StoichiometryChallenge benchChallenge;

    // =========================================================
    // LEVEL
    // =========================================================

    /// <summary>
    /// Expert is earned per experiment by passing it at Standard or harder - in the Lab, in a
    /// challenge, or in the testing scene. Guided passes do not count.
    /// </summary>
    public static bool IsExpertUnlocked(int reactionId)
    {
        List<ExperimentAttempt> attempts = ExperimentHistoryManager.Instance.GetHistoryForReaction(reactionId);
        for (int i = 0; i < attempts.Count; i++)
        {
            ExperimentAttempt attempt = attempts[i];
            if (attempt != null && attempt.outcome == ExperimentOutcome.Success &&
                attempt.Difficulty != LabDifficulty.Guided)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The level the next run of this experiment will use.</summary>
    public static LabDifficulty SelectedDifficulty(int reactionId)
    {
        LabDifficulty level = AtomixSettings.GetLabDifficulty(reactionId);

        // A saved Expert choice whose unlock has since gone (the history was cleared) falls back
        // rather than letting a locked level through.
        if (level == LabDifficulty.Expert && !IsExpertUnlocked(reactionId))
        {
            return LabDifficulty.Standard;
        }
        return level;
    }

    // =========================================================
    // CHALLENGE
    // =========================================================

    /// <summary>Makes the next run of the challenge's experiment a challenge.</summary>
    public static void QueueChallenge(StoichiometryChallenge challenge)
    {
        queuedChallenge = challenge;
    }

    /// <summary>The next run of every experiment is a normal one.</summary>
    public static void ClearChallenge()
    {
        queuedChallenge = null;
    }

    /// <summary>The challenge queued for this experiment, or null.</summary>
    public static StoichiometryChallenge ChallengeFor(int reactionId)
    {
        return queuedChallenge != null && queuedChallenge.ReactionId == reactionId ? queuedChallenge : null;
    }

    // =========================================================
    // APPLYING
    // =========================================================

    /// <summary>
    /// Sets an engine and its recorder up for the next run. Call after the definition has
    /// configured the engine, and again after every <see cref="FreeHandReactionEngine.Reset"/>:
    /// the targets, tolerance and visibility are all set from scratch each time, so nothing a
    /// previous run changed can leak into the next.
    /// </summary>
    public static void Apply(int reactionId, ReactionDefinition definition,
                             FreeHandReactionEngine engine, ReactionHistoryRecorder recorder)
    {
        if (engine == null || definition == null)
        {
            return;
        }

        StoichiometryChallenge challenge = ChallengeFor(reactionId);

        // A challenge is its own mode, judged at the recipe's own tolerance.
        LabDifficulty level = challenge != null ? LabDifficulty.Standard : SelectedDifficulty(reactionId);
        bool hidden = challenge != null || level == LabDifficulty.Expert;

        definition.ResetTargets(engine);
        if (challenge != null)
        {
            challenge.ApplyTargets(engine);
        }

        engine.toleranceScale = ExperimentScoring.ToleranceScale(level);
        engine.hideTargets = hidden;
        engine.revealTargetsWhenResolved = hidden;
        engine.trackerTitle = challenge != null ? challenge.TrackerTitle : definition.LabTrackerTitle;
        engine.hudCaption = challenge != null ? challenge.Headline : string.Empty;
        engine.failureAppendix = challenge != null ? challenge.WorkedSolution : string.Empty;

        if (recorder != null)
        {
            // Keeps the targets out of the step log too, which the history panel shows live -
            // otherwise the first "Added 3.1 ml (target 12.5)" would hand over the answer.
            recorder.hideTargets = hidden;
            recorder.Difficulty = level;
            recorder.Challenge = challenge;
        }

        benchReactionId = reactionId;
        benchLevel = level;
        benchChallenge = challenge;
    }

    /// <summary>Called when an experiment's equipment is put away.</summary>
    public static void NoteBenchCleared(int reactionId)
    {
        if (benchReactionId == reactionId)
        {
            benchReactionId = -1;
            benchChallenge = null;
        }
    }

    /// <summary>
    /// True when this experiment is already on the bench but set up differently from how it
    /// would be set up now. Picking an experiment that is already out does not reset anything,
    /// so new settings could only reach it through a bench reset.
    /// </summary>
    public static bool NeedsBenchReset(int reactionId)
    {
        if (benchReactionId != reactionId)
        {
            return false;
        }

        StoichiometryChallenge challenge = ChallengeFor(reactionId);
        LabDifficulty level = challenge != null ? LabDifficulty.Standard : SelectedDifficulty(reactionId);

        bool sameChallenge = challenge == null
            ? benchChallenge == null
            : challenge.SameAs(benchChallenge);

        return level != benchLevel || !sameChallenge;
    }
}
