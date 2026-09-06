using System.Collections.Generic;
using Inworld;
using Inworld.Entities;
using Inworld.Packet;
using UnityEngine;

/// <summary>
/// Copies the Inworld lab-assistant conversation into the experiment history, so a student can
/// look back at what they asked and what they were told about a given attempt.
///
/// The assistant lives in its own scene, so this rides on the history manager's DontDestroyOnLoad
/// object and re-subscribes whenever new characters appear. Everything is null-guarded: if the
/// Inworld session never connects, this simply records nothing.
/// </summary>
public class LabAssistantHistoryBridge : MonoBehaviour
{
    [Tooltip("Seconds of assistant silence before the collected reply is written to history.")]
    public float replyFlushSeconds = 2.0f;

    [Tooltip("How often to look for newly spawned Inworld characters.")]
    public float rescanSeconds = 2.0f;

    private readonly List<InworldCharacter> subscribed = new List<InworldCharacter>();

    private string pendingQuestion = string.Empty;
    private string pendingReply = string.Empty;
    private float lastReplyChunkTime = 0.0f;
    private float nextRescanTime = 0.0f;

    void Update()
    {
        if (Time.unscaledTime >= nextRescanTime)
        {
            nextRescanTime = Time.unscaledTime + Mathf.Max(0.5f, rescanSeconds);
            SubscribeToNewCharacters();
        }

        if (pendingReply.Length > 0 &&
            Time.unscaledTime - lastReplyChunkTime >= Mathf.Max(0.25f, replyFlushSeconds))
        {
            FlushPendingInteraction();
        }
    }

    void OnDisable()
    {
        FlushPendingInteraction();
    }

    private void SubscribeToNewCharacters()
    {
        InworldCharacter[] characters =
            FindObjectsByType<InworldCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (characters == null)
        {
            return;
        }

        // Drop characters destroyed with a previous scene.
        for (int i = subscribed.Count - 1; i >= 0; i--)
        {
            if (subscribed[i] == null)
            {
                subscribed.RemoveAt(i);
            }
        }

        for (int i = 0; i < characters.Length; i++)
        {
            InworldCharacter character = characters[i];
            if (character == null || subscribed.Contains(character))
            {
                continue;
            }

            CharacterEvents events = character.Event;
            if (events == null || events.onPacketReceived == null)
            {
                continue;
            }

            events.onPacketReceived.AddListener(HandlePacket);
            subscribed.Add(character);
        }
    }

    private void HandlePacket(InworldPacket packet)
    {
        TextPacket textPacket = packet as TextPacket;
        if (textPacket == null || textPacket.text == null)
        {
            return;
        }

        string body = textPacket.text.text;
        if (string.IsNullOrEmpty(body) || string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        if (packet.Source == SourceType.PLAYER)
        {
            // A new question means whatever the assistant said before it is now complete.
            FlushPendingInteraction();

            if (textPacket.text.final)
            {
                pendingQuestion = body.Trim();
            }
            return;
        }

        if (packet.Source == SourceType.AGENT)
        {
            // Replies arrive in chunks; stitch them back together.
            pendingReply = pendingReply.Length == 0 ? body.Trim() : pendingReply + " " + body.Trim();
            lastReplyChunkTime = Time.unscaledTime;
        }
    }

    private void FlushPendingInteraction()
    {
        if (pendingQuestion.Length == 0 && pendingReply.Length == 0)
        {
            return;
        }

        ExperimentHistoryManager manager = ExperimentHistoryManager.Instance;
        if (manager != null)
        {
            // Attaches to the running attempt, falling back to the most recent one - students
            // usually ask the assistant right after an experiment has already failed.
            manager.LogAIInteraction(manager.ActiveAttemptId, pendingQuestion, pendingReply);
        }

        pendingQuestion = string.Empty;
        pendingReply = string.Empty;
    }
}
