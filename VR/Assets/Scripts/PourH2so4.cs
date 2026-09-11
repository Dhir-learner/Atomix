using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PourH2so4 : MonoBehaviour, IPourSource
{
    public GameObject FirstGlass;
    public GameObject SecondGlass;
    public ParticleSystem waterLeak;
    public AudioSource audioSource;
    public AudioClip clip;
    public bool containsHCL = false;
    // Exposed for FreeHandReactionEngine quantity tracking (containsHCL behaviour unchanged).
    public bool IsPouring { get { return play; } }

    /// <summary>How hard the vessel is pouring, 0..1 of its full rate. See <see cref="PourTilt"/>.</summary>
    public float FlowRate { get { return play ? flow : 0.0f; } }

    private bool play = false;
    private bool isPlaying = false;
    private float flow = 0.0f;
    private GameObject substance;
    private GameObject StartForSubstanceLeak;
    private Renderer firstGlassSubstanceRenderer;
    private Renderer secondGlassSubstanceRenderer;

    void Start()
    {
        waterLeak.Stop();
        Quaternion firstGlassRotation = FirstGlass.transform.rotation;
        StartForSubstanceLeak = FirstGlass.transform.Find("Pivot").gameObject;
        substance = SecondGlass.transform.Find("Substance").gameObject;
        substance.SetActive(false);
        firstGlassSubstanceRenderer = FirstGlass.transform.Find("Substance").gameObject.GetComponent<Renderer>();
        secondGlassSubstanceRenderer = substance.GetComponent<Renderer>();
    }

    void Update()
    {
        Vector3 firstGlassPosition = FirstGlass.transform.position;
        Vector3 secondGlassPosition = SecondGlass.transform.position;
        Vector3 pivotPosition = StartForSubstanceLeak.transform.position;
        float tilt = PourTilt.TiltAngle(FirstGlass.transform);
        if (PourTilt.IsTipped(tilt)
            && (
                firstGlassPosition.y > secondGlassPosition.y &&
                pivotPosition.x <= secondGlassPosition.x + 0.15 && pivotPosition.x >= secondGlassPosition.x - 0.15 &&
                pivotPosition.z <= secondGlassPosition.z + 0.15 && pivotPosition.z >= secondGlassPosition.z - 0.15
                )
            )
        {
            flow = PourTilt.FlowFactor(tilt);
            waterLeak.transform.position = pivotPosition;
            waterLeak.Play();
            PourTilt.ScaleEmission(waterLeak, flow);
            PourTilt.ScaleVolume(audioSource, flow);
            secondGlassSubstanceRenderer.material = firstGlassSubstanceRenderer.material;
            substance.SetActive(true);
            play = true;
            containsHCL = true;
            if (!isPlaying)
            {
                StartCoroutine(PlaySoundRepeatedly());
            }
        }
        else
        {
            if (play)
            {
                waterLeak.Stop();
                PourTilt.RestoreEmission(waterLeak);
                PourTilt.RestoreVolume(audioSource);
                play = false;
                isPlaying = false;
                audioSource.Stop();
            //    substance.SetActive(false);
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
