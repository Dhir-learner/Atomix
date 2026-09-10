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
  V            Hold push-to-talk (Lab Assistant)

AI LAB ASSISTANT
  Enter        Type a question (works with no microphone)
  Y            Ask why the last experiment went wrong
  M            Minimise the assistant panel

HELD OBJECT ROTATION
  Q / E        Rotate left or right
  Z / X        Tilt forward or back
  C            Roll (hold Shift to reverse)
  Mouse Wheel  Spin object

CHEMISTRY BOOK
  B            Open or close the book
  Left/Right   Flip pages
  1 to 8       Start a reaction directly
  F5           Reset the bench and retry the experiment

UI
  F1           Pause, settings and achievements
  H            Toggle this help
  L            Measurement label: full / compact / off
  O            Bring the video panel back in front of you
  Tab          Experiment history
  F            Scientific graphs
  P            Periodic table

TESTING SCENE
  F2           Buy a hint (40 coins)
  F3           Buy 30 more seconds (60 coins)
  F4           Skip the current task (100 coins)

MOLECULAR VIEW
  3D VIEW      Live ball-and-stick animation of the reaction
  ASK AI       Ask about the step currently on screen
  Space        Play / pause";

    private GameObject helpPanel;
    private Text helpTextComponent;
    private bool isVisible = true;

    void Start()
    {
        CreateHelpPanel();
    }

    void Update()
    {
        // A question is being typed into the assistant panel; every letter belongs to it.
        if (LabTextInput.IsCapturing)
        {
            return;
        }

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
