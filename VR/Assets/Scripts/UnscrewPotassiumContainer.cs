using UnityEngine;

public class UnscrewPotassiumContainer : MonoBehaviour
{
    public GameObject lid;
    [SerializeField] private Animator unscrewAnimationController;

    [Header("Actuation")]
    [Tooltip("Movement of the control is what operates it. The latch ignores movement while the " +
             "scene is still assembling itself, so being placed by the desktop rig cannot " +
             "actuate it.")]
    public MovementLatch movement = new MovementLatch();

    private bool hasTriggered = false;


    void OnEnable()
    {
        // Re-armed on every activation, not just the first. Both labs switch equipment off until
        // its experiment is chosen, and whatever places the object when it comes back would
        // otherwise land outside the window opened by Start and actuate it.
        movement.Begin(lid != null ? lid.transform : null);
    }

    void Start()
    {
        movement.Begin(lid != null ? lid.transform : null);
    }

    void Update()
    {
        if (!hasTriggered &&
            movement.StoppedThisFrame(lid != null ? lid.transform : null))
        {
            TriggerUnscrew();
        }
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
