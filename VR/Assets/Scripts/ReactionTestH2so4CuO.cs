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
    public float tooltipHeightOffset = 0.20f;
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the task is failed.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Recipe")]
    [Tooltip("Matches the Lab reaction number so test attempts group with lab attempts.")]
    public int reactionId = 2;
    [Tooltip("Optional. Left empty, Resources/ReactionDefinitions is used - the same file the Lab reads.")]
    public ReactionDefinition definition;

    private const string TaskPrompt = "Create Copper(II) sulfate.";

    private ExamReactionRunner exam;
    private float h2so4Flow;
    private float cuoFlow;

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

        if (definition == null)
        {
            definition = ReactionDefinition.Load(reactionId);
        }
        if (definition == null)
        {
            return;
        }

        exam = new ExamReactionRunner();
        exam.Begin(definition, "ExamTooltip_H2SO4_CuO", canvasText, tooltipFontSize, countdown, randomizer);
        exam.SetFailureAudio(audioSource_failure, clip_failure);

        h2so4Flow = definition.FlowFor("H2SO4");
        cuoFlow = definition.FlowFor("CuO");
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
            exam.PourFrom("H2SO4", h2so4Flow, h2so4);
            exam.PourFrom("CuO", cuoFlow, salt);
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
