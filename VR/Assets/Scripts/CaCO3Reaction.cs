using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CaCO3Reaction : MonoBehaviour
{
    public GameObject pivotEprubeta;
    public GameObject pivotFoc;
    [SerializeField] LightFire foc;
    [SerializeField] MeshRendererScript balloon;
    public GameObject fume;
    public GameObject balon;

    public GameObject label;
    public Material CaO_material;

    public TMP_Text canvasText;
    public GameObject popupWindow;

    public AudioSource audioSource_guidance;
    public AudioClip clip_guidance1;
    public AudioClip clip_guidance2;
    public AudioClip clip_guidance3;

    private float targetPoint;
    private Vector3 initialScale;
    private Vector3 finalScale;
    private Vector3 initialPosition;
    private Vector3 finalPosition;
    private bool reactionCompleted = false;

    private bool audioSource1Started = false;
    private bool audioSource2Started = false;
    private bool audioSource3Started = false;
    private bool balon_ok = false;
    private bool balloonSnapped = false;
    private Transform snappedBalloonTransform;
    [SerializeField] private float desktopBalloonSnapDistance = 0.35f;
    [SerializeField] private float alreadyConnectedDistance = 0.08f;
    [SerializeField] private Vector3 desktopBalloonSnapOffset = Vector3.zero;
    [SerializeField] private Vector3 desktopBalloonSnapEulerOffset = Vector3.zero;

    void Start()
    {
        fume.SetActive(false);
        InitializeBalloonInflationTargets();
        RefreshConnectedStateFromPlacement();
    }

    void Update()
    {
        TrySnapBalloonToSocket();
        RefreshConnectedStateFromPlacement();

        if (balloonSnapped)
        {
            if (audioSource1Started == false)
            {
                canvasText.text = "You have successfully attached the balloon! Now you can light the Bunsen burner by pressing the white button on the burner with the grep button.";
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance1);
                audioSource1Started = true;
                balon_ok = true;
            }
        }
        if(balon_ok == true && foc.esteAprins == true)
        {
            if(audioSource2Started == false)
            {
                canvasText.text = "You have lit the Bunsen burner successfully! Now you can hold the CaCO3 test tube over the flame and observe the balloon inflating.";
                audioSource_guidance.Stop();
                audioSource_guidance.PlayOneShot(clip_guidance2);
                audioSource2Started = true;
            }
        }
        if (!reactionCompleted && 
            Math.Abs(pivotFoc.transform.position.z - pivotEprubeta.transform.position.z) < 0.05 &&
            Math.Abs(pivotFoc.transform.position.x - pivotEprubeta.transform.position.x) < 0.05 &&
            Math.Abs(pivotFoc.transform.position.y - pivotEprubeta.transform.position.y) < 0.1 && foc.esteAprins == true
            )
        {
            fume.SetActive(true);
            if (balon_ok)
            {
                targetPoint += Time.deltaTime / 10;
                balon.transform.localScale = Vector3.Lerp(initialScale, finalScale, targetPoint);
                balon.transform.localPosition = Vector3.Lerp(initialPosition, finalPosition, targetPoint);
                if (targetPoint >= 1f)
                {
                    targetPoint = 1f;
                    reactionCompleted = true;
                    if (targetPoint >= 1f)
                    {
                        targetPoint = 1f;
                        label.GetComponent<Renderer>().material = CaO_material;
                        if (!audioSource3Started)
                        {
                            canvasText.text = "Chemical reaction equation: CaCO3 = CaO + CO2. Now you can put the test tube with CaO on the support, close the burner and learn another reaction.";
                            audioSource_guidance.Stop();
                            audioSource_guidance.PlayOneShot(clip_guidance3);
                            audioSource3Started = true;
                        }
                        fume.SetActive(false);
                    }
                }
            }
        }
        else
        {
            fume.SetActive(false);
        }
    }

    void LateUpdate()
    {
        MaintainBalloonSnapPose();
    }

    void TrySnapBalloonToSocket()
    {
        if (balloon == null || balon == null || balloonSnapped)
        {
            return;
        }

        if (Vector3.Distance(balon.transform.position, balloon.transform.position) > desktopBalloonSnapDistance)
        {
            return;
        }

        GameObject balloonRoot = ResolveBalloonRoot();

        ObjectInteraction.ReleaseIfHolding(balloonRoot);
        if (balloonRoot != balon)
        {
            ObjectInteraction.ReleaseIfHolding(balon);
        }

        FinalizeBalloonSnap(balloonRoot);
    }

    void RefreshConnectedStateFromPlacement()
    {
        if (balloonSnapped || balloon == null || balon == null)
        {
            return;
        }

        if (Vector3.Distance(balon.transform.position, balloon.transform.position) > alreadyConnectedDistance)
        {
            return;
        }

        FinalizeBalloonSnap(ResolveBalloonRoot());
    }

    GameObject ResolveBalloonRoot()
    {
        if (balon == null)
        {
            return null;
        }

        ObjectGrabbable grabbable = balon.GetComponentInParent<ObjectGrabbable>();
        if (grabbable != null)
        {
            return grabbable.gameObject;
        }

        Rigidbody rb = balon.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            return rb.gameObject;
        }

        return balon;
    }

    void InitializeBalloonInflationTargets()
    {
        if (balon == null)
        {
            return;
        }

        initialScale = balon.transform.localScale;
        finalScale = initialScale * 17f / 10f;
        initialPosition = balon.transform.localPosition;
        finalPosition = new Vector3(initialPosition.x, initialPosition.y - 0.35f, initialPosition.z);
    }

    void FinalizeBalloonSnap(GameObject balloonRoot)
    {
        if (balloonRoot == null || balloon == null)
        {
            return;
        }

        Rigidbody balloonRigidbody = balloonRoot.GetComponent<Rigidbody>();
        if (balloonRigidbody != null)
        {
            balloonRigidbody.linearVelocity = Vector3.zero;
            balloonRigidbody.angularVelocity = Vector3.zero;
            balloonRigidbody.useGravity = false;
            balloonRigidbody.isKinematic = true;
        }

        ObjectGrabbable grabbable = balloonRoot.GetComponent<ObjectGrabbable>();
        if (grabbable != null)
        {
            grabbable.SetGrabbable(false);
        }

        snappedBalloonTransform = balloonRoot.transform;
        snappedBalloonTransform.SetParent(null, true);
        AlignBalloonRootToSocket(balloonRoot);

        InitializeBalloonInflationTargets();
        balloon.Enable();
        balloonSnapped = true;
        balon_ok = true;
    }

    void AlignBalloonRootToSocket(GameObject balloonRoot)
    {
        if (balloonRoot == null || balloon == null)
        {
            return;
        }

        balloonRoot.transform.position = balloon.transform.position + (balloon.transform.rotation * desktopBalloonSnapOffset);
        balloonRoot.transform.rotation = balloon.transform.rotation * Quaternion.Euler(desktopBalloonSnapEulerOffset);
    }

    void MaintainBalloonSnapPose()
    {
        if (!balloonSnapped || snappedBalloonTransform == null || balloon == null)
        {
            return;
        }

        AlignBalloonRootToSocket(snappedBalloonTransform.gameObject);
    }
}
