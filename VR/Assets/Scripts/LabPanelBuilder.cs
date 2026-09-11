using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared construction helpers for the runtime world-space panels.
///
/// <see cref="ExperimentHistoryUI"/>, <see cref="ReactionGraphUI"/> and
/// <see cref="ReactionLearningController"/> each grew their own copy of "make a world-space canvas
/// 1.5 m in front of the camera, add a dark plate, add some text, add a button". The panels added
/// here use this instead, so a fourth, fifth and sixth copy did not appear - and so a change to
/// the house style lands everywhere at once.
///
/// Deliberately left as a static helper rather than a base class: the panels have nothing else in
/// common, and Unity components do not inherit comfortably.
/// </summary>
public static class LabPanelBuilder
{
    /// <summary>World-space canvases are built at pixel sizes and scaled down by this.</summary>
    public const float WorldScale = 0.001f;

    public static Color RowColour { get { return new Color(0.13f, 0.15f, 0.20f, 1.0f); } }
    public static Color RowSelectedColour { get { return new Color(0.16f, 0.30f, 0.50f, 1.0f); } }
    public static Color ButtonColour { get { return new Color(0.16f, 0.30f, 0.50f, 1.0f); } }
    public static Color ButtonActiveColour { get { return new Color(0.24f, 0.52f, 0.78f, 1.0f); } }
    public static Color MutedTextColour { get { return new Color(0.62f, 0.68f, 0.78f, 1.0f); } }

    /// <summary>
    /// Builds the canvas + dim backdrop + panel plate every panel starts with.
    /// Returns the panel plate; the canvas is the returned transform's grandparent.
    /// </summary>
    public static RectTransform CreatePanelCanvas(Transform owner, string canvasName,
                                                  Vector2 canvasSize, Vector2 panelSize,
                                                  int sortingOrder, out Canvas canvas)
    {
        GameObject canvasObject = new GameObject(
            canvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(owner, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = sortingOrder;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = canvasSize;
        canvasRect.localScale = Vector3.one * WorldScale;

        // Dims the lab behind the panel. Must not swallow crosshair clicks meant for the buttons.
        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(canvasObject.transform, false);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Image backdropImage = backdrop.GetComponent<Image>();
        backdropImage.color = new Color(0.0f, 0.0f, 0.0f, 0.72f);
        backdropImage.raycastTarget = false;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = AtomixSettings.PanelColour;
        panelImage.raycastTarget = false;

        return panelRect;
    }

    /// <summary>
    /// Builds a genuine full-screen panel: a Screen Space Overlay canvas that scales with the
    /// window, rather than a world-space plate floating at a fixed distance.
    ///
    /// Used for the pause menu, which is a lot of small controls read head-on. A world-space panel
    /// has to be small enough to fit the field of view at 1.5 m, which left it as a postage stamp
    /// in the middle of the lab. Overlay also renders crisp at any resolution, because the text is
    /// rasterised at screen pixels instead of being scaled down by 0.001 and back up again.
    ///
    /// Returns the panel plate, stretched to the canvas minus <paramref name="margin"/>.
    /// </summary>
    public static RectTransform CreateFullScreenCanvas(Transform owner, string canvasName,
                                                       Vector2 referenceResolution, int sortingOrder,
                                                       float margin, out Canvas canvas,
                                                       out CanvasScaler scaler)
    {
        GameObject canvasObject = new GameObject(
            canvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(owner, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        // Split the difference so the layout survives both 16:9 and wider desktop aspect ratios.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.scaleFactor = AtomixSettings.UiScale;

        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(canvasObject.transform, false);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Image backdropImage = backdrop.GetComponent<Image>();
        backdropImage.color = new Color(0.0f, 0.0f, 0.0f, 0.80f);
        // Full-screen and opaque: this one DOES absorb clicks, so a stray click behind the menu
        // cannot grab a beaker you cannot see.
        backdropImage.raycastTarget = true;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = new Vector2(margin, margin);
        panelRect.offsetMax = new Vector2(-margin, -margin);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = AtomixSettings.PanelColour;
        panelImage.raycastTarget = false;

        return panelRect;
    }

    /// <summary>
    /// Places a world-space panel squarely in front of the player's eyes, at a comfortable reading
    /// distance - or the one the student last chose with the mouse wheel.
    /// </summary>
    /// <param name="chosenFill">The panel's remembered wheel choice; 0 until the student scrolls.</param>
    public static void FaceCamera(Canvas canvas, float chosenFill)
    {
        if (canvas == null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        canvas.worldCamera = camera;
        canvas.transform.localScale = Vector3.one * WorldScale;
        canvas.transform.rotation = camera.transform.rotation;
        canvas.transform.position = camera.transform.position +
                                    camera.transform.forward * OpeningDistance(canvas, chosenFill);
        KeepInsideLab(canvas);
    }

    // =========================================================
    // PLACEMENT - reading distance, mouse wheel, kept inside the lab
    // =========================================================

    /// <summary>
    /// Share of the view height a panel fills when it opens, at the default Menu size.
    ///
    /// Panels used to open a fixed 1.5 - 1.6 m away. A fixed physical distance means the apparent
    /// size of the text depends on how big the panel is and on the field of view: at the default
    /// 60 degrees the panels filled well under half the screen and a 20 pt hint came out about
    /// 12 px tall on a 1080p display, and widening the field of view shrank it further. Placing
    /// by share of the view gives every panel the same, readable apparent size on any setting.
    /// </summary>
    public const float ComfortableFill = 0.7f;

    /// <summary>
    /// Closest the mouse wheel can bring a panel: the whole of it still fits on screen, so no
    /// text is pushed off the edge or blown up past reading size.
    /// </summary>
    public const float NearestFill = 0.95f;

    /// <summary>
    /// Furthest the mouse wheel can push a panel. Beyond this the smallest labels on the denser
    /// panels drop below about 11 px on a 1080p display.
    /// </summary>
    public const float FarthestFill = 0.5f;

    /// <summary>How much one wheel notch changes the distance - 8 %, so it feels the same near or far.</summary>
    private const float ScrollStepFactor = 1.08f;

    /// <summary>Gap kept between a panel and the wall, bench or equipment behind it.</summary>
    private const float PanelClearance = 0.05f;

    /// <summary>
    /// Nearest a wall may push a panel in towards the eyes. Deliberately closer than
    /// <see cref="NearestFill"/> allows: a panel opened with the player's back to a wall has to go
    /// somewhere, and in front of the wall is better than through it.
    /// </summary>
    private const float NearestPanelDistance = 0.3f;

    /// <summary>
    /// Metres from the eyes a panel should open at: where the student last put it with the wheel,
    /// or a comfortable reading distance scaled by the Menu size setting if they have not.
    /// </summary>
    /// <param name="chosenFill">The panel's remembered wheel choice; 0 until the student scrolls.</param>
    public static float OpeningDistance(Canvas canvas, float chosenFill)
    {
        float fill = chosenFill > 0.0f
            ? chosenFill
            : ComfortableFill * AtomixSettings.UiScale;

        return DistanceForFill(canvas, Mathf.Clamp(fill, FarthestFill, NearestFill));
    }

    /// <summary>
    /// Metres from the eyes at which the panel fills <paramref name="fill"/> of the view - by
    /// height, or by width on a screen narrow enough for that to be the tighter fit.
    /// </summary>
    private static float DistanceForFill(Canvas canvas, float fill)
    {
        Vector2 half = HalfSize(canvas);
        float tanHalfHeight = Mathf.Tan(AtomixSettings.BaseFieldOfView * 0.5f * Mathf.Deg2Rad);
        Camera camera = Camera.main;
        float aspect = camera != null ? camera.aspect : 16.0f / 9.0f;

        if (half.y <= 0.0f || tanHalfHeight <= 0.0f || fill <= 0.0f)
        {
            return 1.5f;
        }

        float byHeight = half.y / (fill * tanHalfHeight);
        float byWidth = half.x / (fill * tanHalfHeight * aspect);
        return Mathf.Max(byHeight, byWidth);
    }

    /// <summary>The share of the view the panel fills at <paramref name="distance"/> metres.</summary>
    private static float FillAtDistance(Canvas canvas, float distance)
    {
        // Fill is inversely proportional to distance, so one reference point is enough.
        return distance > 0.0001f ? DistanceForFill(canvas, 1.0f) / distance : NearestFill;
    }

    /// <summary>Everything solid except the UI layer, which the panels themselves sit on.</summary>
    private static readonly int PlacementMask = Physics.DefaultRaycastLayers & ~(1 << 5);

    private static readonly RaycastHit[] placementHits = new RaycastHit[16];

    /// <summary>
    /// Mouse wheel: forward brings the panel closer, back pushes it away, between
    /// <see cref="NearestFill"/> and <see cref="FarthestFill"/> so the text never gets too big or
    /// too small to read. The panel slides along the line from the player's eyes to where it is
    /// now, so it keeps its place in view, and <see cref="KeepInsideLab"/> stops it at walls,
    /// benches and the room boundary.
    ///
    /// Leaves the wheel alone while an object is held - the wheel spins that instead - and when the
    /// panel is behind the player, so a panel they cannot see is never moved.
    /// </summary>
    /// <param name="chosenFill">
    /// The panel's remembered wheel choice, as a share of the view. Updated here, so the panel
    /// reopens at the size the student left it, whatever the field of view is by then.
    /// </param>
    /// <returns>True when the wheel moved the panel.</returns>
    public static bool ScrollPanelDistance(Canvas canvas, ref float chosenFill)
    {
        float wheel = Input.mouseScrollDelta.y;
        if (wheel == 0.0f || canvas == null || !canvas.gameObject.activeInHierarchy)
        {
            return false;
        }

        Camera camera = Camera.main;
        if (camera == null || HeldObjectTransform(camera) != null)
        {
            return false;
        }

        Transform eye = camera.transform;
        Vector3 offset = canvas.transform.position - eye.position;
        float current = offset.magnitude;
        if (current < 0.0001f || Vector3.Dot(offset, eye.forward) <= 0.0f)
        {
            return false;
        }

        // The limits only ever stop the wheel, never reverse it: a panel the player has walked
        // away from, or one a wall pushed in close, must not jump the other way on a scroll.
        float target = current * Mathf.Pow(ScrollStepFactor, -wheel);
        target = wheel > 0.0f
            ? Mathf.Max(target, Mathf.Min(current, DistanceForFill(canvas, NearestFill)))
            : Mathf.Min(target, Mathf.Max(current, DistanceForFill(canvas, FarthestFill)));

        canvas.transform.position = eye.position + offset / current * target;
        KeepInsideLab(canvas);

        chosenFill = FillAtDistance(canvas, target);
        return true;
    }

    /// <summary>
    /// Pulls a world-space panel back inside the laboratory: short of any wall, bench or piece of
    /// equipment between it and the player's eyes, up off a bench its bottom edge would sink into,
    /// and with every corner inside the room boundary so no edge pokes out through a wall.
    ///
    /// Call after the panel's rotation and scale are set - its footprint is measured from them.
    /// </summary>
    public static void KeepInsideLab(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Transform panel = canvas.transform;
        Transform eyeTransform = camera.transform;
        Transform held = HeldObjectTransform(camera);
        Vector3 eye = eyeTransform.position;
        RaycastHit hit;

        // Nothing solid between the eyes and the panel.
        Vector3 offset = panel.position - eye;
        float distance = offset.magnitude;
        if (distance > 0.0001f)
        {
            Vector3 direction = offset / distance;
            if (FirstSolidHit(eye, direction, distance + PanelClearance, eyeTransform, held, out hit))
            {
                distance = Mathf.Max(NearestPanelDistance, hit.distance - PanelClearance);
                panel.position = eye + direction * distance;
            }
        }

        // World-space half extents of the rotated panel.
        Vector2 half = HalfSize(canvas);
        Vector3 extentX = panel.right * half.x;
        Vector3 extentY = panel.up * half.y;
        Vector3 reach = new Vector3(
            Mathf.Abs(extentX.x) + Mathf.Abs(extentY.x),
            Mathf.Abs(extentX.y) + Mathf.Abs(extentY.y),
            Mathf.Abs(extentX.z) + Mathf.Abs(extentY.z));

        // Bottom edge above the bench. At normal eye height it already is; this is for a player
        // who has flown down low with Ctrl, or is looking down at the bench.
        Vector3 centre = panel.position;
        if (FirstSolidHit(centre + Vector3.up * reach.y, Vector3.down, reach.y * 2.0f + PanelClearance,
                          eyeTransform, held, out hit) &&
            hit.normal.y > 0.65f)
        {
            centre.y = Mathf.Max(centre.y, hit.point.y + reach.y + PanelClearance);
        }

        // Every corner inside the room. The walls are only a collider cage at the boundary, so a
        // panel near a corner could otherwise pass the centre ray and still hang out of the room.
        LabBoundary boundary = LabBoundary.Active;
        if (boundary != null && boundary.HasInterior)
        {
            Bounds room = boundary.Interior;
            reach += Vector3.one * PanelClearance;
            centre.x = ClampSpan(centre.x, room.min.x + reach.x, room.max.x - reach.x);
            centre.y = ClampSpan(centre.y, room.min.y + reach.y, room.max.y - reach.y);
            centre.z = ClampSpan(centre.z, room.min.z + reach.z, room.max.z - reach.z);
        }

        panel.position = centre;
    }

    /// <summary>Half the width and height of a world-space canvas, in metres.</summary>
    private static Vector2 HalfSize(Canvas canvas)
    {
        RectTransform rect = canvas.transform as RectTransform;
        if (rect == null)
        {
            return Vector2.zero;
        }

        Vector3 scale = rect.lossyScale;
        return new Vector2(rect.rect.width * Mathf.Abs(scale.x), rect.rect.height * Mathf.Abs(scale.y)) * 0.5f;
    }

    /// <summary>Clamps into [min, max], or centres in the span when the panel is wider than it.</summary>
    private static float ClampSpan(float value, float min, float max)
    {
        return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }

    private static Transform HeldObjectTransform(Camera camera)
    {
        ObjectInteraction interaction = camera.GetComponent<ObjectInteraction>();
        if (interaction == null || interaction.HeldObject == null)
        {
            return null;
        }

        return interaction.HeldObject.transform;
    }

    /// <summary>
    /// Nearest solid hit along a ray, ignoring the player's own body and whatever they are
    /// holding - a beaker carried in front of the eyes is not a wall.
    /// </summary>
    private static bool FirstSolidHit(Vector3 origin, Vector3 direction, float maxDistance,
                                      Transform player, Transform held, out RaycastHit nearest)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, placementHits, maxDistance,
                                            PlacementMask, QueryTriggerInteraction.Ignore);
        nearest = default(RaycastHit);
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            Transform hitTransform = placementHits[i].collider.transform;
            if ((player != null && hitTransform.IsChildOf(player)) ||
                (held != null && hitTransform.IsChildOf(held)))
            {
                continue;
            }

            if (!found || placementHits[i].distance < nearest.distance)
            {
                nearest = placementHits[i];
                found = true;
            }
        }

        return found;
    }

    public static TMP_Text CreateText(string objectName, Transform parent, Vector2 anchoredPosition,
                                      Vector2 size, string content, float fontSize,
                                      TextAlignmentOptions alignment, Color colour)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = colour;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    /// <summary>
    /// A crosshair-clickable button. The label is a child text with raycasting off, so
    /// <see cref="ObjectInteraction.TryUiInteraction"/> resolves the click to the Button itself.
    /// </summary>
    public static Button CreateButton(string objectName, Transform parent, string label,
                                      Vector2 anchoredPosition, Vector2 size, float fontSize,
                                      Action onClick, Color? colourOverride = null)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = buttonObject.GetComponent<Image>();
        image.color = colourOverride.HasValue ? colourOverride.Value : ButtonColour;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        // Hover lift, press dip, and a hover tick. Attached here rather than at each of the
        // twenty-odd call sites so every runtime-built panel gets it for nothing.
        buttonObject.AddComponent<UiButtonFeel>();

        // Registered before the action, so the click is still heard if the action throws - and
        // so buttons whose listener is added later (the toggle rows) are covered too.
        button.onClick.AddListener(AtomixAudio.UiClick);

        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }

        CreateText(objectName + "Label", buttonObject.transform, Vector2.zero, size, label,
            fontSize, TextAlignmentOptions.Center, Color.white);

        return button;
    }

    /// <summary>A flat coloured plate, used for rows, headers and separators.</summary>
    public static RectTransform CreatePlate(string objectName, Transform parent,
                                            Vector2 anchoredPosition, Vector2 size, Color colour)
    {
        GameObject plate = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        plate.transform.SetParent(parent, false);

        RectTransform rect = plate.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = plate.GetComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
        return rect;
    }

    /// <summary>Height of one settings row, and the plate drawn behind it.</summary>
    public const float SettingRowHeight = 48.0f;

    /// <summary>
    /// Only glyphs in the default TextMeshPro atlas may be used in built UI. That atlas covers
    /// Latin-1 and General Punctuation and nothing else, so the arrows and check marks the first
    /// version used (U+25C0, U+25B6, U+2714) rendered as empty boxes on screen. Plain ASCII here,
    /// and a coloured plate instead of a tick, so nothing can turn into tofu.
    /// </summary>
    private const string StepDownGlyph = "-";
    private const string StepUpGlyph = "+";

    /// <summary>
    /// A settings row: backing plate, left-aligned label, right-aligned value, and two nudge
    /// buttons. The label and value sit at fixed offsets from the row edges, so every row in a
    /// column lines up regardless of how long its label is.
    ///
    /// Steppers rather than a uGUI Slider because a Slider needs a pointer <em>drag</em>, and the
    /// crosshair never produces one.
    /// </summary>
    /// <summary>Width of the - and + buttons on a stepper row.</summary>
    private const float StepButtonWidth = 52.0f;

    /// <summary>Width reserved for the value readout on a stepper row.</summary>
    private const float StepValueWidth = 190.0f;

    public static TMP_Text CreateStepperRow(Transform parent, string label, Vector2 anchoredPosition,
                                            float rowWidth, float fontSize,
                                            Func<string> readValue, Action stepDown, Action stepUp)
    {
        // Everything is measured in from the right edge of the row, so a row laid out at 740 px
        // in a two-column tab is as correct as one at 1100 px in a single column. The previous
        // version hard-coded offsets that only worked at 1100 - narrower than that and the label
        // ran straight through the value.
        float half = rowWidth * 0.5f;
        float upX = half - 18.0f - (StepButtonWidth * 0.5f);
        float downX = upX - StepButtonWidth - 6.0f;
        float valueCentre = downX - (StepButtonWidth * 0.5f) - 12.0f - (StepValueWidth * 0.5f);
        float labelLeft = -half + 22.0f;
        float labelRight = valueCentre - (StepValueWidth * 0.5f) - 14.0f;
        float labelWidth = Mathf.Max(120.0f, labelRight - labelLeft);
        float labelCentre = (labelLeft + labelRight) * 0.5f;

        CreatePlate(label + "Row", parent, anchoredPosition,
            new Vector2(rowWidth, SettingRowHeight), RowColour);

        CreateText(label + "Label", parent,
            new Vector2(anchoredPosition.x + labelCentre, anchoredPosition.y),
            new Vector2(labelWidth, SettingRowHeight), label, fontSize,
            TextAlignmentOptions.Left, AtomixSettings.BodyTextColour);

        TMP_Text value = CreateText(label + "Value", parent,
            new Vector2(anchoredPosition.x + valueCentre, anchoredPosition.y),
            new Vector2(StepValueWidth, SettingRowHeight), readValue(), fontSize,
            TextAlignmentOptions.Right, Color.white);

        CreateButton(label + "Down", parent, StepDownGlyph,
            new Vector2(anchoredPosition.x + downX, anchoredPosition.y),
            new Vector2(StepButtonWidth, SettingRowHeight - 10.0f), fontSize + 4.0f,
            () => { stepDown(); value.text = readValue(); });

        CreateButton(label + "Up", parent, StepUpGlyph,
            new Vector2(anchoredPosition.x + upX, anchoredPosition.y),
            new Vector2(StepButtonWidth, SettingRowHeight - 10.0f), fontSize + 4.0f,
            () => { stepUp(); value.text = readValue(); });

        return value;
    }

    /// <summary>A settings row whose control is a single ON/OFF button.</summary>
    public static TMP_Text CreateToggleRow(Transform parent, string label, Vector2 anchoredPosition,
                                           float rowWidth, float fontSize,
                                           Func<bool> read, Action<bool> write)
    {
        const float toggleWidth = 128.0f;

        float half = rowWidth * 0.5f;
        float toggleCentre = half - 22.0f - (toggleWidth * 0.5f);
        float labelLeft = -half + 22.0f;
        float labelRight = toggleCentre - (toggleWidth * 0.5f) - 14.0f;
        float labelWidth = Mathf.Max(120.0f, labelRight - labelLeft);
        float labelCentre = (labelLeft + labelRight) * 0.5f;

        CreatePlate(label + "Row", parent, anchoredPosition,
            new Vector2(rowWidth, SettingRowHeight), RowColour);

        CreateText(label + "Label", parent,
            new Vector2(anchoredPosition.x + labelCentre, anchoredPosition.y),
            new Vector2(labelWidth, SettingRowHeight), label, fontSize,
            TextAlignmentOptions.Left, AtomixSettings.BodyTextColour);

        Button button = CreateButton(label + "Toggle", parent, read() ? "ON" : "OFF",
            new Vector2(anchoredPosition.x + toggleCentre, anchoredPosition.y),
            new Vector2(toggleWidth, SettingRowHeight - 10.0f), fontSize, null);

        TMP_Text state = button.GetComponentInChildren<TMP_Text>();
        Image image = button.GetComponent<Image>();

        button.onClick.AddListener(() =>
        {
            write(!read());
            if (state != null)
            {
                state.text = read() ? "ON" : "OFF";
            }
            if (image != null)
            {
                image.color = read() ? ButtonActiveColour : ButtonColour;
            }
        });

        if (image != null)
        {
            image.color = read() ? ButtonActiveColour : ButtonColour;
        }

        return state;
    }

    /// <summary>A small caps heading that groups the rows beneath it.</summary>
    public static void CreateSectionHeading(Transform parent, string heading,
                                            Vector2 anchoredPosition, float rowWidth, float fontSize)
    {
        CreateText(heading + "Heading", parent,
            new Vector2(anchoredPosition.x - rowWidth * 0.5f + 300.0f, anchoredPosition.y),
            new Vector2(560.0f, 30.0f), heading.ToUpperInvariant(), fontSize,
            TextAlignmentOptions.Left, ButtonActiveColour);
    }

    /// <summary>
    /// A filled or hollow status pip. Used where a check mark would be the obvious choice but is
    /// not available in the font atlas.
    /// </summary>
    public static void CreateStatusPip(Transform parent, Vector2 anchoredPosition, bool filled,
                                       Color filledColour)
    {
        CreatePlate("PipFrame", parent, anchoredPosition, new Vector2(22.0f, 22.0f),
            filled ? filledColour : new Color(0.28f, 0.32f, 0.40f, 1.0f));

        if (!filled)
        {
            // Hollow centre, so an unearned pip reads as an outline rather than a dim block.
            CreatePlate("PipHole", parent, anchoredPosition, new Vector2(14.0f, 14.0f),
                AtomixSettings.PanelColour);
        }
    }

    /// <summary>
    /// Closes every other runtime panel before opening a new one.
    ///
    /// All of them sit at the same depth in front of the camera, so two open at once just stack
    /// and neither is readable. They all live on the one persistent object, so closing the
    /// siblings is enough - and each Close() is idempotent.
    /// </summary>
    public static void CloseOtherPanels(Component opening)
    {
        if (opening == null)
        {
            return;
        }

        GameObject host = opening.gameObject;

        ExperimentHistoryUI history = host.GetComponent<ExperimentHistoryUI>();
        if (history != null && !ReferenceEquals(history, opening))
        {
            history.Close();
        }

        ReactionGraphUI graphs = host.GetComponent<ReactionGraphUI>();
        if (graphs != null && !ReferenceEquals(graphs, opening))
        {
            graphs.Close();
        }

        PeriodicTableUI table = host.GetComponent<PeriodicTableUI>();
        if (table != null && !ReferenceEquals(table, opening))
        {
            table.Close();
        }

        PauseMenuUI pause = host.GetComponent<PauseMenuUI>();
        if (pause != null && !ReferenceEquals(pause, opening))
        {
            pause.Close();
        }

        // The two testing-scene panels belong here too. Both suspend the player while they are
        // up, and two overlapping suspends can hand the controller back in the wrong order and
        // leave the student unable to move.
        ExamSetupUI setup = host.GetComponent<ExamSetupUI>();
        if (setup != null && !ReferenceEquals(setup, opening))
        {
            setup.Close();
        }

        TestResultsUI results = host.GetComponent<TestResultsUI>();
        if (results != null && !ReferenceEquals(results, opening))
        {
            results.Close();
        }
    }

    /// <summary>
    /// Freezes the desktop player while a full-screen panel is up, and reports whether the
    /// controller had been enabled so the caller can put it back exactly as it was.
    /// Disabling the component stops both WASD and mouse-look in one step.
    /// </summary>
    public static bool SuspendPlayer()
    {
        FirstPersonController controller =
            UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Exclude);
        if (controller == null || !controller.enabled)
        {
            return false;
        }

        controller.enabled = false;
        return true;
    }

    public static void ResumePlayer(bool wasEnabled)
    {
        if (!wasEnabled)
        {
            return;
        }

        FirstPersonController controller =
            UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            controller.enabled = true;
        }
    }

    /// <summary>Removes every child of a transform immediately, so a rebuild cannot double up.</summary>
    public static void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            // Destroy() is deferred to end of frame, so unparent first or the rebuilt content
            // briefly coexists with the old inside a layout group.
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }
}
