using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.VisualScripting;

public class ReactionTestH2so4CuO : MonoBehaviour
{
    [SerializeField] PourCuO salt;
    [SerializeField] PourH2so4 h2so4;
    [SerializeField] CountdownTimer countdown;
    [SerializeField] Randomize randomizer;
    public GameObject currentBerzelius;
    public TMP_Text canvasText;
    public Material material1;
    public Material material2;
    public Material material3;
    public GameObject popupWindow;

    [Header("Exam Mode (same rules as the Lab, quantities hidden)")]
    [Tooltip("Judged by the same FreeHandReactionEngine as Reaction_h2so4_cuo, but the student is never told the target or the accepted range.")]
    public bool enableExamMode = true;
    public float targetH2SO4Ml = 20.0f;
    public float targetCuOGrams = 8.0f;
    public float tolerancePercent = 8.0f;
    public float h2so4FlowMlPerSecond = 5.0f;
    public float cuoFlowGramsPerSecond = 2.0f;
    public float tooltipHeightOffset = 0.20f;
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the task is failed.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Experiment History")]
    [Tooltip("Matches the Lab reaction number so test attempts group with lab attempts.")]
    public int reactionId = 2;
    public string reactionDisplayName = "H2SO4 + CuO -> CuSO4 + H2O [Test]";

    private const string TaskPrompt = "Create Copper(II) sulfate.";

    private ExamReactionRunner exam;

    private DateTime timpInitial;
    private bool oneReaction = false;
    private bool showPopup = false;
    bool finishedTask = false;

    void Start()
    {
        if (!enableExamMode)
        {
            return;
        }

        exam = new ExamReactionRunner();
        exam.Begin("ExamTooltip_H2SO4_CuO", canvasText, tooltipFontSize,
            reactionId, reactionDisplayName, countdown, randomizer, tolerancePercent);
        exam.SetFailureAudio(audioSource_failure, clip_failure);

        exam.engine.wrongOrderMessage =
            "Copper(II) oxide was tipped in before any acid was present, so there was no H2SO4 for the oxide to dissolve in.";
        exam.engine.AddSubstance("H2SO4", targetH2SO4Ml, "ml",
            overdose: "Excess acid creates corrosive fumes - the leftover H2SO4 has no CuO left to neutralise it.",
            underdose: "Too little acid leaves most of the copper oxide undissolved, so no CuSO4 forms.");
        exam.engine.AddSubstance("CuO", targetCuOGrams, "g",
            overdose: "Excess copper oxide simply settles out - only the acid present can be converted to CuSO4.",
            underdose: "Insufficient CuO leaves unreacted acid, so the solution stays strongly acidic instead of turning blue.");
    }

    void OnEnable()
    {
        if (exam != null)
        {
            exam.SetTooltipActive(true);
            exam.ResetForNewTask();
        }
        oneReaction = false;
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
            exam.Pour("H2SO4", h2so4FlowMlPerSecond, h2so4 != null && h2so4.IsPouring);
            exam.Pour("CuO", cuoFlowGramsPerSecond, salt != null && salt.IsPouring);
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
            : (salt != null && salt.containsCuO == true && h2so4 != null && h2so4.containsHCL == true);

        if (oneReaction == false && taskComplete)
        {
            finishedTask = true;
            oneReaction = true;
            showPopup = true;
            if (exam != null)
            {
                exam.CompleteSuccess();
            }
            countdown.continua = false;
            canvasText.text = "Task finished!";
            timpInitial = DateTime.Now;
        }
        if (showPopup == true && (DateTime.Now - timpInitial).TotalSeconds >= 10)
        {
            showPopup = false;
        }
        if (oneReaction && (DateTime.Now - timpInitial).TotalSeconds >= 2)
        {
            currentBerzelius.transform.Find("Substance").gameObject.GetComponent<Renderer>().material = material1;
        }
        if (oneReaction && (DateTime.Now - timpInitial).TotalSeconds >= 4)
        {
            currentBerzelius.transform.Find("Substance").gameObject.GetComponent<Renderer>().material = material2;
        }
        if (oneReaction && (DateTime.Now - timpInitial).TotalSeconds >= 6)
        {
            currentBerzelius.transform.Find("Substance").gameObject.GetComponent<Renderer>().material = material3;
        }
        if (oneReaction && (DateTime.Now - timpInitial).TotalSeconds >= 8)
        {
            if (!randomizer.generateNewReaction)
            {
                randomizer.generateNewReaction = true;
            }
        }
    }
}
