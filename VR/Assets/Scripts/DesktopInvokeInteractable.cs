using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Simple desktop interaction that invokes a UnityEvent when clicked.
/// </summary>
public class DesktopInvokeInteractable : DesktopInteractable
{
    [SerializeField] private UnityEvent onInteract = new UnityEvent();

    public override void Interact(ObjectInteraction interactor)
    {
        if (!CanInteract)
        {
            return;
        }

        onInteract.Invoke();
    }

    public void AddListener(UnityAction action)
    {
        onInteract.AddListener(action);
    }
}
