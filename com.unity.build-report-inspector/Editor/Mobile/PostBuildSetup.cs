#if UNITY_ANDROID || UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;

namespace Unity.BuildReportInspector.Mobile
{
    internal class PostBuildSetup : IPostprocessBuildWithReport, IPreprocessBuildWithReport
    {
        private const string k_GuidFileName = "UnityBuildGuid.txt";
        private static string s_LastBuildGuid;
        
        public int callbackOrder { get; }
        public void OnPreprocessBuild(BuildReport report)
        {
            s_LastBuildGuid = null;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.result == BuildResult.Failed || 
                report.summary.result == BuildResult.Cancelled)
                return;
                    
            // Save the guid for BuildPostProcess callback.
            s_LastBuildGuid = report.summary.guid.ToString();
            
            if (report.summary.platform != BuildTarget.iOS)
                return;
            
            // On iOS, label the build with a unique GUID, so that report can be generated later.
            var guidPath = Path.Combine(report.summary.outputPath, "Data", k_GuidFileName);
            File.WriteAllText(guidPath, s_LastBuildGuid);
        }

        [PostProcessBuildAttribute(1)]
        private static void BuildPostProcess(BuildTarget target, string applicationPath)
        {
            if (!File.Exists(applicationPath) || target != BuildTarget.Android || s_LastBuildGuid == null)
                return;

            // On Android, generate the mobile appendix right after the build finishes.
            MobileHelper.GenerateAndroidAppendix(applicationPath, s_LastBuildGuid);
        }
    }
}
#endif
