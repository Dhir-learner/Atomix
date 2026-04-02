using UnityEngine;

/// <summary>
/// Base class for click-to-activate desktop interactions.
/// </summary>
public abstract class DesktopInteractable : MonoBehaviour
{
    public bool canInteract = true;

    public virtual bool CanInteract => canInteract && enabled && gameObject.activeInHierarchy;

    public abstract void Interact(ObjectInteraction interactor);
}
