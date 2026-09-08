using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Renders a <see cref="MolecularScene"/> as a live ball-and-stick animation into a RenderTexture,
/// so it can be dropped into the same RawImage the molecular videos already use.
///
/// How it stays out of the lab's way, with no scene or project-settings changes:
///
///  - The whole stage is built 8000 units below the lab. Nothing else is down there.
///  - The stage camera has a 60-unit far plane, so the lab is far outside its frustum. That is why
///    no culling layer had to be added to TagManager - adding one would have been a project-wide
///    edit for a single feature.
///  - Its two point lights have a 40-unit range, so they cannot spill onto the benches.
///  - stereoTargetEye is None and a targetTexture is assigned, so in VR it never renders to the
///    headset.
///
/// Colour is doing real work here, not decoration: bonds that are breaking go red and thin out,
/// bonds that are forming go green and thicken in. That is the one thing a student is meant to
/// take away from a molecular animation, so it is the one thing given its own visual channel.
/// </summary>
public class MolecularAnimationRenderer : MonoBehaviour
{
    public const int TextureWidth = 1280;
    public const int TextureHeight = 720;

    /// <summary>Far enough below the lab that the stage camera can never see it.</summary>
    private static readonly Vector3 StageOrigin = new Vector3(0.0f, -8000.0f, 0.0f);

    private static readonly Color BackgroundColour = new Color(0.035f, 0.045f, 0.075f, 1.0f);
    private static readonly Color BondColour = new Color(0.78f, 0.80f, 0.86f, 1.0f);
    private static readonly Color BreakingColour = new Color(1.00f, 0.30f, 0.25f, 1.0f);
    private static readonly Color FormingColour = new Color(0.32f, 0.95f, 0.45f, 1.0f);
    private static readonly Color ElectronColour = new Color(1.00f, 0.93f, 0.35f, 1.0f);

    // --- Scene -------------------------------------------------------------------------
    private MolecularScene scene;
    private Transform stageRoot;
    private Camera stageCamera;
    private RenderTexture output;

    private readonly List<Transform> atomObjects = new List<Transform>();
    private readonly List<TextMeshPro> atomLabels = new List<TextMeshPro>();
    private readonly List<TextMeshPro> atomNotes = new List<TextMeshPro>();

    private readonly List<Transform> bondPool = new List<Transform>();
    private readonly List<Renderer> bondRenderers = new List<Renderer>();
    private readonly List<Transform> electronPool = new List<Transform>();

    private TextMeshPro captionLabel;
    private TextMeshPro titleLabel;
    private TextMeshPro stageLabel;
    private TextMeshPro legendLabel;
    private Transform progressBar;

    // --- Playback ----------------------------------------------------------------------
    private float elapsed;
    private bool playing;
    private int lastStageIndex = -1;

    /// <summary>Extra hold on the last stage so the products stay on screen before it ends.</summary>
    public float tailHold = 2.0f;

    /// <summary>Degrees of gentle camera sway, so the model reads as three-dimensional.</summary>
    public float orbitDegrees = 7.0f;

    public bool IsPrepared { get { return scene != null && output != null; } }
    public bool IsPlaying { get { return playing; } }
    public MolecularScene Scene { get { return scene; } }
    public Texture OutputTexture { get { return output; } }

    public bool IsFinished
    {
        get { return scene != null && elapsed >= scene.TotalDuration + tailHold - 0.001f; }
    }

    public float NormalisedTime
    {
        get
        {
            if (scene == null)
            {
                return 0.0f;
            }
            float total = scene.TotalDuration + tailHold;
            return total <= 0.0f ? 0.0f : Mathf.Clamp01(elapsed / total);
        }
    }

    public int CurrentStageIndex { get; private set; }

    public string CurrentCaption
    {
        get
        {
            if (scene == null || CurrentStageIndex >= scene.stages.Count)
            {
                return string.Empty;
            }
            return scene.stages[CurrentStageIndex].caption;
        }
    }

    public string CurrentDetail
    {
        get
        {
            if (scene == null || CurrentStageIndex >= scene.stages.Count)
            {
                return string.Empty;
            }
            return scene.stages[CurrentStageIndex].detail;
        }
    }

    /// <summary>Fires when the stage changes, so a caller can narrate the new step.</summary>
    public event System.Action<int, string, string> StageChanged;

    // =====================================================================================
    // LIFECYCLE
    // =====================================================================================

    /// <summary>
    /// Builds the stage for one reaction. Returns false when there is no animation authored for it,
    /// so the caller can fall back to whatever it did before.
    /// </summary>
    public bool Prepare(int reactionId)
    {
        MolecularScene next = MolecularSceneCatalog.Get(reactionId);
        if (next == null || next.stages.Count == 0)
        {
            return false;
        }

        if (scene != null && scene.reactionId == reactionId && IsPrepared)
        {
            Restart();
            return true;
        }

        Teardown();
        scene = next;
        BuildStage();
        Restart();
        return true;
    }

    public void Play()
    {
        if (!IsPrepared)
        {
            return;
        }

        if (IsFinished)
        {
            Restart();
            return;
        }

        playing = true;
        SetCameraActive(true);
    }

    public void Pause()
    {
        playing = false;
    }

    public void Restart()
    {
        elapsed = 0.0f;
        lastStageIndex = -1;
        playing = true;
        SetCameraActive(true);
        Evaluate();
    }

    /// <summary>Stops playback and switches the camera off, but keeps the stage built for reuse.</summary>
    public void Stop()
    {
        playing = false;
        SetCameraActive(false);
    }

    void OnDestroy()
    {
        Teardown();
    }

    void LateUpdate()
    {
        if (!IsPrepared)
        {
            return;
        }

        if (playing)
        {
            // Unscaled: the pause menu and several reaction scripts do not agree about timeScale,
            // and a molecular animation should run at wall-clock speed regardless.
            elapsed += Time.unscaledDeltaTime;

            float limit = scene.TotalDuration + tailHold;
            if (elapsed >= limit)
            {
                elapsed = limit;
                playing = false;
            }
        }

        Evaluate();
    }

    // =====================================================================================
    // EVALUATION
    // =====================================================================================

    private void Evaluate()
    {
        int stageIndex;
        float t;
        ResolveStage(out stageIndex, out t);

        CurrentStageIndex = stageIndex;

        MolStage to = scene.stages[stageIndex];
        MolStage from = stageIndex > 0 ? scene.stages[stageIndex - 1] : to;

        // Ease so atoms accelerate out of one arrangement and settle into the next, instead of
        // sliding at a constant speed like a slideshow transition.
        float eased = t * t * (3.0f - 2.0f * t);

        ApplyAtoms(from, to, eased, t);
        ApplyBonds(from, to, eased);
        ApplyElectrons(to, t);
        ApplyChrome(stageIndex, to);
        ApplyCamera();

        if (stageIndex != lastStageIndex)
        {
            lastStageIndex = stageIndex;
            if (StageChanged != null)
            {
                StageChanged(stageIndex, to.caption, to.detail);
            }
        }
    }

    private void ResolveStage(out int stageIndex, out float t)
    {
        float cursor = 0.0f;

        for (int i = 0; i < scene.stages.Count; i++)
        {
            float duration = Mathf.Max(0.01f, scene.stages[i].duration);

            if (elapsed < cursor + duration)
            {
                stageIndex = i;
                t = Mathf.Clamp01((elapsed - cursor) / duration);
                return;
            }

            cursor += duration;
        }

        stageIndex = scene.stages.Count - 1;
        t = 1.0f;
    }

    private void ApplyAtoms(MolStage from, MolStage to, float eased, float rawT)
    {
        float jitter = Mathf.Lerp(from.thermalJitter, to.thermalJitter, rawT);

        for (int i = 0; i < atomObjects.Count; i++)
        {
            Vector3 position = Vector3.Lerp(from.positions[i], to.positions[i], eased);

            if (jitter > 0.0f)
            {
                // Perlin rather than Random so the shake is smooth instead of a strobe, and offset
                // per atom so they do not all wobble in lockstep.
                float seed = i * 13.37f;
                float time = Time.unscaledTime * 9.0f;
                position += new Vector3(
                    Mathf.PerlinNoise(seed, time) - 0.5f,
                    Mathf.PerlinNoise(seed + 5.0f, time) - 0.5f,
                    Mathf.PerlinNoise(seed + 9.0f, time) - 0.5f) * (jitter * 2.0f);
            }

            atomObjects[i].localPosition = position;

            string note;
            bool hasNote = to.notes.TryGetValue(i, out note);
            if (!hasNote && rawT < 0.5f)
            {
                hasNote = from.notes.TryGetValue(i, out note);
            }

            TextMeshPro noteLabel = atomNotes[i];
            if (hasNote && !string.IsNullOrEmpty(note))
            {
                if (noteLabel.text != note)
                {
                    noteLabel.text = note;
                }
                noteLabel.gameObject.SetActive(true);
            }
            else if (noteLabel.gameObject.activeSelf)
            {
                noteLabel.gameObject.SetActive(false);
            }
        }
    }

    private void ApplyBonds(MolStage from, MolStage to, float eased)
    {
        int used = 0;

        // Bonds that survive or form.
        for (int i = 0; i < to.bonds.Count; i++)
        {
            MolBond bond = to.bonds[i];
            bool existedBefore = Contains(from.bonds, bond.a, bond.b);
            float strength = existedBefore ? 1.0f : eased;
            Color tint = existedBefore ? BondColour : Color.Lerp(FormingColour, BondColour, eased);
            used = DrawBond(used, bond, strength, tint);
        }

        // Bonds that are breaking - present before, gone after.
        for (int i = 0; i < from.bonds.Count; i++)
        {
            MolBond bond = from.bonds[i];
            if (Contains(to.bonds, bond.a, bond.b))
            {
                continue;
            }
            used = DrawBond(used, bond, 1.0f - eased, Color.Lerp(BondColour, BreakingColour, eased));
        }

        for (int i = used; i < bondPool.Count; i++)
        {
            if (bondPool[i].gameObject.activeSelf)
            {
                bondPool[i].gameObject.SetActive(false);
            }
        }
    }

    private int DrawBond(int cursor, MolBond bond, float strength, Color tint)
    {
        if (strength <= 0.01f)
        {
            return cursor;
        }

        Vector3 a = atomObjects[bond.a].localPosition;
        Vector3 b = atomObjects[bond.b].localPosition;

        Vector3 axis = b - a;
        float length = axis.magnitude;
        if (length < 0.001f)
        {
            return cursor;
        }

        // Perpendicular used both to separate the rods of a multiple bond and to keep dashes tidy.
        Vector3 perpendicular = Vector3.Cross(axis.normalized, Vector3.forward);
        if (perpendicular.sqrMagnitude < 0.001f)
        {
            perpendicular = Vector3.Cross(axis.normalized, Vector3.up);
        }
        perpendicular = perpendicular.normalized;

        int rods = Mathf.Clamp(bond.order, 1, 3);
        float radius = (bond.order > 1 ? 0.055f : 0.075f) * strength;
        float spacing = 0.14f;

        for (int rod = 0; rod < rods; rod++)
        {
            float offsetAmount = (rod - (rods - 1) * 0.5f) * spacing;
            Vector3 offset = perpendicular * offsetAmount;

            if (bond.ionic)
            {
                // Ionic bonds are drawn as a dashed line, because nothing is being shared.
                const int Dashes = 5;
                for (int d = 0; d < Dashes; d++)
                {
                    float t0 = (d + 0.15f) / Dashes;
                    float t1 = (d + 0.85f) / Dashes;
                    cursor = PlaceRod(cursor,
                        a + axis * t0 + offset,
                        a + axis * t1 + offset,
                        radius * 0.9f, tint);
                }
            }
            else
            {
                cursor = PlaceRod(cursor, a + offset, b + offset, radius, tint);
            }
        }

        return cursor;
    }

    private int PlaceRod(int cursor, Vector3 a, Vector3 b, float radius, Color tint)
    {
        Transform rod = GetBond(cursor);
        Vector3 axis = b - a;
        float length = axis.magnitude;

        rod.localPosition = (a + b) * 0.5f;
        rod.localRotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);

        // Unity's cylinder primitive is two units tall, hence the half-length on Y.
        rod.localScale = new Vector3(radius * 2.0f, length * 0.5f, radius * 2.0f);

        SetColour(bondRenderers[cursor], tint, 0.35f);

        if (!rod.gameObject.activeSelf)
        {
            rod.gameObject.SetActive(true);
        }

        return cursor + 1;
    }

    /// <summary>
    /// One rod from the pool, growing it if a stage turns out to need more than the estimate.
    /// The estimate in BuildPools is a worst case, but growing costs one allocation once rather
    /// than silently dropping a bond the student is meant to see.
    /// </summary>
    private Transform GetBond(int index)
    {
        while (index >= bondPool.Count)
        {
            GameObject rod = CreatePrimitiveNoCollider(PrimitiveType.Cylinder, "Bond");
            rod.transform.SetParent(stageRoot, false);

            Renderer renderer = rod.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial(BondColour, 0.35f);

            rod.SetActive(false);
            bondPool.Add(rod.transform);
            bondRenderers.Add(renderer);
        }

        return bondPool[index];
    }

    private void ApplyElectrons(MolStage stage, float t)
    {
        int used = 0;

        for (int i = 0; i < stage.electrons.Count; i++)
        {
            MolElectron transfer = stage.electrons[i];
            int count = Mathf.Max(1, transfer.count);

            for (int e = 0; e < count; e++)
            {
                // Stagger so two electrons on the same path read as two, not one.
                float offset = count == 1 ? 0.0f : (e / (float)count) * 0.25f;
                float local = Mathf.Clamp01((t - offset) / Mathf.Max(0.05f, 1.0f - offset));

                if (local <= 0.0f || local >= 1.0f || used >= electronPool.Count)
                {
                    continue;
                }

                Vector3 a = atomObjects[transfer.from].localPosition;
                Vector3 b = atomObjects[transfer.to].localPosition;

                // Arc the path so the electron visibly travels rather than sliding through bonds.
                Vector3 straight = Vector3.Lerp(a, b, local);
                float lift = Mathf.Sin(local * Mathf.PI) * 0.55f;
                Vector3 side = Vector3.Cross((b - a).normalized, Vector3.forward).normalized;

                Transform dot = electronPool[used];
                dot.localPosition = straight + side * lift + Vector3.forward * -0.4f;

                float pulse = 0.11f + Mathf.Sin(Time.unscaledTime * 12.0f + e) * 0.012f;
                dot.localScale = Vector3.one * pulse;

                if (!dot.gameObject.activeSelf)
                {
                    dot.gameObject.SetActive(true);
                }

                used++;
            }
        }

        for (int i = used; i < electronPool.Count; i++)
        {
            if (electronPool[i].gameObject.activeSelf)
            {
                electronPool[i].gameObject.SetActive(false);
            }
        }
    }

    private void ApplyChrome(int stageIndex, MolStage stage)
    {
        if (captionLabel != null)
        {
            string caption = (stageIndex + 1) + ". " + stage.caption;
            if (captionLabel.text != caption)
            {
                captionLabel.text = caption;
            }
        }

        if (stageLabel != null)
        {
            string counter = "STEP " + (stageIndex + 1) + " OF " + scene.stages.Count;
            if (stageLabel.text != counter)
            {
                stageLabel.text = counter;
            }
        }

        if (progressBar != null)
        {
            float width = Mathf.Max(0.001f, 13.0f * NormalisedTime);
            progressBar.localScale = new Vector3(width, 0.09f, 0.05f);
            progressBar.localPosition = new Vector3(-6.5f + width * 0.5f, -4.32f, 0.4f);
        }

        if (stageCamera != null)
        {
            Color target = stage.backgroundTint.HasValue ? stage.backgroundTint.Value : BackgroundColour;
            stageCamera.backgroundColor = Color.Lerp(stageCamera.backgroundColor, target,
                Mathf.Clamp01(Time.unscaledDeltaTime * 3.0f));
        }
    }

    private void ApplyCamera()
    {
        if (stageCamera == null)
        {
            return;
        }

        float sway = Mathf.Sin(Time.unscaledTime * 0.35f) * orbitDegrees;
        Quaternion rotation = Quaternion.Euler(0.0f, sway, 0.0f);

        stageCamera.transform.localPosition = rotation * new Vector3(0.0f, 0.0f, -11.0f);
        stageCamera.transform.localRotation = Quaternion.LookRotation(
            -stageCamera.transform.localPosition.normalized, Vector3.up);

        // Labels billboard to the camera so they stay readable through the sway.
        Quaternion facing = stageCamera.transform.localRotation;
        for (int i = 0; i < atomLabels.Count; i++)
        {
            atomLabels[i].transform.localRotation = facing;
            atomNotes[i].transform.localRotation = facing;
        }
    }

    private static bool Contains(List<MolBond> bonds, int a, int b)
    {
        for (int i = 0; i < bonds.Count; i++)
        {
            if (bonds[i].a == a && bonds[i].b == b)
            {
                return true;
            }
        }
        return false;
    }

    // =====================================================================================
    // STAGE CONSTRUCTION
    // =====================================================================================

    private void BuildStage()
    {
        GameObject root = new GameObject("MolecularStage");
        root.transform.position = StageOrigin;
        stageRoot = root.transform;

        output = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32);
        output.name = "MolecularAnimationRT";
        output.antiAliasing = 4;
        output.Create();

        BuildCamera();
        BuildLights();
        BuildAtoms();
        BuildPools();
        BuildChrome();
    }

    private void BuildCamera()
    {
        GameObject cameraObject = new GameObject("MolecularStageCamera");
        cameraObject.transform.SetParent(stageRoot, false);

        stageCamera = cameraObject.AddComponent<Camera>();
        stageCamera.clearFlags = CameraClearFlags.SolidColor;
        stageCamera.backgroundColor = BackgroundColour;
        stageCamera.fieldOfView = 42.0f;
        stageCamera.nearClipPlane = 0.3f;

        // Short enough that the lab, 8000 units up, is never inside the frustum.
        stageCamera.farClipPlane = 60.0f;

        stageCamera.targetTexture = output;
        stageCamera.stereoTargetEye = StereoTargetEyeMask.None;
        stageCamera.allowHDR = false;
        stageCamera.allowMSAA = true;
        stageCamera.depth = -50;
        stageCamera.useOcclusionCulling = false;

        AudioListener strayListener = cameraObject.GetComponent<AudioListener>();
        if (strayListener != null)
        {
            Destroy(strayListener);
        }

        stageCamera.enabled = false;
    }

    private void BuildLights()
    {
        // Point lights with a short range: they light the model and cannot reach the laboratory.
        CreateLight(new Vector3(-4.0f, 4.0f, -7.0f), 1.5f);
        CreateLight(new Vector3(5.0f, -3.0f, -6.0f), 0.8f);
    }

    private void CreateLight(Vector3 position, float intensity)
    {
        GameObject lightObject = new GameObject("StageLight");
        lightObject.transform.SetParent(stageRoot, false);
        lightObject.transform.localPosition = position;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 40.0f;
        light.intensity = intensity;
        light.color = new Color(1.0f, 0.97f, 0.93f, 1.0f);
        light.shadows = LightShadows.None;
    }

    private void BuildAtoms()
    {
        for (int i = 0; i < scene.atoms.Count; i++)
        {
            string symbol = scene.atoms[i].symbol;
            float radius = MolecularPalette.Radius(symbol);
            Color colour = MolecularPalette.Colour(symbol);

            GameObject sphere = CreatePrimitiveNoCollider(PrimitiveType.Sphere, "Atom_" + symbol);
            sphere.transform.SetParent(stageRoot, false);
            sphere.transform.localScale = Vector3.one * (radius * 2.0f);

            Renderer renderer = sphere.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial(colour, 0.32f);

            atomObjects.Add(sphere.transform);

            // Symbol on the face of the atom.
            //
            // Both of these are children of the sphere, which is scaled by the atom's *diameter*.
            // So every offset and size below is in that scaled space, and the unit sphere's own
            // surface sits at 0.5 there - not at `radius`. Placing the label at -(radius + 0.02)
            // would bury it inside anything with a radius under 0.48, which is most of the table.
            TextMeshPro label = CreateText("Label_" + symbol, sphere.transform,
                Vector3.forward * -0.56f, 0.60f,
                MolecularPalette.LabelColour(symbol), TextAlignmentOptions.Center);
            label.text = symbol;

            // A constant font size in this space means the symbol scales with the atom, which is
            // what makes a big potassium read as big.
            label.rectTransform.sizeDelta = new Vector2(2.0f, 1.2f);
            atomLabels.Add(label);

            // Stage note under the atom: 0.30 world units clear of the surface.
            TextMeshPro note = CreateText("Note_" + symbol, sphere.transform,
                new Vector3(0.0f, -(radius + 0.30f) / (radius * 2.0f), -0.56f), 0.0f,
                new Color(0.62f, 0.86f, 1.0f, 1.0f), TextAlignmentOptions.Center);

            // The note is undone back to world scale, unlike the label: a caption has to stay
            // legible whether it hangs off a hydrogen or an iodine.
            note.transform.localScale = Vector3.one / (radius * 2.0f);
            note.fontSize = 0.30f;
            note.rectTransform.sizeDelta = new Vector2(3.6f, 0.8f);
            note.gameObject.SetActive(false);
            atomNotes.Add(note);
        }
    }

    private void BuildPools()
    {
        // Worst case across every stage, doubled for the frame where one set breaks as another
        // forms, and multiplied for dashed ionic bonds and multiple-bond rods.
        int maxBonds = 0;
        for (int i = 0; i < scene.stages.Count; i++)
        {
            maxBonds = Mathf.Max(maxBonds, scene.stages[i].bonds.Count);
        }

        int rodCount = Mathf.Max(24, maxBonds * 2 * 3 * 5);

        for (int i = 0; i < rodCount; i++)
        {
            GameObject rod = CreatePrimitiveNoCollider(PrimitiveType.Cylinder, "Bond");
            rod.transform.SetParent(stageRoot, false);

            Renderer renderer = rod.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial(BondColour, 0.35f);

            rod.SetActive(false);
            bondPool.Add(rod.transform);
            bondRenderers.Add(renderer);
        }

        int maxElectrons = 0;
        for (int i = 0; i < scene.stages.Count; i++)
        {
            int total = 0;
            for (int e = 0; e < scene.stages[i].electrons.Count; e++)
            {
                total += Mathf.Max(1, scene.stages[i].electrons[e].count);
            }
            maxElectrons = Mathf.Max(maxElectrons, total);
        }

        for (int i = 0; i < Mathf.Max(4, maxElectrons); i++)
        {
            GameObject dot = CreatePrimitiveNoCollider(PrimitiveType.Sphere, "Electron");
            dot.transform.SetParent(stageRoot, false);
            dot.transform.localScale = Vector3.one * 0.11f;
            dot.GetComponent<Renderer>().sharedMaterial = CreateMaterial(ElectronColour, 1.0f);
            dot.SetActive(false);
            electronPool.Add(dot.transform);
        }
    }

    private void BuildChrome()
    {
        titleLabel = CreateText("Title", stageRoot, new Vector3(0.0f, 4.05f, 0.5f), 0.62f,
            new Color(0.55f, 0.88f, 1.0f, 1.0f), TextAlignmentOptions.Center);
        titleLabel.text = scene.title + "    " + PrettyEquation(scene.equation);
        titleLabel.rectTransform.sizeDelta = new Vector2(15.0f, 1.0f);

        stageLabel = CreateText("StageCounter", stageRoot, new Vector3(-6.5f, 3.35f, 0.5f), 0.38f,
            new Color(0.55f, 0.62f, 0.74f, 1.0f), TextAlignmentOptions.Left);
        stageLabel.rectTransform.sizeDelta = new Vector2(6.0f, 0.7f);

        legendLabel = CreateText("Legend", stageRoot, new Vector3(6.5f, 3.35f, 0.5f), 0.34f,
            new Color(0.55f, 0.62f, 0.74f, 1.0f), TextAlignmentOptions.Right);
        legendLabel.text = "<color=#FF4D40>red = bond breaking</color>   " +
                           "<color=#52F273>green = bond forming</color>   " +
                           "<color=#FFED59>yellow = electron</color>";
        legendLabel.rectTransform.sizeDelta = new Vector2(9.0f, 0.7f);

        captionLabel = CreateText("Caption", stageRoot, new Vector3(0.0f, -3.85f, 0.5f), 0.5f,
            Color.white, TextAlignmentOptions.Center);
        captionLabel.rectTransform.sizeDelta = new Vector2(14.5f, 1.1f);

        GameObject bar = CreatePrimitiveNoCollider(PrimitiveType.Cube, "ProgressBar");
        bar.transform.SetParent(stageRoot, false);
        bar.GetComponent<Renderer>().sharedMaterial = CreateMaterial(
            new Color(0.30f, 0.72f, 1.0f, 1.0f), 0.9f);
        progressBar = bar.transform;

        GameObject track = CreatePrimitiveNoCollider(PrimitiveType.Cube, "ProgressTrack");
        track.transform.SetParent(stageRoot, false);
        track.transform.localPosition = new Vector3(0.0f, -4.32f, 0.55f);
        track.transform.localScale = new Vector3(13.0f, 0.05f, 0.04f);
        track.GetComponent<Renderer>().sharedMaterial = CreateMaterial(
            new Color(0.16f, 0.19f, 0.26f, 1.0f), 0.25f);
    }

    // =====================================================================================
    // HELPERS
    // =====================================================================================

    private static GameObject CreatePrimitiveNoCollider(PrimitiveType type, string name)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            // Primitives ship with a collider. Nothing should ever be able to raycast the stage.
            Destroy(collider);
        }

        return go;
    }

    /// <summary>
    /// Standard with emission, falling back to Unlit/Color. The emission matters: the lab's own
    /// lighting varies from scene to scene, and a molecular diagram that dims with the room is
    /// unreadable. Emission guarantees a floor on brightness.
    /// </summary>
    private static Material CreateMaterial(Color colour, float emission)
    {
        Shader shader = Shader.Find("Standard");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material flat = new Material(shader);
            flat.color = colour;
            return flat;
        }

        Material material = new Material(shader);
        material.color = colour;
        material.SetFloat("_Glossiness", 0.35f);
        material.SetFloat("_Metallic", 0.0f);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        material.SetColor("_EmissionColor", colour * emission);
        return material;
    }

    private static void SetColour(Renderer renderer, Color colour, float emission)
    {
        Material material = renderer.sharedMaterial;
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Color"))
        {
            material.color = colour;
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", colour * emission);
        }
    }

    private static TextMeshPro CreateText(
        string name, Transform parent, Vector3 localPosition, float fontSize,
        Color colour, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.alignment = alignment;
        text.color = colour;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        if (fontSize > 0.0f)
        {
            text.fontSize = fontSize;
        }

        text.rectTransform.sizeDelta = new Vector2(6.0f, 1.0f);
        return text;
    }

    /// <summary>Turns "2Na + 2H2O -> 2NaOH + H2" into subscripted rich text.</summary>
    private static string PrettyEquation(string equation)
    {
        if (string.IsNullOrEmpty(equation))
        {
            return string.Empty;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(equation.Length + 16);

        for (int i = 0; i < equation.Length; i++)
        {
            char c = equation[i];

            if (c == '-' && i + 1 < equation.Length && equation[i + 1] == '>')
            {
                builder.Append('→');
                i++;
                continue;
            }

            // A digit is a subscript only when it follows a letter or a closing bracket; a leading
            // coefficient like the 2 in "2Na" must stay full size.
            if (char.IsDigit(c) && i > 0 && (char.IsLetter(equation[i - 1]) || equation[i - 1] == ')'))
            {
                builder.Append("<sub>").Append(c).Append("</sub>");
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private void SetCameraActive(bool active)
    {
        if (stageCamera != null)
        {
            stageCamera.enabled = active;
        }
    }

    private void Teardown()
    {
        if (stageRoot != null)
        {
            Destroy(stageRoot.gameObject);
            stageRoot = null;
        }

        if (output != null)
        {
            output.Release();
            Destroy(output);
            output = null;
        }

        stageCamera = null;
        scene = null;

        atomObjects.Clear();
        atomLabels.Clear();
        atomNotes.Clear();
        bondPool.Clear();
        bondRenderers.Clear();
        electronPool.Clear();

        captionLabel = null;
        titleLabel = null;
        stageLabel = null;
        legendLabel = null;
        progressBar = null;
    }
}
