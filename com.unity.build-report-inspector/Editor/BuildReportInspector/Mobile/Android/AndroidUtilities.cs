#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Unity.Mobile.BuildReport.Tools;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace Unity.BuildReportInspector.Mobile.Android
{
    internal class AndroidUtilities : IPlatformUtilities
    {
        private enum ApplicationType
        {
            Apk,
            Aab
        }

        private static bool IsTestEnvironment => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BOKKEN_RESOURCEID"));

        private static string JdkPath
        {
            get
            {
#if UNITY_2018
                return EditorPrefs.GetString("JdkPath");
#else
                return AndroidExternalToolsSettings.jdkRootPath;
#endif
            }
        }

        private static string SdkPath
        {
            get
            {
#if UNITY_2018
                return EditorPrefs.GetString("AndroidSdkRoot");
#else
                return AndroidExternalToolsSettings.sdkRootPath;
#endif
            }
        }

        private static string GetBundleToolPath()
        {
            var editorDir = Directory.GetParent(EditorApplication.applicationPath).FullName;
#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX
            var androidToolPath = Path.Combine(editorDir, "Data", "PlaybackEngines", "AndroidPlayer", "Tools");
#else
            var androidToolPath = Path.Combine(EditorApplication.applicationPath, "Contents", "PlaybackEngines", "AndroidPlayer", "Tools");
            if (!Directory.Exists(androidToolPath))
            {
                androidToolPath =  Path.Combine(editorDir, "PlaybackEngines", "AndroidPlayer", "Tools");
            }
#endif
            var bundleToolName = Directory.EnumerateFiles(androidToolPath, "bundletool*jar").SingleOrDefault();
            if (!string.IsNullOrEmpty(bundleToolName))
                return bundleToolName;

            throw new FileNotFoundException($"bundletool*.jar not found at {androidToolPath}");
        }

        private static string GetJavaExecutablePath()
        {
            if (string.IsNullOrEmpty(JdkPath) || !Directory.Exists(JdkPath))
                throw new DirectoryNotFoundException("Could not resolve Java directory. Please install Java through Unity Hub.");

            var javaExecutable = Path.Combine(JdkPath, "bin", "java");
#if UNITY_EDITOR_WIN
            javaExecutable += ".exe";
#endif
            if (File.Exists(javaExecutable))
                return javaExecutable;
            
            throw new FileNotFoundException($"Java executable not found at {javaExecutable}.");
        }

        public MobileArchInfo[] GetArchitectureInfo(string applicationPath)
        {
            MobileArchInfo[] architectures;
            using (var archive = ZipFile.OpenRead(applicationPath))
            {
                var archList = new List<MobileArchInfo>();
                foreach (var file in archive.Entries)
                {
                    if (file.Name != "libunity.so")
                        continue;
                    var parent = file.FullName.Replace("/libunity.so", string.Empty);
                    var architecture = parent.Substring(parent.LastIndexOf('/') + 1);
                    archList.Add(new MobileArchInfo(architecture));
                }

                if (archList.Count < 1)
                {
                    throw new Exception($"Couldn't extract architecture info from application {applicationPath}");
                }

                architectures = archList.ToArray();
            }

            var applicationType = GetApplicationType(applicationPath);
            switch (applicationType)
            {
                case ApplicationType.Aab:
                    GetAabDownloadSizes(applicationPath, ref architectures);
                    break;
                case ApplicationType.Apk:
                    var downloadSize = GetApkDownloadSize(applicationPath);
                    foreach (var archInfo in architectures)
                    {
                        archInfo.DownloadSize = downloadSize;
                    }
                    break;
                default:
                    throw new Exception("Unknown application type to collect architecture data from.");
            }

            return architectures;
        }

        private static ApplicationType GetApplicationType(string applicationPath)
        {
            using (var archive = ZipFile.OpenRead(applicationPath))
            {
                if (archive.Entries.Any(x => x.FullName == "BundleConfig.pb"))
                {
                    return ApplicationType.Aab;
                }
                if (archive.Entries.Any(x => x.FullName == "AndroidManifest.xml"))
                {
                    return ApplicationType.Apk;
                }
                throw new Exception("Couldn't determine Android build type.");
            }
        }

        private static void GetAabDownloadSizes(string applicationPath, ref MobileArchInfo[] architectureInfos)
        {
            var temporaryFolder = Utilities.GetTemporaryFolder();
            try
            {
                var javaPath = GetJavaExecutablePath();
                var bundleTool = GetBundleToolPath();
                var apksPath = Path.Combine(temporaryFolder, $"{Path.GetFileNameWithoutExtension(applicationPath)}.apks");

                var buildApksArgs = $"-jar \"{bundleTool}\" build-apks --bundle \"{applicationPath}\" --output \"{apksPath}\"";
                Utilities.RunProcessAndGetOutput(javaPath, buildApksArgs, out var buildApksError,out var buildApksExitCode);
                if (buildApksExitCode != 0)
                {
                    throw new Exception($"Failed to run bundletool. Error:\n{buildApksError}");
                }
                
                var getSizeArgs = $"-jar \"{bundleTool}\" get-size total --apks \"{apksPath}\" --dimensions=ABI";
                var getSizeOutput = Utilities.RunProcessAndGetOutput(javaPath, getSizeArgs, out var getSizeError,out var exitCode);
                if (exitCode != 0)
                {
                    throw new Exception($"Failed to run bundletool. Error:\n{getSizeError}");
                }

                if (!architectureInfos.All(x => getSizeOutput.Contains(x.Name)))
                {
                    throw new Exception("Mismatch between architectures found in build and reported by bundletool.");
                }
                using (var reader = new StringReader(getSizeOutput))
                {
                    string line;
                    do
                    {
                        line = reader.ReadLine();
                        if (line == null)
                            continue;
                        foreach (var archInfo in architectureInfos)
                        {
                            if (!line.Contains(archInfo.Name))
                                continue;
                            if (long.TryParse(line.Substring(line.LastIndexOf(',') + 1), out var result))
                                archInfo.DownloadSize = result;
                        }
                    } while (line != null);
                }
            }
            finally
            {
                Directory.Delete(temporaryFolder, true);
            }
        }

        private long GetApkDownloadSize(string applicationPath)
        {
            string apkAnalyzerPath;
            if (IsTestEnvironment)
            {
                var sdkEnv = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
                if (!Directory.Exists(sdkEnv))
                {
                    throw new DirectoryNotFoundException($"ANDROID_SDK_ROOT environment variable not pointing to a valid Android SDK directory. Current value: {sdkEnv}");
                }
                apkAnalyzerPath = Path.Combine(sdkEnv, "tools", "bin", "apkanalyzer");
            }
            else
            {
                if (!Directory.Exists(SdkPath))
                {
                    throw new DirectoryNotFoundException("Could not retrieve Android SDK location. Please set it up in Editor Preferences.");
                }
                apkAnalyzerPath = Path.Combine(SdkPath, "tools", "bin", "apkanalyzer");
            }
            
#if UNITY_EDITOR_WIN
            apkAnalyzerPath += ".bat";
#endif // UNITY_EDITOR_WIN

            string apkAnalyzerOutput;
            int exitCode;
            if (File.Exists(apkAnalyzerPath))
            {
                var apkAnalyzerArgs = $"apk download-size \"{applicationPath}\"";
                apkAnalyzerOutput = Utilities.RunProcessAndGetOutput(apkAnalyzerPath, apkAnalyzerArgs, out _, out exitCode);
            }
            else
            {
                var javaExecutablePath = GetJavaExecutablePath();
                var apkAnalyzerArgs = $"{GetApkAnalyzerJavaArgs()} apk download-size \"{applicationPath}\"";
                apkAnalyzerOutput = Utilities.RunProcessAndGetOutput(javaExecutablePath, apkAnalyzerArgs, out _, out exitCode);
            }
            
            if (exitCode != 0 || !long.TryParse(apkAnalyzerOutput, out var result))
            {
                throw new Exception($"apkanalyzer failed to estimate the apk size. Output:\n{apkAnalyzerOutput}");
            }
            
            return result;
        }

        private static string GetApkAnalyzerJavaArgs()
        {
            var appHome = $"\"{Path.Combine(SdkPath, "tools")}\"";
            var defaultJvmOpts = $"-Dcom.android.sdklib.toolsdir={appHome}";
            var classPath = $"{appHome}\\lib\\dvlib-26.0.0-dev.jar;{appHome}\\lib\\util-2.2.1.jar;{appHome}\\lib\\jimfs-1.1.jar;{appHome}\\lib\\" +
                $"annotations-13.0.jar;{appHome}\\lib\\ddmlib-26.0.0-dev.jar;{appHome}\\lib\\repository-26.0.0-dev.jar;{appHome}\\lib\\" +
                $"sdk-common-26.0.0-dev.jar;{appHome}\\lib\\kotlin-stdlib-1.1.3-2.jar;{appHome}\\lib\\protobuf-java-3.0.0.jar;{appHome}\\lib\\" +
                $"apkanalyzer-cli.jar;{appHome}\\lib\\gson-2.3.jar;{appHome}\\lib\\httpcore-4.2.5.jar;{appHome}\\lib\\dexlib2-2.2.1.jar;{appHome}\\" +
                $"lib\\commons-compress-1.12.jar;{appHome}\\lib\\generator.jar;{appHome}\\lib\\error_prone_annotations-2.0.18.jar;{appHome}\\lib\\" +
                $"commons-codec-1.6.jar;{appHome}\\lib\\kxml2-2.3.0.jar;{appHome}\\lib\\httpmime-4.1.jar;{appHome}\\lib\\annotations-12.0.jar;{appHome}\\" +
                $"lib\\bcpkix-jdk15on-1.56.jar;{appHome}\\lib\\jsr305-3.0.0.jar;{appHome}\\lib\\explainer.jar;{appHome}\\lib\\builder-model-3.0.0-dev.jar;" +
                $"{appHome}\\lib\\baksmali-2.2.1.jar;{appHome}\\lib\\j2objc-annotations-1.1.jar;{appHome}\\lib\\layoutlib-api-26.0.0-dev.jar;{appHome}\\" +
                $"lib\\jcommander-1.64.jar;{appHome}\\lib\\commons-logging-1.1.1.jar;{appHome}\\lib\\annotations-26.0.0-dev.jar;{appHome}\\lib\\" +
                $"builder-test-api-3.0.0-dev.jar;{appHome}\\lib\\animal-sniffer-annotations-1.14.jar;{appHome}\\lib\\bcprov-jdk15on-1.56.jar;{appHome}\\lib\\" +
                $"httpclient-4.2.6.jar;{appHome}\\lib\\common-26.0.0-dev.jar;{appHome}\\lib\\jopt-simple-4.9.jar;{appHome}\\lib\\sdklib-26.0.0-dev.jar;{appHome}\\" +
                $"lib\\apkanalyzer.jar;{appHome}\\lib\\shared.jar;{appHome}\\lib\\binary-resources.jar;{appHome}\\lib\\guava-22.0.jar";
            return $"{defaultJvmOpts} -classpath {classPath} com.android.tools.apk.analyzer.ApkAnalyzerCli";
        }
        
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            MobileHelper.RegisterPlatformUtilities(new AndroidUtilities());
        }
    }
}
#endif // UNITY_ANDROID
