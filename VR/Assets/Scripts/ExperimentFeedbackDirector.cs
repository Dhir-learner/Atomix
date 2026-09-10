using System.Collections.Generic;
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
        UpdateExplosionWatch();
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
