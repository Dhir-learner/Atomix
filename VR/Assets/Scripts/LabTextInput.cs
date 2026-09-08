using UnityEngine;

/// <summary>
/// One global "the student is typing" flag, plus the keystroke capture that goes with it.
///
/// Why this has to exist at all: the lab keeps the OS cursor locked so the crosshair works, which
/// rules out a normal uGUI InputField - those need a pointer click to focus. So a typed question
/// has to be read straight from <see cref="Input.inputString"/>. But the lab binds nearly every
/// letter to something: W A S D move, E interacts, C rolls, H is help, B is the book, P is the
/// periodic table, F the graphs, V the microphone, 1-8 start reactions. Typing "why did the sodium
/// fail" without a gate would walk the player across the room, open three panels and start a
/// reaction.
///
/// Every script that reads keys checks <see cref="IsCapturing"/> and returns early while a text
/// field is live. That is one line per script, which is a smaller and far more obvious change than
/// rebinding a dozen keys.
///
/// Ownership is tracked so a stale End() from a destroyed panel cannot unlock a capture that a
/// different panel has since started.
/// </summary>
public static class LabTextInput
{
    private static object owner;

    /// <summary>True while a text field is consuming keystrokes.</summary>
    public static bool IsCapturing { get { return owner != null; } }

    /// <summary>The text typed so far.</summary>
    public static string Buffer { get; private set; }

    public static int MaxLength = 220;

    public static bool IsOwnedBy(object candidate)
    {
        return owner != null && ReferenceEquals(owner, candidate);
    }

    public static void Begin(object newOwner)
    {
        if (newOwner == null)
        {
            return;
        }

        owner = newOwner;
        Buffer = string.Empty;
    }

    public static void End(object callingOwner)
    {
        // Only the owner may end the capture, so a panel closing late cannot steal the keyboard
        // back from a panel that opened after it.
        if (owner != null && !ReferenceEquals(owner, callingOwner))
        {
            return;
        }

        owner = null;
        Buffer = string.Empty;
    }

    public enum Result
    {
        Editing,
        Submitted,
        Cancelled
    }

    /// <summary>
    /// Consumes this frame's keystrokes into the buffer. Call once per frame from the owner.
    /// </summary>
    public static Result Consume(object callingOwner)
    {
        if (!IsOwnedBy(callingOwner))
        {
            return Result.Cancelled;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            return Result.Cancelled;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            return Result.Submitted;
        }

        string typed = Input.inputString;

        for (int i = 0; i < typed.Length; i++)
        {
            char c = typed[i];

            if (c == '\b')
            {
                if (Buffer.Length > 0)
                {
                    Buffer = Buffer.Substring(0, Buffer.Length - 1);
                }
                continue;
            }

            // Return and newline arrive here too on some layouts; they are handled above, and
            // letting them through would put a control character in the question.
            if (c == '\n' || c == '\r')
            {
                continue;
            }

            if (Buffer.Length < MaxLength)
            {
                Buffer += c;
            }
        }

        return Result.Editing;
    }

    /// <summary>The buffer with a blinking caret, for display.</summary>
    public static string Display
    {
        get
        {
            bool caretOn = Mathf.Repeat(Time.unscaledTime, 1.0f) < 0.5f;
            return Buffer + (caretOn ? "|" : " ");
        }
    }
}
