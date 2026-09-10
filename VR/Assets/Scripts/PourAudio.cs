using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives the three pours that never had a sound one.
///
/// Five of the eight pour scripts play a clip while they are tipping - <c>PourSubstance</c>,
/// <c>PourHCL</c>, <c>PourH2so4</c>, <c>PourPhenolphthalein</c> and <c>PourCuO</c> each hold an
/// <c>AudioSource</c> and call <c>PlayOneShot</c>. Three do not, and have no audio fields at all:
///
/// <list type="bullet">
/// <item><see cref="PourNahco3"/> - tipping sodium bicarbonate powder into the acid;</item>
/// <item><see cref="PourMetalSubstance"/> - dropping the sodium, potassium, aluminium and
/// iodine solids;</item>
/// <item><see cref="PourFromPipette"/> - the dropper, used across several experiments.</item>
/// </list>
///
/// So whether adding a reagent made a noise depended on which reagent it was, which reads as the
/// game intermittently failing to notice what the student is doing. Since pouring the right
/// amount <i>is</i> the game, that is the worst possible place for feedback to be unreliable.
///
/// This closes the gap without touching those three scripts: it watches their public
/// <c>IsPouring</c> flags and drives a looping, spatialised source parented to the same particle
/// system they emit from - so the sound comes from the vessel, and gets quieter as the student
/// steps back from the bench.
///
/// A loop rather than a one-shot, because unlike the other five this has to hold for as long as
/// the tipping does, and the tipping is exactly what is being measured.
/// </summary>
[DisallowMultipleComponent]
public class PourAudio : MonoBehaviour
{
    [Tooltip("How often the scene is re-scanned. Both labs reveal equipment one experiment at a " +
             "time, so the set of live pour scripts changes as the student plays.")]
    public float rescanSeconds = 3.0f;

    [Tooltip("Loudest the pour is at the vessel itself.")]
    public float volume = 0.55f;

    /// <summary>One watched pour script and the voice that speaks for it.</summary>
    private class Voice
    {
        public MonoBehaviour Owner;
        public System.Func<bool> IsPouring;
        public AudioSource Source;
    }

    private readonly List<Voice> voices = new List<Voice>();
    private float rescanTimer;

    private static AudioClip granularLoop;
    private static AudioClip dropletLoop;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<PourAudio>() != null)
        {
            return;
        }

        GameObject host = new GameObject("PourAudio");
        host.AddComponent<PourAudio>();
        DontDestroyOnLoad(host);
    }

    void Update()
    {
        rescanTimer -= Time.deltaTime;
        if (rescanTimer <= 0.0f)
        {
            rescanTimer = rescanSeconds;
            Rescan();
        }

        float bus = AtomixSettings.SfxVolume * volume;

        for (int i = voices.Count - 1; i >= 0; i--)
        {
            Voice voice = voices[i];

            // The owning script goes away with its scene, and so does the source.
            if (voice.Owner == null || voice.Source == null)
            {
                voices.RemoveAt(i);
                continue;
            }

            bool pouring = voice.Owner.isActiveAndEnabled && voice.IsPouring();

            // Ramped rather than switched. A pour that starts and stops on a frame boundary
            // clicks, and the student taps the tilt keys constantly while fine-tuning an amount.
            float target = pouring ? bus : 0.0f;
            voice.Source.volume = Mathf.MoveTowards(
                voice.Source.volume, target, Time.deltaTime * 4.0f);

            if (voice.Source.volume > 0.001f)
            {
                if (!voice.Source.isPlaying)
                {
                    // Start at a random point, so two vessels pouring at once do not phase-lock
                    // into one obviously looping sound.
                    voice.Source.time = Random.Range(0.0f, voice.Source.clip.length * 0.9f);
                    voice.Source.Play();
                }
            }
            else if (voice.Source.isPlaying)
            {
                voice.Source.Stop();
            }
        }
    }

    private void Rescan()
    {
        foreach (PourNahco3 pour in FindAll<PourNahco3>())
        {
            Register(pour, () => pour.IsPouring, pour.substanceLeak, granular: true);
        }

        foreach (PourMetalSubstance pour in FindAll<PourMetalSubstance>())
        {
            Register(pour, () => pour.IsPouring, pour.substanceLeak, granular: true);
        }

        foreach (PourFromPipette pour in FindAll<PourFromPipette>())
        {
            // A dropper is discrete drips, not a stream.
            Register(pour, () => pour.IsPouring, pour.substanceLeak, granular: false);
        }
    }

    private void Register(MonoBehaviour owner, System.Func<bool> isPouring,
                          ParticleSystem leak, bool granular)
    {
        if (owner == null)
        {
            return;
        }

        for (int i = 0; i < voices.Count; i++)
        {
            if (voices[i].Owner == owner)
            {
                return;
            }
        }

        // Anchor on the particle system so the sound comes from where the substance leaves the
        // vessel, not from the vessel's pivot - which on several of these is the bench.
        Transform anchor = leak != null ? leak.transform : owner.transform;

        GameObject voiceObject = new GameObject("PourVoice");
        voiceObject.transform.SetParent(anchor, false);

        AudioSource source = voiceObject.AddComponent<AudioSource>();
        source.clip = granular ? GranularLoop() : DropletLoop();
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0.0f;

        // Spatialised, unlike the interface cues: this is a thing happening at a place on the
        // bench, and hearing it move as the student walks around the vessel is the point.
        source.spatialBlend = 1.0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 0.4f;
        source.maxDistance = 6.0f;

        voices.Add(new Voice
        {
            Owner = owner,
            IsPouring = isPouring,
            Source = source
        });
    }

    private static AudioClip GranularLoop()
    {
        if (granularLoop == null)
        {
            granularLoop = AtomixAudioSynth.BuildPourLoop(granular: true);
        }
        return granularLoop;
    }

    private static AudioClip DropletLoop()
    {
        if (dropletLoop == null)
        {
            dropletLoop = AtomixAudioSynth.BuildPourLoop(granular: false);
        }
        return dropletLoop;
    }

    private static T[] FindAll<T>() where T : Object
    {
        return FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }
}
