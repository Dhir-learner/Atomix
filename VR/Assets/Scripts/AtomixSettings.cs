using System;
using UnityEngine;

/// <summary>
/// Every player-facing preference in one place, persisted to PlayerPrefs and applied live.
///
/// Atomix had no in-game settings at all: mouse sensitivity, field of view and volume were
/// compile-time constants on <see cref="FirstPersonController"/> and the scene's AudioSources.
/// That is a problem for a teaching tool, because the two things students most often need to
/// change - "the mouse is too fast" and "I cannot read that" - had no answer.
///
/// Nothing here owns any UI. <see cref="PauseMenuUI"/> presents these values; this class stores
/// them, clamps them, and pushes them into the scene whenever they change.
/// </summary>
public static class AtomixSettings
{
    private const string Prefix = "atomix.settings.";

    // --- backing values ------------------------------------------------------------
    private static float mouseSensitivity = 2.0f;
    private static bool invertLook = false;
    private static float moveSpeed = 2.2f;
    private static float fieldOfView = 60.0f;
    private static float masterVolume = 1.0f;
    private static float uiScale = 1.0f;
    private static bool highContrastUi = false;
    private static bool colourBlindSafe = false;
    private static bool showCrosshair = true;
    private static float musicVolume = 0.55f;
    private static float sfxVolume = 0.85f;

    // Off. The laboratory room tone was reported as sounding like a running tap - which it did,
    // because it was built on filtered noise, and filtered noise is what running water is. The
    // bed has since been rebuilt as a quiet electrical hum with no broadband content at all, but
    // it stays off unless somebody asks for it: a teaching tool should open in silence.
    private static float ambienceVolume = 0.0f;

    private static float screenShake = 1.0f;

    // Off. Head bob was reported as causing motion sickness, and there is no version of that
    // trade worth making in software a student may be required to use for an hour.
    private static float headBob = 0.0f;
    private static bool postFx = true;
    private static int qualityLevel = -1;          // -1 = "whatever the project shipped with"
    private static bool fullscreen = true;
    private static int frameCap = 0;               // 0 = uncapped, left to vsync
    private static bool loaded = false;

    /// <summary>
    /// Bumped whenever a shipped default changes in a way existing players must receive.
    ///
    /// Defaults alone are not enough. <see cref="Commit"/> writes <i>every</i> value the first
    /// time any single one is changed, so a player who has ever touched the pause menu has the
    /// old defaults on disk and would keep them forever. On a version bump the affected keys are
    /// deleted, so the new default is picked up, and everything the player actually chose for
    /// themselves is left alone.
    /// </summary>
    private const int CurrentSettingsVersion = 2;

    /// <summary>Raised whenever any setting changes, so open panels can restyle themselves.</summary>
    public static event Action Changed;

    // --- limits, also used by the sliders in the pause menu -------------------------
    public const float MinSensitivity = 0.25f;
    public const float MaxSensitivity = 8.0f;
    public const float MinMoveSpeed = 0.5f;
    public const float MaxMoveSpeed = 8.0f;
    public const float MinFov = 45.0f;
    public const float MaxFov = 100.0f;
    public const float MinUiScale = 0.7f;
    public const float MaxUiScale = 1.8f;

    /// <summary>Frame caps offered by the pause menu. 0 means "do not cap".</summary>
    public static readonly int[] FrameCapChoices = { 0, 30, 60, 90, 120, 144 };

    // --- properties -----------------------------------------------------------------

    public static float MouseSensitivity
    {
        get { EnsureLoaded(); return mouseSensitivity; }
        set { EnsureLoaded(); mouseSensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity); Commit(); }
    }

    public static bool InvertLook
    {
        get { EnsureLoaded(); return invertLook; }
        set { EnsureLoaded(); invertLook = value; Commit(); }
    }

    public static float MoveSpeed
    {
        get { EnsureLoaded(); return moveSpeed; }
        set { EnsureLoaded(); moveSpeed = Mathf.Clamp(value, MinMoveSpeed, MaxMoveSpeed); Commit(); }
    }

    public static float FieldOfView
    {
        get { EnsureLoaded(); return fieldOfView; }
        set { EnsureLoaded(); fieldOfView = Mathf.Clamp(value, MinFov, MaxFov); Commit(); }
    }

    public static float MasterVolume
    {
        get { EnsureLoaded(); return masterVolume; }
        set { EnsureLoaded(); masterVolume = Mathf.Clamp01(value); Commit(); }
    }

    public static float UiScale
    {
        get { EnsureLoaded(); return uiScale; }
        set { EnsureLoaded(); uiScale = Mathf.Clamp(value, MinUiScale, MaxUiScale); Commit(); }
    }

    /// <summary>Darker plates and brighter text, for projectors and low-contrast screens.</summary>
    public static bool HighContrastUi
    {
        get { EnsureLoaded(); return highContrastUi; }
        set { EnsureLoaded(); highContrastUi = value; Commit(); }
    }

    /// <summary>
    /// Swaps the success/failure red-green pair for a blue-orange pair, which stays
    /// distinguishable under all three common forms of colour blindness.
    /// </summary>
    public static bool ColourBlindSafe
    {
        get { EnsureLoaded(); return colourBlindSafe; }
        set { EnsureLoaded(); colourBlindSafe = value; Commit(); }
    }

    public static bool ShowCrosshair
    {
        get { EnsureLoaded(); return showCrosshair; }
        set { EnsureLoaded(); showCrosshair = value; Commit(); }
    }

    // --- audio buses ----------------------------------------------------------------
    //
    // MasterVolume stays exactly what it always was - AudioListener.volume, the one knob over
    // everything. These three sit underneath it, so a student can silence the room tone without
    // also silencing the narration that explains the experiment.

    /// <summary>Menu and learning-screen music bed.</summary>
    public static float MusicVolume
    {
        get { EnsureLoaded(); return musicVolume; }
        set { EnsureLoaded(); musicVolume = Mathf.Clamp01(value); Commit(); }
    }

    /// <summary>Interaction feedback: grabbing, clicks, success and failure stings.</summary>
    public static float SfxVolume
    {
        get { EnsureLoaded(); return sfxVolume; }
        set { EnsureLoaded(); sfxVolume = Mathf.Clamp01(value); Commit(); }
    }

    /// <summary>The laboratory room tone. Separate because it is the first thing anyone turns off.</summary>
    public static float AmbienceVolume
    {
        get { EnsureLoaded(); return ambienceVolume; }
        set { EnsureLoaded(); ambienceVolume = Mathf.Clamp01(value); Commit(); }
    }

    // --- motion and comfort ----------------------------------------------------------

    /// <summary>
    /// Scales every camera shake. 0 disables them outright - motion sensitivity is common enough
    /// in a classroom that a shake the student cannot switch off is a shake that should not ship.
    /// </summary>
    public static float ScreenShake
    {
        get { EnsureLoaded(); return screenShake; }
        set { EnsureLoaded(); screenShake = Mathf.Clamp01(value); Commit(); }
    }

    /// <summary>
    /// Scales every effect that moves the view while walking - the head bob and the field-of-view
    /// kick while sprinting. <b>Ships at 0</b>, which holds the camera perfectly steady.
    ///
    /// Both are grouped under one control on purpose. A player who turns the bob off because it
    /// makes them ill is not helped by a separate lens zoom that does the same thing, and being
    /// asked to find two switches to stop feeling sick is a bad answer.
    /// </summary>
    public static float HeadBob
    {
        get { EnsureLoaded(); return headBob; }
        set { EnsureLoaded(); headBob = Mathf.Clamp01(value); Commit(); }
    }

    /// <summary>Bloom, colour grading, vignette and ambient occlusion. Off is the cheap setting.</summary>
    public static bool PostFx
    {
        get { EnsureLoaded(); return postFx; }
        set { EnsureLoaded(); postFx = value; Commit(); }
    }

    // --- display ---------------------------------------------------------------------

    /// <summary>Index into <see cref="QualitySettings.names"/>; -1 leaves the project default.</summary>
    public static int QualityLevel
    {
        get { EnsureLoaded(); return qualityLevel; }
        set { EnsureLoaded(); qualityLevel = value; Commit(); }
    }

    public static bool Fullscreen
    {
        get { EnsureLoaded(); return fullscreen; }
        set { EnsureLoaded(); fullscreen = value; Commit(); }
    }

    /// <summary>Target frame rate; 0 leaves it to vsync.</summary>
    public static int FrameCap
    {
        get { EnsureLoaded(); return frameCap; }
        set { EnsureLoaded(); frameCap = Mathf.Max(0, value); Commit(); }
    }

    // --- palette -------------------------------------------------------------------

    /// <summary>Green normally; blue when the colour-blind-safe palette is on.</summary>
    public static Color SuccessColour
    {
        get
        {
            EnsureLoaded();
            return colourBlindSafe
                ? new Color(0.36f, 0.68f, 1.0f, 1.0f)      // clear blue
                : new Color(0.45f, 0.92f, 0.55f, 1.0f);
        }
    }

    /// <summary>Red normally; orange when the colour-blind-safe palette is on.</summary>
    public static Color FailureColour
    {
        get
        {
            EnsureLoaded();
            return colourBlindSafe
                ? new Color(1.0f, 0.60f, 0.15f, 1.0f)      // strong orange
                : new Color(1.0f, 0.51f, 0.42f, 1.0f);
        }
    }

    public static Color PanelColour
    {
        get
        {
            EnsureLoaded();
            return highContrastUi
                ? new Color(0.02f, 0.02f, 0.03f, 0.99f)
                : new Color(0.06f, 0.07f, 0.10f, 0.96f);
        }
    }

    public static Color BodyTextColour
    {
        get
        {
            EnsureLoaded();
            return highContrastUi ? Color.white : new Color(0.86f, 0.89f, 0.94f, 1.0f);
        }
    }

    // --- persistence ---------------------------------------------------------------

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;   // set first: the getters below would otherwise recurse
        Migrate();
        mouseSensitivity = PlayerPrefs.GetFloat(Prefix + "sensitivity", mouseSensitivity);
        invertLook = PlayerPrefs.GetInt(Prefix + "invert", 0) == 1;
        moveSpeed = PlayerPrefs.GetFloat(Prefix + "movespeed", moveSpeed);
        fieldOfView = PlayerPrefs.GetFloat(Prefix + "fov", fieldOfView);
        masterVolume = PlayerPrefs.GetFloat(Prefix + "volume", masterVolume);
        uiScale = PlayerPrefs.GetFloat(Prefix + "uiscale", uiScale);
        highContrastUi = PlayerPrefs.GetInt(Prefix + "contrast", 0) == 1;
        colourBlindSafe = PlayerPrefs.GetInt(Prefix + "cbsafe", 0) == 1;
        showCrosshair = PlayerPrefs.GetInt(Prefix + "crosshair", 1) == 1;
        musicVolume = PlayerPrefs.GetFloat(Prefix + "music", musicVolume);
        sfxVolume = PlayerPrefs.GetFloat(Prefix + "sfx", sfxVolume);
        ambienceVolume = PlayerPrefs.GetFloat(Prefix + "ambience", ambienceVolume);
        screenShake = PlayerPrefs.GetFloat(Prefix + "shake", screenShake);
        headBob = PlayerPrefs.GetFloat(Prefix + "headbob", headBob);
        postFx = PlayerPrefs.GetInt(Prefix + "postfx", 1) == 1;
        qualityLevel = PlayerPrefs.GetInt(Prefix + "quality", -1);
        fullscreen = PlayerPrefs.GetInt(Prefix + "fullscreen", 1) == 1;
        frameCap = PlayerPrefs.GetInt(Prefix + "framecap", 0);

        // A file written by an older build could hold anything.
        mouseSensitivity = Mathf.Clamp(mouseSensitivity, MinSensitivity, MaxSensitivity);
        moveSpeed = Mathf.Clamp(moveSpeed, MinMoveSpeed, MaxMoveSpeed);
        fieldOfView = Mathf.Clamp(fieldOfView, MinFov, MaxFov);
        uiScale = Mathf.Clamp(uiScale, MinUiScale, MaxUiScale);
        masterVolume = Mathf.Clamp01(masterVolume);
        musicVolume = Mathf.Clamp01(musicVolume);
        sfxVolume = Mathf.Clamp01(sfxVolume);
        ambienceVolume = Mathf.Clamp01(ambienceVolume);
        screenShake = Mathf.Clamp01(screenShake);
        headBob = Mathf.Clamp01(headBob);
        frameCap = Mathf.Max(0, frameCap);

        // A quality index saved by a build with more levels than this one would throw.
        if (qualityLevel >= QualitySettings.names.Length)
        {
            qualityLevel = -1;
        }
    }

    /// <summary>
    /// Brings a settings file written by an older build up to date. Only ever removes keys whose
    /// shipped default has changed; a value the player set deliberately in some other row is
    /// never touched.
    /// </summary>
    private static void Migrate()
    {
        int stored = PlayerPrefs.GetInt(Prefix + "version", 1);
        if (stored >= CurrentSettingsVersion)
        {
            return;
        }

        if (stored < 2)
        {
            // v1 shipped the room tone on and the head bob on. Both were wrong.
            PlayerPrefs.DeleteKey(Prefix + "ambience");
            PlayerPrefs.DeleteKey(Prefix + "headbob");
        }

        PlayerPrefs.SetInt(Prefix + "version", CurrentSettingsVersion);
        PlayerPrefs.Save();
    }

    private static void Commit()
    {
        PlayerPrefs.SetFloat(Prefix + "sensitivity", mouseSensitivity);
        PlayerPrefs.SetInt(Prefix + "invert", invertLook ? 1 : 0);
        PlayerPrefs.SetFloat(Prefix + "movespeed", moveSpeed);
        PlayerPrefs.SetFloat(Prefix + "fov", fieldOfView);
        PlayerPrefs.SetFloat(Prefix + "volume", masterVolume);
        PlayerPrefs.SetFloat(Prefix + "uiscale", uiScale);
        PlayerPrefs.SetInt(Prefix + "contrast", highContrastUi ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "cbsafe", colourBlindSafe ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "crosshair", showCrosshair ? 1 : 0);
        PlayerPrefs.SetFloat(Prefix + "music", musicVolume);
        PlayerPrefs.SetFloat(Prefix + "sfx", sfxVolume);
        PlayerPrefs.SetFloat(Prefix + "ambience", ambienceVolume);
        PlayerPrefs.SetFloat(Prefix + "shake", screenShake);
        PlayerPrefs.SetFloat(Prefix + "headbob", headBob);
        PlayerPrefs.SetInt(Prefix + "postfx", postFx ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "quality", qualityLevel);
        PlayerPrefs.SetInt(Prefix + "fullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "framecap", frameCap);
        PlayerPrefs.SetInt(Prefix + "version", CurrentSettingsVersion);
        PlayerPrefs.Save();

        Apply();

        if (Changed != null)
        {
            Changed();
        }
    }

    /// <summary>Restores every value to the shipped default.</summary>
    public static void ResetToDefaults()
    {
        loaded = true;
        mouseSensitivity = 2.0f;
        invertLook = false;
        moveSpeed = 2.2f;
        fieldOfView = 60.0f;
        masterVolume = 1.0f;
        uiScale = 1.0f;
        highContrastUi = false;
        colourBlindSafe = false;
        showCrosshair = true;
        musicVolume = 0.55f;
        sfxVolume = 0.85f;
        ambienceVolume = 0.0f;
        screenShake = 1.0f;
        headBob = 0.0f;
        postFx = true;
        qualityLevel = -1;
        fullscreen = true;
        frameCap = 0;
        Commit();
    }

    // --- application ---------------------------------------------------------------

    /// <summary>
    /// Pushes the current values into the live scene. Safe to call every scene load and after
    /// every change; everything it touches is null-guarded because the lab scenes build their
    /// desktop rig at runtime and it may not exist yet.
    /// </summary>
    public static void Apply()
    {
        EnsureLoaded();

        AudioListener.volume = masterVolume;

        FirstPersonController controller =
            UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Exclude);
        if (controller != null)
        {
            controller.mouseSensitivity = mouseSensitivity;
            controller.moveSpeed = moveSpeed;
            controller.invertLook = invertLook;
        }

        Camera camera = Camera.main;
        if (camera != null && !camera.orthographic)
        {
            // CameraJuice owns the live field of view while it is running a sprint kick, so
            // stamping it here would fight the animation. It reads BaseFieldOfView instead.
            if (CameraJuice.Active == null)
            {
                camera.fieldOfView = fieldOfView;
            }
        }

        // QualitySettings.names allocates a new string[] on every read, and Apply runs every
        // frame for two seconds after each scene load, so the cheap comparison goes first.
        if (qualityLevel >= 0 && QualitySettings.GetQualityLevel() != qualityLevel &&
            qualityLevel < QualitySettings.names.Length)
        {
            // false: do not expensively re-import textures mid-session for a menu nudge.
            QualitySettings.SetQualityLevel(qualityLevel, false);
        }

        if (Application.targetFrameRate != (frameCap > 0 ? frameCap : -1))
        {
            Application.targetFrameRate = frameCap > 0 ? frameCap : -1;
        }

#if !UNITY_EDITOR
        // Screen.fullScreen would throw away the editor's Game view layout, so only a real
        // player build asks for it.
        if (Screen.fullScreen != fullscreen)
        {
            Screen.fullScreen = fullscreen;
        }
#endif
    }

    /// <summary>
    /// The player's chosen field of view, before any transient effect.
    /// <see cref="CameraJuice"/> animates around this value rather than replacing it.
    /// </summary>
    public static float BaseFieldOfView { get { EnsureLoaded(); return fieldOfView; } }
}
