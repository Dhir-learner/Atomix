using System.Collections;
using UnityEngine;

public class RotateButton : MonoBehaviour
{
    public GameObject rotatingButton;
    public ParticleSystem waterLeak;
    public AudioSource audioSource;
    public AudioClip clip;

    private bool isPlaying = false;
    private bool play = false;
    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private bool wasMoving = false;

    void Start()
    {
        waterLeak.Stop();

        if (rotatingButton != null)
        {
            previousPosition = rotatingButton.transform.position;
            previousRotation = rotatingButton.transform.rotation;
        }
    }

    void Update()
    {
        if (MovementStoppedThisFrame())
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

    bool MovementStoppedThisFrame()
    {
        if (rotatingButton == null)
        {
            return false;
        }

        Transform buttonTransform = rotatingButton.transform;
        bool isMoving = Vector3.Distance(previousPosition, buttonTransform.position) > 0.0005f ||
                        Quaternion.Angle(previousRotation, buttonTransform.rotation) > 0.1f;

        bool stoppedThisFrame = wasMoving && !isMoving;
        previousPosition = buttonTransform.position;
        previousRotation = buttonTransform.rotation;
        wasMoving = isMoving;
        return stoppedThisFrame;
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
