using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Unity.Mobile.BuildReport.Tools;
using Debug = UnityEngine.Debug;
using System.Collections.Generic;
using UnityEditor;

namespace Unity.BuildReportInspector.Mobile.Apple
{
    internal class AppleUtilities : IPlatformUtilities
    {
        private const string k_UnityFrameworkRelativePath = "Frameworks/UnityFramework.framework/UnityFramework";
        private const string k_Size = "/usr/bin/size";
        private const string k_File = "/usr/bin/file";

        public MobileArchInfo[] GetArchitectureInfo(string applicationPath)
        {
            var temporaryFolder = Utilities.GetTemporaryFolder();
            try
            {
                var frameworkFile = Path.Combine(temporaryFolder, "UnityFramework");
                long appSizeNoFramework;
                using (var archive = ZipFile.OpenRead(applicationPath))
                {
                    var unityFramework = archive.Entries.FirstOrDefault(x =>
                        x.FullName.EndsWith(k_UnityFrameworkRelativePath, StringComparison.InvariantCulture));
                    if (unityFramework == null)
                    {
                        throw new Exception("Failed to locate UnityFramework file in the build.");
                    }

                    unityFramework.ExtractToFile(frameworkFile);
                    appSizeNoFramework = new FileInfo(applicationPath).Length - unityFramework.CompressedLength;
                }

                var foundArchitectures = new List<string>();
                string archError;
                int archExitCode;
                var archOutput = Utilities.RunProcessAndGetOutput(k_File, string.Format("-b {0}", frameworkFile), out archError, out archExitCode);
                if (archExitCode != 0)
                {
                    throw new Exception(string.Format("Failed to collect UnityFramework data with command: {0} -m {1}. Error:\n{2}", k_Size, frameworkFile, archError));
                }

                using (var reader = new StringReader(archOutput))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var archString = line.Substring(line.LastIndexOf(' ') + 1);
                        if (archString.StartsWith("arm", StringComparison.InvariantCulture))
                        {
                            foundArchitectures.Add(archString.Replace("_", string.Empty));
                        }
                    }
                }

                var appleArchInfos = new List<MobileArchInfo>();
                foreach (var arch in foundArchitectures)
                {
                    var sizeArgs = string.Format("-m -arch {0} {1}", arch, frameworkFile);
                    string sizeError;
                    int sizeExitCode;
                    var sizeOutput = Utilities.RunProcessAndGetOutput(k_Size, sizeArgs, out sizeError, out sizeExitCode);
                    if (sizeExitCode != 0)
                    {
                        throw new Exception(string.Format("Failed to collect UnityFramework data with command: {0} {1}. Error:\n{2}", k_Size, sizeArgs, sizeError));
                    }

                    var segments = new MobileArchInfo.ExecutableSegments();
                    using (var reader = new StringReader(sizeOutput))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!line.StartsWith("Segment __", StringComparison.InvariantCulture))
                            {
                                continue;
                            }

                            var segmentSize = long.Parse(line.Substring(line.LastIndexOf(' ') + 1));

                            if (line.Contains("__TEXT"))
                                segments.TextSize = segmentSize;
                            else if (line.Contains("__DATA"))
                                segments.DataSize = segmentSize;
                            else if (line.Contains("__LLVM"))
                                segments.LlvmSize = segmentSize;
                            else if (line.Contains("__LINKEDIT"))
                                segments.LinkeditSize = segmentSize;
                        }
                    }
                    
                    // Calculate the estimated App Store download size with the formula:
                    // DownloadSize = Whole App - Framework Size + Text Segment + (Data Segment / 5)
                    var downloadSize = appSizeNoFramework + segments.TextSize + segments.DataSize / 5;

                    appleArchInfos.Add(new MobileArchInfo(arch) { DownloadSize = downloadSize, Segments = segments});
                }
                
                if (appleArchInfos.Count < 1)
                {
                    throw new Exception(string.Format("Couldn't extract architecture info from application {0}", applicationPath));
                }

                return appleArchInfos.ToArray();
            }
            finally
            {
                Directory.Delete(temporaryFolder, true);
            }
        }

#if UNITY_IOS
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            MobileHelper.RegisterPlatformUtilities(new AppleUtilities());
        }
#endif // UNITY_IOS
    }
}
