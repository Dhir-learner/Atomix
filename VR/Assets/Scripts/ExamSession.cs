using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>What kind of task a row in the exam record is.</summary>
public enum ExamTaskKind
{
    Practical,
    Theory
}

public enum ExamTaskOutcome
{
    Correct,
    Wrong,
    TimedOut,
    Skipped
}

/// <summary>One task in one testing run.</summary>
[Serializable]
public class ExamTaskRecord
{
    public int order;
    public ExamTaskKind kind;
    public ExamTaskOutcome outcome;

    /// <summary>Reaction 1-8 for a practical, or the question number 9-18 for a theory task.</summary>
    public int taskId;

    public string title;

    /// <summary>The question asked, for theory tasks.</summary>
    public string question;

    /// <summary>The answer chosen, or the failure reason for a practical.</summary>
    public string detail;

    public float secondsTaken;
    public float secondsRemaining;
    public int coinsEarned;

    /// <summary>Help bought while this task was on screen.</summary>
    public string helpUsed = string.Empty;

    public bool Passed { get { return outcome == ExamTaskOutcome.Correct; } }

    public string OutcomeLabel
    {
        get
        {
            switch (outcome)
            {
                case ExamTaskOutcome.Correct: return "Correct";
                case ExamTaskOutcome.Wrong: return "Wrong";
                case ExamTaskOutcome.TimedOut: return "Ran out of time";
                case ExamTaskOutcome.Skipped: return "Skipped";
                default: return "Unknown";
            }
        }
    }
}

/// <summary>
/// The record of one testing run - every task, practical and theoretical, with what happened.
///
/// This exists because the report card could not see half the exam. Practical tasks reached it
/// through <see cref="ReactionHistoryRecorder"/>, but <see cref="TheoreticalTasksManager"/> logged
/// nothing at all: it set `countdown.wasScored` and moved on. So a theory-only run - which is a
/// real setting, `StaticData.includedTasksValue == 1` - ended with the report card saying
/// "No graded experiments were recorded in this run", after ten answered questions.
///
/// Everything in a run now lands here, and the report card and the exported report both read it,
/// so the screen and the file cannot disagree.
/// </summary>
public static class ExamSession
{
    private static readonly List<ExamTaskRecord> tasks = new List<ExamTaskRecord>();

    public static bool IsRunning { get; private set; }
    public static DateTime StartedUtc { get; private set; }
    public static DateTime FinishedUtc { get; private set; }

    /// <summary>Coins earned in this run alone. The score strip shows this.</summary>
    public static int RunCoins { get; private set; }

    /// <summary>Coins spent on help in this run.</summary>
    public static int RunSpent { get; private set; }

    public static int CurrentStreak { get; private set; }
    public static int LongestStreak { get; private set; }

    /// <summary>0 practice only, 1 theory only, 2 both - mirrors StaticData.includedTasksValue.</summary>
    public static int Mode { get; private set; }

    public static IReadOnlyList<ExamTaskRecord> Tasks { get { return tasks; } }

    /// <summary>Help bought since the current task started, so it can be attributed to that task.</summary>
    private static string pendingHelp = string.Empty;

    public static int Passed
    {
        get
        {
            int n = 0;
            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i].Passed) { n++; }
            }
            return n;
        }
    }

    public static int Failed { get { return tasks.Count - Passed; } }

    public static float SuccessRatePercent
    {
        get { return tasks.Count == 0 ? 0.0f : Passed * 100.0f / tasks.Count; }
    }

    public static bool WasUnaided { get { return RunSpent == 0; } }

    public static float TotalSeconds
    {
        get
        {
            DateTime end = IsRunning ? DateTime.UtcNow : FinishedUtc;
            return (float)(end - StartedUtc).TotalSeconds;
        }
    }

    /// <summary>Every practical reaction id this run cleared, for the summary line.</summary>
    public static List<int> PracticalsCleared()
    {
        List<int> cleared = new List<int>();
        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i].kind == ExamTaskKind.Practical && tasks[i].Passed &&
                !cleared.Contains(tasks[i].taskId))
            {
                cleared.Add(tasks[i].taskId);
            }
        }
        return cleared;
    }

    // =====================================================================================
    // LIFECYCLE
    // =====================================================================================

    public static void Begin(int mode)
    {
        tasks.Clear();
        RunCoins = 0;
        RunSpent = 0;
        CurrentStreak = 0;
        LongestStreak = 0;
        pendingHelp = string.Empty;
        Mode = mode;
        StartedUtc = DateTime.UtcNow;
        FinishedUtc = DateTime.MinValue;
        IsRunning = true;
    }

    /// <summary>
    /// Closes the run and files it with the coin bank. Idempotent, because both
    /// <see cref="ExamSessionTracker"/> and the report card notice the run has ended and either
    /// could get there first.
    /// </summary>
    public static void End()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        FinishedUtc = DateTime.UtcNow;

        // A clean sweep with no help bought is worth something on its own.
        bool perfect = tasks.Count > 0 && Failed == 0;
        if (perfect && WasUnaided)
        {
            RunCoins += AtomixCoinBank.BonusUnaidedRun;
            AtomixCoinBank.Instance.Earn(AtomixCoinBank.BonusUnaidedRun);
        }

        AtomixCoinBank.Instance.CommitRun(RunCoins, perfect, LongestStreak);
    }

    // =====================================================================================
    // RECORDING
    // =====================================================================================

    /// <summary>
    /// Files one finished task and awards its coins. Returns the record so a caller can read back
    /// what was awarded for a toast.
    /// </summary>
    public static ExamTaskRecord RecordTask(
        ExamTaskKind kind, int taskId, string title, string question, string detail,
        ExamTaskOutcome outcome, float secondsRemaining, float secondsTaken)
    {
        if (!IsRunning)
        {
            // A task finishing outside a run should still not be lost - open one around it.
            Begin(StaticData.includedTasksValue);
        }

        ExamTaskRecord record = new ExamTaskRecord();
        record.order = tasks.Count + 1;
        record.kind = kind;
        record.taskId = taskId;
        record.title = title ?? string.Empty;
        record.question = question ?? string.Empty;
        record.detail = detail ?? string.Empty;
        record.outcome = outcome;
        record.secondsRemaining = Mathf.Max(0.0f, secondsRemaining);
        record.secondsTaken = Mathf.Max(0.0f, secondsTaken);
        record.helpUsed = pendingHelp;

        pendingHelp = string.Empty;

        if (record.Passed)
        {
            record.coinsEarned = AwardForPass(kind, taskId, secondsRemaining);
        }
        else
        {
            CurrentStreak = 0;
        }

        tasks.Add(record);
        return record;
    }

    private static int AwardForPass(ExamTaskKind kind, int taskId, float secondsRemaining)
    {
        CurrentStreak++;
        if (CurrentStreak > LongestStreak)
        {
            LongestStreak = CurrentStreak;
        }

        // MarkCleared both reports and records, so it must be called exactly once per pass.
        bool firstClear = kind == ExamTaskKind.Practical &&
                          AtomixCoinBank.Instance.MarkCleared(taskId);

        int coins = ComputeAward(secondsRemaining, CurrentStreak, firstClear);

        RunCoins += coins;
        AtomixCoinBank.Instance.Earn(coins);
        return coins;
    }

    /// <summary>
    /// What one passed task is worth. Split out from <see cref="AwardForPass"/> and kept free of
    /// PlayerPrefs so the tiers and the bonus thresholds can be exercised directly - the original
    /// tier table shipped with an unreachable branch precisely because nothing ever ran it.
    /// </summary>
    public static int ComputeAward(float secondsRemaining, int streakAfterThisTask, bool firstClear)
    {
        int coins = SpeedCoins(secondsRemaining);

        // Streak bonuses fire on the exact task that reaches the threshold, not on every task
        // after it - otherwise a long streak would pay the bonus repeatedly.
        if (streakAfterThisTask == 3)
        {
            coins += AtomixCoinBank.BonusStreakThree;
        }
        else if (streakAfterThisTask == 5)
        {
            coins += AtomixCoinBank.BonusStreakFive;
        }

        if (firstClear)
        {
            coins += AtomixCoinBank.BonusFirstClear;
        }

        return coins;
    }

    /// <summary>
    /// The time-remaining tiers, carried over from the original CountdownTimer with its
    /// unreachable branch repaired.
    ///
    /// The original read:
    ///
    ///     if      (currentTime &gt;= 30) score += 100;
    ///     else if (currentTime &gt;= 30) score += 50;   // can never be true
    ///     else if (currentTime &gt;= 20) score += 40;
    ///
    /// The second test duplicates the first, so the 50-point tier was dead code and anything
    /// finished with over 30 s left scored the maximum. The point values are the author's; only
    /// the top threshold is changed, to 45, which is what makes the 50 tier reachable again.
    /// </summary>
    public static int SpeedCoins(float secondsRemaining)
    {
        if (secondsRemaining >= 45.0f) { return 100; }
        if (secondsRemaining >= 30.0f) { return 50; }
        if (secondsRemaining >= 20.0f) { return 40; }
        if (secondsRemaining >= 15.0f) { return 20; }
        if (secondsRemaining >= 10.0f) { return 15; }
        if (secondsRemaining >= 5.0f) { return 10; }
        return 5;
    }

    // =====================================================================================
    // HELP
    // =====================================================================================

    /// <summary>Records a purchase against the run and the task currently on screen.</summary>
    public static void RecordHelpPurchase(string label, int cost)
    {
        RunSpent += cost;

        pendingHelp = string.IsNullOrEmpty(pendingHelp)
            ? label
            : pendingHelp + ", " + label;
    }

    /// <summary>
    /// A readable name for a task number. 1-8 are the practical reactions, 9-18 the theory
    /// questions, matching the numbering Randomize uses.
    /// </summary>
    public static string TitleForTask(int taskId)
    {
        switch (taskId)
        {
            case 1: return "Sodium + Water";
            case 2: return "Sulfuric Acid + Copper(II) Oxide";
            case 3: return "Hydrochloric Acid + Sodium Bicarbonate";
            case 4: return "Potassium + Water";
            case 5: return "Aluminium + Iodine";
            case 6: return "Quicklime + Water";
            case 7: return "Calcium Carbonate Decomposition";
            case 8: return "Iron(II) Sulfate Decomposition";
            default:
                return taskId >= 9 && taskId <= 18
                    ? "Theory question " + taskId
                    : "Task " + taskId;
        }
    }

    public static string Grade
    {
        get
        {
            if (tasks.Count == 0)
            {
                return "-";
            }

            float rate = SuccessRatePercent;
            if (rate >= 90.0f) { return "A"; }
            if (rate >= 80.0f) { return "B"; }
            if (rate >= 65.0f) { return "C"; }
            if (rate >= 50.0f) { return "D"; }
            return "F";
        }
    }
}

/// <summary>
/// Opens a run when the testing scene loads and closes it when the randomizer stops.
///
/// Built from code at bootstrap like every other system added since Task 1, so the testing scene
/// itself needs no edit.
/// </summary>
public class ExamSessionTracker : MonoBehaviour
{
    public string[] enabledScenes = { "TestingPhaseLab" };

    private Randomize randomizer;
    private CountdownTimer countdown;
    private bool endedForThisRun;

    // Which task is on screen, and how many were filed when it appeared. If those two are still
    // equal when the task changes, nothing filed a result for it - it ran out of time.
    private int watchedTaskId = -1;
    private int filedAtTaskStart;

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        randomizer = null;

        if (!IsEnabledScene(scene.name))
        {
            // Leaving the testing scene mid-run still counts as finishing it, so the coins earned
            // so far are banked rather than silently dropped.
            if (ExamSession.IsRunning)
            {
                ExamSession.End();
            }
            return;
        }

        endedForThisRun = false;
        watchedTaskId = -1;
        filedAtTaskStart = 0;
        ExamSession.Begin(StaticData.includedTasksValue);
    }

    void Update()
    {
        if (endedForThisRun || !IsEnabledScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        if (randomizer == null)
        {
            randomizer = FindFirstObjectByType<Randomize>(FindObjectsInactive.Exclude);
            return;
        }

        if (countdown == null)
        {
            countdown = FindFirstObjectByType<CountdownTimer>(FindObjectsInactive.Exclude);
        }

        int taskId = randomizer.currentReactionInTestPhase;
        if (taskId != watchedTaskId)
        {
            FlushUnfinishedTask();
            watchedTaskId = taskId;
            filedAtTaskStart = ExamSession.Tasks.Count;
        }

        if (randomizer.stopTesting)
        {
            endedForThisRun = true;
            FlushUnfinishedTask();
            ExamSession.End();
        }
    }

    /// <summary>
    /// Files a timeout for the task that just left the screen without a result.
    ///
    /// Without this, a task the student never finished would vanish from the report entirely, and
    /// the report card would say "passed 4 of 4" on a run where six tasks were set. A task nobody
    /// answered is a result, and it belongs in the record.
    /// </summary>
    private void FlushUnfinishedTask()
    {
        if (watchedTaskId < 0 || ExamSession.Tasks.Count > filedAtTaskStart)
        {
            return;
        }

        bool theory = watchedTaskId >= 9;

        ExamSession.RecordTask(
            theory ? ExamTaskKind.Theory : ExamTaskKind.Practical,
            watchedTaskId,
            ExamSession.TitleForTask(watchedTaskId),
            string.Empty,
            "No answer was given before the timer ran out",
            ExamTaskOutcome.TimedOut,
            0.0f,
            countdown != null ? countdown.SecondsOnTask : 0.0f);
    }

    private bool IsEnabledScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || enabledScenes == null)
        {
            return false;
        }

        for (int i = 0; i < enabledScenes.Length; i++)
        {
            if (enabledScenes[i] == sceneName)
            {
                return true;
            }
        }
        return false;
    }
}
