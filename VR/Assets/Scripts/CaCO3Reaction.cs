using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CaCO3Reaction : MonoBehaviour
{
    public GameObject pivotEprubeta;
    public GameObject pivotFoc;
    [SerializeField] LightFire foc;
    [SerializeField] MeshRendererScript balloon;
    public GameObject fume;
    public GameObject balon;

    public GameObject label;
    public Material CaO_material;

    public TMP_Text canvasText;
    public GameObject popupWindow;

    public AudioSource audioSource_guidance;
    public AudioClip clip_guidance1;
    public AudioClip clip_guidance2;
    public AudioClip clip_guidance3;

    [Header("Free-Hand Mode (heating time + procedure matter)")]
    public bool enableFreeHandMode = true;
    public float tooltipHeightOffset = 0.22f;
    [Tooltip("World-space font size for the floating tracker. TMP renders roughly (fontSize x 0.12) metres per line, so keep this small.")]
    public float tooltipFontSize = 0.55f;
    [Tooltip("Fail the experiment if the sample is heated before the balloon is fitted (the CO2 would escape).")]
    public bool requireBalloonBeforeHeating = true;
    [Tooltip("Optional - played when the experiment fails.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Recipe and History")]
    [Tooltip("Matches the book / StartReaction number, 1-8.")]
    public int reactionId = 7;
    [Tooltip("Optional. Left empty, Resources/ReactionDefinitions supplies the heating time, tolerance and messages.")]
    public ReactionDefinition definition;
    [Tooltip("Re-selecting this experiment from the book logs a fresh attempt.")]
    public bool restartAttemptOnReSelect = true;

    private FreeHandReactionEngine engine;
    private FreeHandTooltip tooltip;
    private bool failureReported = false;
    private ReactionHistoryRecorder recorder;

    /// <summary>Seconds the tube must stay over the flame; drives the balloon's inflation too.</summary>
    private float targetHeatingSeconds = 10.0f;

    private float targetPoint;
    private Vector3 initialScale;
    private Vector3 finalScale;
    private Vector3 initialPosition;
    private Vector3 finalPosition;
    private bool reactionCompleted = false;

    private bool audioSource1Started = false;
    private bool audioSource2Started = false;
    private bool audioSource3Started = false;
    private bool balon_ok = false;
    private bool balloonSnapped = false;
    private Transform snappedBalloonTransform;
    [SerializeField] private float desktopBalloonSnapDistance = 0.35f;
    [SerializeField] private float alreadyConnectedDistance = 0.08f;
    [SerializeField] private Vector3 desktopBalloonSnapOffset = Vector3.zero;
    [SerializeField] private Vector3 desktopBalloonSnapEulerOffset = Vector3.zero;

    void Start()
    {
        fume.SetActive(false);
        InitializeBalloonInflationTargets();
        RefreshConnectedStateFromPlacement();

        if (!enableFreeHandMode)
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

        engine = new FreeHandReactionEngine();
        definition.Configure(engine);
        targetHeatingSeconds = definition.TargetFor("Heating");

        recorder = new ReactionHistoryRecorder(reactionId, definition.displayName, engine);
        LabRunOptions.Apply(reactionId, definition, engine, recorder);

        tooltip = new FreeHandTooltip();
        tooltip.Create("TubeFloatingTooltip_CaCO3", canvasText, tooltipFontSize);
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
        LabRunOptions.NoteBenchCleared(reactionId);
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
        TrySnapBalloonToSocket();
        RefreshConnectedStateFromPlacement();

        if (balloonSnapped)
        {
            if (audioSource1Started == false)
            {
                canvasText.text = "You have successfully attached the balloon! Now you can light the Bunsen burner by pressing the white button on the burner with the grep button.";
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance1);
                audioSource1Started = true;
                balon_ok = true;
                if (recorder != null)
                {
                    recorder.LogAction("Attached the balloon to the test tube", 0.0f, true);
                }
            }
        }
        if(balon_ok == true && foc.esteAprins == true)
        {
            if(audioSource2Started == false)
            {
                canvasText.text = "You have lit the Bunsen burner successfully! Now you can hold the CaCO3 test tube over the flame and observe the balloon inflating.";
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance2);
                audioSource2Started = true;
                if (recorder != null)
                {
                    recorder.LogAction("Lit the Bunsen burner", 0.0f, true);
                }
            }
        }
        bool overFlame = IsTubeOverFlame();

        if (enableFreeHandMode && engine != null)
        {
            UpdateFreeHandHeating(overFlame);
            return;
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
                    if (targetPoint >= 1f)
                    {
                        targetPoint = 1f;
                        label.GetComponent<Renderer>().material = CaO_material;
                        if (!audioSource3Started)
                        {
                            canvasText.text = "Chemical reaction equation: CaCO3 = CaO + CO2. Now you can put the test tube with CaO on the support, close the burner and learn another reaction.";
                            audioSource_guidance.Stop();
                            audioSource_guidance.PlayOneShot(clip_guidance3);
                            audioSource3Started = true;
                        }
                        fume.SetActive(false);
                    }
                }
            }
        }
        else
        {
            fume.SetActive(false);
        }
    }

    void LateUpdate()
    {
        MaintainBalloonSnapPose();
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
    /// Free-hand heating: the user decides how long to hold the tube in the flame. Too short and
    /// the carbonate never dissociates, too long and the trapped CO2 over-pressurises the balloon.
    /// </summary>
    void UpdateFreeHandHeating(bool overFlame)
    {
        if (engine.HasFailed)
        {
            fume.SetActive(false);
            UpdateTooltip();
            return;
        }

        if (overFlame && requireBalloonBeforeHeating && !balon_ok && !engine.HasSucceeded)
        {
            definition.ForceProcedureFailure(engine, "Heating");
            ReportFreeHandFailure();
            UpdateTooltip();
            return;
        }

        if (!engine.IsResolved)
        {
            engine.UpdatePouringQuantity("Heating", definition.FlowFor("Heating"), overFlame);
        }

        ReactionResult result = engine.CheckReactionOutcome();
        if (recorder != null)
        {
            recorder.Tick();
        }
        fume.SetActive(overFlame && !engine.IsResolved);

        if (balon_ok && !reactionCompleted)
        {
            targetPoint = Mathf.Clamp01(engine.GetCurrent("Heating") / Mathf.Max(0.1f, targetHeatingSeconds));
            balon.transform.localScale = Vector3.Lerp(initialScale, finalScale, targetPoint);
            balon.transform.localPosition = Vector3.Lerp(initialPosition, finalPosition, targetPoint);
        }

        if (result == ReactionResult.Success && !reactionCompleted)
        {
            CompleteFreeHandReaction();
        }
        else if (engine.HasFailed)
        {
            ReportFreeHandFailure();
        }
        else if (overFlame && canvasText != null && !engine.IsResolved)
        {
            canvasText.text = engine.GetTrackerText();
        }

        UpdateTooltip();
    }

    void CompleteFreeHandReaction()
    {
        reactionCompleted = true;
        if (recorder != null)
        {
            recorder.Complete(ReactionResult.Success);
        }
        targetPoint = 1.0f;
        if (balon_ok && balon != null)
        {
            balon.transform.localScale = finalScale;
            balon.transform.localPosition = finalPosition;
        }

        Renderer labelRenderer = label != null ? label.GetComponent<Renderer>() : null;
        if (labelRenderer != null && CaO_material != null)
        {
            labelRenderer.material = CaO_material;
        }

        if (!audioSource3Started)
        {
            if (canvasText)
            {
                canvasText.text = "Chemical reaction equation: CaCO3 = CaO + CO2. Now you can put the test tube with CaO on the support, close the burner and learn another reaction.";
            }
            audioSource_guidance.Stop();
            audioSource_guidance.PlayOneShot(clip_guidance3);
            audioSource3Started = true;
        }
        fume.SetActive(false);
    }

    void ReportFreeHandFailure()
    {
        fume.SetActive(false);
        if (failureReported)
        {
            return;
        }

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

    void UpdateTooltip()
    {
        if (tooltip == null || !tooltip.Exists)
        {
            return;
        }

        Transform anchor = null;
        if (pivotEprubeta != null)
        {
            anchor = pivotEprubeta.transform;
        }
        else if (balon != null)
        {
            anchor = balon.transform;
        }

        tooltip.UpdatePose(anchor, tooltipHeightOffset);
        tooltip.RenderEngineState(engine, "Reaction Success!\nCaCO3 = CaO + CO2");
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

    /// <summary>
    /// Called when the experiment is (re)selected from the book so a retry is logged as its
    /// own attempt. Apparatus flags (balloon fitted, burner lit) are deliberately left alone -
    /// that hardware stays exactly where the student left it.
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
        LabRunOptions.Apply(reactionId, definition, engine, recorder);
        failureReported = false;
        reactionCompleted = false;
        audioSource3Started = false;
        targetPoint = 0.0f;
    }

}
