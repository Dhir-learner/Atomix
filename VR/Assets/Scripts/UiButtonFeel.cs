using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Makes a runtime-built button feel like it is being touched: it lifts slightly under the
/// pointer, dips when pressed, and ticks.
///
/// Atomix builds most of its interface in code - the pause menu, the history browser, the graph
/// panel, the periodic table, the report card - through <see cref="LabPanelBuilder"/>. Those
/// buttons had a <c>Button</c> component with default transitions, which means a colour tint on
/// hover and nothing else. No motion, no sound, and, in the laboratory scenes, not even the
/// colour tint: the crosshair drives the UI by dispatching pointer events by hand from
/// <see cref="ObjectInteraction"/>, and it never sends <c>pointerEnter</c>, so nothing highlighted
/// at all. Clicking a button in the lab was completely unacknowledged until whatever it did
/// happened to become visible.
///
/// One component on every button fixes both, and because it is attached inside
/// <see cref="LabPanelBuilder.CreateButton"/> it reaches every panel in the game at once.
///
/// <b>Scale, not layout.</b> The lift is a <c>localScale</c> change, which does not dirty the
/// Canvas layout and therefore costs no rebuild - important, because these panels can hold a
/// hundred and eighteen buttons at once in the periodic table.
/// </summary>
[DisallowMultipleComponent]
public class UiButtonFeel : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Scale when the pointer is over the button.")]
    public float hoverScale = 1.035f;

    [Tooltip("Scale while the button is held down.")]
    public float pressScale = 0.965f;

    [Tooltip("How quickly the scale reaches its target.")]
    public float response = 14.0f;

    private RectTransform rect;
    private Button button;
    private bool hovered;
    private bool pressed;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        button = GetComponent<Button>();
    }

    void OnDisable()
    {
        // A panel that closes mid-hover must not come back still enlarged.
        hovered = false;
        pressed = false;
        if (rect != null)
        {
            rect.localScale = Vector3.one;
        }
    }

    void Update()
    {
        if (rect == null)
        {
            return;
        }

        // The overwhelmingly common case: a button nobody is pointing at, already at rest. Every
        // panel in the game is built from these, so the idle path has to be free.
        if (!hovered && !pressed && rect.localScale.x == 1.0f)
        {
            return;
        }

        bool usable = button == null || (button.IsActive() && button.IsInteractable());
        float target = 1.0f;

        if (usable)
        {
            if (pressed)
            {
                target = pressScale;
            }
            else if (hovered)
            {
                target = hoverScale;
            }
        }

        // Frame-rate independent: the same fraction of the remaining distance per second at any
        // frame rate, rather than per frame.
        float blend = 1.0f - Mathf.Exp(-response * Time.unscaledDeltaTime);
        float scale = Mathf.Lerp(rect.localScale.x, target, blend);

        // Snap once the difference stops being visible, so idle buttons cost nothing.
        if (Mathf.Abs(scale - target) < 0.0005f)
        {
            scale = target;
        }

        rect.localScale = new Vector3(scale, scale, 1.0f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hovered)
        {
            return;
        }

        hovered = true;

        if (button == null || (button.IsActive() && button.IsInteractable()))
        {
            AtomixAudio.UiHover();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;

        // The crosshair sends down and up on the same frame, so without this the dip would never
        // be visible in the laboratory. Treating a press as a hover means it still reads as one
        // quick pulse rather than as nothing at all.
        hovered = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }
}
