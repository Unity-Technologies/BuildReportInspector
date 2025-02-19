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
}
