using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FillPipette : MonoBehaviour
{
    public GameObject pipette;
    public GameObject liquidInPipette;
    public GameObject glassWithWater;
    public GameObject pivotErlenmeyer;
    public GameObject pivotPipette;

    public bool pipetteIsFull = false;

    [Header("Filling")]
    [Tooltip("How close the pipette tip must get to the water flask mouth to draw liquid up. " +
             "The old build used a 5 cm box on every axis, which was almost impossible to hit while carrying the pipette.")]
    public float fillDistance = 0.18f;
    [Tooltip("Extra vertical slack - the tip may sit this far above the flask pivot and still fill.")]
    public float fillHeightSlack = 0.12f;

    /// <summary>True while the pipette is holding liquid. Mirrors <see cref="pipetteIsFull"/>.</summary>
    public bool IsFull { get { return pipetteIsFull; } }

    /// <summary>How close the tip currently is to the flask - lets the UI nudge the user.</summary>
    public float DistanceToWater { get; private set; }

    /// <summary>True when the tip is inside the fill zone (whether or not it is already full).</summary>
    public bool IsInFillZone { get; private set; }

    void Start()
    {
        if (liquidInPipette != null)
        {
            liquidInPipette.SetActive(false);
        }

        if (pipette != null)
        {
            Transform tip = pipette.transform.Find("pivott");
            if (tip != null)
            {
                pivotPipette = tip.gameObject;
            }
        }

        if (glassWithWater != null)
        {
            Transform mouth = glassWithWater.transform.Find("Pivot");
            if (mouth != null)
            {
                pivotErlenmeyer = mouth.gameObject;
            }
        }

        // Fall back to the parent objects so a renamed child can never dead-lock the experiment.
        if (pivotPipette == null && pipette != null)
        {
            pivotPipette = pipette;
            Debug.LogWarning("FillPipette: no 'pivott' child on the pipette - falling back to the pipette root.");
        }
        if (pivotErlenmeyer == null && glassWithWater != null)
        {
            pivotErlenmeyer = glassWithWater;
            Debug.LogWarning("FillPipette: no 'Pivot' child on the water flask - falling back to the flask root.");
        }
    }

    void Update()
    {
        if (pivotPipette == null || pivotErlenmeyer == null)
        {
            return;
        }

        Vector3 tip = pivotPipette.transform.position;
        Vector3 mouth = pivotErlenmeyer.transform.position;

        DistanceToWater = Vector3.Distance(tip, mouth);

        // A single sphere around the flask mouth, with a little extra room upwards so that
        // holding the pipette just over the neck still counts as dipping it in.
        float horizontal = new Vector2(tip.x - mouth.x, tip.z - mouth.z).magnitude;
        float verticalGap = tip.y - mouth.y;

        IsInFillZone = horizontal <= fillDistance &&
                       verticalGap <= fillHeightSlack &&
                       verticalGap >= -fillDistance;

        if (!IsInFillZone || pipetteIsFull)
        {
            return;
        }

        if (liquidInPipette != null)
        {
            liquidInPipette.SetActive(true);
        }
        pipetteIsFull = true;
    }
}
