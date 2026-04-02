using System.Collections;
using UnityEngine;

public class LightFire : MonoBehaviour
{
    public GameObject burnerSupport;
    public GameObject fireAnimation;
    public AudioSource audioSource;
    public AudioClip clip;

    private bool isPlaying = false;
    private bool play = false;

    public bool esteAprins = false;

    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private bool wasMoving = false;

    void Start()
    {
        fireAnimation.SetActive(false);
        esteAprins = false;

        if (burnerSupport != null)
        {
            previousPosition = burnerSupport.transform.position;
            previousRotation = burnerSupport.transform.rotation;
        }
    }

    void Update()
    {
        if (MovementStoppedThisFrame())
        {
            ToggleFire();
        }

        if (play)
        {
            esteAprins = true;
            fireAnimation.SetActive(true);
            if (!isPlaying)
            {
                StartCoroutine(PlaySoundRepeatedly());
            }
        }
        else
        {
            esteAprins = false;
            fireAnimation.SetActive(false);
            isPlaying = false;
            audioSource.Stop();
        }
    }

    bool MovementStoppedThisFrame()
    {
        if (burnerSupport == null)
        {
            return false;
        }

        Transform supportTransform = burnerSupport.transform;
        bool isMoving = Vector3.Distance(previousPosition, supportTransform.position) > 0.0005f ||
                        Quaternion.Angle(previousRotation, supportTransform.rotation) > 0.1f;

        bool stoppedThisFrame = wasMoving && !isMoving;
        previousPosition = supportTransform.position;
        previousRotation = supportTransform.rotation;
        wasMoving = isMoving;
        return stoppedThisFrame;
    }

    public void ToggleFire()
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
