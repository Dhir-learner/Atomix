using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Feso4Reaction : MonoBehaviour
{
    public Material substantaEprubetaMaterial;
    [Tooltip("Optional explicit renderers that display the FeSO4 liquid color. If empty, renderers are auto-detected.")]
    [SerializeField] Renderer[] substanceRenderers;
    public GameObject pivotEprubeta;
    public GameObject pivotFoc;
    [SerializeField] LightFire foc;
    public GameObject fume;

    public GameObject label;
    public Material Fe2O3_material;

    public TMP_Text canvasText;
    public GameObject popupWindow;

    public AudioSource audioSource_guidance;
    public AudioClip clip_guidance1;
    public AudioClip clip_guidance2;

    public Color[] colors;
    [Tooltip("How close the FeSO4 pivot must be to the flame pivot to start heating.")]
    public float heatingDistance = 0.22f;
    [Tooltip("How many seconds of heating are required for full color change.")]
    public float heatingDuration = 10f;
    public float time;

    [Header("Free-Hand Mode (heating time matters)")]
    public bool enableFreeHandMode = true;
    public float tooltipHeightOffset = 0.22f;
    [Tooltip("World-space font size for the floating tracker. TMP renders roughly (fontSize x 0.12) metres per line, so keep this small.")]
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the experiment fails.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Recipe and History")]
    [Tooltip("Matches the book / StartReaction number, 1-8.")]
    public int reactionId = 8;
    [Tooltip("Optional. Left empty, Resources/ReactionDefinitions supplies the heating time, tolerance and messages.")]
    public ReactionDefinition definition;
    [Tooltip("Re-selecting this experiment from the book logs a fresh attempt.")]
    public bool restartAttemptOnReSelect = true;

    private FreeHandReactionEngine engine;
    private FreeHandTooltip tooltip;
    private bool failureReported = false;
    private ReactionHistoryRecorder recorder;

    private bool audioSource1Started = false;
    private bool audioSource2Started = false;
    private bool reactionCompleted = false;
    private float heatingProgress = 0f;
    private readonly List<Material> runtimeSubstanceMaterials = new List<Material>();

    void Start()
    {
        CacheSubstanceMaterials();

        if (colors != null && colors.Length > 0)
        {
            ApplySubstanceColor(colors[0]);
        }

        time = 0;
        SetFumeActive(false);

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

        recorder = new ReactionHistoryRecorder(reactionId, definition.displayName, engine);
        LabRunOptions.Apply(reactionId, definition, engine, recorder);

        tooltip = new FreeHandTooltip();
        tooltip.Create("TubeFloatingTooltip_FeSO4", canvasText, tooltipFontSize);
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
        if (foc != null && foc.esteAprins && !audioSource1Started)
        {
            canvasText.text = "You have lit the Bunsen burner successfully! Now you can hold the FeSO4 test tube over the flame and observe the color change.";
            audioSource_guidance.Stop();
            audioSource_guidance.PlayOneShot(clip_guidance1);
            audioSource1Started = true;
            if (recorder != null)
            {
                recorder.LogAction("Lit the Bunsen burner", 0.0f, true);
            }
        }

        if (enableFreeHandMode && engine != null)
        {
            UpdateFreeHandHeating(IsTubeOverFlame());
            return;
        }

        if (reactionCompleted)
        {
            return;
        }

        if (IsTubeOverFlame())
        {
            SetFumeActive(true);
            AdvanceHeating();
        }
        else
        {
            SetFumeActive(false);
        }
    }

    /// <summary>
    /// Free-hand heating: the colour gradient follows however long the user actually holds the
    /// tube in the flame, and the experiment is only judged once the tube is taken back out.
    /// </summary>
    void UpdateFreeHandHeating(bool overFlame)
    {
        if (engine.HasFailed)
        {
            SetFumeActive(false);
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
        SetFumeActive(overFlame && !engine.IsResolved);

        // The colour follows the heating the engine is judging, so the two always agree.
        float duration = Mathf.Max(0.1f, definition.TargetFor("Heating"));
        heatingProgress = Mathf.Clamp01(engine.GetCurrent("Heating") / duration);
        ApplySubstanceColor(EvaluateGradientColor(heatingProgress));

        if (result == ReactionResult.Success && !reactionCompleted)
        {
            CompleteReaction();
        }
        else if (engine.HasFailed)
        {
            SetFumeActive(false);
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
        else if (overFlame && canvasText != null && !engine.IsResolved)
        {
            canvasText.text = engine.GetTrackerText();
        }

        UpdateTooltip();
    }

    void UpdateTooltip()
    {
        if (tooltip == null || !tooltip.Exists)
        {
            return;
        }

        Transform anchor = pivotEprubeta != null ? pivotEprubeta.transform : transform;
        tooltip.UpdatePose(anchor, tooltipHeightOffset);
        tooltip.RenderEngineState(engine, "Reaction Success!\n2FeSO4 = Fe2O3 + SO2 + SO3");
    }

    bool IsTubeOverFlame()
    {
        if (pivotFoc == null || pivotEprubeta == null || foc == null)
        {
            return false;
        }

        if (!foc.esteAprins)
        {
            return false;
        }

        return Vector3.Distance(pivotFoc.transform.position, pivotEprubeta.transform.position) <= heatingDistance;
    }

    void AdvanceHeating()
    {
        float duration = Mathf.Max(0.1f, heatingDuration);
        heatingProgress = Mathf.Clamp01(heatingProgress + (Time.deltaTime / duration));

        ApplySubstanceColor(EvaluateGradientColor(heatingProgress));

        if (heatingProgress < 1f)
        {
            return;
        }

        CompleteReaction();
    }

    void CompleteReaction()
    {
        reactionCompleted = true;
        if (recorder != null)
        {
            recorder.Complete(ReactionResult.Success);
        }
        SetFumeActive(false);

        Renderer labelRenderer = label != null ? label.GetComponent<Renderer>() : null;
        if (labelRenderer != null && Fe2O3_material != null)
        {
            labelRenderer.material = Fe2O3_material;
        }

        canvasText.text = "Chemical reaction equation: 2FeSO4 = Fe2O3 + SO2 + SO3. Now you can put the test tube with Fe2O3 on the support, close the burner and learn another reaction.";
        if (!audioSource2Started)
        {
            audioSource_guidance.Stop();
            audioSource_guidance.PlayOneShot(clip_guidance2);
            audioSource2Started = true;
        }
    }

    Color EvaluateGradientColor(float t)
    {
        if (colors == null || colors.Length == 0)
        {
            return GetCurrentSubstanceColor();
        }

        if (colors.Length == 1)
        {
            return colors[0];
        }

        float scaled = Mathf.Clamp01(t) * (colors.Length - 1);
        int leftIndex = Mathf.FloorToInt(scaled);
        int rightIndex = Mathf.Min(leftIndex + 1, colors.Length - 1);
        float localT = scaled - leftIndex;
        return Color.Lerp(colors[leftIndex], colors[rightIndex], localT);
    }

    void CacheSubstanceMaterials()
    {
        runtimeSubstanceMaterials.Clear();

        if (substantaEprubetaMaterial != null)
        {
            runtimeSubstanceMaterials.Add(substantaEprubetaMaterial);
        }

        Renderer[] renderersToScan = (substanceRenderers != null && substanceRenderers.Length > 0)
            ? substanceRenderers
            : GetComponentsInChildren<Renderer>(true);

        string referenceName = substantaEprubetaMaterial != null ? substantaEprubetaMaterial.name : string.Empty;

        foreach (Renderer renderer in renderersToScan)
        {
            if (renderer == null)
            {
                continue;
            }

            Material[] rendererMaterials = renderer.materials;
            foreach (Material material in rendererMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                if (!IsMatchingSubstanceMaterial(material, referenceName))
                {
                    continue;
                }

                if (!runtimeSubstanceMaterials.Contains(material))
                {
                    runtimeSubstanceMaterials.Add(material);
                }
            }
        }
    }

    bool IsMatchingSubstanceMaterial(Material candidate, string referenceName)
    {
        if (substantaEprubetaMaterial != null && candidate == substantaEprubetaMaterial)
        {
            return true;
        }

        if (string.IsNullOrEmpty(referenceName))
        {
            return false;
        }

        return candidate.name.StartsWith(referenceName, StringComparison.OrdinalIgnoreCase);
    }

    void ApplySubstanceColor(Color color)
    {
        if (runtimeSubstanceMaterials.Count == 0)
        {
            CacheSubstanceMaterials();
        }

        foreach (Material material in runtimeSubstanceMaterials)
        {
            SetMaterialColor(material, color);
        }
    }

    Color GetCurrentSubstanceColor()
    {
        foreach (Material material in runtimeSubstanceMaterials)
        {
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }
        }

        return Color.white;
    }

    void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
    }

    void SetFumeActive(bool isActive)
    {
        if (fume != null)
        {
            fume.SetActive(isActive);
        }
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
        audioSource2Started = false;
        heatingProgress = 0.0f;
    }

}
