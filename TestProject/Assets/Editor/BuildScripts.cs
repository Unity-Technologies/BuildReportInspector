using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

public class BuildScripts
{
    // DetailedBuildReport is not available in the UI so this custom build script adds in the flag
    [MenuItem("Build/Build Player")]
    public static void BuildPlayer()
    {
        // Use the settings from the Player window, including scene list.  This will pop up a window for selecting the output
        BuildPlayerOptions buildParameters = BuildPlayerWindow.DefaultBuildMethods.GetBuildPlayerOptions(new BuildPlayerOptions());
        buildParameters.options |= BuildOptions.DetailedBuildReport;

        // When repeating builds by default the previous content is reused and not reported in the build report
        // so clean builds can be useful when testing (but of course much slower)
        buildParameters.options |= BuildOptions.CleanBuildCache;

        BuildReport report = BuildPipeline.BuildPlayer(buildParameters);

        // Give a tip about how to view the result with BuildReportInspector
        Debug.Log("Build " +
            ((report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) ? "succeeded." : "failed.") +
            "\nSelect \"Window / Open Last Build Report\" from the Menu to view the results.");
    }

    // AssetBundle building are not available in the UI (unless you use Addressables, which does not produce a BuildReport)
    // This custom build script demonstrates a build based on the assets in the project.
    // Tip: for real life usage we would not normally put the same assets that are in Player build into AssetBundles as well.
    [MenuItem("Build/Build AssetBundles")]
    public static void BuildAssetBundles()
    {
        var assetBundleDirectory = "Build/AssetBundles";

        if (!Directory.Exists(assetBundleDirectory))
            Directory.CreateDirectory(assetBundleDirectory);

        // Tip: For demo purposes this code puts assets of the same type all together in the same bundle.
        // For real life usage this is not usually a good pattern, unless you always want to download and
        // load all of them at the same time.
        string[] sceneFiles = Directory.GetFiles("Assets/Scenes", "*.unity");
        string[] audioFiles = Directory.GetFiles("Assets/audio", "*.mp3");
        string[] spriteFiles = Directory.GetFiles("Assets/sprites", "*.*")
            .Where(file => !file.EndsWith(".meta"))
            .ToArray();

        var bundleDefinitions = new AssetBundleBuild[]
        {
                new AssetBundleBuild()
                {
                    assetBundleName = "scenes.bundle",
                    assetNames = sceneFiles
                },
                new AssetBundleBuild()
                {
                    assetBundleName = "audio.bundle",
                    assetNames = audioFiles
                },
                new AssetBundleBuild()
                {
                    assetBundleName = "sprites.bundle",
                    assetNames = spriteFiles
                }
        };

        // Note: BuildOptions.DetailedBuildReport is not currently exposed for BuildPipeline.BuildAssetBundles
        BuildAssetBundleOptions options = BuildAssetBundleOptions.UncompressedAssetBundle |
                      BuildAssetBundleOptions.AssetBundleStripUnityVersion |
                      BuildAssetBundleOptions.StrictMode;
#if UNITY_2022_3_OR_NEWER
        options |= BuildAssetBundleOptions.UseContentHash;

        var parameters = new BuildAssetBundlesParameters()
        {
            targetPlatform = EditorUserBuildSettings.activeBuildTarget,
            outputPath = assetBundleDirectory,
            options = options,
            bundleDefinitions = bundleDefinitions
        };
        BuildPipeline.BuildAssetBundles(parameters);

        BuildReport report = BuildReport.GetLatestReport();
#else
        var manifest = BuildPipeline.BuildAssetBundles(assetBundleDirectory, bundleDefinitions, options, EditorUserBuildSettings.activeBuildTarget);

        // in versions prior to 2022 BuildReport.GetLatestReport() is not in the public interface.
        // This low level workaround loads the file. There are several objects inside the file, one of them will be the BuildReport
        var objects = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget("Library/LastBuild.buildreport");
        BuildReport report = null;
        foreach (var obj in objects)
        {
            if (obj is BuildReport)
            {
                report = (BuildReport)obj;
                break;
            }
        }
#endif
        bool success = (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded);
        Debug.Log($"Build to {assetBundleDirectory} {(success ? "succeeded." : "failed.")}\n" +
            "Select \"Window / Open Last Build Report\" from the Menu to view the results.");
    }
}
