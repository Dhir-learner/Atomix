using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns what happens after an experiment succeeds, in one place for all eight reactions:
///
///   success  ->  3 s pause  ->  molecular video  ->  CONTINUE  ->  scientific graphs
///
/// Before this existed the graphs popped up on their own timer. Chaining them behind the video
/// stops the two panels racing each other, and gives the intended teaching order: watch what
/// happened at the molecular level, then read the thermodynamics behind it.
///
/// It hooks <see cref="ReactionHistoryRecorder.Completed"/> - the single method all eight
/// reactions already funnel their verdict through - so no reaction script needs to change.
/// </summary>
public class PostSuccessSequencer : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds between the success message appearing and the video starting.\n\n" +
             "The task asked for 3 s, but the reactions' own post-success visuals run longer than " +
             "that: R2 changes the beaker material in three stages at 2 s, 4 s and 6 s, and R6 " +
             "keeps its explosion effect alive until 6 s. Opening the video at 3 s would hide the " +
             "colour change the student just earned. 7 s clears every reaction's last visual " +
             "(6 s) while the success popup (10 s) is still up.")]
    public float delayBeforeVideo = 7.0f;

    [Header("Stages")]
    [Tooltip("Play the molecular visualisation after a successful experiment.")]
    public bool showVideo = true;

    [Tooltip("Show the scientific graphs once the video is dismissed.")]
    public bool showGraphs = true;

    [Header("Scenes")]
    [Tooltip("The sequence only runs in these scenes.")]
    public string[] enabledScenes = { "LabScene" };

    /// <summary>The live sequencer, so other systems can tell whether the chain is running.</summary>
    public static PostSuccessSequencer Instance { get; private set; }

    /// <summary>True from the moment an experiment succeeds until the student leaves the graphs.</summary>
    public bool IsRunning { get; private set; }

    private Coroutine pendingSequence;

    // The attempt that just succeeded, captured so the "watched the video" step lands on the
    // right row even if the student starts another experiment while the video plays.
    private string sequenceAttemptId = string.Empty;
    private int sequenceReactionId = -1;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    void OnEnable()
    {
        ReactionHistoryRecorder.Completed += HandleExperimentCompleted;
        SceneManager.activeSceneChanged += HandleSceneChanged;
    }

    void OnDisable()
    {
        ReactionHistoryRecorder.Completed -= HandleExperimentCompleted;
        SceneManager.activeSceneChanged -= HandleSceneChanged;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Leaving the lab abandons the sequence rather than following the player out.</summary>
    private void HandleSceneChanged(Scene previous, Scene next)
    {
        CancelSequence();
    }

    private bool IsEnabledScene()
    {
        if (enabledScenes == null || enabledScenes.Length == 0)
        {
            return true;
        }

        string active = SceneManager.GetActiveScene().name;
        for (int i = 0; i < enabledScenes.Length; i++)
        {
            if (enabledScenes[i] == active)
            {
                return true;
            }
        }
        return false;
    }

    // =========================================================
    // SEQUENCE
    // =========================================================

    private void HandleExperimentCompleted(int reactionId, string reactionName,
                                           ExperimentOutcome outcome)
    {
        if (outcome != ExperimentOutcome.Success || !IsEnabledScene())
        {
            return;
        }

        // Completed fires immediately after EndAttempt, so the most recent attempt is this one.
        ExperimentHistoryManager manager = ExperimentHistoryManager.Instance;
        ExperimentAttempt attempt = manager != null ? manager.GetMostRecentAttempt() : null;
        sequenceAttemptId = attempt != null ? attempt.attemptId : string.Empty;
        sequenceReactionId = reactionId;

        CancelSequence();

        IsRunning = true;
        pendingSequence = StartCoroutine(RunSequence(reactionId));
    }

    private IEnumerator RunSequence(int reactionId)
    {
        // Let the reaction's own success message and guidance audio land first.
        yield return new WaitForSeconds(Mathf.Max(0.0f, delayBeforeVideo));

        pendingSequence = null;

        if (!IsEnabledScene())
        {
            IsRunning = false;
            yield break;
        }

        bool videoShown = false;

        if (showVideo)
        {
            ReactionLearningController controller =
                ReactionLearningController.GetOrCreateStandalone();

            if (controller != null)
            {
                videoShown = controller.TryShowAfterSuccess(reactionId, HandleVideoContinue);
            }

            if (videoShown)
            {
                LogVideoStep(reactionId);
            }
            else
            {
                Debug.LogWarning(
                    "PostSuccessSequencer: could not show the molecular video for reaction " +
                    reactionId + "; going straight to the graphs.");
            }
        }

        if (!videoShown)
        {
            // No video stage - do not leave the student with nothing.
            ShowGraphs(reactionId);
        }
    }

    /// <summary>Runs when the student presses CONTINUE on the video panel.</summary>
    private void HandleVideoContinue()
    {
        ShowGraphs(sequenceReactionId);
    }

    private void ShowGraphs(int reactionId)
    {
        IsRunning = false;

        if (!showGraphs || !IsEnabledScene())
        {
            return;
        }

        ReactionGraphUI graphUi = GetComponent<ReactionGraphUI>();
        if (graphUi == null)
        {
            graphUi = FindAnyObjectByType<ReactionGraphUI>();
        }

        if (graphUi == null || ReactionGraphCatalog.Get(reactionId) == null)
        {
            return;
        }

        graphUi.Show(reactionId);
    }

    private void CancelSequence()
    {
        if (pendingSequence != null)
        {
            StopCoroutine(pendingSequence);
            pendingSequence = null;
        }

        IsRunning = false;
    }

    // =========================================================
    // HISTORY (5C)
    // =========================================================

    /// <summary>
    /// Records that the molecular visualisation was shown for this attempt. The attempt is already
    /// closed by now, which is fine - LogStep appends to the stored row either way, so the history
    /// panel shows the video step under the run it belongs to.
    /// </summary>
    private void LogVideoStep(int reactionId)
    {
        ExperimentHistoryManager manager = ExperimentHistoryManager.Instance;
        if (manager == null || string.IsNullOrEmpty(sequenceAttemptId))
        {
            return;
        }

        bool hasClip = HasVideoClip(reactionId);

        if (hasClip && AchievementSystem.Instance != null)
        {
            AchievementSystem.Instance.Unlock("watched_video");
        }

        manager.LogStep(
            sequenceAttemptId,
            hasClip
                ? "Watched the molecular visualization video"
                : "Molecular visualization not available yet for this reaction",
            0.0f,
            true);

        // The attempt was saved when it ended, so this step needs its own write to persist.
        manager.SaveToJson();
    }

    private static bool HasVideoClip(int reactionId)
    {
        ReactionLearningVideoCatalog catalog =
            Resources.Load<ReactionLearningVideoCatalog>("ReactionLearningVideoCatalog");

        if (catalog == null)
        {
            return false;
        }

        ReactionLearningVideoCatalog.Entry entry;
        return catalog.TryGetEntry(reactionId, out entry) &&
               entry != null &&
               entry.videoClip != null;
    }
}
