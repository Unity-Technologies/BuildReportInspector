using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Object = UnityEngine.Object;
using Unity.BuildReportInspector.Mobile;

namespace Unity.BuildReportInspector
{
    /// <summary>
    /// Custom inspector implementation for UnityEditor.Build.Reporting.BuildReport objects.
    /// </summary>
    [CustomEditor(typeof(BuildReport))]
    public class BuildReportInspector : Editor
    {
        static readonly string k_BuildReportDir = "Assets/BuildReports";
        static readonly string k_LastBuildReportFileName = "Library/LastBuild.buildreport";
        static readonly int k_MaxBuiltEntriesToShow = 10000; // To avoid UI freezing for truly large builds

        [MenuItem("Window/Open Last Build Report", true)]
        public static bool ValidateOpenLastBuild()
        {
            return File.Exists("Library/LastBuild.buildreport");
        }

        [MenuItem("Window/Open Last Build Report")]
        public static void OpenLastBuild()
        {
            if (!Directory.Exists(k_BuildReportDir))
                Directory.CreateDirectory(k_BuildReportDir);

            var path = k_BuildReportDir + "/LastBuild.buildreport";
            var date = File.GetLastWriteTime(k_LastBuildReportFileName);
            var name = "Build_" + date.ToString("yyyy-dd-MMM-HH-mm-ss") + ".buildreport";
            File.Copy(k_LastBuildReportFileName, path, true);
            AssetDatabase.ImportAsset(path);
            AssetDatabase.RenameAsset(path, name);
            Selection.objects = new Object[] { AssetDatabase.LoadAssetAtPath<BuildReport>(k_BuildReportDir + "/" + name) };
        }

        private BuildReport report
        {
            get { return target as BuildReport; }
        }

        private MobileAppendix mobileAppendix
        {
            get { return MobileHelper.LoadMobileAppendix(report.summary.guid.ToString()); }
        }

        private static GUIStyle s_SizeStyle;

        private static GUIStyle SizeStyle
        {
            get
            {
                if (s_SizeStyle == null)
                    s_SizeStyle = new GUIStyle(GUI.skin.label);
                s_SizeStyle.alignment = TextAnchor.MiddleRight;
                return s_SizeStyle;
            }
        }

        private static Texture2D MakeColorTexture(Color col)
        {
            var pix = new Color[1];
            pix[0] = col;

            var result = new Texture2D(1, 1);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        private static GUIStyle s_OddStyle;

        private static GUIStyle OddStyle
        {
            get
            {
                if (s_OddStyle != null)
                    return s_OddStyle;
                s_OddStyle = new GUIStyle(GUIStyle.none)
                {
                    normal = { background = MakeColorTexture(new Color(0.5f, 0.5f, 0.5f, 0.1f)) }
                };
                return s_OddStyle;
            }
        }

        private static GUIStyle s_EvenStyle;

        private static GUIStyle EvenStyle
        {
            get
            {
                if (s_EvenStyle != null)
                    return s_EvenStyle;
                s_EvenStyle = new GUIStyle(GUIStyle.none)
                {
                    normal = { background = MakeColorTexture(new Color(0.5f, 0.5f, 0.5f, 0.0f)) }
                };
                return s_EvenStyle;
            }
        }

        static GUIStyle s_DataFileStyle;

        private static GUIStyle DataFileStyle
        {
            get
            {
                if (s_DataFileStyle != null)
                    return s_DataFileStyle;
                s_DataFileStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
                return s_DataFileStyle;
            }
        }

        private const int k_LineHeight = 20;

        private enum ReportDisplayMode
        {
            BuildSteps,
            SourceAssets,
            OutputFiles,
            Stripping,
#if UNITY_2020_1_OR_NEWER
            ScenesUsingAssets,
#endif
        };

        readonly string[] ReportDisplayModeStrings = {
            "BuildSteps",
            "SourceAssets",
            "OutputFiles",
            "Stripping",
    #if UNITY_2020_1_OR_NEWER
            "ScenesUsingAssets",
    #endif
        };

        private enum SourceAssetsDisplayMode
        {
            Size,
            OutputDataFiles,
            ImporterType
        };

        private enum OutputFilesDisplayMode
        {
            Size,
            Role
        };

        private enum MobileOutputDisplayMode
        {
            CompressedSize,
            UncompressedSize
        }

        ReportDisplayMode m_mode;
        SourceAssetsDisplayMode m_sourceDispMode;
        OutputFilesDisplayMode m_outputDispMode;
        MobileOutputDisplayMode m_mobileOutputDispMode;

        static string FormatTime(System.TimeSpan t)
        {
            return t.Hours + ":" + t.Minutes.ToString("D2") + ":" + t.Seconds.ToString("D2") + "." + t.Milliseconds.ToString("D3");
        }

        /// <summary>
        /// Custom inspector implementation for UnityEditor.Build.Reporting.BuildReport objects
        /// </summary>
        public override void OnInspectorGUI()
        {
            if (report == null)
            {
                EditorGUILayout.HelpBox("No Build Report.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Report Info");

            EditorGUILayout.LabelField("    Build Name: ", Application.productName);
            EditorGUILayout.LabelField("    Platform: ", report.summary.platform.ToString());
            EditorGUILayout.LabelField("    Total Time: ", FormatTime(report.summary.totalTime));
            EditorGUILayout.LabelField("    Total Size: ", FormatSize(mobileAppendix == null ? report.summary.totalSize : (ulong)mobileAppendix.BuildSize));
            EditorGUILayout.LabelField("    Build Result: ", report.summary.result.ToString());

            // Show Mobile appendix data below the build summary
            OnMobileAppendixGUI();

            m_mode = (ReportDisplayMode)GUILayout.Toolbar((int)m_mode, ReportDisplayModeStrings);

            if (m_mode == ReportDisplayMode.SourceAssets)
            {
                m_sourceDispMode = (SourceAssetsDisplayMode)EditorGUILayout.EnumPopup("Sort by:", m_sourceDispMode);
            }
            else if (m_mode == ReportDisplayMode.OutputFiles)
            {
                if (mobileAppendix != null)
                {
                    m_mobileOutputDispMode = (MobileOutputDisplayMode) EditorGUILayout.EnumPopup("Sort by:", m_mobileOutputDispMode);
                }
                else
                {
                    m_outputDispMode = (OutputFilesDisplayMode)EditorGUILayout.EnumPopup("Sort by:", m_outputDispMode);
                }
            }

            if (m_mode == ReportDisplayMode.OutputFiles && mobileAppendix != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("File"), GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 260));
                GUILayout.Label("Uncompressed size", SizeStyle);
                GUILayout.Label("Compressed size", SizeStyle);
                GUILayout.EndHorizontal();
            }

            switch (m_mode)
            {
                case ReportDisplayMode.BuildSteps:
                    OnBuildStepGUI();
                    break;
                case ReportDisplayMode.SourceAssets:
                    OnAssetsGUI();
                    break;
                case ReportDisplayMode.OutputFiles:
                    if (mobileAppendix == null)
                        OnOutputFilesGUI();
                    else
                        OnMobileOutputFilesGUI();
                    break;
                case ReportDisplayMode.Stripping:
                    OnStrippingGUI();
                    break;
#if UNITY_2020_1_OR_NEWER
                case ReportDisplayMode.ScenesUsingAssets:
                    OnScenesUsingAssetsGUI();
                    break;
#endif
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static List<LogType> ErrorLogTypes = new List<LogType> { LogType.Error, LogType.Assert, LogType.Exception };

        public static LogType WorseLogType(LogType log1, LogType log2)
        {
            if (ErrorLogTypes.Contains(log1) || ErrorLogTypes.Contains(log2))
                return LogType.Error;
            if (log1 == LogType.Warning || log2 == LogType.Warning)
                return LogType.Warning;
            return LogType.Log;
        }

        private class BuildStepNode
        {
            private BuildStep? step;
            public int depth;
            public List<BuildStepNode> children;
            private LogType worstChildrenLogType;
            public bool foldoutState;

            public BuildStepNode(BuildStep? _step, int _depth)
            {
                step = _step;
                depth = _depth;
                children = new List<BuildStepNode>();

                worstChildrenLogType = LogType.Log;
                if (step.HasValue)
                {
                    foreach (var message in step.Value.messages)
                    {
                        worstChildrenLogType = message.type; // Warning
                        if (ErrorLogTypes.Contains(message.type))
                            break; // Error
                    }
                }

                foldoutState = false;
            }

            internal void UpdateWorstChildrenLogType()
            {
                foreach (var child in children)
                {
                    child.UpdateWorstChildrenLogType();
                    worstChildrenLogType = WorseLogType(worstChildrenLogType, child.worstChildrenLogType);
                }
            }

            public void LayoutGUI(ref bool switchBackgroundColor, float indentPixels)
            {
                switchBackgroundColor = !switchBackgroundColor;
                GUILayout.BeginVertical(switchBackgroundColor ? OddStyle : EvenStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Space(10 + indentPixels);

                if (children.Any() || (step.HasValue && step.Value.messages.Any()))
                {
                    if (worstChildrenLogType != LogType.Log)
                    {
                        var icon = "console.warnicon.sml";
                        if (worstChildrenLogType != LogType.Warning)
                            icon = "console.erroricon.sml";
                        foldoutState = EditorGUILayout.Foldout(foldoutState, EditorGUIUtility.TrTextContentWithIcon(step.GetValueOrDefault().name, icon), true);
                    }
                    else
                    {
                        foldoutState = EditorGUILayout.Foldout(foldoutState, new GUIContent(step.GetValueOrDefault().name), true);
                    }
                }
                else
                    GUILayout.Label(step.GetValueOrDefault().name);

                GUILayout.FlexibleSpace();
                GUILayout.Label(step.GetValueOrDefault().duration.Hours + ":" +
                                step.GetValueOrDefault().duration.Minutes.ToString("D2") + ":" +
                                step.GetValueOrDefault().duration.Seconds.ToString("D2") + "." +
                                step.GetValueOrDefault().duration.Milliseconds.ToString("D3"));
                GUILayout.EndHorizontal();

                if (foldoutState)
                {
                    if (step.HasValue)
                    {
                        foreach (var message in step.Value.messages)
                        {
                            var icon = "console.infoicon.sml";
                            var oldCol = GUI.color;
                            switch (message.type)
                            {
                                case LogType.Warning:
                                    GUI.color = Color.yellow;
                                    icon = "console.warnicon.sml";
                                    break;
                                case LogType.Error:
                                case LogType.Exception:
                                case LogType.Assert:
                                    GUI.color = Color.red;
                                    icon = "console.erroricon.sml";
                                    break;
                            }
                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.Space(20 + indentPixels);
                                GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.ExpandWidth(false));
                                var style = EditorStyles.label;
                                style.wordWrap = true;
                                EditorGUILayout.LabelField(new GUIContent(message.content, message.content), style);
                            }
                            GUILayout.EndHorizontal();
                            GUI.color = oldCol;
                        }
                    }

                    foreach (var child in children)
                        child.LayoutGUI(ref switchBackgroundColor, indentPixels + 20);
                }
                GUILayout.EndVertical();
            }
        }

        private void OnMobileAppendixGUI()
        {
            if (mobileAppendix != null)
            {
                if (mobileAppendix.Architectures != null)
                {
                    EditorGUILayout.LabelField("    Download Sizes: ");
                    foreach (var entry in mobileAppendix.Architectures)
                    {
                        var sizeText = entry.DownloadSize == 0 ? "N/A" : FormatSize((ulong) entry.DownloadSize);
                        EditorGUILayout.LabelField(string.Format("            {0}", entry.Name), sizeText);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Could not determine the architectures present in the build.", MessageType.Warning);
                }
            }
#if UNITY_EDITOR_OSX
            // On macOS, show a help dialog for generating the MobileAppendix for iOS/tvOS
            else if (report.summary.platform == BuildTarget.iOS || report.summary.platform == BuildTarget.tvOS)
            {
                EditorGUILayout.HelpBox("To get more accurate report data, please provide an .ipa file generated from a " +
                                        "matching Unity build using the dialog below.", MessageType.Warning);
                if (!GUILayout.Button("Select an .ipa bundle"))
                {
                    return;
                }
                var ipaPath = EditorUtility.OpenFilePanel("Select an .ipa build.", "", "ipa");
                if (!string.IsNullOrEmpty(ipaPath))
                {
                    // If an .ipa is selected, generate the MobileAppendix
                    MobileHelper.GenerateAppleAppendix(ipaPath, report.summary.guid.ToString());
                }
            }
#endif // UNITY_EDITOR_OSX
        }

        BuildStepNode m_rootStepNode = new BuildStepNode(null, -1);
        private void OnBuildStepGUI()
        {
            if (!m_rootStepNode.children.Any())
            {
                // re-create steps hierarchy
                var branch = new Stack<BuildStepNode>();
                branch.Push(m_rootStepNode);
                foreach (var step in report.steps)
                {
                    while (branch.Peek().depth >= step.depth)
                    {
                        branch.Pop();
                    }

                    while (branch.Peek().depth < (step.depth - 1))
                    {
                        var intermediateNode = new BuildStepNode(null, branch.Count - 1);
                        branch.Peek().children.Add(intermediateNode);
                        branch.Push(intermediateNode);
                    }

                    var stepNode = new BuildStepNode(step, step.depth);
                    branch.Peek().children.Add(stepNode);
                    branch.Push(stepNode);
                }

                m_rootStepNode.UpdateWorstChildrenLogType();

                // expand first step, usually "Build player"
                if (m_rootStepNode.children.Any())
                    m_rootStepNode.children[0].foldoutState = true;
            }

            var odd = false;
            foreach (var stepNode in m_rootStepNode.children)
                stepNode.LayoutGUI(ref odd, 0);
        }

        private static string FormatSize(ulong size)
        {
            if (size < 1024)
                return size + " B";
            if (size < 1024 * 1024)
                return (size / 1024.00).ToString("F2") + " KB";
            if (size < 1024 * 1024 * 1024)
                return (size / (1024.0 * 1024.0)).ToString("F2") + " MB";
            return (size / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
        }

        private struct AssetEntry
        {
            public string path;
            public int size;
            public string outputFile;
            public string type;
            public Texture icon;
        }

        private static void ShowAssets(IEnumerable<AssetEntry> assets, ref float vPos, string fileFilter = null, string typeFilter = null)
        {
            GUILayout.BeginVertical();
            var odd = false;
            foreach (var entry in assets.Where(entry => fileFilter == null || fileFilter == entry.outputFile).Where(entry => typeFilter == null || typeFilter == entry.type))
            {
                GUILayout.BeginHorizontal(odd ? OddStyle : EvenStyle);

                GUILayout.Label(entry.icon, GUILayout.MaxHeight(16), GUILayout.Width(20));
                var fileName = string.IsNullOrEmpty(entry.path) ? "Unknown" : Path.GetFileName(entry.path);
                if (GUILayout.Button(new GUIContent(Path.GetFileName(fileName), entry.path), GUI.skin.label, GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 110)))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(entry.path));
                GUILayout.Label(FormatSize((ulong)entry.size), SizeStyle);
                GUILayout.EndHorizontal();
                vPos += k_LineHeight;
                odd = !odd;
            }
            GUILayout.EndVertical();
        }

        private static void ShowOutputFiles(BuildFile[] files, ref float vPos, int rootLength, string roleFilter = null)
        {
            GUILayout.BeginVertical();
            var odd = false;

            foreach (BuildFile file in files)
            {
                if (roleFilter != null && string.Compare(file.role, roleFilter, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    continue;
                }

                GUILayout.BeginHorizontal(odd ? OddStyle : EvenStyle);
                GUIContent guiContent = new GUIContent(file.path.Substring(rootLength), file.path);
                GUILayout.Label(guiContent, GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 260));

                if (string.IsNullOrEmpty(roleFilter))
                {
                    GUILayout.Label(file.role);
                }

                GUILayout.Label(FormatSize(file.size), SizeStyle);
                GUILayout.EndHorizontal();

                vPos += k_LineHeight;
                odd = !odd;
            }

            GUILayout.EndVertical();
        }

        Dictionary<string, bool> m_assetsFoldout = new Dictionary<string, bool>();
        Dictionary<string, bool> m_outputFilesFoldout = new Dictionary<string, bool>();
        List<AssetEntry> m_assets;
        Dictionary<string, int> m_outputFiles;
        Dictionary<string, int> m_assetTypes;

        private void OnAssetsGUI()
        {
            var vPos = 0;
            if (m_assets == null)
            {
                m_assets = new List<AssetEntry>();
                m_outputFiles = new Dictionary<string, int>();
                m_assetTypes = new Dictionary<string, int>();
                foreach (var packedAsset in report.packedAssets)
                {
                    m_outputFiles[packedAsset.shortPath] = 0;
                    var totalSizeProp = packedAsset.overhead;
                    m_outputFiles[packedAsset.shortPath] = (int)totalSizeProp;
                    foreach (var entry in packedAsset.contents)
                    {
                        var asset = AssetImporter.GetAtPath(entry.sourceAssetPath);
                        var type = asset != null? asset.GetType().Name : "Unknown";
                        if (type.EndsWith("Importer"))
                            type = type.Substring(0, type.Length - 8);
                        var sizeProp = entry.packedSize;
                        if (!m_assetTypes.ContainsKey(type))
                            m_assetTypes[type] = 0;
                        m_outputFiles[packedAsset.shortPath] += (int)sizeProp;
                        m_assetTypes[type] += (int)sizeProp;
                        m_assets.Add(new AssetEntry
                        {
                            size = (int) sizeProp,
                            icon = AssetDatabase.GetCachedIcon(entry.sourceAssetPath),
                            outputFile = packedAsset.shortPath,
                            type = type,
                            path = entry.sourceAssetPath
                        });
                    }
                }
                m_assets = m_assets.OrderBy(p => -p.size).ToList();
                m_outputFiles = m_outputFiles.OrderBy(p => -p.Value).ToDictionary(x => x.Key, x => x.Value);
                m_assetTypes = m_assetTypes.OrderBy(p => -p.Value).ToDictionary(x => x.Key, x => x.Value);
            }
            DisplayAssetsView(vPos);
        }

        private void DisplayAssetsView(float vPos)
        {
            switch (m_sourceDispMode)
            {
                case SourceAssetsDisplayMode.Size:
                    ShowAssets(m_assets, ref vPos);
                    break;
                case SourceAssetsDisplayMode.OutputDataFiles:
                    foreach (var outputFile in m_outputFiles)
                    {
                        if (!m_assetsFoldout.ContainsKey(outputFile.Key))
                            m_assetsFoldout[outputFile.Key] = false;

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(10);
                        m_assetsFoldout[outputFile.Key] = EditorGUILayout.Foldout(m_assetsFoldout[outputFile.Key], outputFile.Key, DataFileStyle);
                        GUILayout.Label(FormatSize((ulong)outputFile.Value), SizeStyle);
                        GUILayout.EndHorizontal();

                        vPos += k_LineHeight;

                        if (m_assetsFoldout[outputFile.Key])
                            ShowAssets(m_assets, ref vPos, outputFile.Key);
                    }
                    break;
                case SourceAssetsDisplayMode.ImporterType:
                    foreach (var outputFile in m_assetTypes)
                    {
                        if (!m_assetsFoldout.ContainsKey(outputFile.Key))
                            m_assetsFoldout[outputFile.Key] = false;

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(10);
                        m_assetsFoldout[outputFile.Key] = EditorGUILayout.Foldout(m_assetsFoldout[outputFile.Key], outputFile.Key, DataFileStyle);
                        GUILayout.Label(FormatSize((ulong)outputFile.Value), SizeStyle);
                        GUILayout.EndHorizontal();

                        vPos += k_LineHeight;

                        if (m_assetsFoldout[outputFile.Key])
                            ShowAssets(m_assets, ref vPos, null, outputFile.Key);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnOutputFilesGUI()
        {
#if UNITY_2022_1_OR_NEWER
            var files = report.GetFiles();
#else
            var files = report.files;
#endif // UNITY_2022_1_OR_NEWER

            if (files.Length == 0)
                return;

            var longestCommonRoot = files[0].path;
            var tempRoot = Path.GetFullPath("Temp");
            foreach (var file in files)
            {
                if (file.path.StartsWith(tempRoot))
                    continue;
                for (var i = 0; i < longestCommonRoot.Length && i < file.path.Length; i++)
                {
                    if (longestCommonRoot[i] == file.path[i])
                        continue;
                    longestCommonRoot = longestCommonRoot.Substring(0, i);
                    break;
                }
            }

            float vPos = 0;
            var odd = false;

            switch (m_outputDispMode)
            {
                case OutputFilesDisplayMode.Size:
                    Array.Sort(files, (fileA, fileB) => { return fileB.size.CompareTo(fileA.size); });
                    ShowOutputFiles(files, ref vPos, longestCommonRoot.Length);
                    break;
                case OutputFilesDisplayMode.Role:
                    Array.Sort(files, (fileA, fileB) =>
                    {
                        int comparison = string.Compare(fileA.role, fileB.role, StringComparison.OrdinalIgnoreCase);
                        return comparison == 0 ? fileB.size.CompareTo(fileA.size) : comparison;
                    });

                    Dictionary<string, ulong> sizePerRole = new Dictionary<string, ulong>();

                    foreach (BuildFile file in files)
                    {
                        if (sizePerRole.ContainsKey(file.role))
                        {
                            sizePerRole[file.role] += file.size;
                        }
                        else
                        {
                            sizePerRole[file.role] = file.size;
                        }
                    }

                    KeyValuePair<string, ulong>[] pairs = sizePerRole.ToArray();
                    Array.Sort(pairs, (pairA, pairB) => { return pairB.Value.CompareTo(pairA.Value); });

                    foreach (KeyValuePair<string, ulong> pair in pairs)
                    {
                        if (!m_outputFilesFoldout.ContainsKey(pair.Key))
                        {
                            m_outputFilesFoldout[pair.Key] = false;
                        }

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(10);
                        m_outputFilesFoldout[pair.Key] = EditorGUILayout.Foldout(m_outputFilesFoldout[pair.Key], pair.Key, DataFileStyle);
                        GUILayout.Label(FormatSize(pair.Value), SizeStyle);
                        GUILayout.EndHorizontal();

                        vPos += k_LineHeight;

                        if (m_outputFilesFoldout[pair.Key])
                        {
                            ShowOutputFiles(files, ref vPos, longestCommonRoot.Length, pair.Key);
                        }
                    }

                    break;
            }

        }

        private void OnMobileOutputFilesGUI()
        {
            MobileFile[] appendixFiles = mobileAppendix.Files;

            if (m_mobileOutputDispMode == MobileOutputDisplayMode.CompressedSize) {
                Array.Sort(appendixFiles, (fileA, fileB) => {
                    return fileB.CompressedSize.CompareTo(fileA.CompressedSize);
                });
            } else {
                Array.Sort(appendixFiles, (fileA, fileB) => {
                    return fileB.UncompressedSize.CompareTo(fileA.UncompressedSize);
                });
            }

            var longestCommonRoot = appendixFiles[0].Path;
            var tempRoot = Path.GetFullPath("Temp");
            foreach (var file in appendixFiles)
            {
                if (file.Path.StartsWith(tempRoot))
                    continue;
                for (var i = 0; i < longestCommonRoot.Length && i < file.Path.Length; i++)
                {
                    if (longestCommonRoot[i] == file.Path[i])
                        continue;
                    longestCommonRoot = longestCommonRoot.Substring(0, i);
                    break;
                }
            }
            var odd = false;
            foreach (var file in appendixFiles)
            {
                if (file.Path.StartsWith(tempRoot))
                    continue;
                GUILayout.BeginHorizontal(odd? OddStyle:EvenStyle);
                odd = !odd;
                GUILayout.Label(new GUIContent(file.Path.Substring(longestCommonRoot.Length), file.Path), GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 260));
                GUILayout.Label(FormatSize((ulong)file.UncompressedSize), SizeStyle);
                GUILayout.Label(FormatSize((ulong)file.CompressedSize), SizeStyle);
                GUILayout.EndHorizontal();

            }
        }

        Dictionary<string, Texture> m_strippingIcons = new Dictionary<string, Texture>();
        Dictionary<string, int> m_strippingSizes = new Dictionary<string, int>();

        static Dictionary<string, Texture> m_iconCache = new Dictionary<string, Texture>();

        private static Texture StrippingEntityIcon(string iconString)
        {
            if (m_iconCache.ContainsKey(iconString))
                return m_iconCache[iconString];

            if (iconString.StartsWith("class/"))
            {
                var type = System.Type.GetType("UnityEngine." + iconString.Substring(6) + ",UnityEngine");
                if (type != null)
                {
                    var image = EditorGUIUtility.ObjectContent(null, System.Type.GetType("UnityEngine." + iconString.Substring(6) + ",UnityEngine")).image;
                    if (image != null)
                        m_iconCache[iconString] = image;
                }
            }
            if (iconString.StartsWith("package/"))
            {
                var path = EditorApplication.applicationContentsPath + "/Resources/PackageManager/Editor/" + iconString.Substring(8) + "/.icon.png";
                if (File.Exists(path))
                {
                    Texture2D tex = new Texture2D(2, 2);
                    tex.LoadImage(File.ReadAllBytes(path));
                    m_iconCache[iconString] = tex;
                }
            }

            if (!m_iconCache.ContainsKey(iconString))
                m_iconCache[iconString] = EditorGUIUtility.ObjectContent(null, typeof(DefaultAsset)).image;

            return m_iconCache[iconString];
        }

        Dictionary<string, bool> m_strippingReasonsFoldout = new Dictionary<string, bool>();
        private void StrippingEntityGui(string entity, ref bool odd)
        {
            GUILayout.BeginHorizontal(odd ? OddStyle : EvenStyle);
            odd = !odd;
            GUILayout.Space(15);
            var reasons = report.strippingInfo.GetReasonsForIncluding(entity).ToList();
            if (!m_strippingIcons.ContainsKey(entity))
                m_strippingIcons[entity] = StrippingEntityIcon(entity);
            var icon = m_strippingIcons[entity];
            if (reasons.Any())
            {
                if (!m_strippingReasonsFoldout.ContainsKey(entity))
                    m_strippingReasonsFoldout[entity] = false;
                m_strippingReasonsFoldout[entity] = EditorGUILayout.Foldout(m_strippingReasonsFoldout[entity], new GUIContent(entity, icon));
            }
            else
                EditorGUILayout.LabelField(new GUIContent(entity, icon), GUILayout.Height(16), GUILayout.MaxWidth(1000));

            GUILayout.FlexibleSpace();

            if (m_strippingSizes.ContainsKey(entity) && m_strippingSizes[entity] != 0)
                GUILayout.Label(FormatSize((ulong)m_strippingSizes[entity]), SizeStyle, GUILayout.Width(100));

            GUILayout.EndHorizontal();

            if (!m_strippingReasonsFoldout.ContainsKey(entity) || !m_strippingReasonsFoldout[entity])
                return;

            EditorGUI.indentLevel++;
            foreach (var reason in reasons)
                StrippingEntityGui(reason, ref odd);
            EditorGUI.indentLevel--;
        }

        private void OnStrippingGUI()
        {
            if (report.strippingInfo == null)
            {
                EditorGUILayout.HelpBox("No stripping info.", MessageType.Info);
                return;
            }

            var so = new SerializedObject(report.strippingInfo);
            var serializedDependencies = so.FindProperty("serializedDependencies");
            //var hasSizes = false;
            if (serializedDependencies != null)
            {
                for (var i = 0; i < serializedDependencies.arraySize; i++)
                {
                    var sp = serializedDependencies.GetArrayElementAtIndex(i);
                    var depKey = sp.FindPropertyRelative("key").stringValue;
                    m_strippingIcons[depKey] = StrippingEntityIcon(sp.FindPropertyRelative("icon").stringValue);
                    m_strippingSizes[depKey] = sp.FindPropertyRelative("size").intValue;
                    //if (m_strippingSizes[depKey] != 0)
                    //    hasSizes = true;
                }
            }

            var analyzeMethod = report.strippingInfo.GetType().GetMethod("Analyze", System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (/*!hasSizes &&*/ analyzeMethod != null)
            {
                if (GUILayout.Button("Analyze size"))
                    analyzeMethod.Invoke(report.strippingInfo, null);
            }

            var odd = false;
            foreach (var module in report.strippingInfo.includedModules)
            {
                StrippingEntityGui(module, ref odd);
            }
        }

#if UNITY_2020_1_OR_NEWER
        class ScenesUsingAssetGUI
        {
            public string assetPath;
            public string[] scenePaths;
            public bool foldoutState;
        }
        List<ScenesUsingAssetGUI> m_scenesUsingAssetGUIs = new List<ScenesUsingAssetGUI>();

        void OnScenesUsingAssetsGUI()
        {
            if (report.scenesUsingAssets == null || report.scenesUsingAssets.Length==0 || report.scenesUsingAssets[0] == null || report.scenesUsingAssets[0].list==null || report.scenesUsingAssets[0].list.Length==0 )
            {
                EditorGUILayout.HelpBox("No info about which scenes are using assets in the build. Did you use BuildOptions.DetailedBuildReport?", MessageType.Info);
                return;
            }

            // re-create list of scenes using assets
            if(!m_scenesUsingAssetGUIs.Any())
            {
                foreach (var scenesUsingAsset in report.scenesUsingAssets[0].list)
                    m_scenesUsingAssetGUIs.Add(new ScenesUsingAssetGUI { assetPath = scenesUsingAsset.assetPath, scenePaths = scenesUsingAsset.scenePaths, foldoutState = true});
            }

            bool odd = true;
            foreach (var scenesUsingAssetGUI in m_scenesUsingAssetGUIs)
            {
                odd = !odd;
                GUILayout.BeginVertical(odd ? OddStyle : EvenStyle);

                GUILayout.BeginHorizontal(odd ? OddStyle : EvenStyle);
                GUILayout.Space(10);
                scenesUsingAssetGUI.foldoutState = EditorGUILayout.Foldout(scenesUsingAssetGUI.foldoutState, scenesUsingAssetGUI.assetPath);
                GUILayout.EndHorizontal();

                if(scenesUsingAssetGUI.foldoutState)
                {
                    foreach (var scenePath in scenesUsingAssetGUI.scenePaths)
                    {
                        odd = !odd;
                        GUILayout.BeginHorizontal(odd ? OddStyle : EvenStyle);
                        GUILayout.Space(20);
                        GUILayout.Label(scenePath);
                        GUILayout.EndHorizontal();
                    }
                }

                GUILayout.EndVertical();
            }
        }
#endif // UNITY_2020_1_OR_NEWER
    }
}
