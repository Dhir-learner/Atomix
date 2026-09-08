using System;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Speaks the assistant's replies without the cloud, using the speech synthesiser that is already
/// part of Windows.
///
/// Feature 5 of the project brief is that the assistant replies in *both* text and voice. Today the
/// voice half comes only from Convai's base64 WAV, so with no internet the assistant goes mute even
/// though it still has plenty to say. This restores the voice half offline.
///
/// Why a PowerShell process rather than System.Speech directly: Unity compiles this assembly
/// against netstandard2.1, which does not contain System.Speech, so the type cannot be referenced
/// at compile time on any scripting backend. Shelling out to the host PowerShell - which does have
/// it - works on both the Editor and a Windows standalone build without adding a dependency, a
/// native plugin, or a per-platform define.
///
/// The text is piped through stdin rather than embedded in the command line. That is deliberate:
/// an assistant reply can contain quotes, semicolons and newlines, and putting any of that into a
/// -Command string would be both fragile and a command-injection hole.
/// </summary>
public class OfflineVoice : MonoBehaviour
{
    /// <summary>Reads whatever is piped in and speaks it. No interpolation, so nothing to escape.</summary>
    private const string SpeakScript =
        "Add-Type -AssemblyName System.Speech; " +
        "$t = [Console]::In.ReadToEnd(); " +
        "if ($t) { " +
        "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
        "$s.Rate = 1; " +
        "$s.Volume = 100; " +
        "$s.Speak($t) }";

    /// <summary>Long answers are trimmed: nobody listens to a four-minute monologue.</summary>
    public int maxSpokenCharacters = 700;

    private Process speaking;
    private bool unavailable;

    private static OfflineVoice instance;

    public static OfflineVoice Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject host = new GameObject("OfflineVoice");
                instance = host.AddComponent<OfflineVoice>();
                DontDestroyOnLoad(host);
            }
            return instance;
        }
    }

    /// <summary>True when this platform can speak at all.</summary>
    public static bool IsSupported
    {
        get
        {
            return Application.platform == RuntimePlatform.WindowsPlayer ||
                   Application.platform == RuntimePlatform.WindowsEditor;
        }
    }

    public bool IsSpeaking
    {
        get
        {
            try
            {
                return speaking != null && !speaking.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    /// <summary>
    /// Speaks the text, cutting off anything already being spoken. Returns false when speech is
    /// not available, so the caller can say so rather than assuming the student heard it.
    /// </summary>
    public bool Speak(string text)
    {
        if (unavailable || !IsSupported || string.IsNullOrEmpty(text))
        {
            return false;
        }

        Stop();

        string spoken = Prepare(text);
        if (string.IsNullOrEmpty(spoken))
        {
            return false;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "powershell.exe";
            startInfo.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" +
                                  SpeakScript + "\"";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            speaking = Process.Start(startInfo);

            if (speaking == null)
            {
                unavailable = true;
                return false;
            }

            speaking.StandardInput.Write(spoken);
            speaking.StandardInput.Close();
            return true;
        }
        catch (Exception error)
        {
            // Almost always "powershell.exe not found" on a locked-down machine. One warning, then
            // stop trying - a failed spawn every time the assistant answers would be worse than
            // being silent.
            UnityEngine.Debug.LogWarning("OfflineVoice: speech unavailable (" + error.Message + ")");
            unavailable = true;
            speaking = null;
            return false;
        }
    }

    public void Stop()
    {
        if (speaking == null)
        {
            return;
        }

        try
        {
            if (!speaking.HasExited)
            {
                speaking.Kill();
            }
        }
        catch (Exception)
        {
            // The process may already be gone; nothing useful to do about it either way.
        }
        finally
        {
            try
            {
                speaking.Dispose();
            }
            catch (Exception)
            {
            }

            speaking = null;
        }
    }

    void OnDestroy()
    {
        Stop();
    }

    void OnApplicationQuit()
    {
        // Without this a long answer keeps talking after the window has closed.
        Stop();
    }

    /// <summary>
    /// Strips the things that sound wrong when read aloud and trims to a listenable length.
    /// </summary>
    private string Prepare(string text)
    {
        string cleaned = text
            .Replace("->", " gives ")
            .Replace("→", " gives ")
            .Replace("\n\n", ". ")
            .Replace("\n", ". ")
            .Replace("  ", " ")
            .Trim();

        if (cleaned.Length <= maxSpokenCharacters)
        {
            return cleaned;
        }

        // Cut at a sentence end rather than mid-word, so the trim is not audible as a cut-off.
        int cut = cleaned.LastIndexOf('.', maxSpokenCharacters - 1);
        if (cut < maxSpokenCharacters / 2)
        {
            cut = maxSpokenCharacters - 1;
        }

        return cleaned.Substring(0, cut + 1);
    }
}
