using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PourFromPipette : MonoBehaviour, IPourSource
{
    public GameObject pipette;
    public GameObject crystallizing_dish;
    public ParticleSystem substanceLeak;
    public GameObject substanceLeakObject;
    private bool play = false;
    private GameObject pivot;
    public bool containsWater = false;
    // Exposed for FreeHandReactionEngine quantity tracking (containsWater behaviour unchanged).
    public bool IsPouring { get { return play; } }

    /// <summary>
    /// A dropper is not tipped - it releases drops at one rate whenever the tip is over the dish -
    /// so it is always at its full rate while it is dispensing.
    /// </summary>
    public float FlowRate { get { return play ? 1.0f : 0.0f; } }

    public GameObject waterInPipette;
    [SerializeField] FillPipette filledPipette;

    [Header("Dispensing")]
    [Tooltip("How far the pipette tip may sit from the dish centre (horizontally) and still drip into it.")]
    public float dishRadius = 0.14f;
    [Tooltip("How high above the dish the tip may be and still drip into it.")]
    public float maxDripHeight = 0.45f;
    [Tooltip("Seconds of dripping a full pipette is worth before it runs empty.")]
    public float dispenseSeconds = 3.0f;

    private float dispensedSeconds = 0.0f;

    /// <summary>True while the pipette still has liquid to give.</summary>
    public bool IsFull { get { return filledPipette != null && filledPipette.pipetteIsFull; } }

    /// <summary>0..1 - how much of the current pipette load has been dispensed.</summary>
    public float DispenseProgress
    {
        get { return Mathf.Clamp01(dispensedSeconds / Mathf.Max(0.01f, dispenseSeconds)); }
    }

    /// <summary>True when the tip is positioned over the dish, whether or not it is full.</summary>
    public bool IsOverDish { get; private set; }

    void Start()
    {
        if (substanceLeakObject != null)
        {
            substanceLeakObject.SetActive(false);
        }
        if (substanceLeak != null)
        {
            substanceLeak.Stop();
            substanceLeak.Clear();
        }

        if (pipette != null)
        {
            Transform tip = pipette.transform.Find("pivott");
            pivot = tip != null ? tip.gameObject : pipette;
            if (tip == null)
            {
                Debug.LogWarning("PourFromPipette: no 'pivott' child on the pipette - falling back to the pipette root.");
            }
        }
    }

    void Update()
    {
        if (pipette == null || crystallizing_dish == null || pivot == null)
        {
            return;
        }

        Vector3 tipPosition = pivot.transform.position;
        Vector3 dishPosition = crystallizing_dish.transform.position;

        // Judge from the pipette TIP, not the pipette root - the root can sit well off to the
        // side while the tip is right over the dish, which is what the user actually aims.
        float horizontal = new Vector2(tipPosition.x - dishPosition.x, tipPosition.z - dishPosition.z).magnitude;
        float height = tipPosition.y - dishPosition.y;
        IsOverDish = horizontal <= dishRadius && height > 0.0f && height <= maxDripHeight;

        bool canDispense = filledPipette != null && filledPipette.pipetteIsFull && IsOverDish;

        if (canDispense)
        {
            if (substanceLeak != null)
            {
                substanceLeak.transform.position = tipPosition;
            }

            if (!play)
            {
                if (substanceLeakObject != null)
                {
                    substanceLeakObject.SetActive(true);
                }
                if (substanceLeak != null)
                {
                    substanceLeak.Clear();
                    substanceLeak.Play();
                }
                containsWater = true;
                play = true;
            }

            // Accumulated rather than wall-clock, so briefly wobbling out of the zone pauses the
            // pipette instead of silently handing the user a whole fresh 3 seconds of water.
            dispensedSeconds += Time.deltaTime;
            if (dispensedSeconds >= dispenseSeconds)
            {
                EmptyPipette();
            }
        }
        else if (play)
        {
            StopDripping();
        }
    }

    void StopDripping()
    {
        if (substanceLeak != null)
        {
            substanceLeak.Stop();
            substanceLeak.Clear();
        }
        play = false;
        if (substanceLeakObject != null)
        {
            substanceLeakObject.SetActive(false);
        }
    }

    void EmptyPipette()
    {
        if (waterInPipette != null)
        {
            waterInPipette.SetActive(false);
        }
        if (filledPipette != null)
        {
            filledPipette.pipetteIsFull = false;
        }
        dispensedSeconds = 0.0f;
        StopDripping();
    }
}
