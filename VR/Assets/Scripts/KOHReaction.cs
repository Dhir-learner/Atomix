using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;

public class KOHReaction : MonoBehaviour
{
    [SerializeField] PourMetalSubstance metal;
    [SerializeField] PourSubstance water;
    [SerializeField] PourPhenolphthalein phenolphthalein;
    public ParticleSystem explosion;
    public GameObject explosionGameObject;
    public GameObject natriumMetal;
    public TMP_Text canvasText;
    public GameObject popupWindow;
    public AudioSource audioSource;
    public AudioClip clip;

    public AudioSource audioSource_guidance;
    public AudioClip clip_guidance1;
    public AudioClip clip_guidance2;
    public AudioClip clip_guidance3;

    [Header("Free-Hand Mode (quantity + order matter)")]
    public bool enableFreeHandMode = true;
    public float targetWaterMl = 50.0f;
    public float targetPotassiumGrams = 3.0f;
    public float tolerancePercent = 5.0f;
    public float waterFlowMlPerSecond = 10.0f;
    [Tooltip("Seconds the potassium container may stay tipped before extra metal starts falling in.")]
    public float potassiumPourGraceSeconds = 2.0f;
    [Tooltip("Extra potassium added per second once the grace period has elapsed.")]
    public float potassiumExtraFlowGramsPerSecond = 1.0f;
    public float tooltipHeightOffset = 0.20f;
    [Tooltip("World-space font size for the floating tracker. TMP renders roughly (fontSize x 0.12) metres per line, so keep this small.")]
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the experiment fails.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Experiment History")]
    [Tooltip("Matches the book / StartReaction number, 1-8.")]
    public int reactionId = 4;
    public string reactionDisplayName = "K + H2O -> KOH + H2";
    [Tooltip("Re-selecting this experiment from the book logs a fresh attempt.")]
    public bool restartAttemptOnReSelect = true;

    private FreeHandReactionEngine engine;
    private FreeHandTooltip tooltip;
    private bool failureReported = false;
    private ReactionHistoryRecorder recorder;
    private bool potassiumDropped = false;
    private float potassiumContactTimer = 0.0f;

    private DateTime timpInitial;
    private bool explosionActive = false;
    private bool phenolphthaleinAdded = false;
    private bool oneExplosion = false;
    private bool showPopup = true;

    private bool audioSource1Started = false;
    private bool audioSource2Started = false;
    private bool audioSource3Started = false;

    void Start()
    {
        natriumMetal.SetActive(false);
        explosionGameObject.SetActive(false);
        explosion.Stop();
        explosion.Clear();

        if (!enableFreeHandMode)
        {
            return;
        }

        engine = new FreeHandReactionEngine();
        engine.tolerancePercent = tolerancePercent;
        engine.settleTimeRequired = 1.5f;
        engine.wrongOrderMessage =
            "Potassium was dropped in before the water was measured out. Alkali metals must meet a known volume of water, never a dry or half-filled vessel.";
        engine.AddSubstance("Water", targetWaterMl, "ml",
            overdose: "Excess water dilutes the reaction - the KOH produced is too dilute to show a clear basic result.",
            underdose: "Too little water cannot dissolve the KOH that forms, so the reaction stalls and heats dangerously.");
        engine.AddSubstance("Potassium", targetPotassiumGrams, "g",
            overdose: "Too much potassium causes a dangerous explosion - the hydrogen released ignites from the reaction heat.",
            underdose: "Too little potassium leaves most of the water unreacted, so hardly any KOH is formed.");

        tooltip = new FreeHandTooltip();
        recorder = new ReactionHistoryRecorder(reactionId, reactionDisplayName, engine);

        tooltip.Create("BeakerFloatingTooltip_KOH", canvasText, tooltipFontSize);
        tooltip.Show(FreeHandTooltip.ProgressColor, engine.GetTooltipText());
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
        string trackerText = string.Empty;
        bool freeHandSuccess = false;

        if (metal.containsNatrium == true)
        {
            natriumMetal.SetActive(true);
        }

        if (enableFreeHandMode && engine != null)
        {
            if (!engine.IsResolved)
            {
                engine.UpdatePouringQuantity("Water", waterFlowMlPerSecond, water != null && water.IsPouring);
                TrackPotassium();
            }

            ReactionResult result = engine.CheckReactionOutcome();
            trackerText = engine.GetTrackerText() + "\n\n";

            if (recorder != null)
            {
                recorder.Tick();
            }

            if (result == ReactionResult.Success)
            {
                freeHandSuccess = true;
            }
            else if (engine.HasFailed)
            {
                if (!failureReported)
                {
                    failureReported = true;
                    if (recorder != null)
                    {
                        recorder.Complete(engine.LastResult);
                    }
                    if (canvasText)
                    {
                        canvasText.text = engine.GetFailureExplanation();
                    }
                    if (audioSource_failure != null && clip_failure != null)
                    {
                        audioSource_failure.PlayOneShot(clip_failure);
                    }
                }
            }
            else if (engine.IsAnyPouring && canvasText)
            {
                canvasText.text = trackerText.TrimEnd();
            }

            UpdateTooltip();

            if (engine.HasFailed)
            {
                return;
            }
        }

        if (water.containsWater == true && metal.containsNatrium == false)
        {
            bool quiet = enableFreeHandMode && engine != null && engine.IsAnyPouring;
            if (canvasText && !quiet)
            {
                canvasText.text = trackerText +
                    "Now you can add Potassium. For this, press twice on the lid with the grep button, and when the lid has disappeared you can grab the glass.";
            }
            if (!audioSource1Started)
            {
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance1);
                audioSource1Started = true;
            }
        }

        bool canTriggerSuccess = enableFreeHandMode && engine != null
            ? freeHandSuccess
            : (metal.containsNatrium == true && water.containsWater == true);

        if (oneExplosion == false && canTriggerSuccess)
        {
            explosionActive = true;
            if (recorder != null)
            {
                recorder.Complete(ReactionResult.Success);
            }
            showPopup = true;
            if (canvasText)
            {
                canvasText.text = "Chemical reaction equation: 2H2O + 2K = 2KOH + H2.  Now, you can highlight the basicity of the solution by adding methyl orange.";
            }
            if (!audioSource2Started)
            {
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance2);
                audioSource2Started = true;
            }
            oneExplosion = true;
            timpInitial = DateTime.Now;
            explosionGameObject.SetActive(true);
            explosion.Clear();
            explosion.Play();
            audioSource.PlayOneShot(clip);
        }
        if ((!enableFreeHandMode || oneExplosion) && phenolphthalein.containsPhenolphthalein == true && !phenolphthaleinAdded)
        {
            phenolphthaleinAdded = true;
            showPopup = true;
            if (canvasText)
            {
                canvasText.text = "You added PH indicator successfully! Now you can learn another reaction.";
            }
            if (!audioSource3Started)
            {
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance3);
                audioSource2Started = true;
            }
            timpInitial = DateTime.Now;
            popupWindow.SetActive(true);
            FirstPersonController.SetCursorLock(true);
        }
        if (explosionActive && (DateTime.Now - timpInitial).TotalSeconds >= 6)
        {
            explosionActive = false;
            explosionGameObject.SetActive(false);
            explosion.Stop();
            explosion.Clear();
            audioSource.Stop();
        }
        if (showPopup == true && (DateTime.Now - timpInitial).TotalSeconds >= 10)
        {
            showPopup = false;
            //popupWindow.SetActive(false);
        }
    }

    /// <summary>
    /// The first tip of the container drops one measured lump of potassium (the correct dose).
    /// Keeping the container tipped past the grace period keeps metal falling in and overdoses
    /// the beaker, which is exactly the mistake that makes this reaction explosive in a real lab.
    /// </summary>
    void TrackPotassium()
    {
        if (metal == null)
        {
            return;
        }

        if (!potassiumDropped)
        {
            if (metal.containsNatrium)
            {
                potassiumDropped = true;
                potassiumContactTimer = 0.0f;
                engine.SetQuantity("Potassium", targetPotassiumGrams);
            }
            return;
        }

        bool stillTipped = metal.IsPouring;
        if (stillTipped)
        {
            potassiumContactTimer += Time.deltaTime;
            if (potassiumContactTimer > potassiumPourGraceSeconds)
            {
                engine.AddDiscreteQuantity("Potassium", potassiumExtraFlowGramsPerSecond * Time.deltaTime);
            }
            // Keep the settle timer honest while the container is still being emptied.
            engine.UpdatePouringQuantity("Potassium", 0.0f, true);
        }
        else
        {
            engine.UpdatePouringQuantity("Potassium", 0.0f, false);
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
        tooltip.RenderEngineState(engine, "Reaction Success!\n2H2O + 2K = 2KOH + H2");
    }

    /// <summary>
    /// Called when the experiment is (re)selected from the book. Closes any attempt left
    /// hanging, clears the measured quantities and the one-shot gates so the student can
    /// try the same experiment again and have it logged as a separate attempt.
    /// </summary>
    void RestartAttemptIfRequested()
    {
        if (!restartAttemptOnReSelect || engine == null)
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
        potassiumDropped = false;
        potassiumContactTimer = 0.0f;
        oneExplosion = false;
        explosionActive = false;
        phenolphthaleinAdded = false;
        audioSource1Started = false;
        audioSource2Started = false;
        audioSource3Started = false;
    }

}
