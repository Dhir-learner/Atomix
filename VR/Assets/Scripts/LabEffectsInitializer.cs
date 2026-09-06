using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Silences every reaction effect at the start of a scene.
///
/// The leak, fume and explosion particle systems in the lab are all authored with
/// playOnAwake + looping, and each pour script is supposed to stop its own in Start(). That is not
/// enough on its own: ControlReactions deactivates most of the recipient GameObjects at startup, so
/// those scripts' Start() never runs, while the particle systems they point at are separate scene
/// objects that stay active and keep emitting. The result is water pouring from the tap - and
/// powder trickling out of containers - from the moment the game opens.
///
/// This sweeps them all once, a frame after each scene loads, whether or not the owning script ever
/// woke up. It only touches particle systems the reaction scripts actually reference, so nothing
/// else in the scene is affected.
/// </summary>
public class LabEffectsInitializer : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<LabEffectsInitializer>() != null)
        {
            return;
        }

        GameObject host = new GameObject("LabEffectsInitializer");
        host.AddComponent<LabEffectsInitializer>();
        DontDestroyOnLoad(host);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(SilenceNextFrame());
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(SilenceNextFrame());
    }

    /// <summary>Waits a frame so the pour scripts that DO run have already had their Start().</summary>
    private IEnumerator SilenceNextFrame()
    {
        yield return null;
        SilenceReactionEffects();
    }

    public static void SilenceReactionEffects()
    {
        List<ParticleSystem> effects = new List<ParticleSystem>();

        // Liquid pours
        foreach (PourSubstance c in FindAll<PourSubstance>()) { Add(effects, c.waterLeak); }
        foreach (PourHCL c in FindAll<PourHCL>()) { Add(effects, c.waterLeak); }
        foreach (PourH2so4 c in FindAll<PourH2so4>()) { Add(effects, c.waterLeak); }
        foreach (PourPhenolphthalein c in FindAll<PourPhenolphthalein>()) { Add(effects, c.waterLeak); }

        // Solids and droppers
        foreach (PourCuO c in FindAll<PourCuO>()) { Add(effects, c.substanceLeak); }
        foreach (PourNahco3 c in FindAll<PourNahco3>()) { Add(effects, c.substanceLeak); }
        foreach (PourMetalSubstance c in FindAll<PourMetalSubstance>()) { Add(effects, c.substanceLeak); }
        foreach (PourFromPipette c in FindAll<PourFromPipette>()) { Add(effects, c.substanceLeak); }

        // Reaction effects
        foreach (Reaction c in FindAll<Reaction>()) { Add(effects, c.explosion); }
        foreach (Reaction_hcl_nahco3 c in FindAll<Reaction_hcl_nahco3>()) { Add(effects, c.explosion); }
        foreach (KOHReaction c in FindAll<KOHReaction>()) { Add(effects, c.explosion); }
        foreach (reactionCaOH c in FindAll<reactionCaOH>()) { Add(effects, c.explosion); }
        foreach (ReactionAli3 c in FindAll<ReactionAli3>())
        {
            Add(effects, c.explosion1);
            Add(effects, c.explosion2);
            Add(effects, c.explosion3);
        }

        for (int i = 0; i < effects.Count; i++)
        {
            effects[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effects[i].Clear(true);
        }
    }

    private static T[] FindAll<T>() where T : Object
    {
        // Inactive included on purpose: the scripts whose GameObject is switched off are exactly
        // the ones that never got to stop their own particles.
        return FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private static void Add(List<ParticleSystem> target, ParticleSystem effect)
    {
        if (effect != null && !target.Contains(effect))
        {
            target.Add(effect);
        }
    }
}
