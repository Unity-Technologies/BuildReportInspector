#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Unity.BuildReportInspector.Mobile
{
    internal static class Utilities
    {
        internal static string RunProcessAndGetOutput(string executable, string arguments, out string error, out int exitCode)
        {
            using (var p = new Process())
            {
                p.StartInfo.FileName = executable;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.Start();
                var output = p.StandardOutput.ReadToEnd();
                error = p.StandardError.ReadToEnd();
                p.WaitForExit();

                exitCode = p.ExitCode;
                return output;
            }
        }

        internal static string GetTemporaryFolder()
        {
            return Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        }

        internal static string Combine(params string[] parts)
        {
            return parts.Aggregate(string.Empty, Path.Combine);
        }
    }
}
#endif
