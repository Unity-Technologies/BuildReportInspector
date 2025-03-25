using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;

#if UNITY_2022_3_OR_NEWER // Earlier versions don't have BuildAssetBundlesParameters and BuildReport.GetLatestReport

/*
Example of how AssetBundle definitions control whether shared assets will be duplicated or shared.
This is relevant to BuildReportInspector because the UI can be used to compare the results and see where the extra size of the results comes from.

This uses the example Assets in the Assets/AssetDuplication folder.

There is a ScriptableObject asset that references two images.
And there are several Scenes that each have a MonoBehaviour that references the ScriptableObject.

If each scene is built into a separate AssetBundle, as demonstrated by one of the menu options,
then Unity will be forced to duplicate the ScriptableObject plus the images into each bundle.
That is because Unity only shares Assets if they are explicitly "exposed" by an AssetBundle,
e.g. if they are listed in the AssetBundleBuild.assetNames field of one of the AssetBundle builds.

The way to deduplicate the ImageList and Images is to add another AssetBundle specifically for this shared content.
This is demonstrated through one of the menu options.  It is only necessary to list the ImageList, we don't have to
list each image explicitly because the ImageList is the only Asset in the build that references them.
At build time Unity automatically recognizes that the referenced ImageList is available in an AssetBundle so it doesn't
save it into the build.

Overall this means that the total build size is smaller, as can be seen by using the BuildReportInspector to compare the two variations.

The third variation demonstrated is where all the Scenes are in the same AssetBundle (and there is no dedicated AssetBundle for the ImageList).
In that case the Image data is not duplicated because all the scenes can share content inside the same bundle.
*/
public class DuplicateAssetExample
{
    enum BuildMode
    {
        WithDuplicates,
        WithoutDuplicates,
        SingleSceneBundle
    }

    [MenuItem("Build/Duplicated Content/Build AssetBundles With Duplicates")]
    public static void BuildAssetBundlesWithDuplicates()
    {
        BuildAssetBundlesReferencingImageList(BuildMode.WithDuplicates);
    }

    [MenuItem("Build/Duplicated Content/Build AssetBundles Without Duplicates")]
    public static void BuildAssetBundlesWithoutDuplicates()
    {
        BuildAssetBundlesReferencingImageList(BuildMode.WithoutDuplicates);
    }

    [MenuItem("Build/Duplicated Content/Build Single AssetBundle")]
    public static void BuildAssetBundlesSingleSceneBundle()
    {
        BuildAssetBundlesReferencingImageList(BuildMode.SingleSceneBundle);
    }

    private static void BuildAssetBundlesReferencingImageList(BuildMode buildMode)
    {
        var assetBundleDirectory = "Build/AssetBundle" + buildMode.ToString();

        if (!Directory.Exists(assetBundleDirectory))
            Directory.CreateDirectory(assetBundleDirectory);

        IncrementalBuildReporter incrementalBuildReporter = new IncrementalBuildReporter(assetBundleDirectory);
        incrementalBuildReporter.ReportToConsole();

        string[] sceneFiles = Directory.GetFiles("Assets/AssetDuplication", "*.unity");

        var bundleDefinitions = new List<AssetBundleBuild>();

        if (buildMode == BuildMode.WithoutDuplicates)
        {
            bundleDefinitions.Add(new AssetBundleBuild()
            {
                    assetBundleName = "imageList.bundle",
                    assetNames = new string[] { "Assets/AssetDuplication/ImageList.asset" }
            });
        };

        if (buildMode != BuildMode.SingleSceneBundle)
        {
            foreach (var sceneFile in sceneFiles)
            {
                bundleDefinitions.Add(new AssetBundleBuild()
                {
                    assetBundleName = Path.GetFileNameWithoutExtension(sceneFile) + ".bundle",
                    assetNames = new string[] { sceneFile }
                });
            }
        }
        else
        {
            bundleDefinitions.Add(new AssetBundleBuild()
            {
                assetBundleName = "scenes.bundle",
                assetNames = sceneFiles
            });
        }

        BuildAssetBundleOptions options = BuildAssetBundleOptions.UncompressedAssetBundle |
                    BuildAssetBundleOptions.AssetBundleStripUnityVersion |
                    BuildAssetBundleOptions.StrictMode |
                    BuildAssetBundleOptions.UseContentHash;

        var parameters = new BuildAssetBundlesParameters()
        {
            targetPlatform = EditorUserBuildSettings.activeBuildTarget,
            outputPath = assetBundleDirectory,
            options = options,
            bundleDefinitions = bundleDefinitions.ToArray()
        };
        BuildPipeline.BuildAssetBundles(parameters);
        BuildReport report = BuildReport.GetLatestReport();

        bool success = (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded);
        Debug.Log($"Build to {assetBundleDirectory} {(success ? "succeeded." : "failed.")}\n" +
            "Select \"Window / Open Last Build Report\" from the Menu to view the results.");

        incrementalBuildReporter.DetectBuildResults();
        incrementalBuildReporter.ReportToConsole();
    }
}
#endif