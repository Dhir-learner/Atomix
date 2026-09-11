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
    public float tooltipHeightOffset = 0.13f;
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the task is failed.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Recipe")]
    [Tooltip("Matches the Lab reaction number so test attempts group with lab attempts.")]
    public int reactionId = 5;
    [Tooltip("Optional. Left empty, Resources/ReactionDefinitions is used - the same file the Lab reads.")]
    public ReactionDefinition definition;

    private const string TaskPrompt = "Create aluminum iodide.";

    private ExamReactionRunner exam;
    private float aluminiumFlow;
    private float iodineFlow;
    private float pipetteFlow;

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

        if (definition == null)
        {
            definition = ReactionDefinition.Load(reactionId);
        }
        if (definition == null)
        {
            return;
        }

        // Aluminium and iodine share order group 0 in the definition - either powder may go first.
        exam = new ExamReactionRunner();
        exam.Begin(definition, "ExamTooltip_AlI3", canvasText, tooltipFontSize, countdown, randomizer);
        exam.SetFailureAudio(audioSource_failure, clip_failure);

        aluminiumFlow = definition.FlowFor("Aluminium");
        iodineFlow = definition.FlowFor("Iodine");
        pipetteFlow = definition.FlowFor("Water drops");
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
            exam.PourFrom("Aluminium", aluminiumFlow, aluminum);
            exam.PourFrom("Iodine", iodineFlow, iodine);
            exam.PourFrom("Water drops", pipetteFlow, pipette);
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
