using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Talks to a Convai character over its plain REST endpoint, so the lab assistant needs no extra
/// SDK in the project: everything here is UnityWebRequest, Microphone and AudioSource.
///
///   POST https://api.convai.com/character/getResponse
///   header  CONVAI-API-KEY: &lt;key&gt;
///   form    charID, sessionID, voiceResponse, and either userText or a mono WAV file
///   returns { "text": "...", "audio": "&lt;base64 wav&gt;", "sessionID": "..." }
///
/// Convai is stateless per request; continuity comes from feeding the returned sessionID back in.
/// </summary>
public class ConvaiAssistantBackend : MonoBehaviour
{
    public const string Endpoint = "https://api.convai.com/character/getResponse";
    private const string NewSession = "-1";

    /// <summary>Raised with whatever the student asked (or a marker for a spoken question).</summary>
    public event Action<string> UserAsked;

    /// <summary>Raised with the assistant's written reply.</summary>
    public event Action<string> AssistantReplied;

    /// <summary>Raised when something goes wrong, with a message fit to show the student.</summary>
    public event Action<string> Failed;

    private LabAssistantSettings settings;
    private AudioSource voiceSource;

    private string sessionId = NewSession;
    private string microphoneDevice;
    private AudioClip recordingClip;
    private bool isRecording = false;
    private bool isWaitingForReply = false;
    private string pendingContext = string.Empty;

    public bool IsRecording { get { return isRecording; } }

    /// <summary>True while the assistant's reply audio is playing - drives the character's animation.</summary>
    public bool IsSpeaking { get { return voiceSource != null && voiceSource.isPlaying; } }
    public bool IsWaitingForReply { get { return isWaitingForReply; } }
    public bool IsReady { get { return settings != null && settings.IsConvaiConfigured; } }

    /// <summary>Text describing the current state, for the panel's status line.</summary>
    public string StatusDetail { get; private set; }

    /// <summary>
    /// Makes the reply audio play from the character instead of flatly in the player's ear. Falls
    /// back to 2D audio when there is no character in the scene.
    /// </summary>
    public void SetVoiceAnchor(Transform anchor)
    {
        if (voiceSource == null)
        {
            return;
        }

        if (anchor == null)
        {
            voiceSource.transform.SetParent(transform, false);
            voiceSource.transform.localPosition = Vector3.zero;
            voiceSource.spatialBlend = 0.0f;
            return;
        }

        voiceSource.transform.SetParent(anchor, false);
        voiceSource.transform.localPosition = Vector3.zero;
        voiceSource.spatialBlend = 0.65f; // mostly positional, but never inaudible across the room
        voiceSource.minDistance = 2.0f;
        voiceSource.maxDistance = 25.0f;
    }

    public void Initialise(LabAssistantSettings assistantSettings)
    {
        settings = assistantSettings;

        if (settings == null || !settings.IsConvaiConfigured)
        {
            StatusDetail = "not configured";
            return;
        }

        if (voiceSource == null)
        {
            // On its own object so SetVoiceAnchor can move it to the character later.
            GameObject voiceObject = new GameObject("AssistantVoice");
            voiceObject.transform.SetParent(transform, false);
            voiceSource = voiceObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 0.0f; // 2D until a character anchors it
            voiceSource.rolloffMode = AudioRolloffMode.Linear;
        }

        StatusDetail = "ready";
    }

    void OnDisable()
    {
        AbortRecording();
    }

    // =========================================================
    // VOICE INPUT
    // =========================================================

    /// <summary>Begins capturing the microphone. Safe to call when already recording.</summary>
    public void StartListening()
    {
        if (isRecording || !IsReady)
        {
            return;
        }

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            RaiseFailure("No microphone was found. You can still type questions.");
            return;
        }

        microphoneDevice = Microphone.devices[0];
        recordingClip = Microphone.Start(microphoneDevice, false,
            Mathf.Max(2, settings.maxRecordingSeconds),
            Mathf.Max(8000, settings.microphoneSampleRate));

        if (recordingClip == null)
        {
            RaiseFailure("The microphone could not be started.");
            return;
        }

        isRecording = true;
        StatusDetail = "listening";
    }

    /// <summary>
    /// Stops capturing and sends what was recorded. The context briefing is not sent with audio -
    /// Convai takes either text or a file, never both - so it is sent as a separate text turn first.
    /// </summary>
    public void StopListeningAndSend(string context)
    {
        if (!isRecording)
        {
            return;
        }

        int samplePosition = Microphone.GetPosition(microphoneDevice);
        Microphone.End(microphoneDevice);
        isRecording = false;

        if (recordingClip == null || samplePosition <= 0)
        {
            StatusDetail = "ready";
            return;
        }

        byte[] wav = EncodeClipAsWav(recordingClip, samplePosition);
        Destroy(recordingClip);
        recordingClip = null;

        if (wav == null || wav.Length == 0)
        {
            StatusDetail = "ready";
            return;
        }

        RaiseUser("(spoken question)");
        StartCoroutine(SendRequest(null, wav, context));
    }

    private void AbortRecording()
    {
        if (!isRecording)
        {
            return;
        }

        Microphone.End(microphoneDevice);
        isRecording = false;

        if (recordingClip != null)
        {
            Destroy(recordingClip);
            recordingClip = null;
        }
    }

    // =========================================================
    // TEXT INPUT
    // =========================================================

    /// <summary>Sends a typed question, with the experiment briefing folded in front of it.</summary>
    public void SendTextQuestion(string context, string question)
    {
        if (!IsReady || string.IsNullOrEmpty(question))
        {
            return;
        }

        RaiseUser(question);

        string body = string.IsNullOrEmpty(context) ? question : context + "\n\nStudent asks: " + question;
        StartCoroutine(SendRequest(body, null, null));
    }

    // =========================================================
    // REQUEST
    // =========================================================

    private IEnumerator SendRequest(string userText, byte[] wavAudio, string contextForAudio)
    {
        // Convai accepts either userText or a file, never both. When the question is spoken, the
        // briefing goes ahead of it as its own silent turn so the character still has the numbers.
        if (wavAudio != null && !string.IsNullOrEmpty(contextForAudio) && contextForAudio != pendingContext)
        {
            pendingContext = contextForAudio;
            yield return SendRequest(contextForAudio, null, null);
        }

        isWaitingForReply = true;
        StatusDetail = "thinking";

        WWWForm form = new WWWForm();
        form.AddField("charID", settings.convaiCharacterId);
        form.AddField("sessionID", sessionId);
        form.AddField("voiceResponse", settings.convaiVoiceResponse ? "True" : "False");

        if (wavAudio != null)
        {
            form.AddBinaryData("file", wavAudio, "question.wav", "audio/wav");
            form.AddField("sample_rate", Mathf.Max(8000, settings.microphoneSampleRate).ToString());
        }
        else
        {
            form.AddField("userText", userText);
        }

        using (UnityWebRequest request = UnityWebRequest.Post(Endpoint, form))
        {
            request.SetRequestHeader("CONVAI-API-KEY", settings.convaiApiKey);
            request.timeout = 30;

            yield return request.SendWebRequest();

            isWaitingForReply = false;

            if (request.result != UnityWebRequest.Result.Success)
            {
                StatusDetail = "error";
                RaiseFailure(DescribeError(request));
                yield break;
            }

            StatusDetail = "ready";
            HandleResponse(request.downloadHandler.text);
        }
    }

    private string DescribeError(UnityWebRequest request)
    {
        long code = request.responseCode;

        if (code == 401 || code == 403)
        {
            return "Convai rejected the API key (HTTP " + code + "). Check it in Resources/LabAssistantSettings.";
        }
        if (code == 404)
        {
            return "Convai could not find that character ID (HTTP 404). Check it in Resources/LabAssistantSettings.";
        }
        if (code == 429)
        {
            return "Convai rate limit or free-tier quota reached (HTTP 429).";
        }
        if (code >= 500)
        {
            return "Convai server error (HTTP " + code + "). Try again shortly.";
        }

        return "Could not reach Convai: " + request.error;
    }

    private void HandleResponse(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            RaiseFailure("Convai returned an empty response.");
            return;
        }

        string replyText = ExtractJsonString(json, "text");
        string returnedSession = ExtractJsonString(json, "sessionID");
        string audioBase64 = ExtractJsonString(json, "audio");

        if (!string.IsNullOrEmpty(returnedSession) && returnedSession != "null")
        {
            sessionId = returnedSession;
        }

        if (!string.IsNullOrEmpty(replyText))
        {
            RaiseAssistant(replyText);
        }

        if (!string.IsNullOrEmpty(audioBase64))
        {
            StartCoroutine(PlayBase64Wav(audioBase64));
        }
    }

    /// <summary>
    /// Pulls one string field out of the JSON by hand. Deliberately not JsonUtility: Convai returns
    /// sample_rate as a number in some responses and null in others, which makes a typed model
    /// brittle for the two fields we actually care about.
    /// </summary>
    private static string ExtractJsonString(string json, string field)
    {
        string token = "\"" + field + "\"";
        int keyIndex = json.IndexOf(token, StringComparison.Ordinal);
        if (keyIndex < 0)
        {
            return null;
        }

        int colon = json.IndexOf(':', keyIndex + token.Length);
        if (colon < 0)
        {
            return null;
        }

        int cursor = colon + 1;
        while (cursor < json.Length && char.IsWhiteSpace(json[cursor]))
        {
            cursor++;
        }

        if (cursor >= json.Length || json[cursor] != '"')
        {
            return null; // null, a number, or an object - not a string we can use
        }

        cursor++;
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        while (cursor < json.Length)
        {
            char c = json[cursor];
            if (c == '\\' && cursor + 1 < json.Length)
            {
                char escaped = json[cursor + 1];
                switch (escaped)
                {
                    case 'n': builder.Append('\n'); break;
                    case 'r': break;
                    case 't': builder.Append(' '); break;
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    default: builder.Append(escaped); break;
                }
                cursor += 2;
                continue;
            }
            if (c == '"')
            {
                break;
            }
            builder.Append(c);
            cursor++;
        }

        return builder.ToString();
    }

    // =========================================================
    // AUDIO
    // =========================================================

    /// <summary>
    /// Writes the reply to a temp WAV and lets Unity decode it, rather than hand-parsing the
    /// header - fewer ways to get the format wrong.
    /// </summary>
    private IEnumerator PlayBase64Wav(string base64)
    {
        byte[] audioBytes;
        try
        {
            audioBytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            yield break; // a malformed clip is not worth interrupting the lesson over
        }

        string path = Path.Combine(Application.temporaryCachePath, "convai_reply.wav");

        bool written = false;
        try
        {
            File.WriteAllBytes(path, audioBytes);
            written = true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Convai] Could not cache the reply audio: " + exception.Message);
        }

        if (!written)
        {
            yield break;
        }

        using (UnityWebRequest request =
               UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip != null && voiceSource != null)
            {
                voiceSource.Stop();
                voiceSource.clip = clip;
                voiceSource.Play();
            }
        }
    }

    /// <summary>Mono 16-bit PCM WAV, which is the format Convai documents for uploads.</summary>
    private static byte[] EncodeClipAsWav(AudioClip clip, int sampleCount)
    {
        if (clip == null || sampleCount <= 0)
        {
            return null;
        }

        sampleCount = Mathf.Min(sampleCount, clip.samples);

        float[] samples = new float[sampleCount * clip.channels];
        clip.GetData(samples, 0);

        // Downmix to mono if the device handed us more than one channel.
        int channels = Mathf.Max(1, clip.channels);
        float[] mono;
        if (channels == 1)
        {
            mono = samples;
        }
        else
        {
            mono = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float sum = 0.0f;
                for (int c = 0; c < channels; c++)
                {
                    sum += samples[i * channels + c];
                }
                mono[i] = sum / channels;
            }
        }

        int dataBytes = mono.Length * 2;
        using (MemoryStream stream = new MemoryStream(44 + dataBytes))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(new char[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new char[] { 'W', 'A', 'V', 'E' });
            writer.Write(new char[] { 'f', 'm', 't', ' ' });
            writer.Write(16);                                   // PCM chunk size
            writer.Write((short)1);                             // PCM format
            writer.Write((short)1);                             // mono
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * 2);                   // byte rate
            writer.Write((short)2);                             // block align
            writer.Write((short)16);                            // bits per sample
            writer.Write(new char[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            for (int i = 0; i < mono.Length; i++)
            {
                writer.Write((short)(Mathf.Clamp(mono[i], -1.0f, 1.0f) * short.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }
    }

    // =========================================================
    // EVENTS
    // =========================================================

    /// <summary>Forgets the conversation so the next question starts a fresh Convai session.</summary>
    public void ResetSession()
    {
        sessionId = NewSession;
        pendingContext = string.Empty;
    }

    private void RaiseUser(string text)
    {
        if (UserAsked != null)
        {
            UserAsked(text);
        }
    }

    private void RaiseAssistant(string text)
    {
        if (AssistantReplied != null)
        {
            AssistantReplied(text);
        }
    }

    private void RaiseFailure(string message)
    {
        Debug.LogWarning("[Convai] " + message);
        if (Failed != null)
        {
            Failed(message);
        }
    }
}
