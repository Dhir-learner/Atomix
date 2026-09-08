using TMPro;
using UnityEngine;

/// <summary>
/// Drives one testing-scene experiment with exactly the same rules as its Lab counterpart:
/// the same <see cref="FreeHandReactionEngine"/>, the same tolerances, the same overdose /
/// underdose / wrong-order judging, and the same history recording.
///
/// The one deliberate difference is what the student is told. In the lab the tracker names the
/// target and the accepted range; here <see cref="FreeHandReactionEngine.hideTargets"/> is on, so
/// the student only ever sees how much of each chemical they have actually used. Knowing the
/// right quantity is the thing being tested.
///
/// This exists as a plain helper class rather than eight copies of the lab wiring so that all
/// eight test experiments genuinely cannot drift apart from each other.
/// </summary>
public class ExamReactionRunner
{
    public readonly FreeHandReactionEngine engine = new FreeHandReactionEngine();

    /// <summary>
    /// Seconds the failure explanation stays on screen before the next task is drawn. This is a
    /// plain helper class rather than a MonoBehaviour, so set it from the owning script's Start
    /// if a particular experiment wants longer.
    /// </summary>
    public float advanceDelayAfterFailure = 6.0f;

    private FreeHandTooltip tooltip;
    private ReactionHistoryRecorder recorder;
    private CountdownTimer countdown;
    private Randomize randomizer;
    private TMP_Text canvasText;
    private AudioSource failureAudioSource;
    private AudioClip failureClip;

    private int reactionId;
    private string reactionName = string.Empty;
    private bool started;
    private bool failureReported;
    private float failureTimer;
    private bool advanceRequested;

    public bool IsReady { get { return started; } }
    public bool HasFailed { get { return started && engine.HasFailed; } }
    public bool IsResolved { get { return started && engine.IsResolved; } }
    public bool IsAnyPouring { get { return started && engine.IsAnyPouring; } }
    public ReactionHistoryRecorder Recorder { get { return recorder; } }

    /// <summary>
    /// Sets the runner up. Call from Start, then register the substances on
    /// <see cref="engine"/> exactly as the matching lab reaction does.
    /// </summary>
    /// <param name="reactionId">The Lab reaction number, so attempts group with the lab ones.</param>
    public void Begin(string tooltipObjectName, TMP_Text canvas, float tooltipFontSize,
                      int reactionId, string displayName,
                      CountdownTimer countdown, Randomize randomizer,
                      float tolerancePercent, string trackerTitle = "[Chemicals Used]")
    {
        this.canvasText = canvas;
        this.countdown = countdown;
        this.randomizer = randomizer;
        this.reactionId = reactionId;
        this.reactionName = displayName;

        engine.hideTargets = true;          // the whole point of the testing scene
        engine.tolerancePercent = tolerancePercent;
        engine.settleTimeRequired = 1.5f;
        engine.trackerTitle = trackerTitle;

        recorder = new ReactionHistoryRecorder(reactionId, displayName, engine);
        recorder.hideTargets = true;        // the history panel must not become an answer key

        if (!string.IsNullOrEmpty(tooltipObjectName))
        {
            tooltip = new FreeHandTooltip();
            tooltip.Create(tooltipObjectName, canvas, tooltipFontSize);
            tooltip.Show(FreeHandTooltip.ProgressColor, engine.GetTooltipText());
        }

        started = true;
    }

    /// <summary>Optional failure sound, matching the lab reactions' optional pair.</summary>
    public void SetFailureAudio(AudioSource source, AudioClip clip)
    {
        failureAudioSource = source;
        failureClip = clip;
    }

    /// <summary>Feeds one continuously-poured input. Mirrors the lab call of the same name.</summary>
    public void Pour(string substanceName, float ratePerSecond, bool isPouring)
    {
        if (started && !engine.IsResolved)
        {
            engine.UpdatePouringQuantity(substanceName, ratePerSecond, isPouring);
        }
    }

    /// <summary>
    /// Advances the judging one frame. Returns the engine's verdict so the caller can gate its
    /// existing success block on <see cref="ReactionResult.Success"/> instead of on the old
    /// "containsX &amp;&amp; containsY" booleans.
    /// </summary>
    public ReactionResult Tick()
    {
        if (!started)
        {
            return ReactionResult.InProgress;
        }

        ReactionResult result = engine.CheckReactionOutcome();

        if (recorder != null)
        {
            recorder.Tick();
        }

        if (engine.HasFailed)
        {
            HandleFailure();
        }

        return result;
    }

    void HandleFailure()
    {
        if (!failureReported)
        {
            failureReported = true;
            failureTimer = 0.0f;

            if (recorder != null)
            {
                recorder.Complete(engine.LastResult);
            }

            if (canvasText != null)
            {
                canvasText.text = engine.GetFailureExplanation();
            }

            if (failureAudioSource != null && failureClip != null)
            {
                failureAudioSource.PlayOneShot(failureClip);
            }

            // A failed task scores nothing. Setting both flags stops CountdownTimer's scoring
            // branch, which only runs while wasScored is still false.
            if (countdown != null)
            {
                countdown.continua = false;
                countdown.wasScored = true;
            }

            ExamSession.RecordTask(
                ExamTaskKind.Practical, reactionId, reactionName, string.Empty,
                engine.GetFailureHeadline(), ExamTaskOutcome.Wrong,
                countdown != null ? countdown.TimeRemaining : 0.0f,
                countdown != null ? countdown.SecondsOnTask : 0.0f);
        }

        // Never leave the student stuck on a failed task - move on like the timeout does.
        failureTimer += Time.deltaTime;
        if (!advanceRequested && failureTimer >= advanceDelayAfterFailure)
        {
            advanceRequested = true;
            if (randomizer != null && !randomizer.generateNewReaction)
            {
                randomizer.generateNewReaction = true;
            }
        }
    }

    /// <summary>Closes the attempt as a success. Call from the existing task-finished block.</summary>
    public void CompleteSuccess()
    {
        if (recorder != null)
        {
            recorder.Complete(ReactionResult.Success);
        }

        // The clock is stopped by the caller's own success block, so read it before it is reset
        // for the next task. SecondsLeftAtStop is only set once CountdownTimer notices the stop,
        // which may be the next frame - TimeRemaining is the reading that is correct right now.
        float remaining = countdown != null ? countdown.TimeRemaining : 0.0f;
        float taken = countdown != null ? countdown.SecondsOnTask : 0.0f;

        ExamSession.RecordTask(
            ExamTaskKind.Practical, reactionId, reactionName, string.Empty,
            "Correct quantities and order", ExamTaskOutcome.Correct, remaining, taken);
    }

    /// <summary>
    /// The line shown on the task canvas: the question the student was set, followed by how much
    /// of each chemical they have used so far. Never the target, never the accepted range.
    /// </summary>
    public string Status(string taskPrompt)
    {
        if (!started)
        {
            return taskPrompt;
        }

        return taskPrompt + "\n\n" + engine.GetTrackerText().TrimEnd();
    }

    public void UpdateTooltip(Transform anchor, float heightOffset, string successText)
    {
        if (tooltip == null || !tooltip.Exists)
        {
            return;
        }

        tooltip.UpdatePose(anchor, heightOffset);
        tooltip.RenderEngineState(engine, successText);
    }

    public void SetTooltipActive(bool isActive)
    {
        if (tooltip != null)
        {
            tooltip.SetActive(isActive);
        }
    }

    public void DestroyTooltip()
    {
        if (tooltip != null)
        {
            tooltip.Destroy();
            tooltip = null;
        }
    }

    /// <summary>
    /// Called when the equipment for this experiment is switched off - the student either moved
    /// on or ran out of time. Closes any attempt still open so it is not left hanging.
    /// </summary>
    public void Abandon()
    {
        if (recorder != null)
        {
            recorder.Abandon();
        }
    }

    /// <summary>
    /// Called when Randomize hands this experiment out again. Clears the measured quantities and
    /// the one-shot gates so the task starts from zero.
    /// </summary>
    public void ResetForNewTask()
    {
        if (!started)
        {
            return; // OnEnable also runs before Start on the very first activation.
        }

        if (recorder != null)
        {
            recorder.Abandon();
            recorder.ResetForNewAttempt();
        }

        engine.Reset();
        failureReported = false;
        failureTimer = 0.0f;
        advanceRequested = false;
    }
}
