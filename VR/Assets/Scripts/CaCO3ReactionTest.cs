using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CaCO3ReactionTest : MonoBehaviour
{
    [SerializeField] CountdownTimer countdown;
    [SerializeField] Randomize randomizer;
    public TMP_Text canvasText;
    private bool finishedTask = false;
    private DateTime timpInitial;

    public GameObject pivotEprubeta;
    public GameObject pivotFoc;
    [SerializeField] LightFire foc;
    [SerializeField] MeshRendererScript balloon;
    public GameObject fume;
    public GameObject balon;

    public GameObject label;
    public Material CaO_material;

    private float targetPoint;
    private Vector3 initialScale;
    private Vector3 finalScale;
    private Vector3 initialPosition;
    private Vector3 finalPosition;
    private bool reactionCompleted = false;
    private bool balon_ok = false;
    private bool balloonSnapped = false;
    private Transform snappedBalloonTransform;
    [SerializeField] private float desktopBalloonSnapDistance = 0.35f;
    [SerializeField] private float alreadyConnectedDistance = 0.08f;
    [SerializeField] private Vector3 desktopBalloonSnapOffset = Vector3.zero;
    [SerializeField] private Vector3 desktopBalloonSnapEulerOffset = Vector3.zero;

    [Header("Exam Mode (same rules as the Lab, quantities hidden)")]
    [Tooltip("Judged by the same FreeHandReactionEngine as CaCO3Reaction, but the student is never told how long to heat for.")]
    public bool enableExamMode = true;
    [Tooltip("Heating before the balloon is fitted lets the CO2 escape, and fails the task.")]
    public bool requireBalloonBeforeHeating = true;
    public float tooltipHeightOffset = 0.22f;
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the task is failed.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Recipe")]
    [Tooltip("Matches the Lab reaction number so test attempts group with lab attempts.")]
    public int reactionId = 7;
    [Tooltip("Optional. Left empty, Resources/ReactionDefinitions is used - the same file the Lab reads.")]
    public ReactionDefinition definition;

    private const string TaskPrompt = "Decompose CaCO3 and inflate the balloon.";

    private ExamReactionRunner exam;
    private bool balloonLogged = false;
    private float targetHeatingSeconds = 10.0f;

    void Start()
    {
        fume.SetActive(false);
        InitializeBalloonInflationTargets();
        RefreshConnectedStateFromPlacement();

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
        exam.Begin(definition, "ExamTooltip_CaCO3", canvasText, tooltipFontSize, countdown, randomizer);
        exam.SetFailureAudio(audioSource_failure, clip_failure);

        targetHeatingSeconds = definition.TargetFor("Heating");
    }

    void OnEnable()
    {
        if (exam != null)
        {
            exam.SetTooltipActive(true);
            exam.ResetForNewTask();
        }
        balloonLogged = false;
        reactionCompleted = false;
        finishedTask = false;
        targetPoint = 0.0f;
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
        TrySnapBalloonToSocket();
        RefreshConnectedStateFromPlacement();

        if (balloonSnapped)
        {
            balon_ok = true;
            if (exam != null && !balloonLogged)
            {
                balloonLogged = true;
                if (exam.Recorder != null)
                {
                    exam.Recorder.LogAction("Attached the balloon to the test tube", 0.0f, true);
                }
            }
        }

        bool overFlame = IsTubeOverFlame();

        if (exam != null)
        {
            UpdateExamHeating(overFlame);
        }
        else
        {
            if (!finishedTask)
            {
                canvasText.text = TaskPrompt;
            }
            if (!reactionCompleted && overFlame)
            {
                fume.SetActive(true);
                if (balon_ok)
                {
                    targetPoint += Time.deltaTime / 10;
                    balon.transform.localScale = Vector3.Lerp(initialScale, finalScale, targetPoint);
                    balon.transform.localPosition = Vector3.Lerp(initialPosition, finalPosition, targetPoint);
                    if (targetPoint >= 1f)
                    {
                        targetPoint = 1f;
                        reactionCompleted = true;
                        CompleteTask();
                    }
                }
            }
            else
            {
                fume.SetActive(false);
            }
        }

        if (finishedTask && !randomizer.generateNewReaction && (DateTime.Now - timpInitial).TotalSeconds >= 5)
        {
            randomizer.generateNewReaction = true;
        }
    }

    bool IsTubeOverFlame()
    {
        if (pivotFoc == null || pivotEprubeta == null || foc == null || !foc.esteAprins)
        {
            return false;
        }

        return Math.Abs(pivotFoc.transform.position.z - pivotEprubeta.transform.position.z) < 0.05 &&
               Math.Abs(pivotFoc.transform.position.x - pivotEprubeta.transform.position.x) < 0.05 &&
               Math.Abs(pivotFoc.transform.position.y - pivotEprubeta.transform.position.y) < 0.1;
    }

    /// <summary>
    /// Free-hand heating: the student decides how long to hold the tube in the flame, and is not
    /// told the right duration. Too short and the carbonate never dissociates, too long and the
    /// trapped CO2 over-pressurises the balloon. Same rules as the Lab's CaCO3Reaction.
    /// </summary>
    void UpdateExamHeating(bool overFlame)
    {
        if (exam.HasFailed)
        {
            fume.SetActive(false);
            exam.Tick();
            exam.UpdateTooltip(transform, tooltipHeightOffset, "Task finished!");
            return;
        }

        if (overFlame && requireBalloonBeforeHeating && !balon_ok && !exam.engine.HasSucceeded)
        {
            definition.ForceProcedureFailure(exam.engine, "Heating");
            fume.SetActive(false);
            exam.Tick();
            exam.UpdateTooltip(transform, tooltipHeightOffset, "Task finished!");
            return;
        }

        exam.Pour("Heating", 1.0f, overFlame);
        ReactionResult result = exam.Tick();
        fume.SetActive(overFlame && !exam.IsResolved);

        if (balon_ok && !reactionCompleted)
        {
            targetPoint = Mathf.Clamp01(exam.engine.GetCurrent("Heating") / Mathf.Max(0.1f, targetHeatingSeconds));
            balon.transform.localScale = Vector3.Lerp(initialScale, finalScale, targetPoint);
            balon.transform.localPosition = Vector3.Lerp(initialPosition, finalPosition, targetPoint);
        }

        if (result == ReactionResult.Success && !reactionCompleted)
        {
            reactionCompleted = true;
            exam.CompleteSuccess();
            targetPoint = 1.0f;
            if (balon_ok && balon != null)
            {
                balon.transform.localScale = finalScale;
                balon.transform.localPosition = finalPosition;
            }
            CompleteTask();
        }
        else if (!finishedTask && canvasText != null && !exam.IsResolved)
        {
            canvasText.text = exam.Status(TaskPrompt);
        }

        exam.UpdateTooltip(transform, tooltipHeightOffset, "Task finished!");
    }

    void CompleteTask()
    {
        Renderer labelRenderer = label != null ? label.GetComponent<Renderer>() : null;
        if (labelRenderer != null && CaO_material != null)
        {
            labelRenderer.material = CaO_material;
        }

        timpInitial = DateTime.Now;
        fume.SetActive(false);
        canvasText.text = "Task finished!";
        countdown.continua = false;
        finishedTask = true;
    }

    void LateUpdate()
    {
        MaintainBalloonSnapPose();
    }

    void TrySnapBalloonToSocket()
    {
        if (balloon == null || balon == null || balloonSnapped)
        {
            return;
        }

        if (Vector3.Distance(balon.transform.position, balloon.transform.position) > desktopBalloonSnapDistance)
        {
            return;
        }

        GameObject balloonRoot = ResolveBalloonRoot();

        ObjectInteraction.ReleaseIfHolding(balloonRoot);
        if (balloonRoot != balon)
        {
            ObjectInteraction.ReleaseIfHolding(balon);
        }

        FinalizeBalloonSnap(balloonRoot);
    }

    void RefreshConnectedStateFromPlacement()
    {
        if (balloonSnapped || balloon == null || balon == null)
        {
            return;
        }

        if (Vector3.Distance(balon.transform.position, balloon.transform.position) > alreadyConnectedDistance)
        {
            return;
        }

        FinalizeBalloonSnap(ResolveBalloonRoot());
    }

    GameObject ResolveBalloonRoot()
    {
        if (balon == null)
        {
            return null;
        }

        ObjectGrabbable grabbable = balon.GetComponentInParent<ObjectGrabbable>();
        if (grabbable != null)
        {
            return grabbable.gameObject;
        }

        Rigidbody rb = balon.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            return rb.gameObject;
        }

        return balon;
    }

    void InitializeBalloonInflationTargets()
    {
        if (balon == null)
        {
            return;
        }

        initialScale = balon.transform.localScale;
        finalScale = initialScale * 17f / 10f;
        initialPosition = balon.transform.localPosition;
        finalPosition = new Vector3(initialPosition.x, initialPosition.y - 0.35f, initialPosition.z);
    }

    void FinalizeBalloonSnap(GameObject balloonRoot)
    {
        if (balloonRoot == null || balloon == null)
        {
            return;
        }

        Rigidbody balloonRigidbody = balloonRoot.GetComponent<Rigidbody>();
        if (balloonRigidbody != null)
        {
            balloonRigidbody.linearVelocity = Vector3.zero;
            balloonRigidbody.angularVelocity = Vector3.zero;
            balloonRigidbody.useGravity = false;
            balloonRigidbody.isKinematic = true;
        }

        ObjectGrabbable grabbable = balloonRoot.GetComponent<ObjectGrabbable>();
        if (grabbable != null)
        {
            grabbable.SetGrabbable(false);
        }

        snappedBalloonTransform = balloonRoot.transform;
        snappedBalloonTransform.SetParent(null, true);
        AlignBalloonRootToSocket(balloonRoot);

        InitializeBalloonInflationTargets();
        balloon.Enable();
        balloonSnapped = true;
        balon_ok = true;
    }

    void AlignBalloonRootToSocket(GameObject balloonRoot)
    {
        if (balloonRoot == null || balloon == null)
        {
            return;
        }

        balloonRoot.transform.position = balloon.transform.position + (balloon.transform.rotation * desktopBalloonSnapOffset);
        balloonRoot.transform.rotation = balloon.transform.rotation * Quaternion.Euler(desktopBalloonSnapEulerOffset);
    }

    void MaintainBalloonSnapPose()
    {
        if (!balloonSnapped || snappedBalloonTransform == null || balloon == null)
        {
            return;
        }

        AlignBalloonRootToSocket(snappedBalloonTransform.gameObject);
    }
}
