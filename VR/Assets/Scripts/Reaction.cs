using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;

public class Reaction : MonoBehaviour
{
    [SerializeField] PourMetalSubstance metal;
    [SerializeField] PourSubstance water;
    [SerializeField] PourPhenolphthalein phenolphthalein;
    public ParticleSystem explosion;
    public GameObject natriumMetal;
    public TMP_Text canvasText;
    public GameObject popupWindow;
    public AudioSource audioSource;
    public AudioClip clip;
    public AudioSource audioSource_guidance;
    public AudioClip clip_guidance1;
    public AudioClip clip_guidance2;
    public AudioClip clip_guidance3;

    [Header("Failure Feedback (optional - matches reactions 2-8)")]
    [Tooltip("Left unassigned this is silently skipped, so no scene edit is required.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Intelligent Proportions (Zero-Collision Math)")]
    public bool enableIntelligentMode = true;
    public float targetWaterMl = 50.0f;
    public float targetSodiumG = 5.0f;
    public float tolerance = 0.05f; // 5% deviation
    public float tooltipHeightOffset = 0.20f;
    [Tooltip("World-space font size for the floating tracker. TMP renders roughly (fontSize x 0.12) metres per line, so keep this small.")]
    public float tooltipFontSize = 0.55f;

    [Header("Experiment History")]
    [Tooltip("Matches the book / StartReaction number, 1-8.")]
    public int reactionId = 1;
    public string reactionDisplayName = "Na + H2O -> NaOH + H2";
    [Tooltip("Re-selecting this experiment from the book logs a fresh attempt.")]
    public bool restartAttemptOnReSelect = true;

    private float currentWaterMl = 0.0f;
    private float currentSodiumG = 0.0f;
    private bool reactionFailed = false;
    private float settleTimer = 0.0f;

    private FreeHandTooltip tooltip;
    private ReactionHistoryRecorder recorder;
    private bool waterWasPouring = false;
    private bool sodiumLogged = false;

    private DateTime timpInitial;
    private bool explosionActive = false;
    private bool phenolphthaleinAdded = false;
    private bool oneExplosion = false;
    private bool showPopup = false;
    private bool audioSource1Started = false;
    private bool audioSource2Started = false;
    private bool audioSource3Started = false;

    void Start()
    {
        natriumMetal.SetActive(false);
        explosion.Stop();
        explosion.Clear();

        if (recorder == null)
        {
            // No FreeHandReactionEngine here - this reaction tracks its quantities inline,
            // so the recorder is fed by hand rather than by ReactionHistoryRecorder.Tick().
            recorder = new ReactionHistoryRecorder(reactionId, reactionDisplayName, null);
        }

        if (enableIntelligentMode && tooltip == null)
        {
            tooltip = new FreeHandTooltip();
            tooltip.Create("BeakerFloatingTooltip_Reaction", canvasText, tooltipFontSize);
            tooltip.Show(FreeHandTooltip.ProgressColor,
                $"Water: 0.0 / {targetWaterMl} ml\nSodium: 0.0 / {targetSodiumG} g");
        }
    }

    void OnEnable()
    {
        if (tooltip != null)
        {
            tooltip.SetActive(true);
        }
        RestartAttemptIfRequested();
    }

    void OnDisable()
    {
        if (tooltip != null)
        {
            tooltip.SetActive(false);
        }
        if (recorder != null)
        {
            recorder.Abandon(); // switching experiments away mid-run
        }
    }

    void OnDestroy()
    {
        if (tooltip != null)
        {
            tooltip.Destroy();
        }
    }

    void Update()
    {
        if (metal.containsNatrium == true)
        {
            natriumMetal.SetActive(true);
            currentSodiumG = targetSodiumG; // Discrete solid block added successfully!
            if (!sodiumLogged && recorder != null)
            {
                sodiumLogged = true;
                recorder.LogAction(string.Format("Added {0:F1} g of Sodium", currentSodiumG),
                    currentSodiumG, true);
            }
        }

        string trackerText = "";
        if (enableIntelligentMode && !oneExplosion && !reactionFailed)
        {
            bool isCurrentlyPouring = false;
            if (water != null && water.IsPouring)
            {
                currentWaterMl += 10.0f * Time.deltaTime; // Smooth 10 ml/sec pouring flow rate
                isCurrentlyPouring = true;
            }

            if (isCurrentlyPouring && !waterWasPouring)
            {
                waterWasPouring = true;
                if (recorder != null)
                {
                    recorder.LogAction("Started pouring Water", currentWaterMl, true);
                }
            }
            else if (!isCurrentlyPouring && waterWasPouring)
            {
                waterWasPouring = false;
                if (recorder != null)
                {
                    bool inRange = currentWaterMl >= targetWaterMl * (1.0f - tolerance) &&
                                   currentWaterMl <= targetWaterMl * (1.0f + tolerance);
                    recorder.LogAction(string.Format("Added {0:F1} ml of Water (target {1:F1})",
                        currentWaterMl, targetWaterMl), currentWaterMl, inRange);
                }
            }

            trackerText = $"[Lab Measurement Tracker]\nWater: {currentWaterMl:F1} ml / {targetWaterMl} ml (Target: ~50 ml)\nSodium: {currentSodiumG:F1} g / {targetSodiumG} g\n\n";
            if (isCurrentlyPouring && canvasText != null)
            {
                canvasText.text = trackerText + "Pouring water... Stop between 47.5 ml and 52.5 ml for correct chemical equilibrium!";
            }

            // Check Overdose during pouring
            float maxWater = targetWaterMl * (1.0f + tolerance);
            if (currentWaterMl > maxWater)
            {
                reactionFailed = true;
                showPopup = true;
                timpInitial = DateTime.Now;
                string overdoseReason =
                    "Excess water alters the concentration and disrupts the controlled reaction.";
                if (canvasText != null)
                {
                    canvasText.text = BuildFailureText(
                        ReactionResult.FailOverdose, "Water", overdoseReason);
                }
                PlayFailureSound();
                CompleteAttempt(ExperimentOutcome.FailOverdose, overdoseReason);
                return;
            }

            // Settle timer check after pouring pauses
            if (currentWaterMl > 0f && currentSodiumG > 0f && !isCurrentlyPouring)
            {
                settleTimer += Time.deltaTime;
                if (settleTimer >= 1.5f && !oneExplosion)
                {
                    float minWater = targetWaterMl * (1.0f - tolerance);
                    if (currentWaterMl < minWater)
                    {
                        reactionFailed = true;
                        showPopup = true;
                        timpInitial = DateTime.Now;
                        string underdoseReason =
                            "Insufficient solvent prevents proper ion dissociation.";
                        if (canvasText != null)
                        {
                            canvasText.text = BuildFailureText(
                                ReactionResult.FailUnderdose, "Water", underdoseReason);
                        }
                        PlayFailureSound();
                        CompleteAttempt(ExperimentOutcome.FailUnderdose, underdoseReason);
                        return;
                    }
                }
            }
            else if (!isCurrentlyPouring)
            {
                settleTimer = 0f;
            }
        }

        // Update floating tooltip position above target beaker with billboard camera facing
        if (tooltip != null && tooltip.Exists)
        {
            Transform anchor = null;
            if (water != null && water.SecondGlass != null)
            {
                anchor = water.SecondGlass.transform;
            }
            else if (explosion != null)
            {
                anchor = explosion.transform;
            }

            tooltip.UpdatePose(anchor, tooltipHeightOffset);

            if (!oneExplosion && !reactionFailed)
            {
                tooltip.Show(FreeHandTooltip.ProgressColor,
                    $"Water: {currentWaterMl:F1} / {targetWaterMl} ml\nSodium: {currentSodiumG:F1} / {targetSodiumG} g");
            }
            else if (oneExplosion)
            {
                tooltip.Show(FreeHandTooltip.SuccessColor, "Reaction Success!\n2H2O + 2Na = 2NaOH + H2");
            }
            else if (reactionFailed)
            {
                if (currentWaterMl > targetWaterMl * (1.0f + tolerance))
                {
                    tooltip.Show(FreeHandTooltip.FailureColor,
                        $"FAILED: Overdose!\nWater: {currentWaterMl:F1} ml (Max {targetWaterMl * (1.0f + tolerance):F1} ml)");
                }
                else
                {
                    tooltip.Show(FreeHandTooltip.FailureColor,
                        $"FAILED: Incorrect Ratios\nWater: {currentWaterMl:F1} ml | Na: {currentSodiumG:F1} g");
                }
            }
        }

        if (water.containsWater == true && metal.containsNatrium == false)
        {
            if (canvasText && (!enableIntelligentMode || !water.IsPouring))
            {
                canvasText.text = trackerText + "Now you can add Sodium. For this, press twice on the lid with the grep button, and when the lid has disappeared you can grab the glass.";
            }
            if (!audioSource1Started)
            {
                if (audioSource_guidance != null && clip_guidance1 != null)
                {
                    audioSource_guidance.Stop();
                    audioSource_guidance.PlayOneShot(clip_guidance1);
                }
                audioSource1Started = true;
            }
        }

        bool canTriggerSuccess = false;
        if (!enableIntelligentMode)
        {
            canTriggerSuccess = (metal.containsNatrium == true && water.containsWater == true);
        }
        else if (!reactionFailed && currentWaterMl > 0f && currentSodiumG > 0f && settleTimer >= 1.5f)
        {
            canTriggerSuccess = true;
        }

        if (oneExplosion == false && canTriggerSuccess)
        {
            explosionActive = true;
            showPopup = true;
            if (canvasText)
            {
                canvasText.text = "Chemical reaction equation: 2H2O + 2Na = 2NaOH + H2. Now, you can highlight the basicity of the solution by adding phenolphthalein.";
            }
            oneExplosion = true;
            CompleteAttempt(ExperimentOutcome.Success, null);
            timpInitial = DateTime.Now;
            explosion.Clear();
            explosion.Play();
            audioSource.PlayOneShot(clip);
            if (!audioSource2Started)
            {
                if (audioSource_guidance != null && clip_guidance2 != null)
                {
                    audioSource_guidance.Stop();
                    audioSource_guidance.PlayOneShot(clip_guidance2);
                }
                audioSource2Started = true;
            }
        }
        if ((!enableIntelligentMode || oneExplosion) && phenolphthalein.containsPhenolphthalein == true && !phenolphthaleinAdded)
        {
            phenolphthaleinAdded = true;
            if (recorder != null)
            {
                recorder.LogAction("Added phenolphthalein indicator", 0.0f, true);
            }
            showPopup = true;
            if (canvasText)
            {
                canvasText.text = "You added PH indicator successfully! Now you can learn another reaction.";
            }
            if (!audioSource3Started)
            {
                if (audioSource_guidance != null && clip_guidance3 != null)
                {
                    audioSource_guidance.Stop();
                    audioSource_guidance.PlayOneShot(clip_guidance3);
                }
                audioSource3Started = true;
            }
            timpInitial = DateTime.Now;
            popupWindow.SetActive(true);
            FirstPersonController.SetCursorLock(true);
        }
        if (explosionActive && (DateTime.Now - timpInitial).TotalSeconds >= 6)
        {
            explosionActive = false;
            explosion.Stop();
            explosion.Clear();
            audioSource.Stop();
        }
        if (showPopup == true && (DateTime.Now - timpInitial).TotalSeconds >= 10)
        {
            showPopup = false;
            popupWindow.SetActive(false);
        }
    }

    /// <summary>
    /// Builds the failure message in the shared layout used by reactions 2-8, so this experiment
    /// does not read differently just because it tracks its quantities inline.
    /// </summary>
    string BuildFailureText(ReactionResult result, string offendingSubstance, string reason)
    {
        string measurements =
            FreeHandReactionEngine.ComposeMeasurementLine("Water", currentWaterMl, "ml", targetWaterMl) +
            FreeHandReactionEngine.ComposeMeasurementLine("Sodium", currentSodiumG, "g", targetSodiumG);

        return FreeHandReactionEngine.ComposeFailureExplanation(
            FreeHandReactionEngine.ComposeFailureHeadline(result, offendingSubstance),
            measurements,
            reason);
    }

    /// <summary>
    /// Plays the dedicated failure clip when one is assigned - the same optional pair reactions
    /// 2-8 have. Falls back to the original behaviour (the reaction clip) so that leaving the new
    /// fields empty sounds exactly as it did before, rather than going silent.
    /// </summary>
    void PlayFailureSound()
    {
        if (audioSource_failure != null && clip_failure != null)
        {
            audioSource_failure.PlayOneShot(clip_failure);
            return;
        }

        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    /// <summary>Writes the final quantities and closes the history attempt.</summary>
    void CompleteAttempt(ExperimentOutcome outcome, string reason)
    {
        if (recorder == null)
        {
            return;
        }

        recorder.BeginIfNeeded();

        Dictionary<string, float> used = new Dictionary<string, float>();
        used["Water"] = currentWaterMl;
        used["Sodium"] = currentSodiumG;

        Dictionary<string, float> targets = new Dictionary<string, float>();
        targets["Water"] = targetWaterMl;
        targets["Sodium"] = targetSodiumG;

        ExperimentHistoryManager.Instance.RecordQuantities(recorder.AttemptId, used, targets);

        if (!string.IsNullOrEmpty(reason))
        {
            recorder.LogAction("Why it failed: " + reason, 0.0f, false);
        }

        recorder.Complete(outcome);
    }

    /// <summary>
    /// Called when the experiment is (re)selected from the book so a retry is logged as its
    /// own attempt.
    /// </summary>
    void RestartAttemptIfRequested()
    {
        if (!restartAttemptOnReSelect || recorder == null)
        {
            return; // OnEnable also runs before Start on the very first activation.
        }

        recorder.Abandon();
        recorder.ResetForNewAttempt();

        currentWaterMl = 0.0f;
        currentSodiumG = 0.0f;
        settleTimer = 0.0f;
        reactionFailed = false;
        oneExplosion = false;
        explosionActive = false;
        phenolphthaleinAdded = false;
        showPopup = false;
        waterWasPouring = false;
        sodiumLogged = false;
        audioSource1Started = false;
        audioSource2Started = false;
        audioSource3Started = false;
    }

}


