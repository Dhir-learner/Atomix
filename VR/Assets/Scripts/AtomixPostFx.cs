using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Gives Atomix a colour grade, a bloom, a vignette, contact shadowing and edge antialiasing.
///
/// The project already depended on <c>com.unity.postprocessing</c> 3.5.1 - it is in
/// <c>Packages/manifest.json</c> and its assembly is referenced by <c>Assembly-CSharp</c> - and
/// used <b>none of it</b>. Not one scene contained a <c>PostProcessVolume</c>. The laboratory was
/// rendering raw forward-lit built-in-pipeline output in linear colour: flat, slightly grey, with
/// hard aliased edges on every piece of glassware and no sense that light in the room comes from
/// anywhere in particular.
///
/// The grade here is deliberately restrained. This is a teaching tool: a student has to read
/// numbers off a burette and text off a floating label, so the pass is tuned to make the room look
/// like a room, not to look like a film.
///
/// <list type="bullet">
/// <item><b>Bloom</b> is thresholded above white, so it catches the burner flame, the tap and
/// specular highlights on wet glass - and leaves the UI alone.</item>
/// <item><b>Colour grading</b> is ACES with a slightly cool white point. Laboratories are lit by
/// fluorescent tubes, and the warm default made the room look like a kitchen.</item>
/// <item><b>Ambient occlusion</b> is what actually sells the bench: without contact shadowing,
/// every beaker looks like it is hovering a millimetre above the surface it is standing on.</item>
/// <item><b>Antialiasing</b> is the single biggest legibility win. The scene is full of thin
/// glass rims and burette graduations, and the project ships with MSAA off on four of its six
/// quality levels.</item>
/// </list>
///
/// Built entirely at runtime, like every system added since Task 1, so no scene file changes and
/// VR authoring is untouched. It attaches only to the player's camera - never to the off-screen
/// camera that renders the molecular animation to a <c>RenderTexture</c>, which would grade the
/// animation stage and cost a full post stack on a 512-pixel target.
///
/// The whole pass is switchable from the pause menu, and the expensive half of it
/// (ambient occlusion, SMAA) is only enabled at the higher quality levels.
/// </summary>
[DisallowMultipleComponent]
public class AtomixPostFx : MonoBehaviour
{
    /// <summary>Layer 9, added to the project alongside this class.</summary>
    public const string VolumeLayerName = "PostProcessing";

    /// <summary>
    /// Where the shader set lives, relative to a <c>Resources</c> folder.
    ///
    /// <b>This is not optional and it is the reason a copy of the asset was added to
    /// <c>Assets/Resources</c>.</b> A <c>PostProcessLayer</c> added with <c>AddComponent</c>
    /// runs <c>OnEnable</c>, which calls <c>Init(null)</c> and leaves its internal
    /// <c>m_Resources</c> field null - the field is normally filled in by the inspector, and
    /// there is no inspector here. The layer then null-references every frame it tries to build
    /// a command buffer, which is every rendered frame.
    ///
    /// The package's own copy lives under <c>Packages/</c> and is not reachable by
    /// <c>Resources.Load</c> in a player build, so a copy sits in <c>Assets/Resources</c>. It
    /// references the same package shaders by GUID, so it is a pointer, not a duplication of the
    /// shaders themselves - about 9 kB of YAML.
    /// </summary>
    public const string ResourcesAssetName = "PostProcessResources";

    /// <summary>Loaded once and shared; null if the asset is missing, which disables the stack.</summary>
    private static PostProcessResources sharedResources;
    private static bool resourcesLookupDone;

    private static AtomixPostFx instance;

    private PostProcessLayer cameraLayer;
    private PostProcessVolume volume;
    private PostProcessProfile profile;
    private Camera targetCamera;

    private Bloom bloom;
    private ColorGrading grading;
    private Vignette vignette;
    private AmbientOcclusion occlusion;

    private bool menuGrade;

    /// <summary>
    /// Attaches the stack to <paramref name="camera"/>, or moves it there if it is already
    /// running on a different one. Called by <see cref="DesktopBootstrap"/> once the desktop rig
    /// exists; safe to call repeatedly.
    /// </summary>
    public static void Attach(Camera camera, bool isMenuScene)
    {
        if (camera == null || camera.targetTexture != null)
        {
            return;
        }

        if (instance == null)
        {
            GameObject host = new GameObject("AtomixPostFx");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<AtomixPostFx>();
        }

        instance.Bind(camera, isMenuScene);
    }

    /// <summary>Re-reads the settings. Called when the pause menu changes anything.</summary>
    public static void Refresh()
    {
        if (instance != null)
        {
            instance.ApplySettings();
        }
    }

    void OnEnable()
    {
        AtomixSettings.Changed += ApplySettings;
    }

    void OnDisable()
    {
        AtomixSettings.Changed -= ApplySettings;
    }

    void OnDestroy()
    {
        // The profile is a runtime ScriptableObject; nothing else will collect it.
        if (profile != null)
        {
            Destroy(profile);
            profile = null;
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    private void Bind(Camera camera, bool isMenuScene)
    {
        bool gradeChanged = menuGrade != isMenuScene;
        menuGrade = isMenuScene;

        if (targetCamera != camera)
        {
            // The old camera belonged to a scene that has been unloaded; its layer went with it.
            targetCamera = camera;
            cameraLayer = camera.GetComponent<PostProcessLayer>();
            if (cameraLayer == null)
            {
                cameraLayer = camera.gameObject.AddComponent<PostProcessLayer>();
            }

            // Must happen before the first frame is rendered. AddComponent has already run
            // OnEnable with no resources; nothing dereferences them until rendering, which is
            // later in this same frame.
            PostProcessResources resources = LoadResources();
            if (resources == null)
            {
                // Better a flat image than a null reference on every rendered frame.
                cameraLayer.enabled = false;
                return;
            }

            cameraLayer.Init(resources);
            cameraLayer.volumeTrigger = camera.transform;
            cameraLayer.volumeLayer = VolumeMask();
        }

        EnsureVolume();

        if (gradeChanged)
        {
            TuneForScene();
        }

        ApplySettings();
    }

    /// <summary>
    /// The mask the camera looks for volumes on. Falls back to everything if the project layer
    /// is missing, because a stack that silently does nothing is worse than one extra layer test.
    /// </summary>
    private static LayerMask VolumeMask()
    {
        int layer = LayerMask.NameToLayer(VolumeLayerName);
        return layer >= 0 ? (LayerMask)(1 << layer) : (LayerMask)~0;
    }

    private static PostProcessResources LoadResources()
    {
        if (resourcesLookupDone)
        {
            return sharedResources;
        }

        resourcesLookupDone = true;
        sharedResources = Resources.Load<PostProcessResources>(ResourcesAssetName);

        if (sharedResources == null)
        {
            // In the editor the package's own copy is usually already loaded, so this rescues a
            // project where the Assets/Resources copy has been deleted. It cannot be relied on
            // in a build, which is exactly why the copy exists.
            PostProcessResources[] loaded = Resources.FindObjectsOfTypeAll<PostProcessResources>();
            if (loaded != null && loaded.Length > 0)
            {
                sharedResources = loaded[0];
            }
        }

        if (sharedResources == null)
        {
            Debug.LogWarning("[Atomix] " + ResourcesAssetName + " is missing from Resources; " +
                             "post-processing is disabled. Restore " +
                             "Assets/Resources/" + ResourcesAssetName + ".asset to re-enable it.");
        }

        return sharedResources;
    }

    private void EnsureVolume()
    {
        if (volume != null)
        {
            return;
        }

        profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        profile.name = "AtomixRuntimeProfile";

        bloom = profile.AddSettings<Bloom>();
        grading = profile.AddSettings<ColorGrading>();
        vignette = profile.AddSettings<Vignette>();
        occlusion = profile.AddSettings<AmbientOcclusion>();

        GameObject volumeObject = new GameObject("AtomixPostFxVolume");
        volumeObject.transform.SetParent(transform, false);

        int layer = LayerMask.NameToLayer(VolumeLayerName);
        volumeObject.layer = layer >= 0 ? layer : 0;

        volume = volumeObject.AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.priority = 100.0f;
        volume.weight = 1.0f;
        volume.profile = profile;

        TuneForScene();
    }

    // =========================================================
    // GRADE
    // =========================================================

    private void TuneForScene()
    {
        if (profile == null)
        {
            return;
        }

        TuneBloom();
        TuneGrading();
        TuneVignette();
        TuneOcclusion();
    }

    private void TuneBloom()
    {
        bloom.active = true;

        // Above 1.0 in linear space, so it only ever catches genuine highlights - the burner
        // flame, the specular hit on wet glass, the tap. Pure white UI text sits at exactly 1.0
        // and is left alone, which matters: MainMenuScene's menu is a world-space canvas and
        // would otherwise be inside the effect.
        // Raised well above white and cut hard. The first version bloomed at 1.10 with an
        // intensity of 2.1, and in a white-walled laboratory under bright lights a great many
        // pixels sit just above 1.0 - so instead of catching the burner flame it laid a haze over
        // the whole room, which is a large part of why the image was reported as looking dull.
        // Bloom should be something you notice on the flame and nowhere else.
        bloom.threshold.Override(menuGrade ? 1.45f : 1.35f);
        bloom.softKnee.Override(0.35f);
        bloom.intensity.Override(menuGrade ? 0.5f : 0.75f);
        bloom.diffusion.Override(5.0f);
        bloom.anamorphicRatio.Override(0.0f);
        bloom.dirtIntensity.Override(0.0f);

        // Fast mode halves the resolution of the blur pyramid. Free on a scene this simple, and
        // it is the difference between bloom being affordable on a classroom laptop and not.
        bloom.fastMode.Override(QualitySettings.GetQualityLevel() < 3);
    }

    /// <summary>
    /// Colour grading is <b>off</b>, and that is the whole of this method.
    ///
    /// The first version tonemapped with ACES, lifted the shadows off black, cooled the white
    /// point and pushed contrast and saturation. On paper that is a normal filmic grade. In this
    /// project it was simply wrong, and the report back was exact: <i>"the colour contrast was
    /// perfect before, now it is looking dull"</i>.
    ///
    /// Two reasons, both avoidable:
    ///
    /// <list type="number">
    /// <item><b>ACES is a display transform for HDR content.</b> Atomix's lighting is authored to
    /// look right as it is, so tonemapping it a second time compresses highlights that were never
    /// blown and desaturates colour that was already correct. White walls go grey.</item>
    /// <item><b>The shadow lift removes contrast by definition.</b> Raising black to 0.012 to
    /// avoid "holes" in the geometry costs exactly the depth that made the bench read as solid.</item>
    /// </list>
    ///
    /// The authored look was good. The right amount of grading to apply to it is none - so the
    /// pass now contributes antialiasing, contact shadowing and a restrained bloom, and does not
    /// touch a single colour value.
    /// </summary>
    private void TuneGrading()
    {
        grading.active = false;
    }

    /// <summary>
    /// The vignette is off, for the same reason the grade is.
    ///
    /// It darkens the edge of the frame, which is where the measurement readout, the objective
    /// line and the failure explanation all live - and darkening the corners of an already
    /// even-looking image is a direct contributor to it reading as dull. A laboratory is evenly
    /// lit; pretending otherwise gains nothing here.
    /// </summary>
    private void TuneVignette()
    {
        vignette.active = false;
    }

    private void TuneOcclusion()
    {
        // Scalable rather than multi-scale: multi-scale needs compute shader support, which is
        // not guaranteed on the hardware this is likely to be run on.
        occlusion.mode.Override(AmbientOcclusionMode.ScalableAmbientObscurance);

        // A small radius. The interesting occlusion here is where a beaker meets the bench, not
        // where the wall meets the ceiling - a large radius would just grey the whole room.
        occlusion.radius.Override(0.22f);
        occlusion.intensity.Override(0.60f);
        occlusion.quality.Override(AmbientOcclusionQuality.Medium);
        occlusion.color.Override(new Color(0.03f, 0.04f, 0.06f, 1.0f));
    }

    // =========================================================
    // SETTINGS
    // =========================================================

    private void ApplySettings()
    {
        if (cameraLayer == null)
        {
            return;
        }

        bool hasResources = LoadResources() != null;
        bool wanted = AtomixSettings.PostFx && hasResources;
        int quality = QualitySettings.GetQualityLevel();

        // The layer stays on whenever it can render at all, because antialiasing is worth having
        // on its own - it is the one part of this pass that makes text and glassware rims easier
        // to read rather than merely prettier.
        cameraLayer.enabled = hasResources;

        // The grade, however, is laboratory-only.
        //
        // MainMenuScene's menu is a *world-space* canvas 1.1 m in front of the camera, which means
        // it is inside the post stack rather than composited over it. ACES would pull its white
        // text down towards grey and the vignette would dim whichever corner a button happened to
        // sit in - a real legibility cost, on the one screen every student sees first, to grade a
        // flat panel that has no lighting to grade. So the menu gets crisp edges and nothing else.
        if (volume != null)
        {
            volume.enabled = wanted && !menuGrade;
        }

        // Antialiasing is worth having even when the rest of the grade is off - it is the one
        // part of this pass that makes text and glassware rims easier to read rather than
        // prettier - so it is driven by quality alone.
        cameraLayer.antialiasingMode = quality >= 3
            ? PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing
            : PostProcessLayer.Antialiasing.FastApproximateAntialiasing;

        if (cameraLayer.antialiasingMode == PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing)
        {
            cameraLayer.subpixelMorphologicalAntialiasing.quality = SubpixelMorphologicalAntialiasing.Quality.Medium;
        }
        else
        {
            cameraLayer.fastApproximateAntialiasing.fastMode = quality < 2;
            cameraLayer.fastApproximateAntialiasing.keepAlpha = false;
        }

        if (occlusion != null)
        {
            // The most expensive thing in the stack, and the first thing to go on a weak machine.
            occlusion.active = wanted && !menuGrade && quality >= 3;
        }

        if (bloom != null)
        {
            bloom.fastMode.Override(quality < 3);
        }
    }
}
