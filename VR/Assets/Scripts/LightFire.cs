using System.Collections;
using UnityEngine;

public class LightFire : MonoBehaviour
{
    public GameObject burnerSupport;
    public GameObject fireAnimation;
    public AudioSource audioSource;
    public AudioClip clip;

    [Header("Actuation")]
    [Tooltip("Movement of the control is what operates it. The latch ignores movement while the " +
             "scene is still assembling itself, so being placed by the desktop rig cannot " +
             "actuate it.")]
    public MovementLatch movement = new MovementLatch();

    private bool isPlaying = false;
    private bool play = false;

    public bool esteAprins = false;

    void Awake()
    {
        // Awake, not Start. The flame's particle system is authored active with playOnAwake set,
        // so it is already emitting by the time anything else runs - and if ControlReactions has
        // this burner switched off at load, Start never runs at all and the flame simply stays
        // lit. Putting it out here covers both.
        if (fireAnimation != null)
        {
            fireAnimation.SetActive(false);
        }
    }


    void OnEnable()
    {
        // Re-armed on every activation, not just the first. Both labs switch equipment off until
        // its experiment is chosen, and whatever places the object when it comes back would
        // otherwise land outside the window opened by Start and actuate it.
        movement.Begin(burnerSupport != null ? burnerSupport.transform : null);
    }

    void Start()
    {
        if (fireAnimation != null)
        {
            fireAnimation.SetActive(false);
        }

        esteAprins = false;
        play = false;

        movement.Begin(burnerSupport != null ? burnerSupport.transform : null);
    }

    void Update()
    {
        if (movement.StoppedThisFrame(burnerSupport != null ? burnerSupport.transform : null))
        {
            ToggleFire();
        }

        if (play)
        {
            esteAprins = true;
            if (fireAnimation != null)
            {
                fireAnimation.SetActive(true);
            }
            if (!isPlaying)
            {
                StartCoroutine(PlaySoundRepeatedly());
            }
        }
        else
        {
            esteAprins = false;
            if (fireAnimation != null)
            {
                fireAnimation.SetActive(false);
            }
            isPlaying = false;
            if (audioSource != null)
            {
                audioSource.Stop();
            }
        }
    }

    public void ToggleFire()
    {
        play = !play;
    }

    IEnumerator PlaySoundRepeatedly()
    {
        if (audioSource == null || clip == null)
        {
            yield break;
        }

        isPlaying = true;
        while (isPlaying)
        {
            audioSource.PlayOneShot(clip);
            yield return new WaitForSeconds(clip.length);
        }
    }
}
