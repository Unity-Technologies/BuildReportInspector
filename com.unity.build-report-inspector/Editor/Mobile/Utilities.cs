using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Unity.BuildReportInspector.Mobile
{
    internal static class Utilities
    {
        private const string k_Unzip = "/usr/bin/unzip";
        public static bool IsTestEnvironment => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BOKKEN_RESOURCEID"));

        internal static string RunProcessAndGetOutput(string executable, string arguments, out int exitCode)
        {
            using (var p = new Process())
            {
                p.StartInfo.FileName = executable;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var output = string.Empty;
                p.OutputDataReceived += (sender, e) => { output += $"{e.Data}{Environment.NewLine}"; };
                p.ErrorDataReceived += (sender, e) => { output += $"{e.Data}{Environment.NewLine}"; };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
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
        
        /// <summary>
        /// Unzip a file from a zip archive (macOS only).
        /// </summary>
        internal static void UnzipFile(string archivePath, string fileName, string destination)
        {
            using (var p = new Process())
            {
                p.StartInfo.FileName = k_Unzip;
                p.StartInfo.Arguments = $"-p \"{archivePath}\" \"{fileName}\"";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.Start();
                var baseStream = p.StandardOutput.BaseStream as FileStream;
                byte[] fileBytes;
                using (var memoryStream = new MemoryStream())
                {
                    var buffer = new byte[65536];
                    int lastRead;
                    do
                    {
                        lastRead = baseStream.Read(buffer, 0, buffer.Length);
                        memoryStream.Write(buffer, 0, lastRead);
                    } while (lastRead > 0);

                    fileBytes = memoryStream.ToArray();
                }

                using (var fileStream = new FileStream(destination, FileMode.Create))
                {
                    fileStream.Write(fileBytes, 0, fileBytes.Length);
                }
                p.WaitForExit();
            }
        }
    }
}
