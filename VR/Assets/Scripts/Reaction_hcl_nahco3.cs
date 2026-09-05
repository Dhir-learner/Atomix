using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;
//using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
//using static UnityEditor.Experimental.GraphView.GraphView;

public class Reaction_hcl_nahco3 : MonoBehaviour
{
    [SerializeField] PourNahco3 salt;
    [SerializeField] PourHCL hcl;
    public GameObject currentBerzelius;
    public Material material;
    public ParticleSystem explosion;
    public GameObject explosionGameObject;
    public TMP_Text canvasText;
    public GameObject popupWindow;
    public AudioSource audioSource;
    public AudioClip clip;
    public AudioSource audioSource_guidance;
    public AudioClip clip_guidance1;
    public AudioClip clip_guidance2;

    [Header("Free-Hand Mode (quantity + order matter)")]
    public bool enableFreeHandMode = true;
    public float targetHClMl = 15.0f;
    public float targetNaHCO3Grams = 12.0f;
    public float tolerancePercent = 10.0f;
    public float hclFlowMlPerSecond = 5.0f;
    public float nahco3FlowGramsPerSecond = 3.0f;
    public float tooltipHeightOffset = 0.20f;
    [Tooltip("World-space font size for the floating tracker. TMP renders roughly (fontSize x 0.12) metres per line, so keep this small.")]
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the experiment fails.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    private FreeHandReactionEngine engine;
    private FreeHandTooltip tooltip;
    private bool failureReported = false;

    private DateTime timpInitial;
    private bool isPlaying = false;
    private bool explosionActive = false;
    private bool oneExplosion = false;
    private bool showPopup = false;
    private bool audioSource1Started = false;
    private bool audioSource2Started = false;

    void Start()
    {
        explosionGameObject.SetActive(false);
        explosion.Stop();
        explosion.Clear();

        if (!enableFreeHandMode)
        {
            return;
        }

        engine = new FreeHandReactionEngine();
        engine.tolerancePercent = tolerancePercent;
        engine.settleTimeRequired = 1.5f;
        engine.wrongOrderMessage =
            "Sodium bicarbonate was tipped in before the acid, so the CO2 escaped from a dry powder instead of a controlled neutralisation.";
        engine.AddSubstance("HCl", targetHClMl, "ml",
            overdose: "Too much acid produces excessive CO2 gas violently - the froth overflows and the leftover HCl stays in the beaker.",
            underdose: "Too little acid leaves most of the bicarbonate unreacted, so effervescence stops almost immediately.");
        engine.AddSubstance("NaHCO3", targetNaHCO3Grams, "g",
            overdose: "Excess bicarbonate cannot react - only the HCl present can be neutralised, so solid NaHCO3 is left behind.",
            underdose: "Insufficient bicarbonate leaves an acidic solution, so the neutralisation to NaCl is incomplete.");

        tooltip = new FreeHandTooltip();
        tooltip.Create("BeakerFloatingTooltip_HCl_NaHCO3", canvasText, tooltipFontSize);
        tooltip.Show(FreeHandTooltip.ProgressColor, engine.GetTooltipText());
    }

    void OnEnable()
    {
        if (tooltip != null)
        {
            tooltip.SetActive(true);
        }
    }

    void OnDisable()
    {
        if (tooltip != null)
        {
            tooltip.SetActive(false);
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
                engine.UpdatePouringQuantity("HCl", hclFlowMlPerSecond, hcl != null && hcl.IsPouring);
                engine.UpdatePouringQuantity("NaHCO3", nahco3FlowGramsPerSecond, salt != null && salt.IsPouring);
            }

            ReactionResult result = engine.CheckReactionOutcome();
            trackerText = engine.GetTrackerText() + "\n\n";

            if (result == ReactionResult.Success)
            {
                freeHandSuccess = true;
            }
            else if (engine.HasFailed)
            {
                if (!failureReported)
                {
                    failureReported = true;
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

        if (hcl != null && hcl.containsHCL == true && salt != null && salt.containsNahco3 == false)
        {
            bool quiet = enableFreeHandMode && engine != null && engine.IsAnyPouring;
            if (canvasText && !quiet)
            {
                canvasText.text = trackerText +
                    "Now you can add Sodium bicarbonate. For this, grab the NaHCO3 beaker by pressing the grep button.";
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
            : (salt != null && salt.containsNahco3 == true && hcl != null && hcl.containsHCL == true);

        if (oneExplosion == false && canTriggerSuccess)
        {
            explosionActive = true;
            explosionGameObject.SetActive(true);
            showPopup = true;
            canvasText.text = "Chemical reaction equation: HCl + NaHCO3 = NaCl + H2O + CO2. Now you can learn another reaction.";
            if (!audioSource2Started)
            {
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance2);
                audioSource2Started = true;
            }
            popupWindow.SetActive(true);
            FirstPersonController.SetCursorLock(true);
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
        if (showPopup == true && (DateTime.Now - timpInitial).TotalSeconds >= 10)
        {
            showPopup = false;
            popupWindow.SetActive(false);
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
        else if (hcl != null && hcl.SecondGlass != null)
        {
            anchor = hcl.SecondGlass.transform;
        }

        tooltip.UpdatePose(anchor, tooltipHeightOffset);
        tooltip.RenderEngineState(engine, "Reaction Success!\nHCl + NaHCO3 = NaCl + H2O + CO2");
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
