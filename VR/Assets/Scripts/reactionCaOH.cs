using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class reactionCaOH : MonoBehaviour
{
    [SerializeField] PourCuO salt;
    [SerializeField] PourSubstance h2o;
    public GameObject CaOHPivot;
    public GameObject TurnesolPivot;
    public GameObject wetLitmusPaper;
    public GameObject wetLitmusPaper1;
    public Material blueLitmusMaterial;

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
    public AudioClip clip_guidance3;

    [Header("Free-Hand Mode (quantity + order matter)")]
    public bool enableFreeHandMode = true;
    public float tooltipHeightOffset = 0.20f;
    [Tooltip("World-space font size for the floating tracker. TMP renders roughly (fontSize x 0.12) metres per line, so keep this small.")]
    public float tooltipFontSize = 0.55f;
    [Tooltip("Optional - played when the experiment fails.")]
    public AudioSource audioSource_failure;
    public AudioClip clip_failure;

    [Header("Recipe and History")]
    [Tooltip("Matches the book / StartReaction number, 1-8.")]
    public int reactionId = 6;
    [Tooltip("Optional. Left empty, Resources/ReactionDefinitions supplies the targets, tolerance and messages.")]
    public ReactionDefinition definition;
    [Tooltip("Re-selecting this experiment from the book logs a fresh attempt.")]
    public bool restartAttemptOnReSelect = true;

    private FreeHandReactionEngine engine;
    private FreeHandTooltip tooltip;
    private bool failureReported = false;
    private ReactionHistoryRecorder recorder;
    private float waterFlow;
    private float caoFlow;

    private DateTime timpInitial;
    private bool isPlaying = false;
    private bool explosionActive = false;
    private bool oneExplosion = false;
    private bool showPopup = false;
    private bool done = false;
    private bool audioSource1Started = false;
    private bool audioSource2Started = false;
    private bool audioSource3Started = false;

    void Start()
    {
        explosionGameObject.SetActive(false);
        explosion.Stop();
        explosion.Clear();

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
        waterFlow = definition.FlowFor("Water");
        caoFlow = definition.FlowFor("CaO");

        tooltip = new FreeHandTooltip();
        recorder = new ReactionHistoryRecorder(reactionId, definition.displayName, engine);
        LabRunOptions.Apply(reactionId, definition, engine, recorder);

        tooltip.Create("BeakerFloatingTooltip_CaOH", canvasText, tooltipFontSize);
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
        string trackerText = string.Empty;
        bool freeHandSuccess = false;

        if (enableFreeHandMode && engine != null)
        {
            if (!engine.IsResolved)
            {
                engine.UpdatePour("Water", waterFlow, h2o);
                engine.UpdatePour("CaO", caoFlow, salt);
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

        if (h2o.containsWater == true && salt.containsCuO == false)
        {
            bool quiet = enableFreeHandMode && engine != null && engine.IsAnyPouring;
            if (canvasText && !quiet)
            {
                canvasText.text = trackerText +
                    "Now you can add Calcium oxide. For this, grab the CaO beaker by pressing the grep button.";
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
            : (salt.containsCuO == true && h2o.containsWater == true);

        if (oneExplosion == false && canTriggerSuccess)
        {
            done = true;
            if (recorder != null)
            {
                recorder.Complete(ReactionResult.Success);
            }
            explosionActive = true;
            explosionGameObject.SetActive(true);
            showPopup = true;
            canvasText.text = "Chemical reaction equation: CaO + H2O = Ca(OH)2. Now you can check the basicity of the substance with red litmus paper.";
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
        if (Math.Abs(TurnesolPivot.transform.position.z - CaOHPivot.transform.position.z) < 0.05 &&
            Math.Abs(TurnesolPivot.transform.position.x - CaOHPivot.transform.position.x) < 0.05 &&
            Math.Abs(TurnesolPivot.transform.position.y - CaOHPivot.transform.position.y) < 0.01 &&
            done == true)
        {
            wetLitmusPaper.gameObject.GetComponent<Renderer>().material = blueLitmusMaterial;
            wetLitmusPaper1.gameObject.GetComponent<Renderer>().material = blueLitmusMaterial;
            popupWindow.SetActive(true);
            FirstPersonController.SetCursorLock(true);
            canvasText.text = "You have successfully checked the basicity of the substance. Now you can learn another reaction.";
            if (!audioSource3Started)
            {
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance3);
                audioSource3Started = true;
            }
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
        else if (h2o != null && h2o.SecondGlass != null)
        {
            anchor = h2o.SecondGlass.transform;
        }

        tooltip.UpdatePose(anchor, tooltipHeightOffset);
        tooltip.RenderEngineState(engine, "Reaction Success!\nCaO + H2O = Ca(OH)2");
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
        LabRunOptions.Apply(reactionId, definition, engine, recorder);
        failureReported = false;
        oneExplosion = false;
        explosionActive = false;
        showPopup = false;
        done = false;
        audioSource1Started = false;
        audioSource2Started = false;
        audioSource3Started = false;
    }

}
