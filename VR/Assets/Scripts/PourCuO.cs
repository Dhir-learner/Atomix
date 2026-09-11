using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PourCuO : MonoBehaviour, IPourSource
{
    public GameObject Container;
    public GameObject SecondGlass;
    public ParticleSystem substanceLeak;
    public AudioSource audioSource;
    public AudioClip clip;
    public bool containsCuO = false;
    // Exposed for FreeHandReactionEngine quantity tracking (containsCuO behaviour unchanged).
    public bool IsPouring { get { return play; } }

    /// <summary>How hard the vessel is pouring, 0..1 of its full rate. See <see cref="PourTilt"/>.</summary>
    public float FlowRate { get { return play ? flow : 0.0f; } }

    private bool play = false;
    private bool isPlaying = false;
    private float flow = 0.0f;
    private GameObject StartForSubstanceLeak;

    void Start()
    {
        // Leak particles are authored with playOnAwake, so silence them until we pour.
        substanceLeak.Stop();
        substanceLeak.Clear();
        Quaternion firstGlassRotation = Container.transform.rotation;
        StartForSubstanceLeak = Container.transform.Find("pivott").gameObject;
        Debug.Log(StartForSubstanceLeak);
    }

    void Update()
    {
        Vector3 firstGlassPosition = Container.transform.position;
        Vector3 secondGlassPosition = SecondGlass.transform.position;
        Vector3 pivotPosition = StartForSubstanceLeak.transform.position;
        float tilt = PourTilt.TiltAngle(Container.transform);
        if (PourTilt.IsTipped(tilt)
            && (
                firstGlassPosition.y > secondGlassPosition.y &&
                pivotPosition.x <= secondGlassPosition.x + 0.15 && pivotPosition.x >= secondGlassPosition.x - 0.15 &&
                pivotPosition.z <= secondGlassPosition.z + 0.15 && pivotPosition.z >= secondGlassPosition.z - 0.15
                )
            )
        {
            flow = PourTilt.FlowFactor(tilt);
            substanceLeak.transform.position = pivotPosition;
            if (!play)
            {
                substanceLeak.Clear(); // Clear existing particles before replaying
                substanceLeak.Play();
                containsCuO = true;
                play = true;
                if (!isPlaying)
                {
                    StartCoroutine(PlaySoundRepeatedly());
                }
            }
            PourTilt.ScaleEmission(substanceLeak, flow);
            PourTilt.ScaleVolume(audioSource, flow);
        }
        else
        {
            if (play)
            {
                substanceLeak.Stop();
                substanceLeak.Clear(); // Clear existing particles when stopping
                PourTilt.RestoreEmission(substanceLeak);
                PourTilt.RestoreVolume(audioSource);
                play = false;
                isPlaying = false;
                audioSource.Stop();
            }
        }
    }

    IEnumerator PlaySoundRepeatedly()
    {
        isPlaying = true;
        while (isPlaying)
        {
            audioSource.PlayOneShot(clip);
            yield return new WaitForSeconds(clip.length);
        }
    }
}
