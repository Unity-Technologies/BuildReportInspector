using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;
using Unity.BuildReportInspector.Mobile.ZipUtility;

namespace Unity.BuildReportInspector.Mobile
{
    internal class AndroidUtilities : IPlatformUtilities
    {
        private enum ApplicationType
        {
            Apk,
            Aab
        }

        private static string AndroidToolRoot
        {
            get
            {
                var editorDir = Directory.GetParent(EditorApplication.applicationPath).FullName;
#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX
                return Utilities.Combine(editorDir, "Data", "PlaybackEngines", "AndroidPlayer");
#else
                var androidToolPath = Utilities.Combine(EditorApplication.applicationPath, "Contents", "PlaybackEngines", "AndroidPlayer");
                if (!Directory.Exists(androidToolPath))
                {
                    androidToolPath =  Utilities.Combine(editorDir, "PlaybackEngines", "AndroidPlayer");
                }
                return androidToolPath;
#endif
            }
        }

        private static string JdkPath
        {
            get
            {
#if UNITY_ANDROID
                return AndroidExternalToolsSettings.jdkRootPath;
#else
                return GuessToolPath("JdkPath", "OpenJDK");
#endif
            }
        }

        private static string SdkPath
        {
            get
            {
#if UNITY_ANDROID
                return AndroidExternalToolsSettings.sdkRootPath;
#else
                return GuessToolPath("AndroidSdkRoot", "SDK");
#endif
            }
        }

        private static string GuessToolPath(string nameKey, string toolName)
        {
            var hubVersion = Path.Combine(AndroidToolRoot, toolName);
            if (Directory.Exists(hubVersion))
                return hubVersion;


            var custom = EditorPrefs.GetString(nameKey);
            return Directory.Exists(custom) ? custom : null;
        }

        private static string GetBundleToolPath()
        {
            var bundleRoot = Path.Combine(AndroidToolRoot, "Tools");
            var bundleToolName = Directory.GetFiles(bundleRoot, "bundletool*jar").SingleOrDefault();
            if (!string.IsNullOrEmpty(bundleToolName))
                return bundleToolName;

            throw new FileNotFoundException($"bundletool*.jar not found at {bundleRoot}");
        }

        private static string GetJavaExecutablePath()
        {
            if (string.IsNullOrEmpty(JdkPath) || !Directory.Exists(JdkPath))
                throw new DirectoryNotFoundException($"Could not resolve Java directory. Please install Java through Unity Hub.");

            var javaExecutable = Utilities.Combine(JdkPath, "bin", "java");
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
            using (var archive = new ZipBundle(applicationPath))
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
            using (var archive = new ZipBundle(applicationPath))
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
                var buildApksOutput = Utilities.RunProcessAndGetOutput(javaPath, buildApksArgs,out var buildApksExitCode);
                if (buildApksExitCode != 0)
                {
                    throw new Exception($"Failed to run bundletool. Output:\n{buildApksOutput}");
                }
                
                var getSizeArgs = $"-jar \"{bundleTool}\" get-size total --apks \"{apksPath}\" --dimensions=ABI";
                var getSizeOutput = Utilities.RunProcessAndGetOutput(javaPath, getSizeArgs, out var getSizeExitCode);
                if (getSizeExitCode != 0)
                {
                    throw new Exception($"Failed to run bundletool. Output:\n{getSizeOutput}");
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

        private static long GetApkDownloadSize(string applicationPath)
        {
            string apkAnalyzerPath;

            if (Utilities.IsTestEnvironment)
            {
                var sdkEnv = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
                if (!Directory.Exists(sdkEnv))
                {
                    throw new DirectoryNotFoundException($"ANDROID_SDK_ROOT environment variable not pointing to a valid Android SDK directory. Current value: {sdkEnv}");
                }
                apkAnalyzerPath = Utilities.Combine(sdkEnv, "cmdline-tools", "6.0", "bin", "apkanalyzer");
                if(!File.Exists(apkAnalyzerPath) && !File.Exists(apkAnalyzerPath + ".bat"))
                    apkAnalyzerPath = Utilities.Combine(sdkEnv, "tools", "bin", "apkanalyzer");
            }
            else
            {
                if (!Directory.Exists(SdkPath))
                {
                    throw new DirectoryNotFoundException("Could not retrieve Android SDK location. Please set it up in Editor Preferences.");
                }
                apkAnalyzerPath = Utilities.Combine(SdkPath, "cmdline-tools", "6.0", "bin", "apkanalyzer");
                if(!File.Exists(apkAnalyzerPath) && !File.Exists(apkAnalyzerPath + ".bat"))
                    apkAnalyzerPath = Utilities.Combine(SdkPath, "tools", "bin", "apkanalyzer");
            }
#if UNITY_EDITOR_WIN
            apkAnalyzerPath += ".bat";
#endif // UNITY_EDITOR_WIN

            string apkAnalyzerOutput;
            int exitCode;
            if (File.Exists(apkAnalyzerPath))
            {
                var apkAnalyzerArgs = $"apk download-size \"{applicationPath}\"";
                apkAnalyzerOutput = Utilities.RunProcessAndGetOutput(apkAnalyzerPath, apkAnalyzerArgs, out exitCode);
            }
            else
            {
                var javaExecutablePath = GetJavaExecutablePath();
                var apkAnalyzerArgs = $"{GetApkAnalyzerJavaArgs()} apk download-size \"{applicationPath}\"";
                apkAnalyzerOutput = Utilities.RunProcessAndGetOutput(javaExecutablePath, apkAnalyzerArgs, out exitCode);
            }

            if (exitCode != 0 || !long.TryParse(apkAnalyzerOutput, out var result))
            {
                throw new Exception($"apkanalyzer failed to estimate the apk size. Output:\n{apkAnalyzerOutput}");
            }
            
            return result;
        }

        private static string GetApkAnalyzerJavaArgs()
        {
            var appHome = $"\"{Path.Combine(SdkPath, "cmdline-tools", "6.0")}\"";
            if(!Directory.Exists(appHome))
                appHome = $"\"{Path.Combine(SdkPath, "tools")}\"";

            var defaultJvmOpts = $"-Dcom.android.sdklib.toolsdir={appHome}";
            var classPath = string.Format("{0}\\lib\\dvlib-26.0.0-dev.jar;{0}\\lib\\util-2.2.1.jar;{0}\\l" +
                "ib\\jimfs-1.1.jar;{0}\\lib\\annotations-13.0.jar;{0}\\lib\\ddmlib-26.0.0-dev.jar;{0}\\lib\\repositor" +
                "y-26.0.0-dev.jar;{0}\\lib\\sdk-common-26.0.0-dev.jar;{0}\\lib\\kotlin-stdlib-1.1.3-2.jar;{0}\\lib\\p" +
                "rotobuf-java-3.0.0.jar;{0}\\lib\\apkanalyzer-cli.jar;{0}\\lib\\gson-2.3.jar;{0}\\lib\\httpcore-4.2.5" +
                ".jar;{0}\\lib\\dexlib2-2.2.1.jar;{0}\\lib\\commons-compress-1.12.jar;{0}\\lib\\generator.jar;{0}\\li" +
                "b\\error_prone_annotations-2.0.18.jar;{0}\\lib\\commons-codec-1.6.jar;{0}\\lib\\kxml2-2.3.0.jar;{0}" +
                "\\lib\\httpmime-4.1.jar;{0}\\lib\\annotations-12.0.jar;{0}\\lib\\bcpkix-jdk15on-1.56.jar;{0}\\lib\\j" +
                "sr305-3.0.0.jar;{0}\\lib\\explainer.jar;{0}\\lib\\builder-model-3.0.0-dev.jar;{0}\\lib\\baksmali-2.2" +
                ".1.jar;{0}\\lib\\j2objc-annotations-1.1.jar;{0}\\lib\\layoutlib-api-26.0.0-dev.jar;{0}\\lib\\jcomman" +
                "der-1.64.jar;{0}\\lib\\commons-logging-1.1.1.jar;{0}\\lib\\annotations-26.0.0-dev.jar;{0}\\lib\\buil" +
                "der-test-api-3.0.0-dev.jar;{0}\\lib\\animal-sniffer-annotations-1.14.jar;{0}\\lib\\bcprov-jdk15on-1." +
                "56.jar;{0}\\lib\\httpclient-4.2.6.jar;{0}\\lib\\common-26.0.0-dev.jar;{0}\\lib\\jopt-simple-4.9.jar;" +
                "{0}\\lib\\sdklib-26.0.0-dev.jar;{0}\\lib\\apkanalyzer.jar;{0}\\lib\\shared.jar;{0}\\lib\\binary-reso" +
                "urces.jar;{0}\\lib\\guava-22.0.jar", appHome);
            return $"{defaultJvmOpts} -classpath {classPath} com.android.tools.apk.analyzer.ApkAnalyzerCli";
        }
    }
}
