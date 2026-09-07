using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One unlockable milestone.</summary>
public class Achievement
{
    public string id;
    public string title;
    public string description;
    public bool hidden;          // not described until earned

    public Achievement(string id, string title, string description, bool hidden = false)
    {
        this.id = id;
        this.title = title;
        this.description = description;
        this.hidden = hidden;
    }
}

/// <summary>
/// Light progress tracking layered on top of <see cref="ExperimentHistoryManager"/>.
///
/// Everything here is derived from data the history already records - no reaction script knows
/// this exists. It hangs off <see cref="ReactionHistoryRecorder.Completed"/>, the same single
/// funnel the graphs and the video sequencer use, so all eight experiments feed it for free.
///
/// The point is not gamification for its own sake: "you have now succeeded at all eight" and
/// "you got this one right first time" are exactly the summaries a student cannot easily read
/// out of a list of attempts, and a teacher wants to see.
/// </summary>
public class AchievementSystem : MonoBehaviour
{
    private const string Prefix = "atomix.achievement.";

    public static AchievementSystem Instance { get; private set; }

    /// <summary>Raised when something is unlocked, so the HUD can toast it.</summary>
    public static event Action<Achievement> Unlocked;

    private readonly List<Achievement> catalogue = new List<Achievement>();
    private readonly HashSet<string> earned = new HashSet<string>();

    public IList<Achievement> Catalogue { get { return catalogue; } }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        BuildCatalogue();
        Load();
    }

    void OnEnable()
    {
        ReactionHistoryRecorder.Completed += HandleCompleted;
    }

    void OnDisable()
    {
        ReactionHistoryRecorder.Completed -= HandleCompleted;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void BuildCatalogue()
    {
        catalogue.Clear();
        catalogue.Add(new Achievement("first_success", "First Reaction",
            "Complete any experiment successfully."));
        catalogue.Add(new Achievement("first_time_right", "Measured Twice",
            "Succeed at an experiment on your very first attempt at it."));
        catalogue.Add(new Achievement("three_reactions", "Getting Comfortable",
            "Succeed at three different experiments."));
        catalogue.Add(new Achievement("all_reactions", "Full Bench",
            "Succeed at all eight experiments."));
        catalogue.Add(new Achievement("precise", "Steady Hand",
            "Finish an experiment with every reagent inside half the allowed tolerance."));
        catalogue.Add(new Achievement("learn_from_failure", "Back to the Bench",
            "Fail an experiment, then succeed at the same one afterwards."));
        catalogue.Add(new Achievement("asked_ai", "Ask the Assistant",
            "Ask the AI lab assistant a question about an experiment."));
        catalogue.Add(new Achievement("watched_video", "Molecular View",
            "Watch a molecular visualisation after a successful experiment."));
        catalogue.Add(new Achievement("read_graphs", "Thermodynamicist",
            "Open the scientific graphs for a completed reaction."));
        catalogue.Add(new Achievement("periodic_table", "Know Your Elements",
            "Open the periodic table reference."));
        catalogue.Add(new Achievement("exam_pass", "Examined",
            "Finish a full run of the testing phase."));
        catalogue.Add(new Achievement("exam_distinction", "Distinction",
            "Finish the testing phase with a grade of A or better.", hidden: true));
        catalogue.Add(new Achievement("chemist", "Chemist",
            "Succeed at all eight experiments without a single failure on your record.", hidden: true));
    }

    // =========================================================
    // UNLOCKING
    // =========================================================

    /// <summary>Unlocks by id. Safe to call repeatedly; only the first call does anything.</summary>
    public void Unlock(string id)
    {
        if (string.IsNullOrEmpty(id) || earned.Contains(id))
        {
            return;
        }

        Achievement achievement = Find(id);
        if (achievement == null)
        {
            return;
        }

        earned.Add(id);
        PlayerPrefs.SetInt(Prefix + id, 1);
        PlayerPrefs.Save();

        if (Unlocked != null)
        {
            Unlocked(achievement);
        }
    }

    public bool IsEarned(string id)
    {
        return earned.Contains(id);
    }

    public int EarnedCount { get { return earned.Count; } }

    public Achievement Find(string id)
    {
        for (int i = 0; i < catalogue.Count; i++)
        {
            if (catalogue[i].id == id)
            {
                return catalogue[i];
            }
        }
        return null;
    }

    private void Load()
    {
        earned.Clear();
        for (int i = 0; i < catalogue.Count; i++)
        {
            if (PlayerPrefs.GetInt(Prefix + catalogue[i].id, 0) == 1)
            {
                earned.Add(catalogue[i].id);
            }
        }
    }

    /// <summary>Clears every unlock. Wired to the history panel's Clear button.</summary>
    public void ResetAll()
    {
        for (int i = 0; i < catalogue.Count; i++)
        {
            PlayerPrefs.DeleteKey(Prefix + catalogue[i].id);
        }
        PlayerPrefs.Save();
        earned.Clear();
    }

    // =========================================================
    // DERIVED FROM HISTORY
    // =========================================================

    private void HandleCompleted(int reactionId, string reactionName, ExperimentOutcome outcome)
    {
        if (outcome != ExperimentOutcome.Success)
        {
            return;
        }

        Unlock("first_success");
        EvaluateHistory(reactionId);

        // The recorder that just closed is still the active one, and it owns the engine that
        // judged the attempt - so precision can be checked here rather than in eight scripts.
        ReactionHistoryRecorder active = ReactionHistoryRecorder.Active;
        if (active != null)
        {
            ReportPrecision(active.Engine);
        }
    }

    /// <summary>
    /// Re-derives every history-based achievement from the stored attempts. Done by re-reading
    /// rather than by incrementing counters, so it stays correct across sessions and after the
    /// history is loaded back from disk.
    /// </summary>
    public void EvaluateHistory(int justCompletedReactionId = -1)
    {
        ExperimentHistoryManager manager = ExperimentHistoryManager.Instance;
        if (manager == null)
        {
            return;
        }

        List<ExperimentAttempt> attempts = manager.GetHistory();
        if (attempts == null)
        {
            return;
        }

        HashSet<int> succeeded = new HashSet<int>();
        HashSet<int> failed = new HashSet<int>();
        bool anyAiInteraction = false;

        for (int i = 0; i < attempts.Count; i++)
        {
            ExperimentAttempt attempt = attempts[i];
            if (attempt == null)
            {
                continue;
            }

            if (attempt.outcome == ExperimentOutcome.Success)
            {
                succeeded.Add(attempt.reactionId);
            }
            else if (IsFailure(attempt.outcome))
            {
                failed.Add(attempt.reactionId);
            }

            if (attempt.aiInteractions != null && attempt.aiInteractions.Count > 0)
            {
                anyAiInteraction = true;
            }
        }

        if (succeeded.Count >= 3)
        {
            Unlock("three_reactions");
        }
        if (succeeded.Count >= 8)
        {
            Unlock("all_reactions");
            if (failed.Count == 0)
            {
                Unlock("chemist");
            }
        }
        if (anyAiInteraction)
        {
            Unlock("asked_ai");
        }

        // "Succeeded first time" - the reaction's only recorded attempt is a success.
        if (justCompletedReactionId >= 0)
        {
            List<ExperimentAttempt> forReaction = manager.GetHistoryForReaction(justCompletedReactionId);
            if (forReaction != null)
            {
                int attemptsHere = 0;
                bool everFailedHere = false;
                for (int i = 0; i < forReaction.Count; i++)
                {
                    if (forReaction[i] == null || forReaction[i].outcome == ExperimentOutcome.Abandoned)
                    {
                        continue;
                    }
                    attemptsHere++;
                    if (IsFailure(forReaction[i].outcome))
                    {
                        everFailedHere = true;
                    }
                }

                if (attemptsHere == 1 && !everFailedHere)
                {
                    Unlock("first_time_right");
                }
                if (everFailedHere)
                {
                    // A failure exists on this reaction and we just succeeded at it.
                    Unlock("learn_from_failure");
                }
            }
        }
    }

    /// <summary>
    /// Unlocked from <see cref="ExamReactionRunner"/>-driven runs: every reagent landed inside
    /// half the permitted tolerance, which is a genuinely careful pour rather than a lucky one.
    /// </summary>
    public void ReportPrecision(FreeHandReactionEngine engine)
    {
        if (engine == null || !engine.HasSucceeded)
        {
            return;
        }

        List<string> substances = engine.Substances;
        if (substances == null || substances.Count == 0)
        {
            return;
        }

        for (int i = 0; i < substances.Count; i++)
        {
            string substance = substances[i];
            float target = 0.0f;
            if (!engine.targetQuantities.TryGetValue(substance, out target) || target <= 0.0f)
            {
                return;
            }

            float current = engine.GetCurrent(substance);
            float halfTolerance = target * (engine.ToleranceFor(substance) / 100.0f) * 0.5f;
            if (Mathf.Abs(current - target) > halfTolerance)
            {
                return;
            }
        }

        Unlock("precise");
    }

    private static bool IsFailure(ExperimentOutcome outcome)
    {
        return outcome == ExperimentOutcome.FailOverdose
            || outcome == ExperimentOutcome.FailUnderdose
            || outcome == ExperimentOutcome.FailWrongOrder
            || outcome == ExperimentOutcome.FailTimeout;
    }
}
