using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

// DetailedBuildReport is not available in the UI so this custom build script adds in the flag
public class PlayerBuildWithDetailedReport
{
    [MenuItem("Build/Build Player")]
    public static void BuildPlayer()
    {
        string buildPath = "Builds/TestBuild";

        BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;

        if (buildTarget == BuildTarget.StandaloneWindows64)
            buildPath += ".exe";

        BuildOptions buildOptions = BuildOptions.Development | BuildOptions.DetailedBuildReport;

        string[] scenes = EditorBuildSettings.scenes
                   .Where(scene => scene.enabled)
                   .Select(scene => scene.path)
                   .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("No scenes are enabled in Build Settings. Please add scenes to the build.");
            return;
        }

        var buildParameters = new BuildPlayerOptions()
        {
            scenes = scenes,
            locationPathName = buildPath,
            options = buildOptions,
            target = buildTarget
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildParameters);

        Debug.Log("Development build " +
            ((report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) ? "succeeded" : "failed") +
            ".\In the select \"Window/Open Last Build Report\" to view the results.");
    }
}
