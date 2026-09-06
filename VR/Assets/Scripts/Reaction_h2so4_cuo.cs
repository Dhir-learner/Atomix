using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;
//using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
//using static UnityEditor.Experimental.GraphView.GraphView;

public class Reaction_h2so4_cuo : MonoBehaviour
{
    [SerializeField] PourCuO salt;
    [SerializeField] PourH2so4 h2so4;
    public GameObject currentBerzelius;
    public TMP_Text canvasText;
    public Material material1;
    public Material material2;
    public Material material3;
    public GameObject popupWindow;
    public AudioSource audioSource_guidance;
    public AudioClip clip_guidance1;
    public AudioClip clip_guidance2;

    [Header("Free-Hand Mode (quantity + order matter)")]
    public bool enableFreeHandMode = true;
    public float targetH2SO4Ml = 20.0f;
    public float targetCuOGrams = 8.0f;
    public float tolerancePercent = 8.0f;
    public float h2so4FlowMlPerSecond = 5.0f;
    public float cuoFlowGramsPerSecond = 2.0f;
    public float tooltipHeightOffset = 0.20f;
    [Tooltip("World-space font size for the floating tracker. TMP renders roughly (fontSize x 0.12) metres per line, so keep this small.")]
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the experiment fails.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Experiment History")]
    [Tooltip("Matches the book / StartReaction number, 1-8.")]
    public int reactionId = 2;
    public string reactionDisplayName = "H2SO4 + CuO -> CuSO4 + H2O";
    [Tooltip("Re-selecting this experiment from the book logs a fresh attempt.")]
    public bool restartAttemptOnReSelect = true;

    private FreeHandReactionEngine engine;
    private FreeHandTooltip tooltip;
    private bool failureReported = false;
    private ReactionHistoryRecorder recorder;

    private DateTime timpInitial;
    private bool oneReaction = false;
    private bool showPopup = false;
    private bool audioSource1Started = false;
    private bool audioSource2Started = false;

    void Start()
    {
        if (!enableFreeHandMode)
        {
            return;
        }

        engine = new FreeHandReactionEngine();
        engine.tolerancePercent = tolerancePercent;
        engine.settleTimeRequired = 1.5f;
        engine.wrongOrderMessage =
            "Copper(II) oxide was tipped in before any acid was present, so there was no H2SO4 for the oxide to dissolve in.";
        engine.AddSubstance("H2SO4", targetH2SO4Ml, "ml",
            overdose: "Excess acid creates corrosive fumes - the leftover H2SO4 has no CuO left to neutralise it.",
            underdose: "Too little acid leaves most of the copper oxide undissolved, so no CuSO4 forms.");
        engine.AddSubstance("CuO", targetCuOGrams, "g",
            overdose: "Excess copper oxide simply settles out - only the acid present can be converted to CuSO4.",
            underdose: "Insufficient CuO leaves unreacted acid, so the solution stays strongly acidic instead of turning blue.");

        tooltip = new FreeHandTooltip();
        recorder = new ReactionHistoryRecorder(reactionId, reactionDisplayName, engine);

        tooltip.Create("BeakerFloatingTooltip_H2SO4_CuO", canvasText, tooltipFontSize);
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
        string trackerText = string.Empty;
        bool freeHandSuccess = false;

        if (enableFreeHandMode && engine != null)
        {
            if (!engine.IsResolved)
            {
                engine.UpdatePouringQuantity("H2SO4", h2so4FlowMlPerSecond, h2so4 != null && h2so4.IsPouring);
                engine.UpdatePouringQuantity("CuO", cuoFlowGramsPerSecond, salt != null && salt.IsPouring);
            }

            ReactionResult result = engine.CheckReactionOutcome();
            trackerText = engine.GetTrackerText() + "\n\n";

            if (recorder != null)
            {
                recorder.Tick();
            }

            if (result == ReactionResult.Success)
            {
                freeHandSuccess = true;
            }
            else if (engine.HasFailed)
            {
                if (!failureReported)
                {
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
            }
            else if (engine.IsAnyPouring && canvasText)
            {
                canvasText.text = trackerText.TrimEnd();
            }

            UpdateTooltip();

            if (engine.HasFailed)
            {
                return;
            }
        }

        if (h2so4 != null && h2so4.containsHCL == true && salt != null && salt.containsCuO == false)
        {
            bool quiet = enableFreeHandMode && engine != null && engine.IsAnyPouring;
            if (canvasText && !quiet)
            {
                canvasText.text = trackerText +
                    "Now you can add Copper oxide. For this, grab the CuO beaker by pressing the grep button.";
            }
            if (!audioSource1Started)
            {
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance1);
                audioSource1Started = true;
            }
        }

        bool canTriggerSuccess = enableFreeHandMode && engine != null
            ? freeHandSuccess
            : (salt != null && salt.containsCuO == true && h2so4 != null && h2so4.containsHCL == true);

        if (oneReaction == false && canTriggerSuccess)
        {
            oneReaction = true;
            if (recorder != null)
            {
                recorder.Complete(ReactionResult.Success);
            }
            showPopup = true;
            canvasText.text = "Chemical reaction equation: H2SO4 + CuO = CuSO4 + H2O. Now you can learn another reaction.";
            if (!audioSource2Started)
            {
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance2);
                audioSource2Started = true;
            }
            popupWindow.SetActive(true);
            FirstPersonController.SetCursorLock(true);
            timpInitial = DateTime.Now;
        }
        if (showPopup == true && (DateTime.Now - timpInitial).TotalSeconds >= 10)
        {
            showPopup = false;
            popupWindow.SetActive(false);
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
    }

    void UpdateTooltip()
    {
        if (tooltip == null || !tooltip.Exists)
        {
            return;
        }

        Transform anchor = null;
        if (currentBerzelius != null)
        {
            anchor = currentBerzelius.transform;
        }
        else if (h2so4 != null && h2so4.SecondGlass != null)
        {
            anchor = h2so4.SecondGlass.transform;
        }

        tooltip.UpdatePose(anchor, tooltipHeightOffset);
        tooltip.RenderEngineState(engine, "Reaction Success!\nH2SO4 + CuO = CuSO4 + H2O");
    }

    /// <summary>
    /// Called when the experiment is (re)selected from the book. Closes any attempt left
    /// hanging, clears the measured quantities and the one-shot gates so the student can
    /// try the same experiment again and have it logged as a separate attempt.
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
        failureReported = false;
        oneReaction = false;
        showPopup = false;
        audioSource1Started = false;
        audioSource2Started = false;
    }

}
