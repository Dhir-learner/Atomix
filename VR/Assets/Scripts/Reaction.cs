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

    [Header("Intelligent Proportions (quantity matters)")]
    public bool enableIntelligentMode = true;
    public float tooltipHeightOffset = 0.20f;
    [Tooltip("World-space font size for the floating tracker. TMP renders roughly (fontSize x 0.12) metres per line, so keep this small.")]
    public float tooltipFontSize = 0.55f;

    [Header("Recipe and History")]
    [Tooltip("Matches the book / StartReaction number, 1-8.")]
    public int reactionId = 1;
    [Tooltip("Optional. Left empty, Resources/ReactionDefinitions supplies the targets, tolerance and messages.")]
    public ReactionDefinition definition;
    [Tooltip("Re-selecting this experiment from the book logs a fresh attempt.")]
    public bool restartAttemptOnReSelect = true;

    // This experiment used to track its water and sodium inline, with its own copy of the
    // tolerance arithmetic and its own failure wording. It now runs on FreeHandReactionEngine like
    // the other seven, which is what gives it the top-of-screen readout, the pour gauge, levels,
    // stars and a lab assistant that can read its numbers - none of which could see it before.
    private FreeHandReactionEngine engine;
    private FreeHandTooltip tooltip;
    private ReactionHistoryRecorder recorder;
    private bool failureReported = false;
    private bool sodiumAdded = false;
    private float waterFlow;

    private DateTime timpInitial;
    private bool explosionActive = false;
    private bool phenolphthaleinAdded = false;
    private bool oneExplosion = false;
    private bool showPopup = false;
    private bool audioSource1Started = false;
    private bool audioSource2Started = false;
    private bool audioSource3Started = false;

    private bool Judged { get { return enableIntelligentMode && engine != null; } }

    void Start()
    {
        natriumMetal.SetActive(false);
        explosion.Stop();
        explosion.Clear();

        if (definition == null)
        {
            definition = ReactionDefinition.Load(reactionId);
        }

        if (enableIntelligentMode && definition != null)
        {
            engine = new FreeHandReactionEngine();
            definition.Configure(engine);

            // This reaction has never enforced an addition order in the Lab: sodium put in first
            // simply waits for the water. Kept that way - the testing scene's copy does enforce
            // it, and still does.
            engine.enforceOrder = false;
            waterFlow = definition.FlowFor("Water");
        }

        if (recorder == null)
        {
            recorder = new ReactionHistoryRecorder(reactionId,
                definition != null ? definition.displayName : "Na + H2O -> NaOH + H2", engine);
        }

        if (engine != null)
        {
            LabRunOptions.Apply(reactionId, definition, engine, recorder);

            tooltip = new FreeHandTooltip();
            tooltip.Create("BeakerFloatingTooltip_Reaction", canvasText, tooltipFontSize);
            tooltip.Show(FreeHandTooltip.ProgressColor, engine.GetTooltipText());
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
        LabRunOptions.NoteBenchCleared(reactionId);
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

            // Sodium is one measured block: the full dose arrives the moment it lands.
            if (!sodiumAdded)
            {
                sodiumAdded = true;
                if (Judged)
                {
                    engine.SetQuantity("Sodium", definition.TargetFor("Sodium"));
                }
            }
        }

        string trackerText = string.Empty;
        bool judgedSuccess = false;

        if (Judged)
        {
            if (!engine.IsResolved)
            {
                engine.UpdatePour("Water", waterFlow, water);
            }

            ReactionResult result = engine.CheckReactionOutcome();
            trackerText = engine.GetTrackerText() + "\n\n";

            if (recorder != null)
            {
                recorder.Tick();
            }

            if (result == ReactionResult.Success)
            {
                judgedSuccess = true;
            }
            else if (engine.HasFailed)
            {
                if (!failureReported)
                {
                    failureReported = true;
                    showPopup = true;
                    timpInitial = DateTime.Now;
                    if (recorder != null)
                    {
                        recorder.Complete(engine.LastResult);
                    }
                    if (canvasText != null)
                    {
                        canvasText.text = engine.GetFailureExplanation();
                    }
                    PlayFailureSound();
                }
            }
            else if (engine.IsAnyPouring && canvasText != null)
            {
                canvasText.text = trackerText.TrimEnd();
            }

            UpdateTooltip();

            if (engine.HasFailed)
            {
                // The failure explanation stays on the canvas. The guidance below used to
                // overwrite it with "Now you can add Sodium" the moment the student stopped
                // pouring, which is how an overdose before the sodium went unexplained.
                ClosePopupWhenDue();
                return;
            }
        }

        if (water.containsWater == true && metal.containsNatrium == false)
        {
            if (canvasText && (!Judged || !water.IsPouring))
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

        bool canTriggerSuccess = Judged
            ? judgedSuccess
            : (metal.containsNatrium == true && water.containsWater == true);

        if (oneExplosion == false && canTriggerSuccess)
        {
            explosionActive = true;
            showPopup = true;
            if (canvasText)
            {
                canvasText.text = "Chemical reaction equation: 2H2O + 2Na = 2NaOH + H2. Now, you can highlight the basicity of the solution by adding phenolphthalein.";
            }
            oneExplosion = true;
            if (recorder != null)
            {
                recorder.Complete(ReactionResult.Success);
            }
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
        if ((!Judged || oneExplosion) && phenolphthalein.containsPhenolphthalein == true && !phenolphthaleinAdded)
        {
            phenolphthaleinAdded = true;
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
        ClosePopupWhenDue();
    }

    void ClosePopupWhenDue()
    {
        if (showPopup == true && (DateTime.Now - timpInitial).TotalSeconds >= 10)
        {
            showPopup = false;
            popupWindow.SetActive(false);
        }
    }

    void UpdateTooltip()
    {
        if (tooltip == null || !tooltip.Exists)
        {
            return;
        }

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
        tooltip.RenderEngineState(engine, "Reaction Success!\n2H2O + 2Na = 2NaOH + H2");
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

        if (engine != null)
        {
            engine.Reset();
            LabRunOptions.Apply(reactionId, definition, engine, recorder);
        }

        failureReported = false;
        sodiumAdded = false;
        oneExplosion = false;
        explosionActive = false;
        phenolphthaleinAdded = false;
        showPopup = false;
        audioSource1Started = false;
        audioSource2Started = false;
        audioSource3Started = false;
    }

}
