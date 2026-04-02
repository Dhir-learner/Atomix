using System;
using UnityEngine;

public class NatriumContainerScriptAnimation : MonoBehaviour
{
    public GameObject lid;
    [SerializeField] private Animator unscrewAnimationController;
    public GameObject container;
    public GameObject new_container;

    private DateTime timpInitial;
    private bool active = false;
    private Vector3 coord;
    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private bool wasMoving = false;
    private bool hasTriggered = false;

    void Start()
    {
        new_container.SetActive(false);

        if (lid != null)
        {
            previousPosition = lid.transform.position;
            previousRotation = lid.transform.rotation;
        }
    }

    void Update()
    {
        if (active && (DateTime.Now - timpInitial).TotalSeconds >= 4)
        {
            coord = container.transform.position;
            container.SetActive(false);
            new_container.transform.position = coord;
            new_container.SetActive(true);
            active = false;
        }

        if (MovementStoppedThisFrame())
        {
            TriggerUnscrew();
        }
    }

    bool MovementStoppedThisFrame()
    {
        if (lid == null || hasTriggered)
        {
            return false;
        }

        Transform lidTransform = lid.transform;
        bool isMoving = Vector3.Distance(previousPosition, lidTransform.position) > 0.0005f ||
                        Quaternion.Angle(previousRotation, lidTransform.rotation) > 0.1f;

        bool stoppedThisFrame = wasMoving && !isMoving;
        previousPosition = lidTransform.position;
        previousRotation = lidTransform.rotation;
        wasMoving = isMoving;
        return stoppedThisFrame;
    }

    public void TriggerUnscrew()
    {
        if (hasTriggered || unscrewAnimationController == null)
        {
            return;
        }

        hasTriggered = true;
        unscrewAnimationController.SetTrigger("TrUnscrew");
        active = true;
        timpInitial = DateTime.Now;
    }
}
