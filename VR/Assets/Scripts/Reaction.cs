using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;

public class Reaction : MonoBehaviour
{
    [SerializeField] PourMetalSubstance metal;
    [SerializeField] PourSubstance water;
    [SerializeField] PourPhenolphthalein phenolphthalein;
    public ParticleSystem explosion;
    public GameObject natriumMetal;
    public TMP_Text canvasText;
    public GameObject popupWindow;
    public AudioSource audioSource;
    public AudioClip clip;
    public AudioSource audioSource_guidance;
    public AudioClip clip_guidance1;
    public AudioClip clip_guidance2;
    public AudioClip clip_guidance3;

    [Header("Intelligent Proportions (Zero-Collision Math)")]
    public bool enableIntelligentMode = true;
    public float targetWaterMl = 50.0f;
    public float targetSodiumG = 5.0f;
    public float tolerance = 0.05f; // 5% deviation
    public float tooltipHeightOffset = 0.20f;
    [Tooltip("World-space font size for the floating tracker. TMP renders roughly (fontSize x 0.12) metres per line, so keep this small.")]
    public float tooltipFontSize = 0.55f;

    private float currentWaterMl = 0.0f;
    private float currentSodiumG = 0.0f;
    private bool reactionFailed = false;
    private float settleTimer = 0.0f;

    private FreeHandTooltip tooltip;

    private DateTime timpInitial;
    private bool explosionActive = false;
    private bool phenolphthaleinAdded = false;
    private bool oneExplosion = false;
    private bool showPopup = false;
    private bool audioSource1Started = false;
    private bool audioSource2Started = false;
    private bool audioSource3Started = false;

    void Start()
    {
        natriumMetal.SetActive(false);
        explosion.Stop();
        explosion.Clear();

        if (enableIntelligentMode && tooltip == null)
        {
            tooltip = new FreeHandTooltip();
            tooltip.Create("BeakerFloatingTooltip_Reaction", canvasText, tooltipFontSize);
            tooltip.Show(FreeHandTooltip.ProgressColor,
                $"Water: 0.0 / {targetWaterMl} ml\nSodium: 0.0 / {targetSodiumG} g");
        }
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
        if (metal.containsNatrium == true)
        {
            natriumMetal.SetActive(true);
            currentSodiumG = targetSodiumG; // Discrete solid block added successfully!
        }

        string trackerText = "";
        if (enableIntelligentMode && !oneExplosion && !reactionFailed)
        {
            bool isCurrentlyPouring = false;
            if (water != null && water.IsPouring)
            {
                currentWaterMl += 10.0f * Time.deltaTime; // Smooth 10 ml/sec pouring flow rate
                isCurrentlyPouring = true;
            }

            trackerText = $"[Lab Measurement Tracker]\nWater: {currentWaterMl:F1} ml / {targetWaterMl} ml (Target: ~50 ml)\nSodium: {currentSodiumG:F1} g / {targetSodiumG} g\n\n";
            if (isCurrentlyPouring && canvasText != null)
            {
                canvasText.text = trackerText + "Pouring water... Stop between 47.5 ml and 52.5 ml for correct chemical equilibrium!";
            }

            // Check Overdose during pouring
            float maxWater = targetWaterMl * (1.0f + tolerance);
            if (currentWaterMl > maxWater)
            {
                reactionFailed = true;
                showPopup = true;
                timpInitial = DateTime.Now;
                if (canvasText != null)
                {
                    canvasText.text = $"Experiment Failed! You poured too much water.\nAdded: {currentWaterMl:F1} ml (Expected ~{targetWaterMl} ml)\n\nExcessive water alters concentration and disrupts the controlled reaction! Ask your AI Assistant why excessive quantities cause failures.";
                }
                if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
                return;
            }

            // Settle timer check after pouring pauses
            if (currentWaterMl > 0f && currentSodiumG > 0f && !isCurrentlyPouring)
            {
                settleTimer += Time.deltaTime;
                if (settleTimer >= 1.5f && !oneExplosion)
                {
                    float minWater = targetWaterMl * (1.0f - tolerance);
                    if (currentWaterMl < minWater)
                    {
                        reactionFailed = true;
                        showPopup = true;
                        timpInitial = DateTime.Now;
                        if (canvasText != null)
                        {
                            canvasText.text = $"Experiment Failed! Incorrect proportions.\nWater added: {currentWaterMl:F1} ml (Expected ~{targetWaterMl} ml)\n\nInsufficient solvent prevents proper ion dissociation! Ask your AI Assistant why correct stoichiometric ratios are critical.";
                        }
                        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
                        return;
                    }
                }
            }
            else if (!isCurrentlyPouring)
            {
                settleTimer = 0f;
            }
        }

        // Update floating tooltip position above target beaker with billboard camera facing
        if (tooltip != null && tooltip.Exists)
        {
            Transform anchor = null;
            if (water != null && water.SecondGlass != null)
            {
                anchor = water.SecondGlass.transform;
            }
            else if (explosion != null)
            {
                anchor = explosion.transform;
            }

            tooltip.UpdatePose(anchor, tooltipHeightOffset);

            if (!oneExplosion && !reactionFailed)
            {
                tooltip.Show(FreeHandTooltip.ProgressColor,
                    $"Water: {currentWaterMl:F1} / {targetWaterMl} ml\nSodium: {currentSodiumG:F1} / {targetSodiumG} g");
            }
            else if (oneExplosion)
            {
                tooltip.Show(FreeHandTooltip.SuccessColor, "Reaction Success!\n2H2O + 2Na = 2NaOH + H2");
            }
            else if (reactionFailed)
            {
                if (currentWaterMl > targetWaterMl * (1.0f + tolerance))
                {
                    tooltip.Show(FreeHandTooltip.FailureColor,
                        $"FAILED: Overdose!\nWater: {currentWaterMl:F1} ml (Max {targetWaterMl * (1.0f + tolerance):F1} ml)");
                }
                else
                {
                    tooltip.Show(FreeHandTooltip.FailureColor,
                        $"FAILED: Incorrect Ratios\nWater: {currentWaterMl:F1} ml | Na: {currentSodiumG:F1} g");
                }
            }
        }

        if (water.containsWater == true && metal.containsNatrium == false)
        {
            if (canvasText && (!enableIntelligentMode || !water.IsPouring))
            {
                canvasText.text = trackerText + "Now you can add Sodium. For this, press twice on the lid with the grep button, and when the lid has disappeared you can grab the glass.";
            }
            if (!audioSource1Started)
            {
                if (audioSource_guidance != null && clip_guidance1 != null)
                {
                    audioSource_guidance.Stop();
                    audioSource_guidance.PlayOneShot(clip_guidance1);
                }
                audioSource1Started = true;
            }
        }

        bool canTriggerSuccess = false;
        if (!enableIntelligentMode)
        {
            canTriggerSuccess = (metal.containsNatrium == true && water.containsWater == true);
        }
        else if (!reactionFailed && currentWaterMl > 0f && currentSodiumG > 0f && settleTimer >= 1.5f)
        {
            canTriggerSuccess = true;
        }

        if (oneExplosion == false && canTriggerSuccess)
        {
            explosionActive = true;
            showPopup = true;
            if (canvasText)
            {
                canvasText.text = "Chemical reaction equation: 2H2O + 2Na = 2NaOH + H2. Now, you can highlight the basicity of the solution by adding phenolphthalein.";
            }
            oneExplosion = true;
            timpInitial = DateTime.Now;
            explosion.Clear();
            explosion.Play();
            audioSource.PlayOneShot(clip);
            if (!audioSource2Started)
            {
                if (audioSource_guidance != null && clip_guidance2 != null)
                {
                    audioSource_guidance.Stop();
                    audioSource_guidance.PlayOneShot(clip_guidance2);
                }
                audioSource2Started = true;
            }
        }
        if ((!enableIntelligentMode || oneExplosion) && phenolphthalein.containsPhenolphthalein == true && !phenolphthaleinAdded)
        {
            phenolphthaleinAdded = true;
            showPopup = true;
            if (canvasText)
            {
                canvasText.text = "You added PH indicator successfully! Now you can learn another reaction.";
            }
            if (!audioSource3Started)
            {
                if (audioSource_guidance != null && clip_guidance3 != null)
                {
                    audioSource_guidance.Stop();
                    audioSource_guidance.PlayOneShot(clip_guidance3);
                }
                audioSource3Started = true;
            }
            timpInitial = DateTime.Now;
            popupWindow.SetActive(true);
            FirstPersonController.SetCursorLock(true);
        }
        if (explosionActive && (DateTime.Now - timpInitial).TotalSeconds >= 6)
        {
            explosionActive = false;
            explosion.Stop();
            explosion.Clear();
            audioSource.Stop();
        }
        if (showPopup == true && (DateTime.Now - timpInitial).TotalSeconds >= 10)
        {
            showPopup = false;
            popupWindow.SetActive(false);
        }
    }
}


