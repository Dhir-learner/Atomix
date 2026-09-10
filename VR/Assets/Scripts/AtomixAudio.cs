using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The one cue every interaction in Atomix is played through, and the laboratory room tone
/// underneath them.
///
/// The project had a great deal of audio and almost no <i>feedback</i>: 38 clips in
/// <c>Assets/Sounds</c>, and every one of them is narration - "add the sodium now", "this is how
/// copper sulfate forms". Nothing in the lab made a sound because the student <i>did</i> something.
/// Picking up a beaker was silent. Clicking a menu button was silent. Succeeding at an experiment
/// after four failed attempts was silent. In a first-person game that is the difference between
/// controls that feel connected and controls that feel like they are ignoring you.
///
/// <b>Every cue here is synthesised at runtime</b> rather than imported. That is a deliberate
/// choice, not a shortcut:
///
/// <list type="bullet">
/// <item>the repository gains no binary assets, so the audio cannot go missing from a build the
/// way an unassigned <c>AudioClip</c> field silently can;</item>
/// <item>no scene or prefab has to be edited to wire a source up, which is how every other
/// system added since Task 1 has been delivered;</item>
/// <item>the cues are consistent with each other by construction - one palette of pitches, one
/// envelope shape - instead of eight sample packs that do not agree on loudness.</item>
/// </list>
///
/// Cues are cheap: the whole set is about 400 kB of float samples, built once on first use and
/// cached for the session.
///
/// Routing is <see cref="AtomixSettings.MasterVolume"/> (which is <c>AudioListener.volume</c>,
/// over everything) with three buses underneath it - SFX, ambience and music - so a student can
/// silence the room tone without silencing the narration that explains the experiment.
/// </summary>
[DisallowMultipleComponent]
public class AtomixAudio : MonoBehaviour
{
    /// <summary>Every feedback sound in the game. Kept small on purpose.</summary>
    public enum Cue
    {
        /// <summary>Menu and panel buttons.</summary>
        UiClick,
        /// <summary>Crosshair moving onto something you can pick up.</summary>
        UiHover,
        /// <summary>An action that was refused - a locked button, a grab that cannot happen.</summary>
        UiDenied,
        /// <summary>A toast or panel appearing.</summary>
        UiOpen,
        /// <summary>A panel closing.</summary>
        UiClose,
        /// <summary>Glassware picked up.</summary>
        Grab,
        /// <summary>Glassware set down.</summary>
        Release,
        /// <summary>The experiment worked.</summary>
        Success,
        /// <summary>The experiment failed.</summary>
        Failure,
        /// <summary>Coins earned in the testing scene.</summary>
        Coin,
        /// <summary>An achievement unlocked.</summary>
        Achievement,
        /// <summary>The bench has been reset for another attempt.</summary>
        Reset
    }

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private const int SfxVoices = 8;

    private static AtomixAudio instance;
    private static readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();

    private AudioSource[] sfxVoices;
    private int nextVoice;
    private AudioSource ambienceSource;
    private AudioSource musicSource;

    private AudioClip ambienceClip;
    private AudioClip musicClip;

    // The two beds are ~1.5 MB and ~2.3 MB of samples and cost roughly a fifth of a second
    // each to synthesise. Building them inline would hitch the frame the lab loads on - which
    // is the one frame a student is already waiting through. They are generated on a worker
    // thread instead and picked up here when they land; the beds fade in over half a second
    // anyway, so the delay is invisible.
    private AtomixAudioSynth.BedJob ambienceJob;
    private AtomixAudioSynth.BedJob musicJob;

    // The short cues are cheap individually but not free all at once, and the most expensive of
    // them - the success sting - would otherwise be built on the exact frame it is first needed.
    // One per frame while the lab is idle costs nothing and means every cue is ready before the
    // student can reach it.
    private int warmIndex;
    private static readonly Cue[] WarmOrder =
    {
        Cue.UiClick, Cue.Grab, Cue.Release, Cue.Success, Cue.Failure,
        Cue.UiHover, Cue.UiOpen, Cue.UiClose, Cue.UiDenied, Cue.Coin,
        Cue.Achievement, Cue.Reset
    };

    /// <summary>Faded rather than cut, so walking through a scene load is not a click.</summary>
    private float ambienceTarget;
    private float musicTarget;

    /// <summary>The live director, or null before the first scene has finished loading.</summary>
    public static AtomixAudio Instance { get { return instance; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("AtomixAudio");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AtomixAudio>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildVoices();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        AtomixSettings.Changed += HandleSettingsChanged;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        AtomixSettings.Changed -= HandleSettingsChanged;
    }

    void Start()
    {
        ApplyScene(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void BuildVoices()
    {
        sfxVoices = new AudioSource[SfxVoices];
        for (int i = 0; i < SfxVoices; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0.0f;         // 2D: feedback belongs to the interface, not the room
            source.bypassReverbZones = true;
            sfxVoices[i] = source;
        }

        ambienceSource = gameObject.AddComponent<AudioSource>();
        ambienceSource.playOnAwake = false;
        ambienceSource.loop = true;
        ambienceSource.spatialBlend = 0.0f;
        ambienceSource.volume = 0.0f;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0.0f;
        musicSource.volume = 0.0f;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyScene(scene.name);
    }

    private void HandleSettingsChanged()
    {
        // Levels are reached by the fade in Update; this only matters for an immediate mute.
        if (AtomixSettings.AmbienceVolume <= 0.0f && ambienceSource != null)
        {
            ambienceSource.volume = 0.0f;
        }
        if (AtomixSettings.MusicVolume <= 0.0f && musicSource != null)
        {
            musicSource.volume = 0.0f;
        }

        // A bed that was skipped because its volume was zero has to be built the moment the
        // player turns it up, or the slider would appear to do nothing at all.
        ApplyScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Room tone in the two laboratories, a slow pad in the menu, silence in the tutor scene -
    /// where the assistant is talking and anything underneath it is just in the way.
    /// </summary>
    private void ApplyScene(string sceneName)
    {
        bool isLab = sceneName == "LabScene" || sceneName == "TestingPhaseLab";
        bool isMenu = sceneName == "MainMenuScene";

        ambienceTarget = isLab ? 1.0f : 0.0f;
        musicTarget = isMenu ? 1.0f : 0.0f;

        // Only build a bed somebody is actually going to hear. The room tone ships at zero,
        // so synthesising it on every lab load would be pure waste; if it is switched on later,
        // HandleSettingsChanged starts the job then.
        if (isLab && ambienceClip == null && ambienceJob == null &&
            AtomixSettings.AmbienceVolume > 0.0f)
        {
            ambienceJob = AtomixAudioSynth.BeginLabAmbience();
        }

        if (isMenu && musicClip == null && musicJob == null &&
            AtomixSettings.MusicVolume > 0.0f)
        {
            musicJob = AtomixAudioSynth.BeginMenuPad();
        }

        StartBed(ambienceSource, ambienceClip, isLab);
        StartBed(musicSource, musicClip, isMenu);
    }

    private static void StartBed(AudioSource source, AudioClip clip, bool wanted)
    {
        if (source == null || clip == null)
        {
            return;
        }

        if (!wanted)
        {
            return;
        }

        if (source.clip != clip)
        {
            source.clip = clip;
        }

        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    /// <summary>Collects a finished bed and starts it, if the scene still wants it.</summary>
    private void CollectBeds()
    {
        if (ambienceJob != null && ambienceJob.IsComplete)
        {
            ambienceClip = ambienceJob.CreateClip();
            ambienceJob = null;
            StartBed(ambienceSource, ambienceClip, ambienceTarget > 0.0f);
        }

        if (musicJob != null && musicJob.IsComplete)
        {
            musicClip = musicJob.CreateClip();
            musicJob = null;
            StartBed(musicSource, musicClip, musicTarget > 0.0f);
        }
    }

    /// <summary>Builds one not-yet-built cue per frame until the whole set is cached.</summary>
    private void WarmOneCue()
    {
        while (warmIndex < WarmOrder.Length)
        {
            Cue cue = WarmOrder[warmIndex++];
            if (!clips.ContainsKey(cue))
            {
                GetClip(cue);
                return;
            }
        }
    }

    void Update()
    {
        CollectBeds();
        WarmOneCue();

        // unscaled: the pause menu does not stop time, but a future one might, and a bed that
        // freezes mid-fade is worse than one that keeps going.
        float step = Time.unscaledDeltaTime * 1.6f;

        if (ambienceSource != null)
        {
            float target = ambienceTarget * AtomixSettings.AmbienceVolume;
            ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, target, step);
            if (ambienceSource.volume <= 0.0f && ambienceSource.isPlaying && target <= 0.0f)
            {
                ambienceSource.Stop();
            }
        }

        if (musicSource != null)
        {
            float target = musicTarget * AtomixSettings.MusicVolume;
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, step);
            if (musicSource.volume <= 0.0f && musicSource.isPlaying && target <= 0.0f)
            {
                musicSource.Stop();
            }
        }
    }

    // =========================================================
    // PLAYBACK
    // =========================================================

    /// <summary>
    /// Plays one cue. Safe from anywhere at any time: before the director exists it is a no-op
    /// rather than an exception, because callers are scattered across UI builders that also run
    /// in scenes the director has not reached yet.
    /// </summary>
    public static void Play(Cue cue, float volumeScale = 1.0f, float pitch = 1.0f)
    {
        if (instance == null)
        {
            return;
        }

        instance.PlayInternal(cue, volumeScale, pitch);
    }

    private void PlayInternal(Cue cue, float volumeScale, float pitch)
    {
        float bus = AtomixSettings.SfxVolume;
        if (bus <= 0.0f || volumeScale <= 0.0f || sfxVoices == null)
        {
            return;
        }

        AudioClip clip = GetClip(cue);
        if (clip == null)
        {
            return;
        }

        AudioSource voice = sfxVoices[nextVoice];
        nextVoice = (nextVoice + 1) % sfxVoices.Length;

        voice.pitch = Mathf.Clamp(pitch, 0.25f, 3.0f);
        voice.PlayOneShot(clip, Mathf.Clamp01(bus * volumeScale));
    }

    private static AudioClip GetClip(Cue cue)
    {
        AudioClip clip;
        if (clips.TryGetValue(cue, out clip) && clip != null)
        {
            return clip;
        }

        clip = AtomixAudioSynth.Build(cue);
        clips[cue] = clip;
        return clip;
    }

    // --- named shorthands, so call sites read as intent rather than as an enum ---------

    public static void UiClick() { Play(Cue.UiClick); }
    public static void UiHover() { Play(Cue.UiHover, 0.5f); }
    public static void UiDenied() { Play(Cue.UiDenied); }
    public static void UiOpen() { Play(Cue.UiOpen, 0.8f); }
    public static void UiClose() { Play(Cue.UiClose, 0.7f); }
    public static void Grab() { Play(Cue.Grab); }
    public static void Release() { Play(Cue.Release, 0.85f); }
    public static void Success() { Play(Cue.Success); }
    public static void Failure() { Play(Cue.Failure); }
    public static void Coin() { Play(Cue.Coin, 0.9f); }
    public static void Achievement() { Play(Cue.Achievement); }
    public static void ResetBench() { Play(Cue.Reset, 0.8f); }
}

/// <summary>
/// Builds the cue set. Split out of <see cref="AtomixAudio"/> so the director stays a small
/// routing class and the synthesis - which is the part with the interesting decisions in it -
/// can be read on its own.
///
/// Two rules keep the set coherent:
///
/// <list type="number">
/// <item><b>One pitch palette.</b> Everything is drawn from a D major triad (D5 587.3, F#5 740.0,
/// A5 880.0, D6 1174.7). Cues therefore agree with each other when two land at once, which they
/// routinely do - a success sting and a coin award fire on the same frame.</item>
/// <item><b>One envelope family.</b> A few milliseconds of attack so nothing clicks, then
/// exponential decay. Percussive interface sounds with a linear release sound synthetic; an
/// exponential tail is what a struck object actually does.</item>
/// </list>
/// </summary>
public static class AtomixAudioSynth
{
    // The palette. Held as a named set rather than as magic numbers at the call sites.
    private const float D5 = 587.33f;
    private const float Fs5 = 739.99f;
    private const float A5 = 880.00f;
    private const float D6 = 1174.66f;

    private static int SampleRate
    {
        get
        {
            int rate = AudioSettings.outputSampleRate;
            return rate > 0 ? rate : 48000;
        }
    }

    /// <summary>
    /// A bed being synthesised on a worker thread. Sample generation is pure arithmetic over a
    /// <c>float[]</c> and touches no Unity object, so it is safe off the main thread;
    /// <see cref="AudioClip.Create"/> is not, so that half waits for <see cref="CreateClip"/>.
    /// </summary>
    public class BedJob
    {
        private readonly string clipName;
        private readonly float peak;
        private readonly int rate;
        private volatile float[] samples;

        internal BedJob(string clipName, float peak, int rate, System.Func<int, float[]> generate)
        {
            this.clipName = clipName;
            this.peak = peak;
            this.rate = rate;

            System.Threading.Thread worker = new System.Threading.Thread(() =>
            {
                try
                {
                    samples = generate(rate);
                }
                catch (System.Exception error)
                {
                    // A bed that fails to build must not take the game with it; the lab is
                    // simply quiet, which is exactly how it sounded before this class existed.
                    Debug.LogWarning("[Atomix] Audio bed '" + clipName + "' failed: " + error.Message);
                    samples = new float[rate];
                }
            });

            worker.IsBackground = true;      // never hold up domain reload or quit
            worker.Priority = System.Threading.ThreadPriority.BelowNormal;
            worker.Start();
        }

        public bool IsComplete { get { return samples != null; } }

        /// <summary>Main thread only. Null until <see cref="IsComplete"/>.</summary>
        public AudioClip CreateClip()
        {
            float[] data = samples;
            return data == null ? null : Finish(data, rate, clipName, peak, looping: true);
        }
    }

    public static BedJob BeginLabAmbience()
    {
        return new BedJob("AtomixLabAmbience", 0.16f, SampleRate, GenerateLabAmbience);
    }

    public static BedJob BeginMenuPad()
    {
        return new BedJob("AtomixMenuPad", 0.30f, SampleRate, GenerateMenuPad);
    }

    public static AudioClip Build(AtomixAudio.Cue cue)
    {
        switch (cue)
        {
            case AtomixAudio.Cue.UiClick: return BuildClick();
            case AtomixAudio.Cue.UiHover: return BuildHover();
            case AtomixAudio.Cue.UiDenied: return BuildDenied();
            case AtomixAudio.Cue.UiOpen: return BuildWhoosh(rising: true);
            case AtomixAudio.Cue.UiClose: return BuildWhoosh(rising: false);
            case AtomixAudio.Cue.Grab: return BuildGrab();
            case AtomixAudio.Cue.Release: return BuildRelease();
            case AtomixAudio.Cue.Success: return BuildSuccess();
            case AtomixAudio.Cue.Failure: return BuildFailure();
            case AtomixAudio.Cue.Coin: return BuildCoin();
            case AtomixAudio.Cue.Achievement: return BuildAchievement();
            case AtomixAudio.Cue.Reset: return BuildReset();
            default: return null;
        }
    }

    // =========================================================
    // CUES
    // =========================================================

    /// <summary>A short bright tick. The most-heard sound in the game, so the quietest.</summary>
    private static AudioClip BuildClick()
    {
        float[] data = Allocate(0.055f, out int rate);
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            float env = Envelope(t, 0.001f, 0.050f, 26.0f);
            data[i] = env * (0.55f * Sine(D6, t) + 0.25f * Sine(D6 * 2.0f, t));
        }
        return Finish(data, rate, "AtomixUiClick", 0.30f);
    }

    /// <summary>Barely there. It exists so the crosshair crossing a beaker is not silent.</summary>
    private static AudioClip BuildHover()
    {
        float[] data = Allocate(0.040f, out int rate);
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            float env = Envelope(t, 0.002f, 0.038f, 30.0f);
            data[i] = env * Sine(A5 * 2.0f, t);
        }
        return Finish(data, rate, "AtomixUiHover", 0.16f);
    }

    /// <summary>Two low blips. Reads as "no" without being a buzzer.</summary>
    private static AudioClip BuildDenied()
    {
        float[] data = Allocate(0.22f, out int rate);
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            float sample = 0.0f;
            sample += Envelope(t, 0.003f, 0.09f, 22.0f) * Sine(220.0f, t);
            sample += Envelope(t - 0.10f, 0.003f, 0.10f, 20.0f) * Sine(185.0f, t);
            data[i] = sample;
        }
        return Finish(data, rate, "AtomixUiDenied", 0.34f);
    }

    /// <summary>A panel arriving or leaving: filtered noise swept through a resonant peak.</summary>
    private static AudioClip BuildWhoosh(bool rising)
    {
        float[] data = Allocate(0.28f, out int rate);
        System.Random random = new System.Random(rising ? 8801 : 8802);

        float lowpass = 0.0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            float progress = t / 0.28f;
            float env = Envelope(t, 0.030f, 0.250f, 7.0f);

            // Sweeping the cutoff is what makes it read as a movement rather than a hiss.
            float cutoff = rising
                ? Mathf.Lerp(0.02f, 0.30f, progress)
                : Mathf.Lerp(0.30f, 0.02f, progress);

            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            lowpass += (noise - lowpass) * cutoff;
            data[i] = env * lowpass * 0.9f;
        }
        return Finish(data, rate, rising ? "AtomixUiOpen" : "AtomixUiClose", 0.22f);
    }

    /// <summary>
    /// Picking a piece of glassware off the bench: a soft low thunk with a trace of contact
    /// noise on the front. Low and short - it happens constantly.
    /// </summary>
    private static AudioClip BuildGrab()
    {
        float[] data = Allocate(0.16f, out int rate);
        System.Random random = new System.Random(4211);

        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;

            // Body: a pitch-dropping sine is what a hand closing on something sounds like.
            float frequency = Mathf.Lerp(190.0f, 130.0f, Mathf.Clamp01(t / 0.09f));
            float body = Envelope(t, 0.004f, 0.150f, 20.0f) * Mathf.Sin(2.0f * Mathf.PI * frequency * t);

            // Contact: 12 ms of noise, gone before it registers as a hiss.
            float contact = Envelope(t, 0.001f, 0.012f, 90.0f) *
                            (float)(random.NextDouble() * 2.0 - 1.0) * 0.35f;

            data[i] = body * 0.85f + contact;
        }
        return Finish(data, rate, "AtomixGrab", 0.34f);
    }

    /// <summary>Setting it down: the same gesture, higher and shorter, so the pair is a phrase.</summary>
    private static AudioClip BuildRelease()
    {
        float[] data = Allocate(0.13f, out int rate);
        System.Random random = new System.Random(4212);

        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            float frequency = Mathf.Lerp(230.0f, 175.0f, Mathf.Clamp01(t / 0.07f));
            float body = Envelope(t, 0.003f, 0.120f, 26.0f) * Mathf.Sin(2.0f * Mathf.PI * frequency * t);
            float contact = Envelope(t, 0.001f, 0.010f, 110.0f) *
                            (float)(random.NextDouble() * 2.0 - 1.0) * 0.30f;
            data[i] = body * 0.8f + contact;
        }
        return Finish(data, rate, "AtomixRelease", 0.30f);
    }

    /// <summary>
    /// The experiment worked. A rising D-F#-A-D arpeggio with bell partials.
    ///
    /// This is the single most important sound in the game and the one the project did not have.
    /// A student who gets the quantities right after four attempts should hear that they did.
    /// </summary>
    private static AudioClip BuildSuccess()
    {
        float[] data = Allocate(1.15f, out int rate);
        float[] notes = { D5, Fs5, A5, D6 };
        float[] starts = { 0.00f, 0.085f, 0.170f, 0.265f };

        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            float sample = 0.0f;

            for (int n = 0; n < notes.Length; n++)
            {
                float local = t - starts[n];
                if (local < 0.0f)
                {
                    continue;
                }

                // The last note rings longest; the passing ones get out of its way.
                float decay = n == notes.Length - 1 ? 2.4f : 5.5f;
                float amplitude = n == notes.Length - 1 ? 1.0f : 0.62f;
                sample += amplitude * Bell(notes[n], local, decay);
            }

            data[i] = sample * 0.34f;
        }
        return Finish(data, rate, "AtomixSuccess", 0.5f);
    }

    /// <summary>
    /// The experiment failed. Deliberately <b>not</b> a harsh buzzer: this is a teaching tool,
    /// failure is the expected path, and a student is going to hear it many times an hour.
    /// Two falling tones, warm and quiet - it says "that did not work", not "you are bad at this".
    /// </summary>
    private static AudioClip BuildFailure()
    {
        float[] data = Allocate(0.80f, out int rate);
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            float sample = 0.0f;

            sample += 0.85f * Bell(392.00f, t, 4.2f);                 // G4
            sample += 0.85f * Bell(311.13f, t - 0.16f, 2.6f);         // Eb4 - a minor third down

            // A slow tremolo on the tail keeps it from sounding like a broken note.
            sample *= 1.0f - 0.15f * Mathf.Sin(2.0f * Mathf.PI * 5.0f * t);
            data[i] = sample * 0.30f;
        }
        return Finish(data, rate, "AtomixFailure", 0.46f);
    }

    /// <summary>Two quick high notes. The testing scene pays out constantly, so keep it light.</summary>
    private static AudioClip BuildCoin()
    {
        float[] data = Allocate(0.30f, out int rate);
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            float sample = Bell(A5 * 2.0f, t, 14.0f) + 0.9f * Bell(D6 * 2.0f, t - 0.055f, 11.0f);
            data[i] = sample * 0.26f;
        }
        return Finish(data, rate, "AtomixCoin", 0.34f);
    }

    /// <summary>A milestone. The success shape, held longer and with a fifth underneath it.</summary>
    private static AudioClip BuildAchievement()
    {
        float[] data = Allocate(1.5f, out int rate);
        float[] notes = { D5, A5, D6 };
        float[] starts = { 0.00f, 0.11f, 0.22f };

        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            float sample = 0.0f;

            for (int n = 0; n < notes.Length; n++)
            {
                sample += Bell(notes[n], t - starts[n], n == 2 ? 1.9f : 4.0f);
            }

            sample += 0.35f * Bell(D5 * 0.5f, t, 1.6f);   // the octave below, for weight
            data[i] = sample * 0.26f;
        }
        return Finish(data, rate, "AtomixAchievement", 0.5f);
    }

    /// <summary>The bench has been cleared. A short two-note fall - "back to the start".</summary>
    private static AudioClip BuildReset()
    {
        float[] data = Allocate(0.40f, out int rate);
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate;
            float sample = Bell(A5, t, 9.0f) + 0.8f * Bell(D5, t - 0.075f, 6.5f);
            data[i] = sample * 0.24f;
        }
        return Finish(data, rate, "AtomixReset", 0.32f);
    }

    // =========================================================
    // BEDS
    // =========================================================

    /// <summary>
    /// The laboratory room tone: mains hum and extraction, eight seconds, looped.
    ///
    /// <b>This was rebuilt after being reported as sounding like a running tap.</b> That report
    /// was correct, and the cause was a design error rather than a tuning one.
    ///
    /// The first version layered a low hum over a bed of low-pass-filtered white noise, intended
    /// as "the wash of air a hard room has in it". But filtered broadband noise <i>is</i> what
    /// running water sounds like - a tap, a shower and a waterfall are all essentially shaped
    /// noise, and the ear identifies them by their spectrum, not by their context. Putting that
    /// under a scene containing a visible sink and a tap made the identification certain. Starting
    /// it on every scene load then put a tap on the title screen too.
    ///
    /// So the noise is gone entirely, and what is left is only what a laboratory's <i>machinery</i>
    /// sounds like: mains hum at 50 Hz with its harmonics, and the low beat of an extraction fan.
    /// Every partial is a whole number of cycles in the loop length, so the bed is periodic by
    /// construction - no cross-fade is needed and the seam is exact.
    ///
    /// It also now ships <b>off</b>. See <see cref="AtomixSettings.AmbienceVolume"/>.
    /// </summary>
    private static float[] GenerateLabAmbience(int rate)
    {
        const float lengthSeconds = 8.0f;
        int count = Mathf.RoundToInt(lengthSeconds * rate);

        float[] data = new float[count];

        // Every frequency is snapped to a multiple of this, which is what makes the loop exact.
        float fundamental = 1.0f / lengthSeconds;      // 0.125 Hz

        // Mains hum and its harmonics, plus the low throb of an extraction fan. Nothing above
        // 200 Hz: the moment there is broadband energy up in the speech band it starts to sound
        // like moving water again.
        float[] humHz = { 50.0f, 100.0f, 150.0f, 200.0f, 24.0f, 36.0f };
        float[] humGain = { 0.085f, 0.042f, 0.016f, 0.007f, 0.055f, 0.030f };

        for (int h = 0; h < humHz.Length; h++)
        {
            float frequency = Mathf.Round(humHz[h] / fundamental) * fundamental;
            float phase = h * 0.7f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                data[i] += humGain[h] * Mathf.Sin(2.0f * Mathf.PI * frequency * t + phase);
            }
        }

        // A slow swell, also on an exact period, so the bed breathes instead of sitting perfectly
        // still - a completely static tone is the other way to make ambience noticeable.
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float drift = 1.0f + 0.20f * Mathf.Sin(2.0f * Mathf.PI * fundamental * t) +
                                 0.09f * Mathf.Sin(2.0f * Mathf.PI * fundamental * 3.0f * t + 1.3f);
            data[i] *= drift;
        }

        return data;
    }

    /// <summary>
    /// The main menu pad: a slow, wide D major chord that breathes. Twelve seconds, looped.
    ///
    /// Every partial is an exact multiple of 1/12 Hz, so the loop is seamless without a
    /// cross-fade. It is a bed, not a tune - the menu should feel unhurried, and a melody in a
    /// screen a student sees forty times becomes irritating by the fifth.
    /// </summary>
    private static float[] GenerateMenuPad(int rate)
    {
        const float lengthSeconds = 12.0f;
        int count = Mathf.RoundToInt(lengthSeconds * rate);
        float fundamental = 1.0f / lengthSeconds;

        // D2 D3 A3 D4 F#4 A4 - a wide, open voicing.
        float[] voices = { 73.42f, 146.83f, 220.00f, 293.66f, 369.99f, 440.00f };
        float[] gains = { 0.30f, 0.26f, 0.18f, 0.16f, 0.11f, 0.09f };

        float[] data = new float[count];

        for (int v = 0; v < voices.Length; v++)
        {
            float frequency = Mathf.Round(voices[v] / fundamental) * fundamental;

            // A slight detune, also snapped, gives the chorus width a single sine cannot have.
            float detune = Mathf.Round((voices[v] * 1.004f) / fundamental) * fundamental;

            // Each voice swells on its own slow cycle, so the chord never sits still.
            float swellHz = fundamental * (v + 1);
            float phase = v * 1.1f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float swell = 0.55f + 0.45f * Mathf.Sin(2.0f * Mathf.PI * swellHz * t + phase);
                float tone = Mathf.Sin(2.0f * Mathf.PI * frequency * t) +
                             0.7f * Mathf.Sin(2.0f * Mathf.PI * detune * t + 0.6f);
                data[i] += gains[v] * swell * tone * 0.5f;
            }
        }

        return data;
    }

    /// <summary>
    /// A two-second looping pour, in two flavours.
    ///
    /// <paramref name="granular"/> true gives dry powder or solids landing in glass: a bed of
    /// bright filtered noise, chopped by fast random grain envelopes so it rattles rather than
    /// hisses. False gives a dropper: sparse, irregular droplet plinks over near-silence.
    ///
    /// Short enough to be cheap and long enough that the loop is not obvious, especially since
    /// <see cref="PourAudio"/> starts each voice at a random offset. The seam is handled the same
    /// way the room tone's is - the tail is equal-power cross-faded into the head, which on
    /// noise-based material is inaudible.
    /// </summary>
    public static AudioClip BuildPourLoop(bool granular)
    {
        const float lengthSeconds = 2.0f;
        int rate = SampleRate;
        int count = Mathf.RoundToInt(lengthSeconds * rate);
        int crossfade = Mathf.RoundToInt(0.15f * rate);

        float[] raw = new float[count + crossfade];
        System.Random random = new System.Random(granular ? 5150 : 5151);

        if (granular)
        {
            float highpass = 0.0f, previous = 0.0f, lowpass = 0.0f;
            float grainEnvelope = 0.0f;
            int grainRemaining = 0;

            for (int i = 0; i < raw.Length; i++)
            {
                // A new grain every 1-6 ms. This is what turns a hiss into individual particles
                // striking glass.
                if (grainRemaining <= 0)
                {
                    grainRemaining = random.Next(Mathf.Max(1, rate / 900), Mathf.Max(2, rate / 160));
                    grainEnvelope = 0.35f + (float)random.NextDouble() * 0.65f;
                }
                grainRemaining--;
                grainEnvelope *= 0.9986f;

                float noise = (float)(random.NextDouble() * 2.0 - 1.0);

                lowpass += (noise - lowpass) * 0.42f;          // take the very top off
                highpass = 0.92f * (highpass + lowpass - previous);
                previous = lowpass;

                raw[i] = highpass * grainEnvelope;
            }
        }
        else
        {
            // Droplets: a soft pitched blip every 120-380 ms, each a short decaying sine that
            // rises in pitch - which is what a drop hitting liquid actually does.
            int nextDrop = 0;
            float dropAge = 0.0f;
            float dropPitch = 900.0f;
            bool dropping = false;

            for (int i = 0; i < raw.Length; i++)
            {
                if (nextDrop <= 0)
                {
                    nextDrop = random.Next(Mathf.RoundToInt(0.12f * rate), Mathf.RoundToInt(0.38f * rate));
                    dropPitch = 700.0f + (float)random.NextDouble() * 600.0f;
                    dropAge = 0.0f;
                    dropping = true;
                }
                nextDrop--;

                if (!dropping)
                {
                    continue;
                }

                dropAge += 1.0f / rate;
                if (dropAge > 0.09f)
                {
                    dropping = false;
                    continue;
                }

                float frequency = dropPitch * (1.0f + dropAge * 9.0f);
                float envelope = Mathf.Exp(-42.0f * dropAge);
                raw[i] += envelope * Mathf.Sin(2.0f * Mathf.PI * frequency * dropAge);
            }
        }

        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = raw[i];
        }

        for (int i = 0; i < crossfade; i++)
        {
            float x = (float)i / crossfade;
            data[i] = data[i] * Mathf.Sqrt(x) + raw[count + i] * Mathf.Sqrt(1.0f - x);
        }

        return Finish(data, rate, granular ? "AtomixPourGranular" : "AtomixPourDroplets",
                      granular ? 0.30f : 0.34f, looping: true);
    }

    // =========================================================
    // PRIMITIVES
    // =========================================================

    private static float[] Allocate(float seconds, out int rate)
    {
        rate = SampleRate;
        return new float[Mathf.Max(1, Mathf.RoundToInt(seconds * rate))];
    }

    private static float Sine(float frequency, float t)
    {
        return Mathf.Sin(2.0f * Mathf.PI * frequency * t);
    }

    /// <summary>
    /// Attack-then-exponential-decay, clamped to a hard end so the clip never ends mid-swing.
    /// Returns 0 before the note starts, which is what lets cues be written as overlapping
    /// notes at offsets rather than as separate buffers spliced together.
    /// </summary>
    private static float Envelope(float t, float attack, float length, float decayRate)
    {
        if (t < 0.0f || t > length)
        {
            return 0.0f;
        }

        float attackGain = attack <= 0.0f ? 1.0f : Mathf.Clamp01(t / attack);
        float decay = Mathf.Exp(-decayRate * t);

        // Taper the last 15% to zero, so a clip cut short by its length does not click.
        float tail = Mathf.Clamp01((length - t) / (length * 0.15f));
        return attackGain * decay * tail;
    }

    /// <summary>
    /// A struck-bell tone: fundamental plus two inharmonic partials that die faster than it
    /// does. That decay difference is the whole trick - it is why this reads as a struck object
    /// and a plain sine reads as a test tone.
    /// </summary>
    private static float Bell(float frequency, float t, float decayRate)
    {
        if (t < 0.0f)
        {
            return 0.0f;
        }

        float attack = Mathf.Clamp01(t / 0.004f);
        float fundamental = Mathf.Exp(-decayRate * t) * Mathf.Sin(2.0f * Mathf.PI * frequency * t);
        float second = 0.42f * Mathf.Exp(-decayRate * 2.1f * t) *
                       Mathf.Sin(2.0f * Mathf.PI * frequency * 2.76f * t);
        float third = 0.18f * Mathf.Exp(-decayRate * 3.4f * t) *
                      Mathf.Sin(2.0f * Mathf.PI * frequency * 5.40f * t);

        return attack * (fundamental + second + third);
    }

    /// <summary>
    /// Normalises to <paramref name="peak"/>, applies a short fade at both ends, and hands back
    /// a clip. Normalising is what keeps the set balanced: the cues are written for shape, and
    /// their absolute level is decided here, once, in one place.
    /// </summary>
    private static AudioClip Finish(float[] data, int rate, string name, float peak,
                                    bool looping = false)
    {
        float loudest = 0.0f;
        for (int i = 0; i < data.Length; i++)
        {
            float magnitude = data[i] < 0.0f ? -data[i] : data[i];
            if (magnitude > loudest)
            {
                loudest = magnitude;
            }
        }

        if (loudest > 0.0001f)
        {
            float scale = peak / loudest;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= scale;
            }
        }

        // 2 ms of guard at each end, so a one-shot cannot click on its first or last sample.
        //
        // Never for a looping bed. The beds are built to join to themselves exactly - the hum
        // uses only frequencies that complete whole cycles in the loop length, and the noise is
        // cross-faded into its own head - and fading their ends to silence would throw all of
        // that away, replacing a seamless join with a 4 ms hole in the room tone every 8 seconds.
        if (looping)
        {
            return MakeClip(data, rate, name);
        }

        int guard = Mathf.Min(Mathf.RoundToInt(0.002f * rate), data.Length / 2);
        for (int i = 0; i < guard; i++)
        {
            float gain = (float)i / guard;
            data[i] *= gain;
            data[data.Length - 1 - i] *= gain;
        }

        return MakeClip(data, rate, name);
    }

    private static AudioClip MakeClip(float[] data, int rate, string name)
    {

        AudioClip clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
