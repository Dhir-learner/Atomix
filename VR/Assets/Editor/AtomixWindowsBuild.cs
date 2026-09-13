using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Release build for the itch.io Windows download: 64-bit, non-development, every scene enabled in
/// Build Settings, output to Builds/Windows/Atomix.exe with the third-party notices beside it.
/// </summary>
public static class AtomixWindowsBuild
{
    private const string OutputFolder = "Builds/Windows";
    private const string ExecutableName = "Atomix.exe";
    private const string NoticesFile = "THIRD_PARTY_NOTICES.txt";

    [MenuItem("Atomix/Build Windows (itch.io)")]
    public static void BuildFromMenu()
    {
        Build();
    }

    /// <summary>Unity.exe -batchmode -quit -projectPath VR -executeMethod AtomixWindowsBuild.BuildFromCommandLine</summary>
    public static void BuildFromCommandLine()
    {
        if (!Build())
        {
            EditorApplication.Exit(1);
        }
    }

    private static bool Build()
    {
        // Every file in a player build can be extracted, so a key here is a key given to every buyer.
        LabAssistantSettings assistant = LabAssistantSettings.Load();
        if (assistant != null && !string.IsNullOrEmpty(assistant.convaiApiKey))
        {
            Debug.LogError("Atomix build stopped: Assets/Resources/LabAssistantSettings.asset contains a " +
                           "Convai API key. Clear it before building a public release.");
            return false;
        }

        string[] scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
        if (scenes.Length == 0)
        {
            Debug.LogError("Atomix build stopped: no scenes are enabled in Build Settings.");
            return false;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string noticesSource = Path.Combine(projectRoot, "BuildExtras", NoticesFile);
        if (!File.Exists(noticesSource))
        {
            Debug.LogError("Atomix build stopped: " + noticesSource + " is missing. The MIT and asset " +
                           "licenses require their notices to ship with the game.");
            return false;
        }

        // Unity leaves files from older builds in place, and they would end up in the zip.
        string outputDir = Path.Combine(projectRoot, OutputFolder);
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outputDir, ExecutableName),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("Atomix build failed: " + report.summary.result + ", " +
                           report.summary.totalErrors + " error(s). See the log above.");
            return false;
        }

        // Burst writes debug symbols to a folder it names *_DoNotShip.
        foreach (string directory in Directory.GetDirectories(outputDir, "*_DoNotShip"))
        {
            Directory.Delete(directory, true);
        }

        File.Copy(noticesSource, Path.Combine(outputDir, NoticesFile), true);

        Debug.Log("Atomix Windows build succeeded: " + outputDir + " (" +
                  (report.summary.totalSize / (1024 * 1024)) + " MB reported by Unity)");
        return true;
    }
}
