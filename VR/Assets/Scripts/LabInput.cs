using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every key, mouse button, axis and scroll read in Atomix goes through here instead of
/// <see cref="Input"/>. On desktop it returns exactly what <see cref="Input"/> would. On-screen
/// touch controls call the Virtual* methods to press the same keys the scripts already listen
/// for, so no gameplay script needs a separate touch path.
/// </summary>
public static class LabInput
{
    /// <summary>Axis name the virtual scroll uses for <see cref="MouseScrollDelta"/>.</summary>
    public const string ScrollDeltaAxis = "LabInput Scroll Delta";

    /// <summary>True on phones and tablets, where on-screen touch controls replace keyboard and mouse.</summary>
    public static bool IsTouch
    {
        get
        {
#if UNITY_ANDROID || UNITY_IOS
            return true;
#else
            return false;
#endif
        }
    }

    // Virtual input is reported on the frame AFTER it happens. uGUI dispatches touches from the
    // EventSystem's Update, which can run before or after the script reading the key; delaying by
    // one frame gives every reader the same answer regardless of script execution order.
    private static readonly Dictionary<KeyCode, int> pressedFrame = new Dictionary<KeyCode, int>();
    private static readonly Dictionary<KeyCode, int> releasedFrame = new Dictionary<KeyCode, int>();
    private static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
    private static readonly Dictionary<string, AxisSlot> axes = new Dictionary<string, AxisSlot>();

    private class AxisSlot
    {
        public int frame = -1;
        public float current;
        public float previous;
    }

    /// <summary>Picks on-screen wording for the current device: key names on desktop, button names on touch.</summary>
    public static string Prompt(string keyboardText, string touchText)
    {
        return IsTouch ? touchText : keyboardText;
    }

    // ------------------------------------------------------------------ reading

    public static bool GetKey(KeyCode key)
    {
        return Input.GetKey(key) || held.Contains(key);
    }

    public static bool GetKeyDown(KeyCode key)
    {
        return Input.GetKeyDown(key) || HappenedLastFrame(pressedFrame, key);
    }

    public static bool GetKeyUp(KeyCode key)
    {
        return Input.GetKeyUp(key) || HappenedLastFrame(releasedFrame, key);
    }

    public static bool GetMouseButton(int button)
    {
        return Input.GetMouseButton(button) || held.Contains(MouseKey(button));
    }

    public static bool GetMouseButtonDown(int button)
    {
        return Input.GetMouseButtonDown(button) || HappenedLastFrame(pressedFrame, MouseKey(button));
    }

    public static float GetAxis(string axisName)
    {
        return Input.GetAxis(axisName) + ReadVirtualAxis(axisName);
    }

    public static Vector2 MouseScrollDelta
    {
        get { return Input.mouseScrollDelta + new Vector2(0.0f, ReadVirtualAxis(ScrollDeltaAxis)); }
    }

    public static string InputString
    {
        get { return Input.inputString; }
    }

    // ------------------------------------------------------------------ virtual input

    /// <summary>Starts holding a key, as if it were pressed down.</summary>
    public static void VirtualPress(KeyCode key)
    {
        if (held.Add(key))
        {
            pressedFrame[key] = Time.frameCount;
        }
    }

    /// <summary>Stops holding a key started with <see cref="VirtualPress"/>.</summary>
    public static void VirtualRelease(KeyCode key)
    {
        if (held.Remove(key))
        {
            releasedFrame[key] = Time.frameCount;
        }
    }

    /// <summary>A single press and release: GetKeyDown fires once, GetKey never stays true.</summary>
    public static void VirtualTap(KeyCode key)
    {
        pressedFrame[key] = Time.frameCount;
        releasedFrame[key] = Time.frameCount;
    }

    /// <summary>Adds to an axis (for example "Mouse X") for the next frame.</summary>
    public static void AddVirtualAxis(string axisName, float delta)
    {
        Roll(GetSlot(axisName)).current += delta;
    }

    /// <summary>Releases everything held - call when touch controls hide or the app pauses.</summary>
    public static void ReleaseAllVirtual()
    {
        foreach (KeyCode key in new List<KeyCode>(held))
        {
            VirtualRelease(key);
        }
    }

    // ------------------------------------------------------------------ helpers

    private static KeyCode MouseKey(int button)
    {
        return KeyCode.Mouse0 + Mathf.Clamp(button, 0, 6);
    }

    private static bool HappenedLastFrame(Dictionary<KeyCode, int> frames, KeyCode key)
    {
        int frame;
        return frames.TryGetValue(key, out frame) && frame == Time.frameCount - 1;
    }

    private static AxisSlot GetSlot(string axisName)
    {
        AxisSlot slot;
        if (!axes.TryGetValue(axisName, out slot))
        {
            slot = new AxisSlot();
            axes[axisName] = slot;
        }
        return slot;
    }

    private static AxisSlot Roll(AxisSlot slot)
    {
        if (slot.frame != Time.frameCount)
        {
            slot.previous = slot.frame == Time.frameCount - 1 ? slot.current : 0.0f;
            slot.current = 0.0f;
            slot.frame = Time.frameCount;
        }
        return slot;
    }

    private static float ReadVirtualAxis(string axisName)
    {
        AxisSlot slot;
        return axes.TryGetValue(axisName, out slot) ? Roll(slot).previous : 0.0f;
    }
}
