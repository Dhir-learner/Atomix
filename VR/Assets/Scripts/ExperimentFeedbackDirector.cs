using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes the moment an experiment resolves land.
///
/// Atomix decided success and failure well before this class existed - the free-hand engine
/// judged quantities, order and settling time, and wrote a careful chemistry-specific explanation
/// of what went wrong. What it did not do was <i>tell the player anything had happened</i> in the
/// half second the verdict arrived. The HUD strip changed one word from blue to green, and that
/// was the entire celebration for getting a titration right on the fifth attempt.
///
/// This is the missing half-second. On a verdict it fires, together:
///
/// <list type="bullet">
/// <item>a sting - the rising arpeggio for success, the soft falling pair for failure;</item>
/// <item>a camera shake, scaled to the outcome and to the player's own comfort setting;</item>
/// <item>a very short hit-stop, so the eye registers the change instead of sliding past it;</item>
/// <item>a coloured flash around the edge of the frame, which is the only one of the four that
/// works if the student happens to be looking at the far wall when the mixture settles.</item>
/// </list>
///
/// It also shakes the camera when a reaction actually explodes, which it detects by watching the
/// scene's own explosion particle systems rather than by editing eight reaction scripts to call
/// it. That keeps every reaction script untouched, which is the same reason the effects sweep in
/// <see cref="LabEffectsInitializer"/> enumerates them the way it does.
///
/// The flash colour comes from <see cref="AtomixSettings.SuccessColour"/> and
/// <see cref="AtomixSettings.FailureColour"/>, so the colour-blind-safe palette applies here as
/// well as to the panels - a red-green flash would otherwise be the one piece of result feedback
/// that ignored the accessibility setting.
/// </summary>
[DisallowMultipleComponent]
public class ExperimentFeedbackDirector : MonoBehaviour
{
    [Header("Verdict")]
    [Tooltip("Camera shake when an experiment succeeds.")]
    public float successShake = 0.45f;

    [Tooltip("Camera shake when an experiment fails. Slightly stronger - a failure should feel " +
             "like something went wrong, not like nothing did.")]
    public float failureShake = 0.85f;

    [Tooltip("Seconds of hit-stop on a verdict.")]
    public float verdictPunch = 0.07f;

    [Header("Flash")]
    [Tooltip("Seconds the edge flash takes to fade out.")]
    public float flashSeconds = 0.9f;

    [Tooltip("Peak opacity of the edge flash.")]
    public float flashStrength = 0.42f;

    [Header("Result card")]
    [Tooltip("Show the one-to-three star rating and accuracy when an experiment succeeds.")]
    public bool showStars = true;

    [Tooltip("Seconds the result card stays up, including its fade.")]
    public float starCardSeconds = 3.6f;

    [Header("Explosions")]
    [Tooltip("Camera shake when a reaction's explosion effect fires.")]
    public float explosionShake = 1.6f;

    [Tooltip("How often the scene is re-scanned for explosion effects, in seconds. The lab " +
             "reveals equipment one experiment at a time, so the set changes as you play.")]
    public float rescanSeconds = 4.0f;

    // --- flash -----------------------------------------------------------------------
    private Canvas flashCanvas;
    private Image flashImage;
    private Color flashColour;
    private float flashRemaining;

    // --- explosion watch ---------------------------------------------------------------
    private readonly List<ParticleSystem> explosions = new List<ParticleSystem>();
    private readonly HashSet<ParticleSystem> firing = new HashSet<ParticleSystem>();
    private float rescanTimer;

    void OnEnable()
    {
        ReactionHistoryRecorder.Resolved += HandleResolved;
        AchievementSystem.Unlocked += HandleAchievement;
    }

    void OnDisable()
    {
        ReactionHistoryRecorder.Resolved -= HandleResolved;
        AchievementSystem.Unlocked -= HandleAchievement;
    }

    // =========================================================
    // VERDICT
    // =========================================================

    private void HandleResolved(int reactionId, string reactionName, ExperimentOutcome outcome)
    {
        // Abandoned means the student walked away and picked something else. That is not a
        // result and should not be announced as one.
        if (outcome == ExperimentOutcome.Abandoned)
        {
            return;
        }

        bool success = outcome == ExperimentOutcome.Success;

        if (success)
        {
            AtomixAudio.Success();
            CameraJuice.Shake(successShake);
            Flash(AtomixSettings.SuccessColour);

            // Set by the recorder just before this event, so it is the attempt that just closed.
            ExperimentAttempt attempt = ReactionHistoryRecorder.LastResolvedAttempt;
            if (showStars && attempt != null && attempt.reactionId == reactionId)
            {
                float accuracy;
                int stars;
                if (ExperimentScoring.TryGetScore(attempt, out accuracy, out stars) && stars > 0)
                {
                    ShowStars(stars, accuracy, attempt.ModeLabel);
                }
            }
        }
        else
        {
            AtomixAudio.Failure();
            CameraJuice.Shake(failureShake);
            Flash(AtomixSettings.FailureColour);
        }

        CameraJuice.Punch(verdictPunch);
    }

    private void HandleAchievement(Achievement achievement)
    {
        // Deliberately late: the verdict sting that unlocked this is still ringing, and two
        // arpeggios on the same frame is a chord neither of them was written for.
        Invoke(nameof(PlayAchievementCue), 0.55f);
    }

    private void PlayAchievementCue()
    {
        AtomixAudio.Achievement();
    }

    // =========================================================
    // FLASH
    // =========================================================

    /// <summary>
    /// Colours the edge of the frame. Public so anything else that wants to signal a result -
    /// a timed-out exam task, say - reads the same way as an experiment verdict does.
    /// </summary>
    public void Flash(Color colour)
    {
        EnsureFlashBuilt();
        if (flashImage == null)
        {
            return;
        }

        flashColour = colour;
        flashRemaining = flashSeconds;
        flashImage.gameObject.SetActive(true);
    }

    void Update()
    {
        UpdateFlash();
        UpdateStarCard();
        UpdateExplosionWatch();
    }

    // =========================================================
    // STARS
    // =========================================================
    //
    // A pass used to be a pass: the titration that landed on the mark and the one that scraped in
    // at the edge of the tolerance got the same green word. The card says how good it was - one
    // to three stars from ExperimentScoring, the accuracy behind them, and the level it was
    // played at - and it pops the earned stars in one at a time, each with a rising chime.

    private RectTransform starCard;
    private CanvasGroup starCardGroup;
    private readonly Image[] starImages = new Image[ExperimentScoring.MaxStars];
    private TMP_Text starCaption;
    private float starCardElapsed = -1.0f;
    private int starsToShow;
    private int starsPopped;

    private const float FirstStarDelay = 0.35f;
    private const float StarInterval = 0.22f;
    private const float StarPopSeconds = 0.25f;

    private static readonly Color EarnedStarColour = new Color(1.0f, 0.84f, 0.30f, 1.0f);
    private static readonly Color EmptyStarColour = new Color(1.0f, 1.0f, 1.0f, 0.16f);

    /// <summary>Puts the result card up. Public so a future results screen can reuse it.</summary>
    public void ShowStars(int stars, float accuracyPercent, string modeLabel)
    {
        EnsureStarCardBuilt();
        if (starCard == null)
        {
            return;
        }

        starsToShow = Mathf.Clamp(stars, 0, ExperimentScoring.MaxStars);
        starsPopped = 0;
        starCardElapsed = 0.0f;

        for (int i = 0; i < starImages.Length; i++)
        {
            starImages[i].color = EmptyStarColour;
            starImages[i].rectTransform.localScale = Vector3.one;
        }

        starCaption.text = string.Format("Accuracy {0:0}%   -   {1}", Mathf.Max(0.0f, accuracyPercent), modeLabel);
        starCardGroup.alpha = 1.0f;
        starCard.gameObject.SetActive(true);
    }

    private void UpdateStarCard()
    {
        if (starCard == null || starCardElapsed < 0.0f)
        {
            return;
        }

        // Unscaled: the verdict's hit-stop slows time for a moment, and the card must not stall.
        starCardElapsed += Time.unscaledDeltaTime;

        for (int i = 0; i < starsToShow; i++)
        {
            float popStart = FirstStarDelay + i * StarInterval;
            float t = (starCardElapsed - popStart) / StarPopSeconds;
            if (t < 0.0f)
            {
                continue;
            }

            if (i >= starsPopped)
            {
                starsPopped = i + 1;
                starImages[i].color = EarnedStarColour;
                AtomixAudio.Play(AtomixAudio.Cue.Coin, 0.7f, 1.0f + 0.12f * i);
            }

            // Grow past full size to 1.3, then settle back to 1.
            float scale;
            if (t >= 1.0f)
            {
                scale = 1.0f;
            }
            else if (t < 0.6f)
            {
                scale = Mathf.Lerp(0.2f, 1.3f, t / 0.6f);
            }
            else
            {
                scale = Mathf.Lerp(1.3f, 1.0f, (t - 0.6f) / 0.4f);
            }
            starImages[i].rectTransform.localScale = Vector3.one * scale;
        }

        float fadeStart = Mathf.Max(0.1f, starCardSeconds - 0.6f);
        if (starCardElapsed > fadeStart)
        {
            starCardGroup.alpha = Mathf.Clamp01(1.0f - (starCardElapsed - fadeStart) / 0.6f);
        }

        if (starCardElapsed >= starCardSeconds)
        {
            starCardElapsed = -1.0f;
            starCard.gameObject.SetActive(false);
        }
    }

    private void EnsureStarCardBuilt()
    {
        if (starCard != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("AtomixResultCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 550;      // over the edge flash (540), under the HUD strip (560)

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);

        GameObject card = new GameObject("StarCard", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        card.transform.SetParent(canvasObject.transform, false);

        starCard = card.GetComponent<RectTransform>();
        starCard.anchorMin = new Vector2(0.5f, 1.0f);
        starCard.anchorMax = new Vector2(0.5f, 1.0f);
        starCard.pivot = new Vector2(0.5f, 1.0f);
        starCard.anchoredPosition = new Vector2(0.0f, -80.0f);   // just under the measurement strip
        starCard.sizeDelta = new Vector2(440.0f, 118.0f);

        Image plate = card.GetComponent<Image>();
        plate.color = new Color(0.04f, 0.05f, 0.08f, 0.86f);
        plate.raycastTarget = false;

        starCardGroup = card.GetComponent<CanvasGroup>();
        starCardGroup.interactable = false;
        starCardGroup.blocksRaycasts = false;

        Sprite starSprite = StarSprite.Get();
        const float starSize = 54.0f;
        const float spacing = 70.0f;
        for (int i = 0; i < starImages.Length; i++)
        {
            GameObject starObject = new GameObject("Star" + (i + 1), typeof(RectTransform), typeof(Image));
            starObject.transform.SetParent(card.transform, false);

            RectTransform rect = starObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1.0f);
            rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2((i - 1) * spacing, -40.0f);
            rect.sizeDelta = new Vector2(starSize, starSize);

            Image image = starObject.GetComponent<Image>();
            image.sprite = starSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            starImages[i] = image;
        }

        GameObject captionObject = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
        captionObject.transform.SetParent(card.transform, false);

        RectTransform captionRect = captionObject.GetComponent<RectTransform>();
        captionRect.anchorMin = new Vector2(0.0f, 0.0f);
        captionRect.anchorMax = new Vector2(1.0f, 0.0f);
        captionRect.pivot = new Vector2(0.5f, 0.0f);
        captionRect.anchoredPosition = new Vector2(0.0f, 10.0f);
        captionRect.sizeDelta = new Vector2(-24.0f, 32.0f);

        starCaption = captionObject.GetComponent<TextMeshProUGUI>();
        starCaption.fontSize = 22.0f;
        starCaption.alignment = TextAlignmentOptions.Center;
        starCaption.color = new Color(0.85f, 0.90f, 0.97f, 1.0f);
        starCaption.textWrappingMode = TextWrappingModes.NoWrap;
        starCaption.overflowMode = TextOverflowModes.Ellipsis;
        starCaption.raycastTarget = false;

        card.SetActive(false);
    }

    private void UpdateFlash()
    {
        if (flashRemaining <= 0.0f || flashImage == null)
        {
            return;
        }

        flashRemaining -= Time.unscaledDeltaTime;

        // Squared falloff: bright immediately, then out of the way quickly. A linear fade on a
        // full-frame overlay reads as the screen being dirty rather than as a flash.
        float t = Mathf.Clamp01(flashRemaining / Mathf.Max(0.01f, flashSeconds));
        Color colour = flashColour;
        colour.a = t * t * flashStrength;
        flashImage.color = colour;

        if (flashRemaining <= 0.0f)
        {
            flashImage.gameObject.SetActive(false);
        }
    }

    private void EnsureFlashBuilt()
    {
        if (flashCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("AtomixFlashCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);

        flashCanvas = canvasObject.GetComponent<Canvas>();
        flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Under the HUD (560) so a flash never washes out the measurement readout, and well
        // above the world-space panels so it is visible over an open graph.
        flashCanvas.sortingOrder = 540;

        GameObject imageObject = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        flashImage = imageObject.GetComponent<Image>();
        flashImage.sprite = BuildEdgeSprite();
        flashImage.type = Image.Type.Simple;
        flashImage.raycastTarget = false;      // must never eat a crosshair click
        flashImage.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);

        imageObject.SetActive(false);
    }

    /// <summary>
    /// A radial mask: transparent in the middle, opaque at the corners.
    ///
    /// Flashing the whole frame a flat colour would hide the very thing the student needs to
    /// look at - the vessel, the measurement label, the failure explanation. Confining the
    /// flash to the edge signals the result in peripheral vision and leaves the centre clear.
    /// </summary>
    private static Sprite BuildEdgeSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        float centre = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Normalised distance from the centre, 0 at the middle and 1 at an edge midpoint.
                float dx = (x - centre) / centre;
                float dy = (y - centre) / centre;
                float distance = Mathf.Sqrt(dx * dx + dy * dy) * 0.72f;

                // Nothing at all until 45% out, then a smooth ramp to full at the corner.
                float alpha = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(0.45f, 1.0f, distance));
                pixels[(y * size) + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);       // no mipmaps, then make it non-readable to free the copy

        return Sprite.Create(texture, new Rect(0.0f, 0.0f, size, size), new Vector2(0.5f, 0.5f));
    }

    // =========================================================
    // EXPLOSIONS
    // =========================================================

    /// <summary>
    /// Shakes the camera when a reaction's explosion effect starts.
    ///
    /// Watching the particle systems rather than adding a call to each reaction script is what
    /// keeps all eight of them untouched. The set is small - six systems across the two labs -
    /// and it is re-scanned occasionally because both labs reveal their equipment one experiment
    /// at a time, so an explosion effect may not exist yet when the scene loads.
    /// </summary>
    private void UpdateExplosionWatch()
    {
        rescanTimer -= Time.deltaTime;
        if (rescanTimer <= 0.0f)
        {
            rescanTimer = rescanSeconds;
            RescanExplosions();
        }

        for (int i = explosions.Count - 1; i >= 0; i--)
        {
            ParticleSystem effect = explosions[i];
            if (effect == null)
            {
                explosions.RemoveAt(i);
                continue;
            }

            bool emitting = effect.isEmitting;
            bool wasEmitting = firing.Contains(effect);

            if (emitting && !wasEmitting)
            {
                firing.Add(effect);

                // Scaled by how much is coming out: the sodium fizz and the aluminium-iodine
                // flash both run through here and should not feel the same.
                float rate = effect.emission.rateOverTime.constant;
                float weight = Mathf.Clamp(rate / 60.0f, 0.35f, 1.0f);

                CameraJuice.Shake(explosionShake * weight);
            }
            else if (!emitting && wasEmitting)
            {
                firing.Remove(effect);
            }
        }
    }

    private void RescanExplosions()
    {
        explosions.Clear();

        foreach (Reaction reaction in FindAll<Reaction>()) { Add(reaction.explosion); }
        foreach (Reaction_hcl_nahco3 reaction in FindAll<Reaction_hcl_nahco3>()) { Add(reaction.explosion); }
        foreach (KOHReaction reaction in FindAll<KOHReaction>()) { Add(reaction.explosion); }
        foreach (reactionCaOH reaction in FindAll<reactionCaOH>()) { Add(reaction.explosion); }
        foreach (ReactionAli3 reaction in FindAll<ReactionAli3>())
        {
            Add(reaction.explosion1);
            Add(reaction.explosion2);
            Add(reaction.explosion3);
        }

        // Anything destroyed with its scene must not be remembered as "still firing".
        firing.RemoveWhere(effect => effect == null || !explosions.Contains(effect));
    }

    private void Add(ParticleSystem effect)
    {
        if (effect != null && !explosions.Contains(effect))
        {
            explosions.Add(effect);
        }
    }

    private static T[] FindAll<T>() where T : Object
    {
        return FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
}
