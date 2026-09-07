using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ReactionAlI3Test : MonoBehaviour
{
    [SerializeField] CountdownTimer countdown;
    [SerializeField] Randomize randomizer;
    public TMP_Text canvasText;
    private bool finishedTask = false;

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
    public AudioSource audioSource;
    public AudioClip clip;

    [Header("Exam Mode (same rules as the Lab, quantities hidden)")]
    [Tooltip("Judged by the same FreeHandReactionEngine as ReactionAli3, but the student is never told the target or the accepted range.")]
    public bool enableExamMode = true;
    public float targetAluminumGrams = 5.0f;
    public float targetIodineGrams = 15.0f;
    public float targetWaterMl = 2.0f;
    public float tolerancePercent = 10.0f;
    public float aluminumFlowGramsPerSecond = 1.25f;
    public float iodineFlowGramsPerSecond = 3.75f;
    public float pipetteFlowMlPerSecond = 0.7f;
    public float tooltipHeightOffset = 0.13f;
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the task is failed.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Experiment History")]
    [Tooltip("Matches the Lab reaction number so test attempts group with lab attempts.")]
    public int reactionId = 5;
    public string reactionDisplayName = "2Al + 3I2 -> 2AlI3 [Test]";

    private const string TaskPrompt = "Create aluminum iodide.";

    private ExamReactionRunner exam;

    private DateTime timpInitial;
    private bool explosionActive = false;
    private bool state2 = false;
    private bool soundStarted = false;

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

        if (!enableExamMode)
        {
            return;
        }

        exam = new ExamReactionRunner();
        exam.Begin("ExamTooltip_AlI3", canvasText, tooltipFontSize,
            reactionId, reactionDisplayName, countdown, randomizer, tolerancePercent);
        exam.SetFailureAudio(audioSource_failure, clip_failure);

        exam.engine.wrongOrderMessage =
            "The water was added before both solids were in the dish. Water only acts as the catalyst once aluminium and iodine are already mixed as dry powders.";
        // Aluminium and iodine share order group 0 - either powder may go in first.
        exam.engine.AddSubstance("Aluminium", targetAluminumGrams, "g",
            overdose: "Incorrect Al:I2 ratio prevents stoichiometric completion - the surplus aluminium stays as grey metal in the dish.",
            underdose: "Incorrect Al:I2 ratio prevents stoichiometric completion - too little aluminium leaves unreacted violet iodine behind.",
            orderGroup: 0);
        exam.engine.AddSubstance("Iodine", targetIodineGrams, "g",
            overdose: "Incorrect Al:I2 ratio prevents stoichiometric completion - excess iodine sublimes off as violet vapour instead of forming AlI3.",
            underdose: "Incorrect Al:I2 ratio prevents stoichiometric completion - 2Al needs 3I2, so a shortage of iodine caps the yield.",
            orderGroup: 0);
        exam.engine.AddSubstance("Water drops", targetWaterMl, "ml",
            overdose: "Too much water floods the mixture and carries the heat away, so the catalysed reaction never reaches ignition.",
            underdose: "Too few drops of catalyst - without enough water the aluminium oxide layer is never broken and the mixture stays inert.",
            orderGroup: 1);
    }

    void OnEnable()
    {
        if (exam != null)
        {
            exam.SetTooltipActive(true);
            exam.ResetForNewTask();
        }
        soundStarted = false;
        state2 = false;
        explosionActive = false;
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
            exam.Pour("Aluminium", aluminumFlowGramsPerSecond, aluminum != null && aluminum.IsPouring);
            exam.Pour("Iodine", iodineFlowGramsPerSecond, iodine != null && iodine.IsPouring);
            exam.Pour("Water drops", pipetteFlowMlPerSecond, pipette != null && pipette.IsPouring);
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
        if (aluminum.containsCuO == true && iodine.containsCuO == false)
        {
            firstPowder.SetActive(true);
        }
        if (aluminum.containsCuO == false && iodine.containsCuO == true)
        {
            firstPowder.SetActive(true);
        }
        if (aluminum.containsCuO == true && iodine.containsCuO == true && pipette.containsWater == false)
        {
            blackPowder.SetActive(true);
        }

        bool quantitiesCorrect = exam != null
            ? examResult == ReactionResult.Success
            : (aluminum.containsCuO == true && iodine.containsCuO == true && pipette.containsWater == true);

        if (soundStarted == false && quantitiesCorrect)
        {
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
        if (!finishedTask && state2 && (DateTime.Now - timpInitial).TotalSeconds >= 2)
        {
            explosionGameObject1.SetActive(true);
            explosion1.Clear();
            explosion1.Play();
            whitePowder.SetActive(true);
            state2 = false;
            if (exam != null)
            {
                exam.CompleteSuccess();
            }
            canvasText.text = "Task finished!";
            countdown.continua = false;
            finishedTask = true;
        }
        if (finishedTask && !randomizer.generateNewReaction && (DateTime.Now - timpInitial).TotalSeconds >= 8)
        {
            randomizer.generateNewReaction = true;
        }
    }
}
