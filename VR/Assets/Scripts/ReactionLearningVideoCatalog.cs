using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "ReactionLearningVideoCatalog", menuName = "Atomix/Reaction Learning Video Catalog")]
public class ReactionLearningVideoCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Min(1)]
        public int reactionId;
        public string reactionName;
        public string displayTitle;
        public VideoClip videoClip;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;

    public bool TryGetEntry(int reactionId, out Entry entry)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Entry candidate = entries[i];
            if (candidate != null && candidate.reactionId == reactionId)
            {
                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }
}
