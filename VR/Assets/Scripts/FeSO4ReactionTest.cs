using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FeSO4ReactionTest : MonoBehaviour
{
    [SerializeField] CountdownTimer countdown;
    [SerializeField] Randomize randomizer;
    public TMP_Text canvasText;
    private bool finishedTask = false;
    private DateTime timpInitial;

    public Material substantaEprubetaMaterial;
    [Tooltip("Optional explicit renderers that display the FeSO4 liquid color. If empty, renderers are auto-detected.")]
    [SerializeField] Renderer[] substanceRenderers;
    public GameObject pivotEprubeta;
    public GameObject pivotFoc;
    [SerializeField] LightFire foc;
    public GameObject fume;

    public GameObject label;
    public Material Fe2O3_material;

    public Color[] colors;
    [Tooltip("How close the FeSO4 pivot must be to the flame pivot to start heating.")]
    public float heatingDistance = 0.22f;
    [Tooltip("How many seconds of heating are required for full color change.")]
    public float heatingDuration = 10f;

    private float heatingProgress = 0f;
    public float time;
    private readonly List<Material> runtimeSubstanceMaterials = new List<Material>();

    [Header("Exam Mode (same rules as the Lab, quantities hidden)")]
    [Tooltip("Judged by the same FreeHandReactionEngine as Feso4Reaction, but the student is never told how long to heat for.")]
    public bool enableExamMode = true;
    public float tooltipHeightOffset = 0.22f;
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the task is failed.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Recipe")]
    [Tooltip("Matches the Lab reaction number so test attempts group with lab attempts.")]
    public int reactionId = 8;
    [Tooltip("Optional. Left empty, Resources/ReactionDefinitions is used - the same file the Lab reads.")]
    public ReactionDefinition definition;

    private const string TaskPrompt = "Decompose FeSO4 and observe the color change.";

    private ExamReactionRunner exam;
    private bool reactionCompleted = false;

    void Start()
    {
        CacheSubstanceMaterials();

        if (colors != null && colors.Length > 0)
        {
            ApplySubstanceColor(colors[0]);
        }

        time = 0;
        SetFumeActive(false);

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
        exam.Begin(definition, "ExamTooltip_FeSO4", canvasText, tooltipFontSize, countdown, randomizer);
        exam.SetFailureAudio(audioSource_failure, clip_failure);
    }

    void OnEnable()
    {
        if (exam != null)
        {
            exam.SetTooltipActive(true);
            exam.ResetForNewTask();
        }
        reactionCompleted = false;
        finishedTask = false;
        heatingProgress = 0.0f;
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

            if (!finishedTask && overFlame)
            {
                AdvanceHeating();
            }
            else
            {
                SetFumeActive(false);
            }
        }

        if (finishedTask && !randomizer.generateNewReaction && (DateTime.Now - timpInitial).TotalSeconds >= 5)
        {
            randomizer.generateNewReaction = true;
        }
    }

    /// <summary>
    /// Free-hand heating: the student decides how long to hold the tube in the flame and is not
    /// told the right duration. The colour gradient is driven by the measured heating time, so it
    /// stays visually correct. Same rules as the Lab's Feso4Reaction.
    /// </summary>
    void UpdateExamHeating(bool overFlame)
    {
        if (exam.HasFailed)
        {
            SetFumeActive(false);
            exam.Tick();
            exam.UpdateTooltip(TooltipAnchor(), tooltipHeightOffset, "Task finished!");
            return;
        }

        exam.Pour("Heating", definition.FlowFor("Heating"), overFlame);
        ReactionResult result = exam.Tick();
        SetFumeActive(overFlame && !exam.IsResolved);

        // The colour follows the heating the engine is judging, so the two always agree.
        float duration = Mathf.Max(0.1f, definition.TargetFor("Heating"));
        heatingProgress = Mathf.Clamp01(exam.engine.GetCurrent("Heating") / duration);
        ApplySubstanceColor(EvaluateGradientColor(heatingProgress));

        if (result == ReactionResult.Success && !reactionCompleted)
        {
            reactionCompleted = true;
            exam.CompleteSuccess();
            CompleteTask();
        }
        else if (!finishedTask && canvasText != null && !exam.IsResolved)
        {
            canvasText.text = exam.Status(TaskPrompt);
        }

        exam.UpdateTooltip(TooltipAnchor(), tooltipHeightOffset, "Task finished!");
    }

    Transform TooltipAnchor()
    {
        return pivotEprubeta != null ? pivotEprubeta.transform : transform;
    }

    void CompleteTask()
    {
        Renderer labelRenderer = label != null ? label.GetComponent<Renderer>() : null;
        if (labelRenderer != null && Fe2O3_material != null)
        {
            labelRenderer.material = Fe2O3_material;
        }

        timpInitial = DateTime.Now;
        SetFumeActive(false);
        canvasText.text = "Task finished!";
        if (countdown != null)
        {
            countdown.continua = false;
        }

        finishedTask = true;
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
        SetFumeActive(true);

        float duration = Mathf.Max(0.1f, heatingDuration);
        heatingProgress = Mathf.Clamp01(heatingProgress + (Time.deltaTime / duration));

        ApplySubstanceColor(EvaluateGradientColor(heatingProgress));

        if (heatingProgress < 1f)
        {
            return;
        }

        reactionCompleted = true;
        CompleteTask();
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
}
