using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ReactionCaOHTest : MonoBehaviour
{
    [SerializeField] CountdownTimer countdown;
    [SerializeField] Randomize randomizer;
    public TMP_Text canvasText;
    private bool finishedTask = false;

    [SerializeField] PourCuO salt;
    [SerializeField] PourSubstance h2o;
    public GameObject CaOHPivot;
    public GameObject TurnesolPivot;
    public GameObject wetLitmusPaper;
    public GameObject wetLitmusPaper1;
    public Material blueLitmusMaterial;

    public GameObject currentBerzelius;
    public Material material;
    public ParticleSystem explosion;
    public GameObject explosionGameObject;
    public AudioSource audioSource;
    public AudioClip clip;

    [Header("Exam Mode (same rules as the Lab, quantities hidden)")]
    [Tooltip("Judged by the same FreeHandReactionEngine as reactionCaOH, but the student is never told the target or the accepted range.")]
    public bool enableExamMode = true;
    public float targetWaterMl = 30.0f;
    public float targetCaOGrams = 15.0f;
    public float tolerancePercent = 8.0f;
    public float waterFlowMlPerSecond = 5.0f;
    public float caoFlowGramsPerSecond = 3.0f;
    public float tooltipHeightOffset = 0.20f;
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the task is failed.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Experiment History")]
    [Tooltip("Matches the Lab reaction number so test attempts group with lab attempts.")]
    public int reactionId = 6;
    public string reactionDisplayName = "CaO + H2O -> Ca(OH)2 [Test]";

    private const string TaskPrompt = "Create Ca(OH)2 and check the basicity.";

    private ExamReactionRunner exam;

    private DateTime timpInitial;
    private bool isPlaying = false;
    private bool explosionActive = false;
    private bool oneExplosion = false;
    private bool showPopup = false;
    private bool done = false;

    void Start()
    {
        explosionGameObject.SetActive(false);
        explosion.Stop();
        explosion.Clear();

        if (!enableExamMode)
        {
            return;
        }

        exam = new ExamReactionRunner();
        exam.Begin("ExamTooltip_CaO_H2O", canvasText, tooltipFontSize,
            reactionId, reactionDisplayName, countdown, randomizer, tolerancePercent);
        exam.SetFailureAudio(audioSource_failure, clip_failure);

        exam.engine.wrongOrderMessage =
            "The quicklime went into a dry beaker. CaO must be slaked into a measured volume of water, otherwise the heat released has nothing to absorb it.";
        exam.engine.AddSubstance("Water", targetWaterMl, "ml",
            overdose: "Excess water produces dilute Ca(OH)2 - limewater so weak that the litmus test barely changes colour.",
            underdose: "Insufficient water leaves unreacted quicklime, so part of the CaO never slakes into calcium hydroxide.");
        exam.engine.AddSubstance("CaO", targetCaOGrams, "g",
            overdose: "Too much quicklime for this volume of water - the surplus CaO stays as a dry lump and the mixture boils dangerously.",
            underdose: "Too little quicklime leaves mostly water in the beaker, so hardly any Ca(OH)2 forms.");
    }

    void OnEnable()
    {
        if (exam != null)
        {
            exam.SetTooltipActive(true);
            exam.ResetForNewTask();
        }
        done = false;
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
            exam.Pour("Water", waterFlowMlPerSecond, h2o != null && h2o.IsPouring);
            exam.Pour("CaO", caoFlowGramsPerSecond, salt != null && salt.IsPouring);
            examResult = exam.Tick();
            exam.UpdateTooltip(currentBerzelius != null ? currentBerzelius.transform : transform,
                tooltipHeightOffset, "Task finished!");

            if (exam.HasFailed)
            {
                return; // the runner owns the canvas text and the move to the next task
            }
        }

        if (!finishedTask && canvasText != null)
        {
            canvasText.text = exam != null ? exam.Status(TaskPrompt) : TaskPrompt;
        }

        bool quantitiesCorrect = exam != null
            ? examResult == ReactionResult.Success
            : (salt != null && salt.containsCuO == true && h2o != null && h2o.containsWater == true);

        if (oneExplosion == false && quantitiesCorrect)
        {
            done = true;
            explosionActive = true;
            explosionGameObject.SetActive(true);
            showPopup = true;
            oneExplosion = true;
            timpInitial = DateTime.Now;
            explosion.Clear();
            explosion.Play();
            if (!isPlaying)
            {
                StartCoroutine(PlaySoundRepeatedly());
            }
        }
        if (explosionActive && (DateTime.Now - timpInitial).TotalSeconds >= 6)
        {
            explosionActive = false;
            explosionGameObject.SetActive(false);
            currentBerzelius.transform.Find("Substance").gameObject.GetComponent<Renderer>().material = material;
            explosion.Stop();
            explosion.Clear();
            isPlaying = false;
            audioSource.Stop();
        }
        if (!finishedTask &&
            Math.Abs(TurnesolPivot.transform.position.z - CaOHPivot.transform.position.z) < 0.05 &&
            Math.Abs(TurnesolPivot.transform.position.x - CaOHPivot.transform.position.x) < 0.05 &&
            Math.Abs(TurnesolPivot.transform.position.y - CaOHPivot.transform.position.y) < 0.01 &&
            done == true)
        {
            wetLitmusPaper.gameObject.GetComponent<Renderer>().material = blueLitmusMaterial;
            wetLitmusPaper1.gameObject.GetComponent<Renderer>().material = blueLitmusMaterial;

            if (exam != null)
            {
                exam.CompleteSuccess();
            }
            canvasText.text = "Task finished!";
            countdown.continua = false;
            finishedTask = true;
        }
        if (finishedTask && !randomizer.generateNewReaction && (DateTime.Now - timpInitial).TotalSeconds >= 9)
        {
            randomizer.generateNewReaction = true;
        }
    }

    IEnumerator PlaySoundRepeatedly()
    {
        isPlaying = true;
        while (isPlaying)
        {
            audioSource.PlayOneShot(clip);
            yield return new WaitForSeconds(clip.length);
        }
    }
}
