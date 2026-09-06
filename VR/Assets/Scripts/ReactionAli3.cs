using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ReactionAli3 : MonoBehaviour
{
    [SerializeField] PourCuO iodine;
    [SerializeField] PourCuO aluminum;
    [SerializeField] PourFromPipette pipette;

    public ParticleSystem explosion1;
    public ParticleSystem explosion2;
    public ParticleSystem explosion3;

    public GameObject explosionGameObject1;
    public GameObject explosionGameObject2;
    public GameObject explosionGameObject3;

    public GameObject firstPowder;
    public GameObject blackPowder;
    public GameObject purplePowder;
    public GameObject whitePowder;

    public TMP_Text canvasText;
    public GameObject popupWindow;
    public AudioSource audioSource;
    public AudioClip clip;

    public AudioSource audioSource_guidance;
    public AudioClip clip_guidance1;
    public AudioClip clip_guidance2;
    public AudioClip clip_guidance3;
    public AudioClip clip_guidance4;

    [Header("Free-Hand Mode (quantity + order matter)")]
    public bool enableFreeHandMode = true;
    public float targetAluminumGrams = 5.0f;
    public float targetIodineGrams = 15.0f;
    public float targetWaterMl = 2.0f;
    public float tolerancePercent = 10.0f;
    public float aluminumFlowGramsPerSecond = 1.25f;
    public float iodineFlowGramsPerSecond = 3.75f;
    [Tooltip("A full pipette empties in about 3 seconds, so this rate sets how many ml those drops are worth.")]
    public float pipetteFlowMlPerSecond = 0.7f;
    public float tooltipHeightOffset = 0.13f;
    [Tooltip("World-space font size for the floating tracker. TMP renders roughly (fontSize x 0.12) metres per line, so keep this small.")]
    public float tooltipFontSize = 0.55f;
    public GameObject tooltipAnchor;
    [Tooltip("Optional - played when the experiment fails.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Experiment History")]
    [Tooltip("Matches the book / StartReaction number, 1-8.")]
    public int reactionId = 5;
    public string reactionDisplayName = "2Al + 3I2 -> 2AlI3";
    [Tooltip("Re-selecting this experiment from the book logs a fresh attempt.")]
    public bool restartAttemptOnReSelect = true;

    private FreeHandReactionEngine engine;
    private FreeHandTooltip tooltip;
    private bool failureReported = false;
    private ReactionHistoryRecorder recorder;

    private DateTime timpInitial;
    private bool explosionActive = false;
    private bool state2 = false;
    private bool soundStarted = false;

    private bool audioSource1Started = false;
    private bool audioSource2Started = false;
    private bool audioSource3Started = false;
    private bool audioSource4Started = false;

    void Start()
    {
        firstPowder.SetActive(false);
        blackPowder.SetActive(false);
        purplePowder.SetActive(false);
        whitePowder.SetActive(false);

        explosionGameObject1.SetActive(false);
        explosionGameObject2.SetActive(false);
        explosionGameObject3.SetActive(false);

        explosion1.Stop();
        explosion2.Stop();
        explosion3.Stop();
        explosion1.Clear();
        explosion2.Clear();
        explosion3.Clear();

        if (!enableFreeHandMode)
        {
            return;
        }

        engine = new FreeHandReactionEngine();
        engine.tolerancePercent = tolerancePercent;
        engine.settleTimeRequired = 1.5f;
        engine.wrongOrderMessage =
            "The water was added before both solids were in the dish. Water only acts as the catalyst once aluminium and iodine are already mixed as dry powders.";
        // Aluminium and iodine share order group 0 - either powder may go in first.
        engine.AddSubstance("Aluminium", targetAluminumGrams, "g",
            overdose: "Incorrect Al:I2 ratio prevents stoichiometric completion - the surplus aluminium stays as grey metal in the dish.",
            underdose: "Incorrect Al:I2 ratio prevents stoichiometric completion - too little aluminium leaves unreacted violet iodine behind.",
            orderGroup: 0);
        engine.AddSubstance("Iodine", targetIodineGrams, "g",
            overdose: "Incorrect Al:I2 ratio prevents stoichiometric completion - excess iodine sublimes off as violet vapour instead of forming AlI3.",
            underdose: "Incorrect Al:I2 ratio prevents stoichiometric completion - 2Al needs 3I2, so a shortage of iodine caps the yield.",
            orderGroup: 0);
        engine.AddSubstance("Water drops", targetWaterMl, "ml",
            overdose: "Too much water floods the mixture and carries the heat away, so the catalysed reaction never reaches ignition.",
            underdose: "Too few drops of catalyst - without enough water the aluminium oxide layer is never broken and the mixture stays inert.",
            orderGroup: 1);

        tooltip = new FreeHandTooltip();
        recorder = new ReactionHistoryRecorder(reactionId, reactionDisplayName, engine);

        tooltip.Create("DishFloatingTooltip_AlI3", canvasText, tooltipFontSize);
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

        if (enableFreeHandMode && engine != null)
        {
            if (!engine.IsResolved)
            {
                engine.UpdatePouringQuantity("Aluminium", aluminumFlowGramsPerSecond,
                    aluminum != null && aluminum.IsPouring);
                engine.UpdatePouringQuantity("Iodine", iodineFlowGramsPerSecond,
                    iodine != null && iodine.IsPouring);
                engine.UpdatePouringQuantity("Water drops", pipetteFlowMlPerSecond,
                    pipette != null && pipette.IsPouring);
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

        bool quiet = enableFreeHandMode && engine != null && engine.IsAnyPouring;

        if (aluminum.containsCuO == true && iodine.containsCuO == false)
        {
            if (canvasText && !quiet)
            {
                canvasText.text = trackerText + "Now you can add Iodine. For this, grab the Iodine beaker by pressing the grep button.";
            }
            if (!audioSource1Started)
            {
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance1);
                audioSource1Started = true;
            }
            firstPowder.SetActive(true);
        }
        if (aluminum.containsCuO == false && iodine.containsCuO == true)
        {
            if (canvasText && !quiet)
            {
                canvasText.text = trackerText + "Now you can add Aluminum. For this, grab the Aluminum beaker by pressing the grep button.";
            }
            if (!audioSource2Started)
            {
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance2);
                audioSource2Started = true;
            }
            firstPowder.SetActive(true);
        }
        if (aluminum.containsCuO == true && iodine.containsCuO == true && pipette.containsWater == false)
        {
            if (canvasText && !quiet)
            {
                canvasText.text = trackerText + "Now you can add water. For this, grab the pipette by pressing the grep button, appropriate it to the mouth of the Water beaker and pour a few drops over the mixture of Aluminum and Iodine." + GetPipetteHint();
            }
            if (!audioSource3Started)
            {
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance3);
                audioSource3Started = true;
            }
            blackPowder.SetActive(true);
        }

        bool canTriggerSuccess = enableFreeHandMode && engine != null
            ? freeHandSuccess
            : (aluminum.containsCuO == true && iodine.containsCuO == true && pipette.containsWater == true);

        if (soundStarted == false && canTriggerSuccess)
        {
            if (recorder != null)
            {
                recorder.Complete(ReactionResult.Success);
            }
            if (canvasText)
            {
                canvasText.text = "Chemical reaction equation: 2Al + 3I2 = 2AlI3. Now you can learn another reaction.";
            }
            if (!audioSource4Started)
            {
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance4);
                audioSource4Started = true;
            }
            blackPowder.SetActive(true);
            purplePowder.SetActive(true);
            soundStarted = true;
            timpInitial = DateTime.Now;
            explosionActive = true;
            explosionGameObject3.SetActive(true);
            explosion3.Clear();
            explosion3.Play();
            audioSource.PlayOneShot(clip);
        }
        if (explosionActive && (DateTime.Now - timpInitial).TotalSeconds >= 1)
        {
            explosionGameObject2.SetActive(true);
            explosion2.Clear();
            explosion2.Play();
            state2 = true;
            explosionActive = false;
        }
        if (state2 && (DateTime.Now - timpInitial).TotalSeconds >= 2)
        {
            explosionGameObject1.SetActive(true);
            explosion1.Clear();
            explosion1.Play();
            whitePowder.SetActive(true);
            state2 = false;
        }
    }

    /// <summary>
    /// Tells the user what the pipette is currently doing. Without this the pipette reads as
    /// broken: there is no other on-screen sign of whether it actually drew any water up.
    /// </summary>
    string GetPipetteHint()
    {
        if (pipette == null)
        {
            return string.Empty;
        }

        if (engine != null && engine.GetCurrent("Water drops") >= engine.MinAllowed("Water drops"))
        {
            return string.Empty;
        }

        if (!pipette.IsFull)
        {
            return "\n\nPipette: EMPTY - dip the tip into the H2O flask to draw water up.";
        }

        return pipette.IsOverDish
            ? "\n\nPipette: dripping into the dish..."
            : "\n\nPipette: FULL - hold the tip over the crystallizing dish to release the drops.";
    }

    void UpdateTooltip()
    {
        if (tooltip == null || !tooltip.Exists)
        {
            return;
        }

        Transform anchor = null;
        if (tooltipAnchor != null)
        {
            anchor = tooltipAnchor.transform;
        }
        else if (pipette != null && pipette.crystallizing_dish != null)
        {
            anchor = pipette.crystallizing_dish.transform;
        }
        else if (explosionGameObject3 != null)
        {
            anchor = explosionGameObject3.transform;
        }

        tooltip.UpdatePose(anchor, tooltipHeightOffset);
        tooltip.RenderEngineState(engine, "Reaction Success!\n2Al + 3I2 = 2AlI3");
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
        soundStarted = false;
        explosionActive = false;
        state2 = false;
        audioSource1Started = false;
        audioSource2Started = false;
        audioSource3Started = false;
        audioSource4Started = false;
    }

}
