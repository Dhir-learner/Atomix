using UnityEngine;

/// <summary>
/// Marks a canvas built by <see cref="LabPanelBuilder.CreateFullScreenCanvas"/>. Those are laid
/// out edge to edge for 1920x1080, so <see cref="MobileControls"/> fits them to the screen height
/// on a phone but does not enlarge them the way it enlarges the small corner widgets.
/// </summary>
public class FullScreenPanelMarker : MonoBehaviour
{
}
