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
        // Tip: you may want to exclude this folder from source code (e.g. in your .gitignore)
        // or adjust it to a preferred location.
        static readonly string k_BuildReportDir = "Assets/BuildReports";

        static readonly string k_LastBuildReportFileName = "Library/LastBuild.buildreport";
        static int k_MaxSourceAssetEntries = 10000; // To avoid UI freezing for truly large builds

        [MenuItem("Window/Open Last Build Report", true)]
        public static bool ValidateOpenLastBuild()
        {
            return File.Exists("Library/LastBuild.buildreport");
        }

        // The BuildReport is written to the library location and each build overwrites the same file.
        // This menu item copies the file into the Assets folder so that it can be inspected.
        // The build timestamp is used so that multiple build reports can exist in the same folder.
        // Note: you can also copy and name the files yourself, the BuildReportInspector works on any build report,
        // and doesn't rely on this copy mechanism.
        [MenuItem("Window/Open Last Build Report")]
        public static void OpenLastBuild()
        {
            if (!Directory.Exists(k_BuildReportDir))
                Directory.CreateDirectory(k_BuildReportDir);

            var date = File.GetLastWriteTime(k_LastBuildReportFileName);
            var name = "Build_" + date.ToString("yyyy-dd-MMM-HH-mm-ss") + ".buildreport";

            var destination = k_BuildReportDir + "/" + name;
            if (!File.Exists(destination))
            {
                var tempPath = k_BuildReportDir + "/LastBuild.buildreport";
                File.Copy(k_LastBuildReportFileName, tempPath, true);
                AssetDatabase.ImportAsset(tempPath);
                AssetDatabase.RenameAsset(tempPath, name);
            }

            Selection.objects = new Object[] { AssetDatabase.LoadAssetAtPath<BuildReport>(destination) };
        }

        #region Helpers

        private BuildReport report
        {
            get { return target as BuildReport; }
        }

        private MobileAppendix mobileAppendix
        {
            // Look for additional mobile information, which is stored in a file based on the build's guid
            get { return MobileHelper.LoadMobileAppendix(report.summary.guid.ToString()); }
        }

#if !UNITY_6000_0_OR_NEWER
        private string m_BuildType;
#endif
        private string BuildType
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return report.summary.buildType.ToString();
#else
                // There is a cost to pulling all the build steps, so cache the result
                if (string.IsNullOrEmpty(m_BuildType))
                {
                    if (report.steps[0].name == "Build Asset Bundles")
                        m_BuildType = "AssetBundle";
                    else
                        m_BuildType = "Player";
                }
                return m_BuildType;
#endif
            }
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

        // Potentially useful sorting to add would be source asset filepath and source asset file extension
        private enum SourceAssetsDisplayMode
        {
            Size,
            OutputDataFiles,
            ImporterType
        };

        private enum OutputFilesDisplayMode
        {
            FilePath,
            Size,
            Role
        };

        private enum MobileOutputDisplayMode
        {
            CompressedSize,
            UncompressedSize
        }

        ToolbarTabs m_mode;
        SourceAssetsDisplayMode m_sourceDispMode;
        OutputFilesDisplayMode m_outputDispMode;
        MobileOutputDisplayMode m_mobileOutputDispMode;

        static string FormatTime(System.TimeSpan t)
        {
            return t.Hours + ":" + t.Minutes.ToString("D2") + ":" + t.Seconds.ToString("D2") + "." + t.Milliseconds.ToString("D3");
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

        #endregion

        #region MainUI

        private enum ToolbarTabs
        {
            BuildSteps,
            ContentSummary,
            SourceAssets,
            OutputFiles,
            Stripping,
#if UNITY_2020_1_OR_NEWER
            ScenesUsingAssets,
#endif
        };

        readonly string[] ToolbarTabStrings = {
            "BuildSteps",
            "ContentSummary",
            "SourceAssets", // Could also be called "Content Details"
            "OutputFiles",
            "Stripping",
    #if UNITY_2020_1_OR_NEWER
            "ScenesUsingAssets",
    #endif
        };

        // The Stripping and ScenesUsingAssets don't apply to AssetBundle builds.
        // Note: this requires that player-only tabs are always at the end of the list.
        readonly string[] ToolbarTabStringsAssetBundle = {
            "BuildSteps",
            "ContentSummary",
            "SourceAssets",
            "OutputFiles"
        };

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
            EditorGUILayout.LabelField("    Build Type: ", BuildType);
            EditorGUILayout.LabelField("    Platform: ", report.summary.platform.ToString());
            EditorGUILayout.LabelField("    Total Time: ", FormatTime(report.summary.totalTime));
            EditorGUILayout.LabelField("    Total Size: ", FormatSize(mobileAppendix == null ? report.summary.totalSize : (ulong)mobileAppendix.BuildSize));
            EditorGUILayout.LabelField("    Build Result: ", report.summary.result.ToString());
            EditorGUILayout.LabelField("    Build Output Path: ", report.summary.outputPath);

            // Show Mobile appendix data below the build summary
            OnMobileAppendixGUI();

            if (BuildType == "AssetBundle")
                // Hide a few tabs which are never populated for AssetBundles
                m_mode = (ToolbarTabs)GUILayout.Toolbar((int)m_mode, ToolbarTabStringsAssetBundle);
            else
                m_mode = (ToolbarTabs)GUILayout.Toolbar((int)m_mode, ToolbarTabStrings);

            if (m_mode == ToolbarTabs.SourceAssets)
            {
                m_sourceDispMode = (SourceAssetsDisplayMode)EditorGUILayout.EnumPopup("Sort by:", m_sourceDispMode);
            }
            else if (m_mode == ToolbarTabs.OutputFiles)
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

            if (m_mode == ToolbarTabs.OutputFiles && mobileAppendix != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("File"), GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 260));
                GUILayout.Label("Uncompressed size", SizeStyle);
                GUILayout.Label("Compressed size", SizeStyle);
                GUILayout.EndHorizontal();
            }

            switch (m_mode)
            {
                case ToolbarTabs.BuildSteps:
                    OnBuildStepGUI();
                    break;
                case ToolbarTabs.ContentSummary:
                    OnContentSummaryGUI();
                    break;
                case ToolbarTabs.SourceAssets:
                    OnSourceAssetsGUI();
                    break;
                case ToolbarTabs.OutputFiles:
                    if (mobileAppendix == null)
                        OnOutputFilesGUI();
                    else
                        OnMobileOutputFilesGUI();
                    break;
                case ToolbarTabs.Stripping:
                    OnStrippingGUI();
                    break;
#if UNITY_2020_1_OR_NEWER
                case ToolbarTabs.ScenesUsingAssets:
                    OnScenesUsingAssetsGUI();
                    break;
#endif
                default:
                    throw new ArgumentOutOfRangeException();
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
                        var sizeText = entry.DownloadSize == 0 ? "N/A" : FormatSize((ulong)entry.DownloadSize);
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

        #endregion

        #region BuildSteps

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
                                    GUI.color = Color.yellow; // Easier to read on black background than red
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

        #endregion

        #region ContentSummary

        ContentSummary m_ContentSummary = null;
        bool m_FoldOutState_TypeList = false;
        bool m_FoldOutState_AssetList = false;

        private void OnContentSummaryGUI()
        {
            if (m_ContentSummary == null && GUILayout.Button("Calculate"))
            {
                m_ContentSummary = new ContentSummary(report);
            }

            if (m_ContentSummary != null)
            {
                BuildOutputStatistics stats = m_ContentSummary.m_Stats;

                EditorGUILayout.LabelField("Serialized File Size: ", FormatSize(stats.totalSerializedFileSize));
                EditorGUILayout.LabelField("Serialized File Headers: ", FormatSize(stats.totalHeaderSize));
                EditorGUILayout.LabelField("Resource Data Size: ", FormatSize(stats.totalResourceSize));
                EditorGUILayout.LabelField("Serialized File Count: ", stats.serializedFileCount.ToString());
                EditorGUILayout.LabelField("Resource File Count: ", stats.resourceFileCount.ToString());
                EditorGUILayout.LabelField("Object Count: ", stats.objectCount.ToString());

                EditorGUILayout.LabelField("Object Type Count: ", stats.sortedTypeStats.Length.ToString());
                EditorGUILayout.LabelField("Source Asset Count: ", stats.sortedAssetStats.Length.ToString());

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                ShowTypeSizeSummary(stats.sortedTypeStats);
                ShowAssetSizeSummary(stats.sortedAssetStats);
            }
        }

        private void ShowTypeSizeSummary(TypeStats[] typeStats)
        {
            m_FoldOutState_TypeList = EditorGUILayout.BeginFoldoutHeaderGroup(m_FoldOutState_TypeList, "Size info by Object Type", EditorStyles.foldoutHeader);

            if (m_FoldOutState_TypeList)
            {
                GUILayout.BeginVertical();
                var odd = false;
                GUILayout.Space(10);

                foreach (var typeInfo in typeStats)
                {
                    GUILayout.BeginHorizontal(odd ? OddStyle : EvenStyle);

                    GUILayout.Label(typeInfo.type.FullName);
                    GUILayout.Space(10);
                    GUILayout.Label("Object count: " + typeInfo.objectCount, SizeStyle);
                    GUILayout.Space(10);
                    GUILayout.Label(FormatSize(typeInfo.size), SizeStyle);

                    GUILayout.EndHorizontal();
                    odd = !odd;
                }
                GUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void ShowAssetSizeSummary(AssetStats[] assetStats)
        {
            m_FoldOutState_AssetList = EditorGUILayout.BeginFoldoutHeaderGroup(m_FoldOutState_AssetList, "Size info by Source Asset", EditorStyles.foldoutHeader);

            if (m_FoldOutState_AssetList)
            {
                // Note: some projects may have hundreds of thousands of assets.
                // The list is sorted by size (decreasing).  It might make sense to limit
                // to the top "N" assets, with a "show all" button or way to control "N".

                GUILayout.BeginVertical();
                var odd = false;
                GUILayout.Space(10);

                foreach (var info in assetStats)
                {
                    GUILayout.BeginHorizontal(odd ? OddStyle : EvenStyle);

                    GUILayout.Label(new GUIContent(info.sourceAssetPath, info.sourceAssetPath), GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 260));
                    GUILayout.Label("Object count: " + info.objectCount, SizeStyle);
                    GUILayout.Label(FormatSize(info.size), SizeStyle);

                    GUILayout.EndHorizontal();
                    odd = !odd;
                }
                GUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        #endregion

        #region SourceAssets

        Dictionary<string, bool> m_assetsFoldout = new Dictionary<string, bool>();
        ContentAnalysis m_contentAnalysis;

        private void OnSourceAssetsGUI()
        {
            // The PackedAsset information can be very large for large builds, so this is only calculated on demand
            if (m_contentAnalysis == null)
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Calculate", GUILayout.Width(200)))
                    m_contentAnalysis = new ContentAnalysis(report, k_MaxSourceAssetEntries, true);

                if (GUILayout.Button("Export to CSV", GUILayout.Width(200)))
                {
                    bool isTempAnalysis = false;
                    if (m_contentAnalysis == null)
                    {
                        m_contentAnalysis = new ContentAnalysis(report, k_MaxSourceAssetEntries, true);
                        isTempAnalysis = true;
                    }
                    string exportPath = AssetDatabase.GetAssetPath(target);
                    exportPath = Path.ChangeExtension(exportPath, null) + "_SourceAssets.csv";

                    string errorMessage = m_contentAnalysis.SaveAssetsToCsv(exportPath);
                    if (string.IsNullOrEmpty(errorMessage))
                        EditorUtility.DisplayDialog("Export Complete", $"Data written to:\n{exportPath}", "OK");
                    else
                        EditorUtility.DisplayDialog("Export Failed", errorMessage, "OK");

                    if (isTempAnalysis)
                        // Doing an export shouldn't also force the results into the UI, which may breakdown for very large builds
                        m_contentAnalysis = null;
                }

                k_MaxSourceAssetEntries = EditorGUILayout.IntField("Maximum Entries:", k_MaxSourceAssetEntries);

                EditorGUILayout.EndHorizontal();
            }

            if (m_contentAnalysis != null)
            {
                if (m_contentAnalysis.HitMaximumEntries())
                    EditorGUILayout.HelpBox("Build has too many objects to show.", MessageType.Info);
                else if (m_contentAnalysis.m_assets.Count == 0)
                {
                    if (report.summary.result == BuildResult.Succeeded)
                        EditorGUILayout.HelpBox("No PackedAsset information was found.  Was this an incremental build without any new content?", MessageType.Info);
                    else
                        // Depending when the build failed there may actually be asset information, so we only display this when nothing was collected
                        EditorGUILayout.HelpBox("No PackedAsset information was found because the build failed, or was canceled.", MessageType.Info);
                }
                else
                {
                    DisplayAssetsView();
                }
            }
        }

        private void DisplayAssetsView()
        {
            float vPos = 0;
            switch (m_sourceDispMode)
            {
                case SourceAssetsDisplayMode.Size:
                    // List all content by source and size
                    // There will be multiple items per source if it has objects of different types inside it
                    ShowAssets(m_contentAnalysis.m_assets, ref vPos);
                    break;
                case SourceAssetsDisplayMode.OutputDataFiles:
                    // Group content by the output file (with size total for all objects of a particular type within each source)
                    foreach (var outputFile in m_contentAnalysis.m_outputFiles)
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
                            ShowAssets(m_contentAnalysis.m_assets, ref vPos, outputFile.Key);
                    }
                    break;
                case SourceAssetsDisplayMode.ImporterType:
                    // Group content by type (with total size of objects of that type listed from each source)
                    foreach (var outputFile in m_contentAnalysis.m_assetTypes)
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
                            ShowAssets(m_contentAnalysis.m_assets, ref vPos, null, outputFile.Key);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // Display rows for all ContentEntrys that match the filters.
        // When no filters passed then all ContentEntries are displayed (they are already sorted by descending size)
        // If fileFilter is specified then only entries from that file are displayed
        // And similar when a typeFilter is specified.
        private static void ShowAssets(IEnumerable<ContentEntry> assets, ref float vPos, string fileFilter = null, string typeFilter = null)
        {
            GUILayout.BeginVertical();
            var odd = false;

            // Warning: When filters are specified this would be a linear scans through all the entries to match each filter,
            // So long as it is only run for open items in the foldout it can work ok for modest sized builds.  For very large builds
            // better data structures would be needed (or export to other software or write a custom script)
            foreach (var entry in assets.Where(entry => fileFilter == null || fileFilter == entry.outputFile).Where(entry => typeFilter == null || typeFilter == entry.type))
            {
                GUILayout.BeginHorizontal(odd ? OddStyle : EvenStyle);

                GUILayout.Label(entry.icon, GUILayout.MaxHeight(16), GUILayout.Width(20));
                var displayName = GetDisplayNameForFile(entry.path);
                var toolTip = entry.path + " (Count: " + entry.objectCount + ")";
                if (typeFilter == null)
                    toolTip += " (Type: " + entry.type + ")";

                if (GUILayout.Button(new GUIContent(displayName, toolTip), GUI.skin.label, GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 110)))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(entry.path));
                GUILayout.Label(FormatSize((ulong)entry.size), SizeStyle);
                GUILayout.EndHorizontal();
                vPos += k_LineHeight;
                odd = !odd;
            }
            GUILayout.EndVertical();
        }

        private static string GetDisplayNameForFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "Unknown";

            try
            {
                return Path.GetFileName(path);
            }
            catch (Exception)
            {
                // revert to path,
                // e.g. for pseudo paths like 'Built-in Texture2D: sactx-0-256x128-DXT5|BC3-ui-sprite-atlas-fff07956'
                return path;
            }
        }

        #endregion

        #region OutputFiles

        Dictionary<string, bool> m_outputFilesFoldout = new Dictionary<string, bool>();

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
            switch (m_outputDispMode)
            {
                case OutputFilesDisplayMode.FilePath:
                    Array.Sort(files, (fileA, fileB) => { return fileB.path.CompareTo(fileA.path); });
                    ShowOutputFiles(files, ref vPos, longestCommonRoot.Length);
                    break;
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
        #endregion

        #region Stripping

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

        #endregion

        #region ScenesUsingAsset

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
        #endregion
    }
}
