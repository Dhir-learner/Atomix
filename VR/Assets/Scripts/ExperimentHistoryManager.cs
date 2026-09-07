using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>How an attempt finished.</summary>
public enum ExperimentOutcome
{
    InProgress = 0,
    Success = 1,
    FailOverdose = 2,
    FailUnderdose = 3,
    FailWrongOrder = 4,
    FailTimeout = 5,
    Abandoned = 6
}

/// <summary>One dictionary entry, in a form Unity's JsonUtility can actually write.</summary>
[Serializable]
public class QuantityEntry
{
    public string name;
    public float amount;

    public QuantityEntry() { }

    public QuantityEntry(string name, float amount)
    {
        this.name = name;
        this.amount = amount;
    }
}

/// <summary>A single thing the student did, timed from the start of the attempt.</summary>
[Serializable]
public class ExperimentStep
{
    public float timestamp;
    public string action;
    public float quantityAtStep;
    public bool wasCorrect;
}

/// <summary>One question to the lab assistant and its answer.</summary>
[Serializable]
public class AIInteraction
{
    public float timestamp;
    public string userQuestion;
    public string aiResponse;
}

/// <summary>
/// One run of one experiment. Dictionaries are the runtime API but JsonUtility cannot serialize
/// them, so they are mirrored into lists on save and rebuilt on load.
/// </summary>
[Serializable]
public class ExperimentAttempt : ISerializationCallbackReceiver
{
    public string attemptId;
    public int reactionId;
    public string reactionName;
    public string timestampIso;
    public float durationSeconds;
    public ExperimentOutcome outcome = ExperimentOutcome.InProgress;
    public List<ExperimentStep> steps = new List<ExperimentStep>();
    public List<AIInteraction> aiInteractions = new List<AIInteraction>();

    [SerializeField] private List<QuantityEntry> quantitiesUsedList = new List<QuantityEntry>();
    [SerializeField] private List<QuantityEntry> targetQuantitiesList = new List<QuantityEntry>();

    [NonSerialized] public Dictionary<string, float> quantitiesUsed = new Dictionary<string, float>();
    [NonSerialized] public Dictionary<string, float> targetQuantities = new Dictionary<string, float>();

    /// <summary>Wall-clock time the attempt began.</summary>
    public DateTime Timestamp
    {
        get
        {
            DateTime parsed;
            if (DateTime.TryParse(timestampIso, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out parsed))
            {
                return parsed;
            }
            return DateTime.MinValue;
        }
        set { timestampIso = value.ToString("o", CultureInfo.InvariantCulture); }
    }

    public bool IsFailure
    {
        get
        {
            return outcome == ExperimentOutcome.FailOverdose ||
                   outcome == ExperimentOutcome.FailUnderdose ||
                   outcome == ExperimentOutcome.FailWrongOrder ||
                   outcome == ExperimentOutcome.FailTimeout;
        }
    }

    public string OutcomeLabel
    {
        get
        {
            switch (outcome)
            {
                case ExperimentOutcome.Success: return "SUCCESS";
                case ExperimentOutcome.FailOverdose: return "FAILED - too much";
                case ExperimentOutcome.FailUnderdose: return "FAILED - too little";
                case ExperimentOutcome.FailWrongOrder: return "FAILED - wrong order";
                case ExperimentOutcome.FailTimeout: return "FAILED - timed out";
                case ExperimentOutcome.Abandoned: return "ABANDONED";
                default: return "IN PROGRESS";
            }
        }
    }

    public void OnBeforeSerialize()
    {
        quantitiesUsedList = ToList(quantitiesUsed);
        targetQuantitiesList = ToList(targetQuantities);
    }

    public void OnAfterDeserialize()
    {
        quantitiesUsed = ToDictionary(quantitiesUsedList);
        targetQuantities = ToDictionary(targetQuantitiesList);
        if (steps == null) steps = new List<ExperimentStep>();
        if (aiInteractions == null) aiInteractions = new List<AIInteraction>();
    }

    private static List<QuantityEntry> ToList(Dictionary<string, float> source)
    {
        List<QuantityEntry> list = new List<QuantityEntry>();
        if (source == null)
        {
            return list;
        }
        foreach (KeyValuePair<string, float> pair in source)
        {
            list.Add(new QuantityEntry(pair.Key, pair.Value));
        }
        return list;
    }

    private static Dictionary<string, float> ToDictionary(List<QuantityEntry> source)
    {
        Dictionary<string, float> dictionary = new Dictionary<string, float>();
        if (source == null)
        {
            return dictionary;
        }
        for (int i = 0; i < source.Count; i++)
        {
            QuantityEntry entry = source[i];
            if (entry != null && !string.IsNullOrEmpty(entry.name))
            {
                dictionary[entry.name] = entry.amount;
            }
        }
        return dictionary;
    }
}

/// <summary>Root object written to disk. JsonUtility cannot serialize a bare list.</summary>
[Serializable]
public class ExperimentHistoryData
{
    public int version = 1;
    public List<ExperimentAttempt> attempts = new List<ExperimentAttempt>();
}

/// <summary>
/// Persistent log of every experiment attempt: what was poured, when, in what order, how it
/// ended, and what the AI assistant was asked about it.
///
/// Creates itself before the first scene loads, so no scene or prefab edits are required, and
/// survives scene changes (the lab and the AI assistant live in different scenes).
/// </summary>
public class ExperimentHistoryManager : MonoBehaviour
{
    public const string SaveFileName = "atomix_experiment_history.json";

    [Tooltip("Oldest attempts beyond this count are dropped so the save file stays small.")]
    public int maxStoredAttempts = 300;

    [Tooltip("Write the history to disk as soon as an attempt finishes.")]
    public bool autoSaveOnAttemptEnd = true;

    [Tooltip("Log every recorded step to the Unity console. Useful while wiring reactions up.")]
    public bool verboseLogging = false;

    private static ExperimentHistoryManager instance;

    private readonly List<ExperimentAttempt> attempts = new List<ExperimentAttempt>();
    private readonly Dictionary<string, float> attemptStartRealtime = new Dictionary<string, float>();
    private string activeAttemptId = string.Empty;
    private bool loaded = false;

    /// <summary>Never null during play: the manager creates itself on demand.</summary>
    public static ExperimentHistoryManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<ExperimentHistoryManager>();
            }
            if (instance == null)
            {
                GameObject host = new GameObject("ExperimentHistoryManager");
                instance = host.AddComponent<ExperimentHistoryManager>();
            }
            return instance;
        }
    }

    /// <summary>The attempt currently running, or empty. Used to attach AI questions.</summary>
    public string ActiveAttemptId { get { return activeAttemptId; } }

    public static string SaveFilePath
    {
        get { return Path.Combine(Application.persistentDataPath, SaveFileName); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // Touching Instance is enough - it builds the manager if the scene has none.
        ExperimentHistoryManager manager = Instance;
        if (manager == null)
        {
            return;
        }

        if (manager.gameObject.GetComponent<ExperimentHistoryUI>() == null)
        {
            manager.gameObject.AddComponent<ExperimentHistoryUI>();
        }

        // The graph panel rides on the same DontDestroyOnLoad object, so it survives the scene
        // change to the assistant and back without needing any scene edits either.
        ReactionGraphUI graphUi = manager.gameObject.GetComponent<ReactionGraphUI>();
        if (graphUi == null)
        {
            graphUi = manager.gameObject.AddComponent<ReactionGraphUI>();
        }

        // The sequencer chains success -> video -> graphs, so the graph panel must not also pop
        // up on its own timer and race the video. Its F-key toggle is unaffected.
        if (manager.gameObject.GetComponent<PostSuccessSequencer>() == null)
        {
            manager.gameObject.AddComponent<PostSuccessSequencer>();
            graphUi.showAfterSuccess = false;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadFromJson();
    }

    void OnApplicationQuit()
    {
        CloseOpenAttempts(ExperimentOutcome.Abandoned);
        SaveToJson();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveToJson();
        }
    }

    // =========================================================
    // RECORDING
    // =========================================================

    /// <summary>Opens a new attempt and returns its id.</summary>
    public string StartAttempt(int reactionId, string reactionName)
    {
        ExperimentAttempt attempt = new ExperimentAttempt();
        attempt.attemptId = Guid.NewGuid().ToString();
        attempt.reactionId = reactionId;
        attempt.reactionName = string.IsNullOrEmpty(reactionName) ? ("Reaction " + reactionId) : reactionName;
        attempt.Timestamp = DateTime.Now;
        attempt.outcome = ExperimentOutcome.InProgress;

        attempts.Add(attempt);
        attemptStartRealtime[attempt.attemptId] = Time.realtimeSinceStartup;
        activeAttemptId = attempt.attemptId;

        TrimToMaximum();

        if (verboseLogging)
        {
            Debug.Log("[History] Started attempt " + attempt.attemptId + " for " + attempt.reactionName);
        }

        return attempt.attemptId;
    }

    /// <summary>Records one action within an attempt.</summary>
    public void LogStep(string attemptId, string action, float quantity, bool wasCorrect)
    {
        ExperimentAttempt attempt = FindAttempt(attemptId);
        if (attempt == null || string.IsNullOrEmpty(action))
        {
            return;
        }

        ExperimentStep step = new ExperimentStep();
        step.timestamp = ElapsedSeconds(attemptId);
        step.action = action;
        step.quantityAtStep = quantity;
        step.wasCorrect = wasCorrect;
        attempt.steps.Add(step);

        if (verboseLogging)
        {
            Debug.Log("[History] " + attempt.reactionName + " step: " + action);
        }
    }

    /// <summary>Records a question put to the lab assistant and its answer.</summary>
    public void LogAIInteraction(string attemptId, string question, string response)
    {
        ExperimentAttempt attempt = FindAttempt(attemptId);
        if (attempt == null)
        {
            // Conversations often happen after an attempt ends, or in the assistant scene -
            // attach them to the most recent attempt rather than dropping them.
            attempt = GetMostRecentAttempt();
        }
        if (attempt == null)
        {
            return;
        }

        AIInteraction interaction = new AIInteraction();
        interaction.timestamp = ElapsedSeconds(attempt.attemptId);
        interaction.userQuestion = question == null ? string.Empty : question;
        interaction.aiResponse = response == null ? string.Empty : response;
        attempt.aiInteractions.Add(interaction);

        if (autoSaveOnAttemptEnd)
        {
            SaveToJson();
        }
    }

    /// <summary>Stores what was actually added versus what should have been.</summary>
    public void RecordQuantities(string attemptId,
                                 Dictionary<string, float> used,
                                 Dictionary<string, float> targets)
    {
        ExperimentAttempt attempt = FindAttempt(attemptId);
        if (attempt == null)
        {
            return;
        }

        if (used != null)
        {
            attempt.quantitiesUsed = new Dictionary<string, float>(used);
        }
        if (targets != null)
        {
            attempt.targetQuantities = new Dictionary<string, float>(targets);
        }
    }

    /// <summary>Closes an attempt with its outcome and stamps the duration.</summary>
    public void EndAttempt(string attemptId, ExperimentOutcome outcome)
    {
        ExperimentAttempt attempt = FindAttempt(attemptId);
        if (attempt == null || attempt.outcome != ExperimentOutcome.InProgress)
        {
            return;
        }

        attempt.outcome = outcome;
        attempt.durationSeconds = ElapsedSeconds(attemptId);

        if (activeAttemptId == attemptId)
        {
            activeAttemptId = string.Empty;
        }

        if (verboseLogging)
        {
            Debug.Log("[History] Ended attempt " + attemptId + " as " + outcome);
        }

        if (autoSaveOnAttemptEnd)
        {
            SaveToJson();
        }

        // Refresh the panel if the student happens to be reading it.
        ExperimentHistoryUI ui = GetComponent<ExperimentHistoryUI>();
        if (ui != null)
        {
            ui.MarkDirty();
        }
    }

    private void CloseOpenAttempts(ExperimentOutcome outcome)
    {
        for (int i = 0; i < attempts.Count; i++)
        {
            if (attempts[i].outcome == ExperimentOutcome.InProgress)
            {
                attempts[i].outcome = outcome;
                attempts[i].durationSeconds = ElapsedSeconds(attempts[i].attemptId);
            }
        }
        activeAttemptId = string.Empty;
    }

    // =========================================================
    // QUERYING
    // =========================================================

    /// <summary>Every attempt, newest last.</summary>
    public List<ExperimentAttempt> GetHistory()
    {
        return new List<ExperimentAttempt>(attempts);
    }

    public List<ExperimentAttempt> GetHistoryForReaction(int reactionId)
    {
        List<ExperimentAttempt> filtered = new List<ExperimentAttempt>();
        for (int i = 0; i < attempts.Count; i++)
        {
            if (attempts[i].reactionId == reactionId)
            {
                filtered.Add(attempts[i]);
            }
        }
        return filtered;
    }

    /// <summary>How many times this experiment has been tried, ever.</summary>
    public int GetAttemptCount(int reactionId)
    {
        return GetHistoryForReaction(reactionId).Count;
    }

    /// <summary>Reaction ids that appear in the history, in first-seen order.</summary>
    public List<int> GetRecordedReactionIds()
    {
        List<int> ids = new List<int>();
        for (int i = 0; i < attempts.Count; i++)
        {
            if (!ids.Contains(attempts[i].reactionId))
            {
                ids.Add(attempts[i].reactionId);
            }
        }
        ids.Sort();
        return ids;
    }

    public string GetReactionName(int reactionId)
    {
        for (int i = attempts.Count - 1; i >= 0; i--)
        {
            if (attempts[i].reactionId == reactionId)
            {
                return attempts[i].reactionName;
            }
        }
        return "Reaction " + reactionId;
    }

    public ExperimentAttempt GetMostRecentAttempt()
    {
        return attempts.Count > 0 ? attempts[attempts.Count - 1] : null;
    }

    public ExperimentAttempt FindAttempt(string attemptId)
    {
        if (string.IsNullOrEmpty(attemptId))
        {
            return null;
        }
        for (int i = attempts.Count - 1; i >= 0; i--)
        {
            if (attempts[i].attemptId == attemptId)
            {
                return attempts[i];
            }
        }
        return null;
    }

    public void ClearHistory()
    {
        attempts.Clear();
        attemptStartRealtime.Clear();
        activeAttemptId = string.Empty;
        SaveToJson();
    }

    private float ElapsedSeconds(string attemptId)
    {
        float startedAt;
        if (string.IsNullOrEmpty(attemptId) || !attemptStartRealtime.TryGetValue(attemptId, out startedAt))
        {
            return 0.0f;
        }
        return Mathf.Max(0.0f, Time.realtimeSinceStartup - startedAt);
    }

    private void TrimToMaximum()
    {
        int limit = Mathf.Max(1, maxStoredAttempts);
        while (attempts.Count > limit)
        {
            attemptStartRealtime.Remove(attempts[0].attemptId);
            attempts.RemoveAt(0);
        }
    }

    // =========================================================
    // PERSISTENCE
    // =========================================================

    public void SaveToJson()
    {
        try
        {
            ExperimentHistoryData data = new ExperimentHistoryData();
            data.attempts = new List<ExperimentAttempt>(attempts);
            File.WriteAllText(SaveFilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[History] Could not save history: " + exception.Message);
        }
    }

    public void LoadFromJson()
    {
        if (loaded)
        {
            return;
        }
        loaded = true;

        try
        {
            if (!File.Exists(SaveFilePath))
            {
                return;
            }

            string json = File.ReadAllText(SaveFilePath);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            ExperimentHistoryData data = JsonUtility.FromJson<ExperimentHistoryData>(json);
            if (data == null || data.attempts == null)
            {
                return;
            }

            attempts.Clear();
            for (int i = 0; i < data.attempts.Count; i++)
            {
                ExperimentAttempt attempt = data.attempts[i];
                if (attempt == null)
                {
                    continue;
                }
                // An attempt left open by a crash can never be resumed - mark it honestly.
                if (attempt.outcome == ExperimentOutcome.InProgress)
                {
                    attempt.outcome = ExperimentOutcome.Abandoned;
                }
                attempts.Add(attempt);
            }

            TrimToMaximum();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[History] Could not load history: " + exception.Message);
        }
    }
}

/// <summary>
/// Per-reaction glue between <see cref="FreeHandReactionEngine"/> and the history manager.
/// Each reaction script owns one; it starts the attempt on the first real action, turns pour
/// start/stop into readable steps, and closes the attempt with the engine's verdict.
/// </summary>
public class ReactionHistoryRecorder
{
    private readonly int reactionId;
    private readonly string reactionName;
    private readonly FreeHandReactionEngine engine;
    private readonly HashSet<string> wasPouring = new HashSet<string>();
    private readonly HashSet<string> everAdded = new HashSet<string>();

    private string attemptId = string.Empty;
    private bool finished = false;

    /// <summary>
    /// The experiment the student is working on right now (or most recently worked on). Lets the
    /// AI assistant read live experiment state without every reaction script having to know it
    /// exists. Set as soon as an attempt opens and left in place afterwards, because questions
    /// are usually asked once the experiment has already gone wrong.
    /// </summary>
    public static ReactionHistoryRecorder Active { get; private set; }

    public int ReactionId { get { return reactionId; } }
    public string ReactionName { get { return reactionName; } }
    public FreeHandReactionEngine Engine { get { return engine; } }

    public string AttemptId { get { return attemptId; } }
    public bool HasStarted { get { return !string.IsNullOrEmpty(attemptId); } }
    public bool IsFinished { get { return finished; } }

    public ReactionHistoryRecorder(int reactionId, string reactionName, FreeHandReactionEngine engine)
    {
        this.reactionId = reactionId;
        this.reactionName = reactionName;
        this.engine = engine;
    }

    /// <summary>Opens the attempt if it is not open yet. Safe to call every frame.</summary>
    public void BeginIfNeeded()
    {
        if (finished || HasStarted)
        {
            return;
        }
        attemptId = ExperimentHistoryManager.Instance.StartAttempt(reactionId, reactionName);
        Active = this;
    }

    /// <summary>
    /// Call once per frame. Watches every tracked input and writes a step when a pour begins and
    /// another when it ends, rather than spamming one per frame.
    /// </summary>
    public void Tick()
    {
        if (engine == null || finished)
        {
            return;
        }

        List<string> substances = engine.Substances;
        for (int i = 0; i < substances.Count; i++)
        {
            string substance = substances[i];
            bool pouringNow = engine.IsPouring(substance);
            bool pouringBefore = wasPouring.Contains(substance);
            float amount = engine.GetCurrent(substance);
            bool isHeating = engine.UnitFor(substance) == "s";

            if (pouringNow && !pouringBefore)
            {
                BeginIfNeeded();
                wasPouring.Add(substance);
                LogAction(isHeating ? "Held the sample over the flame"
                                    : ("Started pouring " + substance), amount, true);
            }
            else if (!pouringNow && pouringBefore)
            {
                wasPouring.Remove(substance);
                bool correct = engine.IsWithinTolerance(substance);
                string unit = engine.UnitFor(substance);
                LogAction(isHeating
                    ? string.Format("Removed from the flame after {0:F1} {1}", amount, unit)
                    : string.Format("Added {0:F1} {1} of {2} (target {3:F1})",
                        amount, unit, substance, engine.targetQuantities[substance]),
                    amount, correct);
            }

            // Solids that arrive in one lump never produce a pour edge, so catch them here.
            if (amount > 0.0f && !everAdded.Contains(substance))
            {
                everAdded.Add(substance);
                if (!pouringNow)
                {
                    BeginIfNeeded();
                    LogAction(string.Format("Added {0:F1} {1} of {2}",
                        amount, engine.UnitFor(substance), substance), amount, true);
                }
            }
        }
    }

    /// <summary>Records a one-off action such as lighting the burner.</summary>
    public void LogAction(string action, float quantity, bool wasCorrect)
    {
        if (finished)
        {
            return;
        }
        BeginIfNeeded();
        ExperimentHistoryManager.Instance.LogStep(attemptId, action, quantity, wasCorrect);
    }

    /// <summary>Closes the attempt using the engine's verdict.</summary>
    public void Complete(ReactionResult result)
    {
        Complete(ToOutcome(result));
    }

    public void Complete(ExperimentOutcome outcome)
    {
        if (finished)
        {
            return;
        }

        if (!HasStarted)
        {
            // Nothing was ever done here - do not clutter the history with an empty row.
            if (outcome == ExperimentOutcome.Abandoned)
            {
                return;
            }

            // A real verdict with no recorded pour still deserves a row: some failures fire
            // before any pouring, e.g. heating CaCO3 before the balloon has been fitted.
            BeginIfNeeded();
        }

        finished = true;
        ExperimentHistoryManager manager = ExperimentHistoryManager.Instance;

        if (engine != null)
        {
            manager.RecordQuantities(attemptId, engine.GetQuantitiesSnapshot(), engine.GetTargetsSnapshot());
            if (engine.HasFailed && !string.IsNullOrEmpty(engine.failureReason))
            {
                manager.LogStep(attemptId, "Why it failed: " + engine.failureReason, 0.0f, false);
            }
        }

        manager.EndAttempt(attemptId, outcome);

        // Every one of the eight reactions closes its attempt through here, so this is the single
        // place the scientific graphs need to hook into - no per-reaction wiring.
        if (outcome == ExperimentOutcome.Success && Completed != null)
        {
            Completed(reactionId, reactionName, outcome);
        }
    }

    /// <summary>
    /// Raised when any experiment finishes successfully, with its reaction id and name.
    /// <see cref="ReactionGraphUI"/> listens for this to show the graphs.
    /// </summary>
    public static event System.Action<int, string, ExperimentOutcome> Completed;

    /// <summary>Closes an unfinished attempt when the student walks away from the experiment.</summary>
    public void Abandon()
    {
        if (finished || !HasStarted)
        {
            return;
        }
        Complete(ExperimentOutcome.Abandoned);
    }

    /// <summary>Clears the recorder so the next selection of this experiment logs a new attempt.</summary>
    public void ResetForNewAttempt()
    {
        attemptId = string.Empty;
        finished = false;
        wasPouring.Clear();
        everAdded.Clear();
    }

    public static ExperimentOutcome ToOutcome(ReactionResult result)
    {
        switch (result)
        {
            case ReactionResult.Success: return ExperimentOutcome.Success;
            case ReactionResult.FailOverdose: return ExperimentOutcome.FailOverdose;
            case ReactionResult.FailUnderdose: return ExperimentOutcome.FailUnderdose;
            case ReactionResult.FailWrongOrder: return ExperimentOutcome.FailWrongOrder;
            default: return ExperimentOutcome.InProgress;
        }
    }
}
