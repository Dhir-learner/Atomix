using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A touch button that reports press and release separately, which a uGUI <see cref="Button"/>
/// cannot: it only fires on release, so it can neither be held (fly, tilt, push-to-talk) nor feel
/// immediate. Built at runtime by <see cref="MobileControls"/>.
/// </summary>
public class MobileHudButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Action Pressed;
    public Action Released;

    /// <summary>
    /// Runs instead of <see cref="Released"/> when the button is hidden under a finger. Hold
    /// buttons leave it unset, so hiding them lets go as normal; tap buttons set it, so hiding
    /// them does not count as a tap.
    /// </summary>
    public Action Cancelled;

    /// <summary>Lit, for toggles that are switched on (sprint) or states that are live (listening).</summary>
    public bool Highlighted;

    public Color IdleColour = new Color(0.08f, 0.10f, 0.14f, 0.55f);
    public Color ActiveColour = new Color(0.24f, 0.52f, 0.78f, 0.85f);

    private Image image;
    private bool isDown;

    public bool IsDown { get { return isDown; } }

    void Awake()
    {
        image = GetComponent<Image>();
    }

    void Update()
    {
        if (image != null)
        {
            Color target = isDown || Highlighted ? ActiveColour : IdleColour;
            image.color = Color.Lerp(image.color, target, 1.0f - Mathf.Exp(-20.0f * Time.unscaledDeltaTime));
        }

        float scale = isDown ? 0.9f : 1.0f;
        transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(scale, scale, 1.0f),
            1.0f - Mathf.Exp(-25.0f * Time.unscaledDeltaTime));
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDown = true;
        AtomixAudio.UiClick();
        if (Pressed != null)
        {
            Pressed();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release(false);
    }

    /// <summary>A button hidden under a finger must still let go, or a held action sticks on.</summary>
    void OnDisable()
    {
        Release(true);
        transform.localScale = Vector3.one;
    }

    private void Release(bool cancelled)
    {
        if (!isDown)
        {
            return;
        }

        isDown = false;
        Action handler = cancelled && Cancelled != null ? Cancelled : Released;
        if (handler != null)
        {
            handler();
        }
    }
}
