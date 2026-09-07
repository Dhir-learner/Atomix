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
    private static bool loaded = false;

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
        mouseSensitivity = PlayerPrefs.GetFloat(Prefix + "sensitivity", mouseSensitivity);
        invertLook = PlayerPrefs.GetInt(Prefix + "invert", 0) == 1;
        moveSpeed = PlayerPrefs.GetFloat(Prefix + "movespeed", moveSpeed);
        fieldOfView = PlayerPrefs.GetFloat(Prefix + "fov", fieldOfView);
        masterVolume = PlayerPrefs.GetFloat(Prefix + "volume", masterVolume);
        uiScale = PlayerPrefs.GetFloat(Prefix + "uiscale", uiScale);
        highContrastUi = PlayerPrefs.GetInt(Prefix + "contrast", 0) == 1;
        colourBlindSafe = PlayerPrefs.GetInt(Prefix + "cbsafe", 0) == 1;
        showCrosshair = PlayerPrefs.GetInt(Prefix + "crosshair", 1) == 1;

        // A file written by an older build could hold anything.
        mouseSensitivity = Mathf.Clamp(mouseSensitivity, MinSensitivity, MaxSensitivity);
        moveSpeed = Mathf.Clamp(moveSpeed, MinMoveSpeed, MaxMoveSpeed);
        fieldOfView = Mathf.Clamp(fieldOfView, MinFov, MaxFov);
        uiScale = Mathf.Clamp(uiScale, MinUiScale, MaxUiScale);
        masterVolume = Mathf.Clamp01(masterVolume);
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
            camera.fieldOfView = fieldOfView;
        }
    }
}
