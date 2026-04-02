using UnityEngine;

public class UnscrewPotassiumContainer : MonoBehaviour
{
    public GameObject lid;
    [SerializeField] private Animator unscrewAnimationController;

    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private bool wasMoving = false;
    private bool hasTriggered = false;

    void Start()
    {
        if (lid != null)
        {
            previousPosition = lid.transform.position;
            previousRotation = lid.transform.rotation;
        }
    }

    void Update()
    {
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
    }
}
