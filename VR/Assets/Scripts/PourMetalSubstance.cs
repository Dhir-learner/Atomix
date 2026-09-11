using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PourMetalSubstance : MonoBehaviour, IPourSource
{
    public GameObject Container;
    public GameObject SecondGlass;
    public ParticleSystem substanceLeak;
    private bool play = false;
    private float flow = 0.0f;
    private GameObject StartForSubstanceLeak;
    public bool containsNatrium = false;
    public bool IsPouring => play;

    /// <summary>How hard the vessel is pouring, 0..1 of its full rate. See <see cref="PourTilt"/>.</summary>
    public float FlowRate => play ? flow : 0.0f;

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
                containsNatrium = true;
                play = true;
            }
            PourTilt.ScaleEmission(substanceLeak, flow);
        }
        else
        {
            if (play)
            {
                substanceLeak.Stop();
                substanceLeak.Clear(); // Clear existing particles when stopping
                PourTilt.RestoreEmission(substanceLeak);
                play = false;
            }
        }
    }
}
