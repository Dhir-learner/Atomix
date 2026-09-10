using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lets a failed experiment be run again without restarting the game.
///
/// Two things stood in the way, and they compounded:
///
/// 1. <b>Re-selecting the same experiment did nothing.</b> Every reaction resets itself in
///    <c>OnEnable</c> (via <c>RestartAttemptIfRequested</c>), and <c>ControlReactions.StartReactionN</c>
///    activates its equipment with <c>SetActive(true)</c>. On an object that is already active that
///    is a no-op - <c>OnEnable</c> never fires - so choosing the same experiment again left the
///    engine sitting in its Failed state permanently.
///
/// 2. <b>The bench itself was never reset.</b> The <c>containsWater</c> / <c>containsCuO</c> /
///    <c>containsHCL</c> flags on the pour scripts are set once and never cleared, the recipient's
///    substance material stays changed, poured containers stay where they were dropped, the CaCO3
///    balloon stays snapped and non-grabbable, and the FeSO4 tube keeps its baked colour. Clearing
///    the engine alone would have produced a half-reset experiment that looks finished but claims
///    to be empty.
///
/// Rather than chase that state across eight reaction scripts, eight pour scripts, materials,
/// transforms and particle systems - which is where a subtle "sometimes it does not reset" bug
/// would live - this reloads the lab and re-selects the experiment the student was on. It is
/// "wash up and start over", which is both what a real bench requires and impossible to get
/// half-right. The experiment history, achievements and settings all live on DontDestroyOnLoad
/// objects, so nothing the student has earned is lost.
/// </summary>
public class LabRetryController : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Resets the bench and re-selects the current experiment.")]
    public KeyCode retryKey = KeyCode.F5;

    [Header("Availability")]
    [Tooltip("Scenes where the retry key works. The testing scene moves on by itself, and has its " +
             "own 'Run the test again' button on the results card.")]
    public string[] enabledScenes = { "LabScene" };

    [Tooltip("Frames to keep looking for ControlReactions after the reload before giving up.")]
    public int reselectAttempts = 120;

    /// <summary>
    /// Survives the scene load, so the reaction the student was on can be re-selected once the
    /// fresh scene is up. Static because this component is rebuilt with the persistent object.
    /// </summary>
    private static int pendingReactionId = -1;
    private static string pendingReactionName = string.Empty;

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void Update()
    {
        // A question is being typed into the assistant panel; every letter belongs to it.
        if (LabTextInput.IsCapturing)
        {
            return;
        }

        if (Input.GetKeyDown(retryKey) && IsEnabledScene())
        {
            RestartCurrentExperiment();
        }
    }

    private bool IsEnabledScene()
    {
        if (enabledScenes == null || enabledScenes.Length == 0)
        {
            return false;
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

    /// <summary>True when there is an experiment on the bench that could be restarted.</summary>
    public bool CanRestart
    {
        get { return IsEnabledScene() && CurrentRecorder() != null; }
    }

    private static ReactionHistoryRecorder CurrentRecorder()
    {
        ReactionHistoryRecorder selected = ReactionHistoryRecorder.Selected;
        return selected != null ? selected : ReactionHistoryRecorder.Active;
    }

    /// <summary>
    /// Reloads the lab and re-selects whatever experiment was on the bench. Also reachable from
    /// the pause menu, because a key nobody knows about is not a fix.
    /// </summary>
    public void RestartCurrentExperiment()
    {
        if (!IsEnabledScene())
        {
            return;
        }

        AtomixAudio.ResetBench();

        ReactionHistoryRecorder recorder = CurrentRecorder();
        pendingReactionId = recorder != null ? recorder.ReactionId : -1;
        pendingReactionName = recorder != null ? recorder.ReactionName : string.Empty;

        // Close the open attempt so the reload does not leave it dangling as InProgress. A
        // finished attempt ignores this, so a failure stays on the record - the point is to retry
        // the experiment, not to erase the fact that it went wrong.
        if (recorder != null)
        {
            recorder.Abandon();
        }

        ExperimentHistoryManager manager = ExperimentHistoryManager.Instance;
        if (manager != null)
        {
            manager.SaveToJson();
        }

        SceneTransition.Reload();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingReactionId < 1 || pendingReactionId > 8)
        {
            pendingReactionId = -1;
            return;
        }

        StartCoroutine(ReselectAfterLoad(pendingReactionId, pendingReactionName));
        pendingReactionId = -1;
        pendingReactionName = string.Empty;
    }

    /// <summary>
    /// ControlReactions.Start() switches every recipient off, so the selection has to happen after
    /// it has run - and it may take a frame or two for the scene to finish building.
    /// </summary>
    private IEnumerator ReselectAfterLoad(int reactionId, string reactionName)
    {
        ControlReactions controls = null;

        for (int i = 0; i < Mathf.Max(1, reselectAttempts); i++)
        {
            yield return null;     // Start() runs during this frame

            controls = FindFirstObjectByType<ControlReactions>(FindObjectsInactive.Exclude);
            if (controls != null && i > 0)
            {
                break;             // found it, and Start has had a full frame
            }
        }

        if (controls == null)
        {
            Debug.LogWarning("[LabRetryController] Reloaded the lab but found no ControlReactions, " +
                             "so the experiment could not be re-selected.");
            yield break;
        }

        Select(controls, reactionId);

        string label = string.IsNullOrEmpty(reactionName)
            ? "Experiment"
            : reactionName.Replace(" [Test]", string.Empty);
        LabHudController.Toast("Bench reset\n" + label + " - ready to try again");
    }

    /// <summary>
    /// Dispatches to the numbered entry points. Done here rather than by adding a dispatcher to
    /// ControlReactions, so that script stays untouched.
    /// </summary>
    private static void Select(ControlReactions controls, int reactionId)
    {
        switch (reactionId)
        {
            case 1: controls.StartReaction1(); break;
            case 2: controls.StartReaction2(); break;
            case 3: controls.StartReaction3(); break;
            case 4: controls.StartReaction4(); break;
            case 5: controls.StartReaction5(); break;
            case 6: controls.StartReaction6(); break;
            case 7: controls.StartReaction7(); break;
            case 8: controls.StartReaction8(); break;
        }
    }
}
