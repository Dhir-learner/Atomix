using UnityEngine;

/// <summary>Which service answers the student's questions.</summary>
public enum LabAssistantProvider
{
    /// <summary>Use Convai when it is configured.</summary>
    Auto = 0,
    Convai = 1,
    /// <summary>No assistant at all - the panel stays hidden.</summary>
    None = 3
}

/// <summary>
/// Where the in-lab assistant gets its credentials. Lives at
/// Assets/Resources/LabAssistantSettings.asset so it can be loaded without any scene wiring.
///
/// The asset ships with empty credentials on purpose: fill them in the Inspector, and do not
/// commit real keys to the repository.
/// </summary>
[CreateAssetMenu(fileName = "LabAssistantSettings", menuName = "Atomix/Lab Assistant Settings")]
public class LabAssistantSettings : ScriptableObject
{
    public const string ResourceName = "LabAssistantSettings";

    [Header("Provider")]
    [Tooltip("Auto uses Convai when an API key and character ID are filled in.")]
    public LabAssistantProvider provider = LabAssistantProvider.Auto;

    [Header("Convai")]
    [Tooltip("From https://convai.com -> profile menu -> API Key.")]
    public string convaiApiKey = "";

    [Tooltip("From the Convai dashboard: open your character and copy its Character ID.")]
    public string convaiCharacterId = "";

    [Tooltip("Ask Convai to speak its replies as well as write them.")]
    public bool convaiVoiceResponse = true;

    [Header("Microphone")]
    [Tooltip("Longest single push-to-talk recording, in seconds.")]
    public int maxRecordingSeconds = 15;

    [Tooltip("Recording sample rate. Convai expects mono; 16000 is plenty for speech.")]
    public int microphoneSampleRate = 16000;

    /// <summary>True when Convai has everything it needs to answer.</summary>
    public bool IsConvaiConfigured
    {
        get
        {
            return !string.IsNullOrEmpty(convaiApiKey) && !string.IsNullOrEmpty(convaiCharacterId);
        }
    }

    /// <summary>The provider that should actually be used, resolving Auto.</summary>
    public LabAssistantProvider ResolvedProvider
    {
        get
        {
            if (provider != LabAssistantProvider.Auto)
            {
                return provider;
            }
            return IsConvaiConfigured ? LabAssistantProvider.Convai : LabAssistantProvider.None;
        }
    }

    private static LabAssistantSettings cached;

    /// <summary>Loads the settings asset, or null when it has not been created.</summary>
    public static LabAssistantSettings Load()
    {
        if (cached == null)
        {
            cached = Resources.Load<LabAssistantSettings>(ResourceName);
        }
        return cached;
    }
}
