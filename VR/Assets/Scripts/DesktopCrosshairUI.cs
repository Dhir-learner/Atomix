using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a simple center crosshair for desktop gameplay.
/// </summary>
public class DesktopCrosshairUI : MonoBehaviour
{
    [Header("Crosshair Settings")]
    [Tooltip("Default color of the crosshair")]
    public Color crosshairColor = new Color(1f, 1f, 1f, 0.92f);

    [Tooltip("Color shown when hovering a grabbable object")]
    public Color hoverColor = new Color(1f, 0.82f, 0.2f, 0.96f);

    [Tooltip("Size of the center gap in pixels")]
    public float gap = 6f;

    [Tooltip("Length of each crosshair arm in pixels")]
    public float armLength = 10f;

    [Tooltip("Thickness of each crosshair arm in pixels")]
    public float thickness = 2f;

    [Tooltip("Show a small center dot")]
    public bool showCenterDot = true;

    private static DesktopCrosshairUI activeCrosshair;
    private GameObject crosshairRoot;
    private readonly List<Image> crosshairImages = new List<Image>();
    private bool isHoveringGrabbable;

    void OnEnable()
    {
        activeCrosshair = this;
    }

    void OnDisable()
    {
        if (activeCrosshair == this)
        {
            activeCrosshair = null;
        }
    }

    void Start()
    {
        CreateCrosshair();
        ApplyCurrentColor();
        UpdateVisibility();
    }

    void Update()
    {
        UpdateVisibility();
    }

    void CreateCrosshair()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GraphicRaycaster raycaster = gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;
        }

        crosshairRoot = new GameObject("Crosshair");
        crosshairRoot.transform.SetParent(transform, false);

        RectTransform rootRect = crosshairRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(40f, 40f);

        CreateArm("Top", new Vector2(0f, gap + (armLength * 0.5f)), new Vector2(thickness, armLength));
        CreateArm("Bottom", new Vector2(0f, -(gap + (armLength * 0.5f))), new Vector2(thickness, armLength));
        CreateArm("Left", new Vector2(-(gap + (armLength * 0.5f)), 0f), new Vector2(armLength, thickness));
        CreateArm("Right", new Vector2(gap + (armLength * 0.5f), 0f), new Vector2(armLength, thickness));

        if (showCenterDot)
        {
            CreateArm("CenterDot", Vector2.zero, new Vector2(thickness + 1f, thickness + 1f));
        }
    }

    void CreateArm(string name, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject armObject = new GameObject(name);
        armObject.transform.SetParent(crosshairRoot.transform, false);

        RectTransform armRect = armObject.AddComponent<RectTransform>();
        armRect.anchorMin = new Vector2(0.5f, 0.5f);
        armRect.anchorMax = new Vector2(0.5f, 0.5f);
        armRect.pivot = new Vector2(0.5f, 0.5f);
        armRect.anchoredPosition = anchoredPosition;
        armRect.sizeDelta = size;

        Image image = armObject.AddComponent<Image>();
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = crosshairColor;
        image.raycastTarget = false;
        crosshairImages.Add(image);
    }

    public static void SetHoveringGrabbable(bool hovering)
    {
        if (activeCrosshair == null)
        {
            return;
        }

        activeCrosshair.SetHoverState(hovering);
    }

    void SetHoverState(bool hovering)
    {
        if (isHoveringGrabbable == hovering)
        {
            return;
        }

        isHoveringGrabbable = hovering;
        ApplyCurrentColor();
    }

    void ApplyCurrentColor()
    {
        Color targetColor = isHoveringGrabbable ? hoverColor : crosshairColor;

        foreach (Image image in crosshairImages)
        {
            if (image != null)
            {
                image.color = targetColor;
            }
        }
    }

    void UpdateVisibility()
    {
        if (crosshairRoot != null)
        {
            crosshairRoot.SetActive(FirstPersonController.IsCursorLocked);
        }
    }
}
