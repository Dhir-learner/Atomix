using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;

public class KOHReactionTest : MonoBehaviour
{
    [SerializeField] PourMetalSubstance metal;
    [SerializeField] PourSubstance water;
    [SerializeField] PourPhenolphthalein phenolphthalein;
    [SerializeField] CountdownTimer countdown;
    [SerializeField] Randomize randomizer;
    public ParticleSystem explosion;
    public GameObject explosionGameObject;
    public GameObject natriumMetal;
    public TMP_Text canvasText;
    public GameObject popupWindow;
    public AudioSource audioSource;
    public AudioClip clip;

    [Header("Exam Mode (same rules as the Lab, quantities hidden)")]
    [Tooltip("Judged by the same FreeHandReactionEngine as KOHReaction, but the student is never told the target or the accepted range.")]
    public bool enableExamMode = true;
    public float tooltipHeightOffset = 0.20f;
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the task is failed.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Recipe")]
    [Tooltip("Matches the Lab reaction number so test attempts group with lab attempts.")]
    public int reactionId = 4;
    [Tooltip("Optional. Left empty, Resources/ReactionDefinitions is used - the same file the Lab reads.")]
    public ReactionDefinition definition;

    private const string TaskPrompt = "Create KOH and check the acidity.";

    private ExamReactionRunner exam;
    private bool potassiumDropped = false;
    private float potassiumContactTimer = 0.0f;
    private float waterFlow;
    private ReagentDefinition potassium;

    private DateTime timpInitial;
    private bool explosionActive = false;
    private bool oneExplosion = false;
    private bool showPopup = false;
    private bool finishedTask = false;

    void Start()
    {
        natriumMetal.SetActive(false);
        explosionGameObject.SetActive(false);
        explosion.Stop();
        explosion.Clear();

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

        exam = new ExamReactionRunner();
        exam.Begin(definition, "ExamTooltip_K_H2O", canvasText, tooltipFontSize, countdown, randomizer);
        exam.SetFailureAudio(audioSource_failure, clip_failure);

        waterFlow = definition.FlowFor("Water");
        potassium = definition.Reagent("Potassium");
    }

    void OnEnable()
    {
        if (exam != null)
        {
            exam.SetTooltipActive(true);
            exam.ResetForNewTask();
        }
        potassiumDropped = false;
        potassiumContactTimer = 0.0f;
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
            exam.PourFrom("Water", waterFlow, water);
            TrackPotassium();
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
            explosionGameObject.SetActive(true);
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
            explosionGameObject.SetActive(false);
            explosion.Stop();
            explosion.Clear();
            audioSource.Stop();
        }
        if (showPopup == true && (DateTime.Now - timpInitial).TotalSeconds >= 10)
        {
            showPopup = false;
            popupWindow.SetActive(false);
        }
        if (finishedTask && !randomizer.generateNewReaction && (DateTime.Now - timpInitial).TotalSeconds >= 10)
        {
            randomizer.generateNewReaction = true;
        }
    }

    /// <summary>
    /// Potassium is a solid: the first tip drops one correctly-measured lump. Holding the
    /// container tipped past the grace period keeps metal falling in, which is the only way to
    /// overdose a discrete solid - and it mirrors the real mistake. Same logic as KOHReaction.
    /// </summary>
    void TrackPotassium()
    {
        if (metal == null || potassium == null)
        {
            return;
        }

        if (!potassiumDropped)
        {
            if (metal.containsNatrium)
            {
                potassiumDropped = true;
                potassiumContactTimer = 0.0f;
                exam.engine.SetQuantity("Potassium", potassium.target);
            }
            return;
        }

        bool stillTipped = metal.IsPouring;
        if (stillTipped)
        {
            potassiumContactTimer += Time.deltaTime;
            if (potassiumContactTimer > potassium.lumpGraceSeconds)
            {
                // Tipped harder, the extra metal falls in faster - the same tilt rule as a pour.
                exam.engine.AddDiscreteQuantity("Potassium",
                    potassium.lumpOverflowPerSecond * metal.FlowRate * Time.deltaTime);
            }
            // Keep the settle timer honest while the container is still being emptied.
            exam.engine.UpdatePouringQuantity("Potassium", 0.0f, true);
        }
        else
        {
            exam.engine.UpdatePouringQuantity("Potassium", 0.0f, false);
        }
    }
}
