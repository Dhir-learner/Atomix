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
    }

    void Update()
    {
        if (foc != null && foc.esteAprins && !audioSource1Started)
        {
            canvasText.text = "You have lit the Bunsen burner successfully! Now you can hold the FeSO4 test tube over the flame and observe the color change.";
            audioSource_guidance.Stop();
            audioSource_guidance.PlayOneShot(clip_guidance1);
            audioSource1Started = true;
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

        reactionCompleted = true;
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
}
