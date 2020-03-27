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

        private static bool IsTestEnvironment
        {
            get { return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BOKKEN_RESOURCEID")); }
        }

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
            var androidToolPath = Utilities.Combine(EditorApplication.applicationPath, "Contents", "PlaybackEngines", "AndroidPlayer", "Tools");
            if (!Directory.Exists(androidToolPath))
            {
                androidToolPath =  Utilities.Combine(editorDir, "PlaybackEngines", "AndroidPlayer", "Tools");
            }
#endif
            var bundleToolName = Directory.GetFiles(androidToolPath, "bundletool*jar").SingleOrDefault();
            if (!string.IsNullOrEmpty(bundleToolName))
                return bundleToolName;

            throw new FileNotFoundException(string.Format("bundletool*.jar not found at {0}", androidToolPath));
        }

        private static string GetJavaExecutablePath()
        {
            if (string.IsNullOrEmpty(JdkPath) || !Directory.Exists(JdkPath))
                throw new DirectoryNotFoundException("Could not resolve Java directory. Please install Java through Unity Hub.");

            var javaExecutable = Utilities.Combine(JdkPath, "bin", "java");
#if UNITY_EDITOR_WIN
            javaExecutable += ".exe";
#endif
            if (File.Exists(javaExecutable))
                return javaExecutable;
            
            throw new FileNotFoundException(string.Format("Java executable not found at {0}.", javaExecutable));
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
                    throw new Exception(string.Format("Couldn't extract architecture info from application {0}", applicationPath));
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
#if UNITY_2018
            foreach (var archInfo in architectureInfos)
                archInfo.DownloadSize = 0;
            return;
#endif
            var temporaryFolder = Utilities.GetTemporaryFolder();
            try
            {
                var javaPath = GetJavaExecutablePath();
                var bundleTool = GetBundleToolPath();
                var apksPath = Path.Combine(temporaryFolder, string.Format("{0}.apks", Path.GetFileNameWithoutExtension(applicationPath)));

                var buildApksArgs = string.Format("-jar \"{0}\" build-apks --bundle \"{1}\" --output \"{2}\"", bundleTool, applicationPath, apksPath);
                string buildApksError;
                int buildApksExitCode;
                Utilities.RunProcessAndGetOutput(javaPath, buildApksArgs, out buildApksError,out buildApksExitCode);
                if (buildApksExitCode != 0)
                {
                    throw new Exception(string.Format("Failed to run bundletool. Error:\n{0}", buildApksError));
                }
                
                var getSizeArgs = string.Format("-jar \"{0}\" get-size total --apks \"{1}\" --dimensions=ABI", bundleTool, apksPath);
                string getSizeError;
                int getSizeExitCode;
                var getSizeOutput = Utilities.RunProcessAndGetOutput(javaPath, getSizeArgs, out getSizeError,out getSizeExitCode);
                if (getSizeExitCode != 0)
                {
                    throw new Exception(string.Format("Failed to run bundletool. Error:\n{0}", getSizeError));
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
                            long result;
                            if (long.TryParse(line.Substring(line.LastIndexOf(',') + 1), out result))
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
                    throw new DirectoryNotFoundException(string.Format("ANDROID_SDK_ROOT environment variable not pointing to a valid Android SDK directory. Current value: {0}", sdkEnv));
                }
                apkAnalyzerPath = Utilities.Combine(sdkEnv, "tools", "bin", "apkanalyzer");
            }
            else
            {
                if (!Directory.Exists(SdkPath))
                {
                    throw new DirectoryNotFoundException("Could not retrieve Android SDK location. Please set it up in Editor Preferences.");
                }
                apkAnalyzerPath = Utilities.Combine(SdkPath, "tools", "bin", "apkanalyzer");
            }
            
#if UNITY_EDITOR_WIN
            apkAnalyzerPath += ".bat";
#endif // UNITY_EDITOR_WIN

            string apkAnalyzerOutput;
            int exitCode;
            if (File.Exists(apkAnalyzerPath))
            {
                var apkAnalyzerArgs = string.Format("apk download-size \"{0}\"", applicationPath);
                string error;
                apkAnalyzerOutput = Utilities.RunProcessAndGetOutput(apkAnalyzerPath, apkAnalyzerArgs, out error, out exitCode);
            }
            else
            {
                var javaExecutablePath = GetJavaExecutablePath();
                var apkAnalyzerArgs = string.Format("{0} apk download-size \"{1}\"", GetApkAnalyzerJavaArgs(), applicationPath);
                string error;
                apkAnalyzerOutput = Utilities.RunProcessAndGetOutput(javaExecutablePath, apkAnalyzerArgs, out error, out exitCode);
            }

            long result;
            if (exitCode != 0 || !long.TryParse(apkAnalyzerOutput, out result))
            {
                throw new Exception(string.Format("apkanalyzer failed to estimate the apk size. Output:\n{0}", apkAnalyzerOutput));
            }
            
            return result;
        }

        private static string GetApkAnalyzerJavaArgs()
        {
            var appHome = string.Format("\"{0}\"", Path.Combine(SdkPath, "tools"));
            var defaultJvmOpts = string.Format("-Dcom.android.sdklib.toolsdir={0}", appHome);
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
            return string.Format("{0} -classpath {1} com.android.tools.apk.analyzer.ApkAnalyzerCli", defaultJvmOpts, classPath);
        }
        
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            MobileHelper.RegisterPlatformUtilities(new AndroidUtilities());
        }
    }
}
#endif // UNITY_ANDROID
