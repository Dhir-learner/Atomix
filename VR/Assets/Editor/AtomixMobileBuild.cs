using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Android builds of the free, ad-supported mobile version.
///
/// Test APK: Google's test ads, debug signing - for installing on your own phone.
/// Google Play AAB: refuses to build until real AdMob IDs and a release keystore are in place.
///
/// Only Android settings are changed; the Windows itch.io build is unaffected.
/// </summary>
public static class AtomixMobileBuild
{
    private const string ApplicationId = "com.TeamAtomix.Atomix";
    private const string AdsDefine = "ATOMIX_ADS";
    private const string OutputFolder = "Builds/Android";
    private const string AdsScriptPath = "Assets/Scripts/Ads/AtomixAds.cs";

    [MenuItem("Atomix/Build Android (test APK)")]
    public static void BuildTestApkFromMenu()
    {
        Build(false);
    }

    [MenuItem("Atomix/Build Android (Google Play AAB)")]
    public static void BuildReleaseFromMenu()
    {
        Build(true);
    }

    public static void BuildTestApkFromCommandLine()
    {
        if (!Build(false))
        {
            EditorApplication.Exit(1);
        }
    }

    public static void BuildReleaseFromCommandLine()
    {
        if (!Build(true))
        {
            EditorApplication.Exit(1);
        }
    }

    private static bool Build(bool release)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            return Fail("Android Build Support is not installed for this Unity version (Unity Hub > Installs > Add modules).");
        }

        // Unity saves this preference as off when the Editor was opened before its bundled JDK was
        // installed, and then reports "JDK not found" even though the JDK is there.
        if (!EditorPrefs.GetBool("JdkUseEmbedded", false) &&
            string.IsNullOrEmpty(EditorPrefs.GetString("JdkPath", string.Empty)))
        {
            EditorPrefs.SetBool("JdkUseEmbedded", true);
            Debug.Log("Atomix: switched on the JDK installed with Unity (Preferences > External Tools).");
        }

        LabAssistantSettings assistant = LabAssistantSettings.Load();
        if (assistant != null && !string.IsNullOrEmpty(assistant.convaiApiKey))
        {
            return Fail("Assets/Resources/LabAssistantSettings.asset contains a Convai API key. Clear it first.");
        }

        if (release && !ReleaseReady())
        {
            return false;
        }

        if (!EnsureAdMobAppId(release))
        {
            return false;
        }

        ConfigureAndroidPlayer();
        EditorUserBuildSettings.buildAppBundle = release;

        string[] scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputDir = Path.Combine(projectRoot, OutputFolder);
        Directory.CreateDirectory(outputDir);
        string outputPath = Path.Combine(outputDir, release ? "Atomix.aab" : "Atomix-test.apk");

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            return Fail("Android build failed: " + report.summary.result + ", " + report.summary.totalErrors + " error(s).");
        }

        long bytes = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
        Debug.Log("Atomix Android build succeeded: " + outputPath + " (" + (bytes / (1024 * 1024)) + " MB)");
        return true;
    }

    private static void ConfigureAndroidPlayer()
    {
        NamedBuildTarget android = NamedBuildTarget.Android;

        PlayerSettings.SetApplicationIdentifier(android, ApplicationId);
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        // Google Play requires Android 16 (API 36) for new apps and updates from 31 Aug 2026.
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;

        // Vulkan first, with OpenGL ES 3 for phones whose Vulkan drivers are missing or unreliable.
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
            new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

        // Touch controls are laid out for landscape only.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        string defines = PlayerSettings.GetScriptingDefineSymbols(android);
        if (!Regex.IsMatch(defines, @"(^|;)" + AdsDefine + @"(;|$)"))
        {
            PlayerSettings.SetScriptingDefineSymbols(android,
                string.IsNullOrEmpty(defines) ? AdsDefine : defines + ";" + AdsDefine);
        }
    }

    private static bool ReleaseReady()
    {
        // Read from the source file: AtomixAds only compiles when ATOMIX_ADS is defined, which the
        // Editor's own assembly may not have yet.
        string source = File.Exists(AdsScriptPath) ? File.ReadAllText(AdsScriptPath) : string.Empty;
        Match unit = Regex.Match(source, "AndroidInterstitialId\\s*=\\s*\"([^\"]*)\"");
        if (!unit.Success || unit.Groups[1].Value.Length == 0)
        {
            return Fail("No real AdMob interstitial ad unit ID. Set AndroidInterstitialId in " + AdsScriptPath + ".");
        }

        if (!PlayerSettings.Android.useCustomKeystore || !File.Exists(PlayerSettings.Android.keystoreName))
        {
            return Fail("No release keystore. Project Settings > Player > Android > Publishing Settings > Keystore Manager.");
        }

        if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) ||
            string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
        {
            return Fail("Enter the keystore and key passwords in Publishing Settings before building (Unity does not save them).");
        }

        return true;
    }

    // Google's published sample app ID. It only ever serves test ads.
    private const string SampleAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
    private const string AdMobSettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";

    /// <summary>
    /// The Google Mobile Ads plugin stops every Android build that has no AdMob app ID. Test builds
    /// get Google's sample ID; store builds must have the real one from the AdMob dashboard.
    /// </summary>
    private static bool EnsureAdMobAppId(bool release)
    {
        ScriptableObject settings = LoadAdMobSettings();
        if (settings == null)
        {
            return Fail("Google Mobile Ads settings not found - is the plugin imported (Assets/GoogleMobileAds)?");
        }

        SerializedObject serialized = new SerializedObject(settings);
        SerializedProperty appId = serialized.FindProperty("adMobAndroidAppId");
        if (appId == null)
        {
            return Fail("The Google Mobile Ads plugin changed its settings format; set the app ID in Assets > Google Mobile Ads > Settings.");
        }

        if (release)
        {
            if (string.IsNullOrEmpty(appId.stringValue) || appId.stringValue == SampleAndroidAppId)
            {
                return Fail("Enter your real Android AdMob app ID in Assets > Google Mobile Ads > Settings.");
            }
            return true;
        }

        if (string.IsNullOrEmpty(appId.stringValue))
        {
            appId.stringValue = SampleAndroidAppId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("Atomix: using Google's sample AdMob app ID for this test build.");
        }
        return true;
    }

    private static ScriptableObject LoadAdMobSettings()
    {
        ScriptableObject existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AdMobSettingsPath);
        if (existing != null)
        {
            return existing;
        }

        // The settings class is internal to the plugin; its own LoadInstance creates the asset.
        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings");
            if (type == null)
            {
                continue;
            }

            System.Reflection.MethodInfo load = type.GetMethod("LoadInstance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            return load != null ? load.Invoke(null, null) as ScriptableObject : null;
        }
        return null;
    }

    private static bool Fail(string message)
    {
        Debug.LogError("Atomix Android build stopped: " + message);
        return false;
    }
}
