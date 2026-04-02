using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CaCO3ReactionTest : MonoBehaviour
{
    [SerializeField] CountdownTimer countdown;
    [SerializeField] Randomize randomizer;
    public TMP_Text canvasText;
    private bool finishedTask = false;
    private DateTime timpInitial;

    public GameObject pivotEprubeta;
    public GameObject pivotFoc;
    [SerializeField] LightFire foc;
    [SerializeField] MeshRendererScript balloon;
    public GameObject fume;
    public GameObject balon;

    public GameObject label;
    public Material CaO_material;

    private float targetPoint;
    private Vector3 initialScale;
    private Vector3 finalScale;
    private Vector3 initialPosition;
    private Vector3 finalPosition;
    private bool reactionCompleted = false;
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

        if (!finishedTask)
        {
            canvasText.text = "Decompose CaCO3 and inflate the balloon.";
        }
        if (balloonSnapped)
        {
            balon_ok = true;
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
                        timpInitial = DateTime.Now;
                        fume.SetActive(false);
                        canvasText.text = "Task finished!";
                        countdown.continua = false;
                        finishedTask = true;
                    }
                }
            }
        }
        else
        {
            fume.SetActive(false);
        }
        if (finishedTask && !randomizer.generateNewReaction && (DateTime.Now - timpInitial).TotalSeconds >= 5)
        {
            randomizer.generateNewReaction = true;
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
