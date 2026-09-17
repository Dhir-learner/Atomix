using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-off project maintenance for the mobile port. Each step can be run from the Atomix menu or
/// from the command line with -executeMethod.
/// </summary>
public static class AtomixProjectTools
{
    private const string HandsFolder = "Assets/Oculus Hands";
    private const string HandAnimationScript = "Assets/Scripts/HandAnimationController.cs";

    // -------------------------------------------------------------------------------------------
    // Oculus Hands removal
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Meta's SDK license limits the Oculus Hands models to Meta devices, and neither desktop nor
    /// touch play shows them. Removes their instances from every build scene, then the assets.
    /// </summary>
    [MenuItem("Atomix/Maintenance/Remove Oculus Hands")]
    public static void RemoveOculusHands()
    {
        if (!RemoveOculusHandsInternal())
        {
            Debug.LogError("Atomix: Oculus Hands removal did not complete. See the messages above.");
        }
    }

    public static void RemoveOculusHandsFromCommandLine()
    {
        EditorApplication.Exit(RemoveOculusHandsInternal() ? 0 : 1);
    }

    private static bool RemoveOculusHandsInternal()
    {
        if (!AssetDatabase.IsValidFolder(HandsFolder))
        {
            Debug.Log("Atomix: " + HandsFolder + " is already gone.");
            return true;
        }

        bool ok = true;
        int removed = 0;

        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled)
            {
                continue;
            }

            var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            var roots = new List<GameObject>();

            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.scene != scene || !PrefabUtility.IsAnyPrefabInstanceRoot(go))
                {
                    continue;
                }

                string source = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go));
                if (source.StartsWith(HandsFolder + "/"))
                {
                    roots.Add(go);
                }
            }

            foreach (GameObject go in roots)
            {
                if (go == null)
                {
                    continue;
                }

                // A hand that belongs to another prefab's asset cannot be deleted from the scene;
                // it would have to be removed from that prefab instead.
                Transform parent = go.transform.parent;
                if (parent != null && PrefabUtility.IsPartOfPrefabInstance(parent.gameObject) &&
                    !PrefabUtility.IsAddedGameObjectOverride(go))
                {
                    Debug.LogError("Atomix: '" + go.name + "' in " + buildScene.path +
                                   " is part of another prefab and was not removed.");
                    ok = false;
                    continue;
                }

                Debug.Log("Atomix: removing '" + go.name + "' from " + buildScene.path);
                Object.DestroyImmediate(go);
                removed++;
            }

            // Matched by name: this tool deletes that script, and a type reference here would stop
            // the Editor assembly compiling afterwards.
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour != null && behaviour.GetType().Name == "HandAnimationController")
                {
                    Debug.LogError("Atomix: HandAnimationController still on '" + behaviour.name + "' in " +
                                   buildScene.path + ".");
                    ok = false;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Atomix: could not save " + buildScene.path);
                ok = false;
            }
        }

        Debug.Log("Atomix: removed " + removed + " hand model instance(s).");

        if (!ok)
        {
            return false;
        }

        AssetDatabase.DeleteAsset(HandAnimationScript);
        AssetDatabase.DeleteAsset(HandsFolder);
        AssetDatabase.SaveAssets();
        Debug.Log("Atomix: deleted " + HandsFolder + " and " + HandAnimationScript + ".");
        return true;
    }

    // -------------------------------------------------------------------------------------------
    // Mobile video compression
    // -------------------------------------------------------------------------------------------

    private static readonly string[] MobilePlatforms = { "Android", "iPhone" };

    /// <summary>
    /// Adds Android and iOS overrides that transcode every video to 1280x720 H.264. The Windows
    /// build keeps the original 1080p files.
    /// </summary>
    [MenuItem("Atomix/Maintenance/Apply Mobile Video Compression")]
    public static void ApplyMobileVideoCompression()
    {
        ApplyMobileVideoCompressionInternal();
    }

    public static void ApplyMobileVideoCompressionFromCommandLine()
    {
        EditorApplication.Exit(ApplyMobileVideoCompressionInternal() > 0 ? 0 : 1);
    }

    private static int ApplyMobileVideoCompressionInternal()
    {
        string[] guids = AssetDatabase.FindAssets("t:VideoClip", new[] { "Assets" });
        int changed = 0;

        foreach (string path in guids.Select(AssetDatabase.GUIDToAssetPath))
        {
            VideoClipImporter importer = AssetImporter.GetAtPath(path) as VideoClipImporter;
            if (importer == null)
            {
                continue;
            }

            foreach (string platform in MobilePlatforms)
            {
                VideoImporterTargetSettings settings = importer.GetTargetSettings(platform) ??
                                                        importer.defaultTargetSettings;
                settings.enableTranscoding = true;
                settings.codec = VideoCodec.H264;
                settings.resizeMode = VideoResizeMode.CustomSize;
                settings.customWidth = 1280;
                settings.customHeight = 720;
                settings.bitrateMode = VideoBitrateMode.Low;
                settings.spatialQuality = VideoSpatialQuality.MediumSpatialQuality;
                importer.SetTargetSettings(platform, settings);
            }

            importer.SaveAndReimport();
            Debug.Log("Atomix: mobile video override set for " + path +
                      " (" + (new FileInfo(path).Length / (1024 * 1024)) + " MB source)");
            changed++;
        }

        Debug.Log("Atomix: mobile video compression applied to " + changed + " clip(s).");
        return changed;
    }
}
