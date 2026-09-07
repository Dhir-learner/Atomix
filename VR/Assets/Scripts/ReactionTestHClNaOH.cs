using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.VisualScripting;

public class ReactionTestHClNaOH : MonoBehaviour
{
    [SerializeField] PourNahco3 salt;
    [SerializeField] PourHCL hcl;
    [SerializeField] CountdownTimer countdown;
    [SerializeField] Randomize randomizer;
    public GameObject currentBerzelius;
    public Material material;
    public ParticleSystem explosion;
    public GameObject explosionGameObject;
    public TMP_Text canvasText;
    public GameObject popupWindow;
    public AudioSource audioSource;
    public AudioClip clip;

    [Header("Exam Mode (same rules as the Lab, quantities hidden)")]
    [Tooltip("Judged by the same FreeHandReactionEngine as Reaction_hcl_nahco3, but the student is never told the target or the accepted range.")]
    public bool enableExamMode = true;
    public float targetHClMl = 15.0f;
    public float targetNaHCO3Grams = 12.0f;
    public float tolerancePercent = 10.0f;
    public float hclFlowMlPerSecond = 5.0f;
    public float nahco3FlowGramsPerSecond = 3.0f;
    public float tooltipHeightOffset = 0.20f;
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the task is failed.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Experiment History")]
    [Tooltip("Matches the Lab reaction number so test attempts group with lab attempts.")]
    public int reactionId = 3;
    public string reactionDisplayName = "HCl + NaHCO3 -> NaCl + H2O + CO2 [Test]";

    private const string TaskPrompt = "Create table salt.";

    private ExamReactionRunner exam;

    private DateTime timpInitial;
    private bool isPlaying = false;
    private bool explosionActive = false;
    private bool oneExplosion = false;
    private bool showPopup = false;
    bool finishedTask = false;

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
        exam.Begin("ExamTooltip_HCl_NaHCO3", canvasText, tooltipFontSize,
            reactionId, reactionDisplayName, countdown, randomizer, tolerancePercent);
        exam.SetFailureAudio(audioSource_failure, clip_failure);

        exam.engine.wrongOrderMessage =
            "Sodium bicarbonate was tipped in before the acid, so the CO2 escaped from a dry powder instead of a controlled neutralisation.";
        exam.engine.AddSubstance("HCl", targetHClMl, "ml",
            overdose: "Too much acid produces excessive CO2 gas violently - the froth overflows and the leftover HCl stays in the beaker.",
            underdose: "Too little acid leaves most of the bicarbonate unreacted, so effervescence stops almost immediately.");
        exam.engine.AddSubstance("NaHCO3", targetNaHCO3Grams, "g",
            overdose: "Excess bicarbonate cannot react - only the HCl present can be neutralised, so solid NaHCO3 is left behind.",
            underdose: "Insufficient bicarbonate leaves an acidic solution, so the neutralisation to NaCl is incomplete.");
    }

    void OnEnable()
    {
        if (exam != null)
        {
            exam.SetTooltipActive(true);
            exam.ResetForNewTask();
        }
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
            exam.Pour("HCl", hclFlowMlPerSecond, hcl != null && hcl.IsPouring);
            exam.Pour("NaHCO3", nahco3FlowGramsPerSecond, salt != null && salt.IsPouring);
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

        bool taskComplete = exam != null
            ? examResult == ReactionResult.Success
            : (salt != null && salt.containsNahco3 == true && hcl != null && hcl.containsHCL == true);

        if (oneExplosion == false && taskComplete)
        {
            finishedTask = true;
            explosionActive = true;
            explosionGameObject.SetActive(true);
            oneExplosion = true;
            if (exam != null)
            {
                exam.CompleteSuccess();
            }
            countdown.continua = false;
            timpInitial = DateTime.Now;
            showPopup = true;
            canvasText.text = "Task finished!";
            explosion.Clear();
            explosion.Play();
            if (!isPlaying)
            {
                StartCoroutine(PlaySoundRepeatedly());
            }
        }
        if (explosionActive && (DateTime.Now - timpInitial).TotalSeconds >= 4)
        {
            explosionActive = false;
            explosionGameObject.SetActive(false);
            currentBerzelius.transform.Find("Substance").gameObject.GetComponent<Renderer>().material = material;
            explosion.Stop();
            explosion.Clear();
            isPlaying = false;
            audioSource.Stop();
            timpInitial = DateTime.Now;
        }
        if (showPopup == true && (DateTime.Now - timpInitial).TotalSeconds >= 5)
        {
            showPopup = false;
            if (!randomizer.generateNewReaction)
            {
                randomizer.generateNewReaction = true;
            }
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
