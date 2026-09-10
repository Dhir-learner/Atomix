using System.Collections;
using UnityEngine;

public class RotateButton : MonoBehaviour
{
    public GameObject rotatingButton;
    public ParticleSystem waterLeak;
    public AudioSource audioSource;
    public AudioClip clip;

    [Header("Actuation")]
    [Tooltip("Movement of the control is what operates it. The latch ignores movement while the " +
             "scene is still assembling itself, so being placed by the desktop rig cannot " +
             "actuate it.")]
    public MovementLatch movement = new MovementLatch();

    private bool isPlaying = false;
    private bool play = false;


    void OnEnable()
    {
        // Re-armed on every activation, not just the first. Both labs switch equipment off until
        // its experiment is chosen, and whatever places the object when it comes back would
        // otherwise land outside the window opened by Start and actuate it.
        movement.Begin(rotatingButton != null ? rotatingButton.transform : null);
    }

    void Start()
    {
        if (waterLeak != null)
        {
            waterLeak.Stop();
        }

        play = false;
        movement.Begin(rotatingButton != null ? rotatingButton.transform : null);
    }

    void Update()
    {
        if (movement.StoppedThisFrame(rotatingButton != null ? rotatingButton.transform : null))
        {
            ToggleWaterFlow();
        }

        if (play)
        {
            waterLeak.Play();
            if (!isPlaying)
            {
                StartCoroutine(PlaySoundRepeatedly());
            }
        }
        else
        {
            waterLeak.Stop();
            isPlaying = false;
            audioSource.Stop();
        }
    }

    public void ToggleWaterFlow()
    {
        play = !play;
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
