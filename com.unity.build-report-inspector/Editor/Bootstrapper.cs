using UnityEditor;
using System.IO;

namespace Unity.BuildReportInspector
{
    [InitializeOnLoad]
    public class Bootstrapper
    {
        private const string SourcePath = "Packages/com.unity.build-report-inspector/Editor/Assets/BuildReports/.gitignore";
        private const string TargetDirectory = "Assets/BuildReports";
        private const string TargetFileName = ".gitignore";

        static Bootstrapper()
        {
            if (!Directory.Exists(TargetDirectory))
            {
                Directory.CreateDirectory(TargetDirectory);
            }

            if (!System.IO.File.Exists(TargetDirectory + "/" + TargetFileName))
            {
                System.IO.File.Copy(SourcePath, TargetDirectory + "/" + TargetFileName, true);
            }
        }
    }
}
