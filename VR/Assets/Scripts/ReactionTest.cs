using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Threading.Tasks;

public class ReactionTest : MonoBehaviour
{
    [SerializeField] PourMetalSubstance metal;
    [SerializeField] PourSubstance water;
    [SerializeField] PourPhenolphthalein phenolphthalein;
    [SerializeField] CountdownTimer countdown;
    [SerializeField] Randomize randomizer;
    public ParticleSystem explosion;
    public GameObject natriumMetal;
    public TMP_Text canvasText;
    public AudioSource audioSource;
    public AudioClip clip;

    [Header("Exam Mode (same rules as the Lab, quantities hidden)")]
    [Tooltip("Uses the shared FreeHandReactionEngine with Reaction.cs's own numbers, but the student is never told the target or the accepted range.")]
    public bool enableExamMode = true;
    public float targetWaterMl = 50.0f;
    public float targetSodiumGrams = 5.0f;
    public float tolerancePercent = 5.0f;
    public float waterFlowMlPerSecond = 10.0f;
    public float tooltipHeightOffset = 0.20f;
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the task is failed.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Experiment History")]
    [Tooltip("Matches the Lab reaction number so test attempts group with lab attempts.")]
    public int reactionId = 1;
    public string reactionDisplayName = "Na + H2O -> NaOH + H2 [Test]";

    private const string TaskPrompt = "Create NaOH and check the acidity.";

    private ExamReactionRunner exam;
    private bool sodiumRecorded = false;

    private DateTime timpInitial;
    private bool explosionActive = false;
    private bool oneExplosion = false;
    private bool showPopup = false;
    private bool finishedTask = false;

    void Start()
    {
        natriumMetal.SetActive(false);
        explosion.Stop();
        explosion.Clear();

        if (!enableExamMode)
        {
            return;
        }

        exam = new ExamReactionRunner();
        exam.Begin("ExamTooltip_Na_H2O", canvasText, tooltipFontSize,
            reactionId, reactionDisplayName, countdown, randomizer, tolerancePercent);
        exam.SetFailureAudio(audioSource_failure, clip_failure);

        exam.engine.wrongOrderMessage =
            "Sodium was dropped in before the water was measured out. Alkali metals must meet a known volume of water, never a dry or half-filled vessel.";
        exam.engine.AddSubstance("Water", targetWaterMl, "ml",
            overdose: "Excess water dilutes the reaction - the NaOH produced is too dilute to show a clear basic result.",
            underdose: "Too little water cannot dissolve the NaOH that forms, so the reaction stalls and heats dangerously.");
        // Sodium arrives as one measured block, exactly as it does in the Lab's Reaction.cs.
        exam.engine.AddSubstance("Sodium", targetSodiumGrams, "g",
            overdose: "Too much sodium causes a dangerous explosion - the hydrogen released ignites from the reaction heat.",
            underdose: "Too little sodium leaves most of the water unreacted, so hardly any NaOH is formed.");
    }

    void OnEnable()
    {
        if (exam != null)
        {
            exam.SetTooltipActive(true);
            exam.ResetForNewTask();
        }
        sodiumRecorded = false;
        oneExplosion = false;
        explosionActive = false;
        showPopup = false;
        finishedTask = false;
    }

    void OnDisable()
    {
        if (exam != null)
        {
            exam.SetTooltipActive(false);
            exam.Abandon();
        }
    }

    void OnDestroy()
    {
        if (exam != null)
        {
            exam.DestroyTooltip();
        }
    }

    void Update()
    {
        ReactionResult examResult = ReactionResult.InProgress;

        if (exam != null)
        {
            exam.Pour("Water", waterFlowMlPerSecond, water != null && water.IsPouring);
            TrackSodium();
            examResult = exam.Tick();
            exam.UpdateTooltip(transform, tooltipHeightOffset, "Task finished!");

            if (exam.HasFailed)
            {
                return; // the runner owns the canvas text and the move to the next task
            }
        }

        if (!finishedTask && canvasText != null)
        {
            canvasText.text = exam != null ? exam.Status(TaskPrompt) : TaskPrompt;
        }
        if (!oneExplosion)
        {
            explosion.Stop();
            explosion.Clear();
            explosion.Stop();
            explosion.Clear();
        }
        if (metal.containsNatrium == true)
        {
            natriumMetal.SetActive(true);
        }
        if (oneExplosion == false && metal.containsNatrium == true && water.containsWater == true)
        {
            explosionActive = true;
            oneExplosion = true;
            timpInitial = DateTime.Now;
            explosion.Clear();
            explosion.Play();
            audioSource.PlayOneShot(clip);
        }

        bool quantitiesCorrect = exam != null
            ? examResult == ReactionResult.Success
            : (metal.containsNatrium && water.containsWater);

        if (!finishedTask && quantitiesCorrect && phenolphthalein.containsPhenolphthalein)
        {
            finishedTask = true;
            if (exam != null)
            {
                exam.CompleteSuccess();
            }
            canvasText.text = "Task finished!";
            countdown.continua = false;
            timpInitial = DateTime.Now;
        }
        if (explosionActive && (DateTime.Now - timpInitial).TotalSeconds >= 6)
        {
            explosionActive = false;
            explosion.Stop();
            explosion.Clear();
            audioSource.Stop();
        }
        if (showPopup == true && (DateTime.Now - timpInitial).TotalSeconds >= 8)
        {
            showPopup = false;
        }
        if (finishedTask && !randomizer.generateNewReaction && (DateTime.Now - timpInitial).TotalSeconds >= 8)
        {
            randomizer.generateNewReaction = true;
        }
    }

    /// <summary>
    /// Sodium is a solid block, not a pour: it arrives in one measured lump the moment it meets
    /// the water. Mirrors how the Lab's Reaction.cs credits the full 5 g in one go.
    /// </summary>
    void TrackSodium()
    {
        if (metal == null || sodiumRecorded)
        {
            return;
        }

        if (metal.containsNatrium)
        {
            sodiumRecorded = true;
            exam.engine.SetQuantity("Sodium", targetSodiumGrams);
        }
    }
}
