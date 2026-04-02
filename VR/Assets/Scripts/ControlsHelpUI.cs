using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays helpful desktop controls on screen.
/// Attach this to a UI Canvas in your scene or let DesktopBootstrap create one.
/// </summary>
public class ControlsHelpUI : MonoBehaviour
{
    [Header("UI Settings")]
    [Tooltip("Press this key to toggle help display")]
    public KeyCode toggleKey = KeyCode.H;

    [TextArea(10, 20)]
    public string helpText = @"CONTROLS
------------------------
MOVEMENT
  W A S D   Move
  Space     Move up
  Ctrl      Move down
  Mouse     Look around
  Shift     Sprint
  Escape    Lock or unlock cursor

INTERACTION
  Left Click   Grab, release, or activate
  R            Release held object
  T            Reset held object pose

HELD OBJECT ROTATION
  Q / E        Rotate left or right
  Z / X        Tilt forward or back
  C / V        Roll
  Mouse Wheel  Spin object

CHEMISTRY BOOK
  B            Open or close the book
  Left/Right   Flip pages
  1 to 8       Start a reaction directly

UI
  H            Toggle this help";

    private GameObject helpPanel;
    private Text helpTextComponent;
    private bool isVisible = true;

    void Start()
    {
        CreateHelpPanel();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleHelp();
        }
    }

    void CreateHelpPanel()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            gameObject.AddComponent<GraphicRaycaster>();
        }

        helpPanel = new GameObject("HelpPanel");
        helpPanel.transform.SetParent(transform);

        RectTransform panelRect = helpPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 0.5f);
        panelRect.anchoredPosition = new Vector2(20, 0);
        panelRect.sizeDelta = new Vector2(400, 540);

        Image panelImage = helpPanel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.7f);

        GameObject textObj = new GameObject("HelpText");
        textObj.transform.SetParent(helpPanel.transform);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);

        helpTextComponent = textObj.AddComponent<Text>();
        helpTextComponent.text = helpText;
        helpTextComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        helpTextComponent.fontSize = 16;
        helpTextComponent.color = Color.white;
        helpTextComponent.alignment = TextAnchor.UpperLeft;
    }

    void ToggleHelp()
    {
        isVisible = !isVisible;
        if (helpPanel != null)
        {
            helpPanel.SetActive(isVisible);
        }
    }

    public void ShowHelp()
    {
        isVisible = true;
        if (helpPanel != null)
        {
            helpPanel.SetActive(true);
        }
    }

    public void HideHelp()
    {
        isVisible = false;
        if (helpPanel != null)
        {
            helpPanel.SetActive(false);
        }
    }
}
