using UnityEngine;

/// <summary>
/// Everything a player can ask the game to do, independent of how they asked.
/// </summary>
public enum AtomixAction
{
    Book,
    PeriodicTable,
    Graphs,
    History,
    CycleLabels,
    PauseMenu,
    Retry,
    RecentrePanel,

    AssistantToggle,
    AssistantType,
    AssistantWhy,
    PushToTalk,

    ExamHint,
    ExamExtraTime,
    ExamSkip,

    Drop,
    ResetHeldPose,
    TurnLeft,
    TurnRight,
    TiltLeft,
    TiltRight,

    FlyUp,
    FlyDown
}

/// <summary>
/// One place every gameplay script asks "was this pressed?", answered from the keyboard on the
/// desktop and from the on-screen controls on a phone.
///
/// The desktop build reads keys straight from <see cref="Input"/> in seventeen scripts, each with
/// its own inspector-configurable <see cref="KeyCode"/>. Replacing those with an input-actions
/// asset would touch every scene and every prefab that serialises a key. Instead each read becomes
/// <c>AtomixInput.GetDown(toggleKey, AtomixAction.Book)</c>: the key still works exactly as it
/// did, and <see cref="MobileControls"/> can press the same action from a touch button.
///
/// Virtual presses are published once per frame, the first time anything asks, so every script
/// sees a button tap for exactly one whole frame regardless of execution order.
/// </summary>
public static class AtomixInput
{
    private const int ActionCount = (int)AtomixAction.FlyDown + 1;

    private static readonly bool[] held = new bool[ActionCount];
    private static readonly bool[] pendingDown = new bool[ActionCount];
    private static readonly bool[] pendingUp = new bool[ActionCount];
    private static readonly int[] downFrame = new int[ActionCount];
    private static readonly int[] upFrame = new int[ActionCount];

    private static int syncedFrame = -1;
    private static Vector2 pendingLook;
    private static Vector2 lookThisFrame;
    private static bool pendingTap;
    private static Vector2 pendingTapPosition;
    private static bool tapThisFrame;
    private static Vector2 tapPosition;

    private static bool mobileResolved;
    private static bool isMobile;

    /// <summary>
    /// Whether gameplay owns the screen on a phone - the touch equivalent of a locked cursor.
    /// Kept separately because a phone has no cursor to lock, and reading
    /// <see cref="Cursor.lockState"/> back on Android does not reflect what the game asked for.
    /// </summary>
    public static bool MobileGameplayActive = true;

    /// <summary>Joystick position, each axis in -1..1. Zero on the desktop.</summary>
    public static Vector2 MoveAxis;

    /// <summary>Sprint, toggled from the on-screen button.</summary>
    public static bool SprintToggled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Static state survives between play sessions when domain reload is turned off.
        for (int i = 0; i < ActionCount; i++)
        {
            held[i] = false;
            pendingDown[i] = false;
            pendingUp[i] = false;
            downFrame[i] = -1;
            upFrame[i] = -1;
        }

        syncedFrame = -1;
        pendingLook = Vector2.zero;
        lookThisFrame = Vector2.zero;
        pendingTap = false;
        tapThisFrame = false;
        mobileResolved = false;
        MobileGameplayActive = true;
        MoveAxis = Vector2.zero;
        SprintToggled = false;
    }

    // =========================================================
    // PLATFORM
    // =========================================================

#if UNITY_EDITOR
    /// <summary>EditorPrefs key for the Tools menu's "Simulate Mobile Controls" switch.</summary>
    public const string SimulateMobilePrefKey = "Atomix.SimulateMobileControls";
#endif

    /// <summary>
    /// True on a phone or tablet - and in the Editor when the Device Simulator is showing one, or
    /// when Tools > Atomix > Simulate Mobile Controls is ticked.
    /// </summary>
    public static bool IsMobile
    {
        get
        {
            if (!mobileResolved)
            {
                mobileResolved = true;
                isMobile = Application.isMobilePlatform;
#if UNITY_EDITOR
                isMobile |= UnityEditor.EditorPrefs.GetBool(SimulateMobilePrefKey, false);
#endif
            }
            return isMobile;
        }
    }

    /// <summary>Picks the hint that matches the controls in the player's hands.</summary>
    public static string Hint(string desktop, string mobile)
    {
        return IsMobile ? mobile : desktop;
    }

    // =========================================================
    // QUERIES - keyboard OR on-screen control
    // =========================================================

    public static bool GetDown(KeyCode key, AtomixAction action)
    {
        return Input.GetKeyDown(key) || Down(action);
    }

    public static bool Get(KeyCode key, AtomixAction action)
    {
        return Input.GetKey(key) || Held(action);
    }

    public static bool GetUp(KeyCode key, AtomixAction action)
    {
        return Input.GetKeyUp(key) || Up(action);
    }

    public static bool Down(AtomixAction action)
    {
        Sync();
        return downFrame[(int)action] == Time.frameCount;
    }

    public static bool Held(AtomixAction action)
    {
        return held[(int)action];
    }

    public static bool Up(AtomixAction action)
    {
        Sync();
        return upFrame[(int)action] == Time.frameCount;
    }

    /// <summary>Look rotation requested by touch this frame, in degrees (x = yaw, y = pitch).</summary>
    public static Vector2 LookDelta
    {
        get
        {
            Sync();
            return lookThisFrame;
        }
    }

    /// <summary>
    /// A short tap on the 3D view this frame - not on a button or panel - in screen pixels.
    /// It is how a phone player picks things up: tap the beaker itself.
    /// </summary>
    public static bool TryGetTap(out Vector2 screenPosition)
    {
        Sync();
        screenPosition = tapPosition;
        return tapThisFrame;
    }

    // =========================================================
    // FEEDING - called by the on-screen controls
    // =========================================================

    /// <summary>A momentary press, like tapping a key.</summary>
    public static void Press(AtomixAction action)
    {
        pendingDown[(int)action] = true;
        pendingUp[(int)action] = true;
    }

    /// <summary>Press-and-hold, like holding a key down until <c>SetHeld(action, false)</c>.</summary>
    public static void SetHeld(AtomixAction action, bool isHeld)
    {
        int index = (int)action;
        if (held[index] == isHeld)
        {
            return;
        }

        held[index] = isHeld;
        if (isHeld)
        {
            pendingDown[index] = true;
        }
        else
        {
            pendingUp[index] = true;
        }
    }

    /// <summary>Lets go of every held control - used when the HUD hides mid-press.</summary>
    public static void ReleaseAll()
    {
        for (int i = 0; i < ActionCount; i++)
        {
            SetHeld((AtomixAction)i, false);
        }

        MoveAxis = Vector2.zero;
        pendingLook = Vector2.zero;
        pendingTap = false;
    }

    public static void AddLook(Vector2 degrees)
    {
        pendingLook += degrees;
    }

    public static void PushTap(Vector2 screenPosition)
    {
        pendingTap = true;
        pendingTapPosition = screenPosition;
    }

    /// <summary>
    /// Moves everything queued since the last frame into this frame. Runs on the first query of
    /// each frame, so it does not depend on which script happens to update first.
    /// </summary>
    private static void Sync()
    {
        int frame = Time.frameCount;
        if (frame == syncedFrame)
        {
            return;
        }

        syncedFrame = frame;

        for (int i = 0; i < ActionCount; i++)
        {
            if (pendingDown[i])
            {
                pendingDown[i] = false;
                downFrame[i] = frame;
            }

            // A tap is down and up in the same frame, which is exactly how a key tapped between
            // two frames reads too.
            if (pendingUp[i])
            {
                pendingUp[i] = false;
                upFrame[i] = frame;
            }
        }

        lookThisFrame = pendingLook;
        pendingLook = Vector2.zero;

        tapThisFrame = pendingTap;
        tapPosition = pendingTapPosition;
        pendingTap = false;
    }
}
