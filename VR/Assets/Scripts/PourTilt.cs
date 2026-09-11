using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anything the reaction engine can be fed from: a vessel that is either pouring or not, and how
/// hard it is pouring right now. Implemented by every Pour* script.
/// </summary>
public interface IPourSource
{
    /// <summary>True while substance is actually leaving the vessel and landing in the target.</summary>
    bool IsPouring { get; }

    /// <summary>
    /// 0..1 - how fast it is pouring as a fraction of the vessel's full rate. Zero whenever
    /// <see cref="IsPouring"/> is false, and never below <see cref="PourTilt.TrickleFlow"/> while it
    /// is true, so a pour that has started always adds something.
    /// </summary>
    float FlowRate { get; }
}

/// <summary>
/// The one place that decides how far a vessel is tipped and how fast that makes it pour.
///
/// Every Pour* script used to carry its own copy of the same Euler-angle test: X between 45 and 90
/// degrees, or Y between 45 and 90 degrees, in either direction. That had two problems.
///
/// <list type="bullet">
/// <item>Euler Y is <i>yaw</i>. A container standing perfectly upright but turned a little on the
/// spot - with Q / E or the mouse wheel - counted as tipped, so spinning a beaker poured it. Most of
/// the solids containers rest at a yaw of exactly 90, one keypress away from that band.</item>
/// <item>Euler Z, the roll the C key applies, was never looked at, so rolling a vessel onto its
/// side never poured anything at all.</item>
/// </list>
///
/// The tilt is now the angle between the vessel's own up axis and world up. Every vessel in both
/// labs rests upright at the scene root with nothing but a yaw applied, so that angle is 0 at rest
/// on every one of them, and it is the same number whichever key did the tipping.
///
/// The pour starts at the same 45 degrees the old test used, but only trickles there; the rate
/// climbs with the tilt and reaches the vessel's full rate - exactly the rate every pour ran at
/// before - once the vessel is on its side. Finishing a pour on the mark is now a matter of easing
/// off, which is what it is at a real bench. Because the ceiling is unchanged, every target and
/// tolerance tuned against the old rates is still valid.
/// </summary>
public static class PourTilt
{
    /// <summary>Degrees from upright at which a vessel starts to pour. Unchanged from the old test.</summary>
    public const float PourStartAngle = 45.0f;

    /// <summary>Degrees from upright at which the pour reaches the vessel's full rate.</summary>
    public const float FullFlowAngle = 90.0f;

    /// <summary>Fraction of the full rate a vessel pours at when it has only just started to tip.</summary>
    public const float TrickleFlow = 0.15f;

    /// <summary>Angle in degrees between the vessel's up axis and world up. 0 upright, 180 inverted.</summary>
    public static float TiltAngle(Transform vessel)
    {
        return vessel == null ? 0.0f : Vector3.Angle(vessel.up, Vector3.up);
    }

    /// <summary>True once the vessel is tipped far enough to pour.</summary>
    public static bool IsTipped(float tiltDegrees)
    {
        return tiltDegrees >= PourStartAngle;
    }

    /// <summary>
    /// 0 while the vessel is not tipped far enough to pour; otherwise from
    /// <see cref="TrickleFlow"/> at <see cref="PourStartAngle"/> up to 1 at
    /// <see cref="FullFlowAngle"/> and beyond.
    /// </summary>
    public static float FlowFactor(float tiltDegrees)
    {
        if (!IsTipped(tiltDegrees))
        {
            return 0.0f;
        }

        return Mathf.Lerp(TrickleFlow, 1.0f, Mathf.InverseLerp(PourStartAngle, FullFlowAngle, tiltDegrees));
    }

    /// <summary>
    /// True when the source exists. Unity-aware, so a Pour* component that has been destroyed with
    /// its scene reads as absent rather than as a live reference to a dead object.
    /// </summary>
    public static bool IsLive(IPourSource source)
    {
        if (source == null)
        {
            return false;
        }

        Object unityObject = source as Object;
        return ReferenceEquals(unityObject, null) || unityObject != null;
    }

    // =========================================================
    // PRESENTATION
    // =========================================================
    //
    // The pour sounds and streams are shared: one liquid stream particle system is driven by six
    // different Pour* scripts, and one AudioSource by seven. So the untouched value of each is
    // captured the first time any script scales it, and every script puts that exact value back
    // when its own pour stops. Scaling relative to a per-script copy instead would compound as soon
    // as a second vessel poured through the same effect.

    private static readonly Dictionary<int, float> baseEmission = new Dictionary<int, float>();
    private static readonly Dictionary<int, float> baseVolume = new Dictionary<int, float>();

    /// <summary>Particle emission multiplier for a flow - a trickle is thin, but still visible.</summary>
    public static float VisualScale(float flow)
    {
        return Mathf.Lerp(0.3f, 1.0f, Mathf.Clamp01(flow));
    }

    /// <summary>Volume multiplier for a flow - a trickle is quiet, but still audible.</summary>
    public static float AudioScale(float flow)
    {
        return Mathf.Lerp(0.35f, 1.0f, Mathf.Clamp01(flow));
    }

    /// <summary>Scales a pour stream's emission to the current flow.</summary>
    public static void ScaleEmission(ParticleSystem stream, float flow)
    {
        if (stream == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = stream.emission;
        int id = stream.GetInstanceID();

        float rate;
        if (!baseEmission.TryGetValue(id, out rate))
        {
            rate = emission.rateOverTimeMultiplier;
            baseEmission[id] = rate;
        }

        emission.rateOverTimeMultiplier = rate * VisualScale(flow);
    }

    /// <summary>Puts a pour stream's emission back exactly as it was authored.</summary>
    public static void RestoreEmission(ParticleSystem stream)
    {
        if (stream == null)
        {
            return;
        }

        float rate;
        if (baseEmission.TryGetValue(stream.GetInstanceID(), out rate))
        {
            ParticleSystem.EmissionModule emission = stream.emission;
            emission.rateOverTimeMultiplier = rate;
        }
    }

    /// <summary>Scales a pour sound's volume to the current flow.</summary>
    public static void ScaleVolume(AudioSource source, float flow)
    {
        if (source == null)
        {
            return;
        }

        int id = source.GetInstanceID();

        float volume;
        if (!baseVolume.TryGetValue(id, out volume))
        {
            volume = source.volume;
            baseVolume[id] = volume;
        }

        source.volume = volume * AudioScale(flow);
    }

    /// <summary>Puts a pour sound's volume back exactly as it was authored.</summary>
    public static void RestoreVolume(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        float volume;
        if (baseVolume.TryGetValue(source.GetInstanceID(), out volume))
        {
            source.volume = volume;
        }
    }
}
