using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Object = UnityEngine.Object;
#if UNITY_2019_3_OR_NEWER
using Unity.BuildReportInspector.Mobile;
#endif

namespace Unity.BuildReportInspector
{
    /// <summary>
    /// Custom inspector implementation for UnityEditor.Build.Reporting.BuildReport objects
    /// </summary>
    [CustomEditor(typeof(BuildReport))]
    public class BuildReportInspector : Editor {
        [MenuItem("Window/Open Last Build Report", true)]
        public static bool ValidateOpenLastBuild()
        {
            return File.Exists("Library/LastBuild.buildreport");
        }

        [MenuItem("Window/Open Last Build Report")]
        public static void OpenLastBuild()
        {
            const string buildReportDir = "Assets/BuildReports";
            if (!Directory.Exists(buildReportDir))
                Directory.CreateDirectory(buildReportDir);

            var date = File.GetLastWriteTime("Library/LastBuild.buildreport");
            var assetPath = buildReportDir + "/Build_" + date.ToString("yyyy-dd-MMM-HH-mm-ss") + ".buildreport";
            File.Copy("Library/LastBuild.buildreport", assetPath, true);
            AssetDatabase.ImportAsset(assetPath);
            Selection.objects = new Object[] { AssetDatabase.LoadAssetAtPath<BuildReport>(assetPath) };
        }

        private BuildReport report
        {
            get { return target as BuildReport; }
        }

#if UNITY_2019_3_OR_NEWER
        private MobileAppendix mobileAppendix
        { 
            get { return MobileHelper.LoadMobileAppendix(report.summary.guid.ToString()); }
        }
#endif // UNITY_2019_3_OR_NEWER

        private static GUIStyle s_SizeStyle;

        private static GUIStyle SizeStyle {
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
                    normal = {background = MakeColorTexture(new Color(0.5f, 0.5f, 0.5f, 0.1f))}
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
                    normal = {background = MakeColorTexture(new Color(0.5f, 0.5f, 0.5f, 0.0f))}
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
                s_DataFileStyle = new GUIStyle(EditorStyles.foldout) {fontStyle = FontStyle.Bold};
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
            FileType
        };

        ReportDisplayMode mode;
        SourceAssetsDisplayMode sourceDispMode;
        OutputFilesDisplayMode outputDispMode;

        private Vector2 scrollPosition;

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
#if UNITY_2019_3_OR_NEWER
            EditorGUILayout.LabelField("    Total Size: ", FormatSize(mobileAppendix == null ? report.summary.totalSize : (ulong)mobileAppendix.BuildSize));
            EditorGUILayout.LabelField("    Build Result: ", report.summary.result.ToString());

            // Show Mobile appendix data below the build summary
            OnMobileAppendixGUI();
#else
            EditorGUILayout.LabelField("    Total Size: ", FormatSize(report.summary.totalSize));
            EditorGUILayout.LabelField("    Build Result: ", report.summary.result.ToString());
#endif
            

            mode = (ReportDisplayMode)GUILayout.Toolbar((int)mode, ReportDisplayModeStrings);

            if (mode == ReportDisplayMode.SourceAssets)
            {
                sourceDispMode = (SourceAssetsDisplayMode)EditorGUILayout.EnumPopup("Sort by:", sourceDispMode);
            } 
            else if (mode == ReportDisplayMode.OutputFiles)
            {
                outputDispMode = (OutputFilesDisplayMode)EditorGUILayout.EnumPopup("Sort by:", outputDispMode);
            }
            
#if UNITY_2019_3_OR_NEWER
            if (mode == ReportDisplayMode.OutputFiles && mobileAppendix != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("File"), GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 260));
                GUILayout.Label("Uncompressed size", SizeStyle);
                GUILayout.Label("Compressed size", SizeStyle);
                GUILayout.EndHorizontal();
            }
#endif
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            switch(mode)
            {
                case ReportDisplayMode.BuildSteps:
                    OnBuildStepGUI();
                    break;
                case ReportDisplayMode.SourceAssets:
#if UNITY_2019_3_OR_NEWER
                    OnAssetsGUI();
#else
                    OnOldAssetsGUI();
#endif
                    break;
                case ReportDisplayMode.OutputFiles:
#if UNITY_2019_3_OR_NEWER
                    if (mobileAppendix == null)
                        OnOutputFilesGUI();
                    else
                        OnMobileOutputFilesGUI();
#else
                    OnOutputFilesGUI();
#endif
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
            EditorGUILayout.EndScrollView();
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
                if(step.HasValue)
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
                foreach(var child in children)
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

#if UNITY_2019_3_OR_NEWER
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
#endif // UNITY_2019_3_OR_NEWER

        BuildStepNode rootStepNode = new BuildStepNode(null, -1);
        private void OnBuildStepGUI()
        {
            if(!rootStepNode.children.Any())
            {
                // re-create steps hierarchy
                var branch = new Stack<BuildStepNode>();
                branch.Push(rootStepNode);
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

                rootStepNode.UpdateWorstChildrenLogType();

                // expand first step, usually "Build player"
                if (rootStepNode.children.Any())
                    rootStepNode.children[0].foldoutState = true;
            }

            var odd = false;
            foreach(var stepNode in rootStepNode.children)
                stepNode.LayoutGUI(ref odd, 0);
        }

        private static string FormatSize(ulong size)
        {
            if (size < 1024)
                return size + " B";
            if (size < 1024*1024)
                return (size/1024.00).ToString("F2") + " KB";
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

        Dictionary<string, bool> assetsFoldout = new Dictionary<string, bool>();
        List<AssetEntry> assets;
        Dictionary<string, int> outputFiles;
        Dictionary<string, int> assetTypes;

#if !UNITY_2019_3_OR_NEWER
        private void OnOldAssetsGUI()
        {
            var vPos = -scrollPosition.y;
            var appendices = serializedObject.FindProperty("m_Appendices");
            if (appendices != null)
            {
                if (assets == null)
                {
                    assets = new List<AssetEntry>();
                    outputFiles = new Dictionary<string, int>();
                    assetTypes = new Dictionary<string, int>();
                    for (var i = 0; i < appendices.arraySize; i++)
                    {
                        var appendix = appendices.GetArrayElementAtIndex(i);
                        if (appendix.objectReferenceValue.GetType() != typeof(Object))
                            continue;
                        var appendixSO = new SerializedObject(appendix.objectReferenceValue);
                        if (appendixSO.FindProperty("m_ShortPath") == null)
                            continue;
                        var pathProperty = appendixSO.FindProperty("m_ShortPath");
                        if (pathProperty == null)
                            continue;
                        var path = pathProperty.stringValue;
                        var contents = appendixSO.FindProperty("m_Contents");
                        outputFiles[path] = 0;
                        var totalSizeProp = appendixSO.FindProperty("m_Overhead");
                        if (totalSizeProp != null)
                            outputFiles[path] = totalSizeProp.intValue;
                        if (contents == null)
                            continue;
                        for (var j = 0; j < contents.arraySize; j++)
                        {
                            var entry = contents.GetArrayElementAtIndex(j);
                            var entryPathProp = entry.FindPropertyRelative("buildTimeAssetPath");
                            if (entryPathProp == null)
                                continue;
                            var entryPath = entryPathProp.stringValue;
                            if (string.IsNullOrEmpty(entryPath))
                                continue;
                            var assetEntry = new AssetEntry();
                            var asset = AssetImporter.GetAtPath(entryPath);
                            var type = asset != null? asset.GetType().Name : "Unknown";
                            if (type.EndsWith("Importer"))
                                type = type.Substring(0, type.Length - 8);
                            var sizeProp = entry.FindPropertyRelative("packedSize");
                            if (!assetTypes.ContainsKey(type))
                                assetTypes[type] = 0;
                            if (sizeProp != null)
                            {
                                assetEntry.size = sizeProp.intValue;
                                outputFiles[path] += sizeProp.intValue;
                                assetTypes[type] += sizeProp.intValue;
                            }
                            assetEntry.icon = AssetDatabase.GetCachedIcon(entryPath);
                            assetEntry.outputFile = path;
                            assetEntry.type = type;
                            assetEntry.path = entryPath;
                            assets.Add(assetEntry);
                        }
                    }
                    assets = assets.OrderBy(p => -p.size).ToList();
                    outputFiles = outputFiles.OrderBy(p => -p.Value).ToDictionary(x => x.Key, x => x.Value);
                    assetTypes = assetTypes.OrderBy(p => -p.Value).ToDictionary(x => x.Key, x => x.Value);
                }
                DisplayAssetsView(vPos);
            }
            else 
                GUILayout.Label("No Appendices property found");
        }
#endif // !UNITY_2019_3_OR_NEWER

#if UNITY_2019_3_OR_NEWER
        private void OnAssetsGUI()
        {
            var vPos = -scrollPosition.y;
            if (assets == null)
            {
                assets = new List<AssetEntry>();
                outputFiles = new Dictionary<string, int>();
                assetTypes = new Dictionary<string, int>();
                foreach (var packedAsset in report.packedAssets)
                {
                    outputFiles[packedAsset.shortPath] = 0;
                    var totalSizeProp = packedAsset.overhead;
                    outputFiles[packedAsset.shortPath] = (int)totalSizeProp;
                    foreach (var entry in packedAsset.contents)
                    {
                        var asset = AssetImporter.GetAtPath(entry.sourceAssetPath);
                        var type = asset != null? asset.GetType().Name : "Unknown";
                        if (type.EndsWith("Importer"))
                            type = type.Substring(0, type.Length - 8);
                        var sizeProp = entry.packedSize;
                        if (!assetTypes.ContainsKey(type))
                            assetTypes[type] = 0;
                        outputFiles[packedAsset.shortPath] += (int)sizeProp;
                        assetTypes[type] += (int)sizeProp;
                        assets.Add(new AssetEntry
                        {
                            size = (int) sizeProp,
                            icon = AssetDatabase.GetCachedIcon(entry.sourceAssetPath),
                            outputFile = packedAsset.shortPath,
                            type = type,
                            path = entry.sourceAssetPath
                        });
                    }
                }
                assets = assets.OrderBy(p => -p.size).ToList();
                outputFiles = outputFiles.OrderBy(p => -p.Value).ToDictionary(x => x.Key, x => x.Value);
                assetTypes = assetTypes.OrderBy(p => -p.Value).ToDictionary(x => x.Key, x => x.Value);
            }
            DisplayAssetsView(vPos);
        }
#endif // UNITY_2019_3_OR_NEWER

        private void DisplayAssetsView(float vPos)
        {
            switch (sourceDispMode)
            {
                case SourceAssetsDisplayMode.Size:
                    ShowAssets(assets, ref vPos);
                    break;
                case SourceAssetsDisplayMode.OutputDataFiles:
                    foreach (var outputFile in outputFiles)
                    {
                        if (!assetsFoldout.ContainsKey(outputFile.Key))
                            assetsFoldout[outputFile.Key] = false;

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(10);
                        assetsFoldout[outputFile.Key] = EditorGUILayout.Foldout(assetsFoldout[outputFile.Key], outputFile.Key, DataFileStyle);
                        GUILayout.Label(FormatSize((ulong)outputFile.Value), SizeStyle);
                        GUILayout.EndHorizontal();

                        vPos += k_LineHeight;

                        if (assetsFoldout[outputFile.Key])
                            ShowAssets(assets, ref vPos, outputFile.Key);
                    }
                    break;
                case SourceAssetsDisplayMode.ImporterType:
                    foreach (var outputFile in assetTypes)
                    {
                        if (!assetsFoldout.ContainsKey(outputFile.Key))
                            assetsFoldout[outputFile.Key] = false;

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(10);
                        assetsFoldout[outputFile.Key] = EditorGUILayout.Foldout(assetsFoldout[outputFile.Key], outputFile.Key, DataFileStyle);
                        GUILayout.Label(FormatSize((ulong)outputFile.Value), SizeStyle);
                        GUILayout.EndHorizontal();

                        vPos += k_LineHeight;

                        if (assetsFoldout[outputFile.Key])
                            ShowAssets(assets, ref vPos, null, outputFile.Key);
                    }             
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnOutputFilesGUI()
        {
            if (report.files.Length == 0)
                return;

            var longestCommonRoot = report.files[0].path;
            var tempRoot = Path.GetFullPath("Temp");
            foreach (var file in report.files)
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

            switch (outputDispMode) {
                case OutputFilesDisplayMode.Size:
                    var odd = false;

                    BuildFile[] reportFiles = report.files;
                    Array.Sort(reportFiles, (fileA, fileB) => { return fileB.size.CompareTo(fileA.size); });
                        
                    foreach (var file in reportFiles)
                    {
                        if (file.path.StartsWith(tempRoot))
                            continue;
                        GUILayout.BeginHorizontal(odd? OddStyle:EvenStyle);
                        odd = !odd;
                        GUILayout.Label(new GUIContent(file.path.Substring(longestCommonRoot.Length), file.path), GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 260));
                        GUILayout.Label(file.role);
                        GUILayout.Label(FormatSize(file.size), SizeStyle);
                        GUILayout.EndHorizontal();
                    }
                    break;
                case OutputFilesDisplayMode.FileType:
                    break;
            }

        }

#if UNITY_2019_3_OR_NEWER
        private void OnMobileOutputFilesGUI()
        {
            var longestCommonRoot = mobileAppendix.Files[0].Path;
            var tempRoot = Path.GetFullPath("Temp");
            foreach (var file in mobileAppendix.Files)
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
            foreach (var file in mobileAppendix.Files)
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
#endif // UNITY_2019_3_OR_NEWER

        Dictionary<string, Texture> strippingIcons = new Dictionary<string, Texture>();
        Dictionary<string, int> strippingSizes = new Dictionary<string, int>();

        static Dictionary<string, Texture> iconCache = new Dictionary<string, Texture>();

        private static Texture StrippingEntityIcon(string iconString)
        {
            if (iconCache.ContainsKey(iconString))
                return iconCache[iconString];

            if (iconString.StartsWith("class/"))
            {
                var type = System.Type.GetType("UnityEngine." + iconString.Substring(6) + ",UnityEngine");
                if (type != null)
                {
                    var image = EditorGUIUtility.ObjectContent(null, System.Type.GetType("UnityEngine." + iconString.Substring(6) + ",UnityEngine")).image;
                    if (image != null)
                        iconCache[iconString] = image;
                }
            }
            if (iconString.StartsWith("package/"))
            {
                var path = EditorApplication.applicationContentsPath + "/Resources/PackageManager/Editor/" + iconString.Substring(8) + "/.icon.png";
                if (File.Exists(path))
                {
                    Texture2D tex = new Texture2D(2, 2);
                    tex.LoadImage(File.ReadAllBytes(path));
                    iconCache[iconString] = tex;
                }
            }

            if (!iconCache.ContainsKey(iconString))
                iconCache[iconString] = EditorGUIUtility.ObjectContent(null, typeof(DefaultAsset)).image;

            return iconCache[iconString];
        }

        Dictionary<string, bool> strippingReasonsFoldout = new Dictionary<string, bool>();
        private void StrippingEntityGui(string entity, ref bool odd)
        {
            GUILayout.BeginHorizontal(odd ? OddStyle : EvenStyle);
            odd = !odd;
            GUILayout.Space(15); 
            var reasons = report.strippingInfo.GetReasonsForIncluding(entity).ToList();
            if (!strippingIcons.ContainsKey(entity))
                strippingIcons[entity] = StrippingEntityIcon(entity);
            var icon = strippingIcons[entity];
            if (reasons.Any())
            {
                if (!strippingReasonsFoldout.ContainsKey(entity))
                    strippingReasonsFoldout[entity] = false;
                strippingReasonsFoldout[entity] = EditorGUILayout.Foldout(strippingReasonsFoldout[entity], new GUIContent(entity, icon));
            }
            else
                EditorGUILayout.LabelField(new GUIContent(entity, icon), GUILayout.Height(16), GUILayout.MaxWidth(1000));

            GUILayout.FlexibleSpace();

            if (strippingSizes.ContainsKey(entity) && strippingSizes[entity] != 0)
                GUILayout.Label(FormatSize((ulong)strippingSizes[entity]), SizeStyle, GUILayout.Width(100));

            GUILayout.EndHorizontal();

            if (!strippingReasonsFoldout.ContainsKey(entity) || !strippingReasonsFoldout[entity])
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
                    strippingIcons[depKey] = StrippingEntityIcon(sp.FindPropertyRelative("icon").stringValue);
                    strippingSizes[depKey] = sp.FindPropertyRelative("size").intValue;
                    //if (strippingSizes[depKey] != 0)
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
        List<ScenesUsingAssetGUI> scenesUsingAssetGUIs = new List<ScenesUsingAssetGUI>();

        void OnScenesUsingAssetsGUI()
        {
            if (report.scenesUsingAssets == null || report.scenesUsingAssets.Length==0 || report.scenesUsingAssets[0] == null || report.scenesUsingAssets[0].list==null || report.scenesUsingAssets[0].list.Length==0 )
            {
                EditorGUILayout.HelpBox("No info about which scenes are using assets in the build. Did you use BuildOptions.DetailedBuildReport?", MessageType.Info);
                return;
            }

            // re-create list of scenes using assets
            if(!scenesUsingAssetGUIs.Any())
            {
                foreach (var scenesUsingAsset in report.scenesUsingAssets[0].list)
                    scenesUsingAssetGUIs.Add(new ScenesUsingAssetGUI { assetPath = scenesUsingAsset.assetPath, scenePaths = scenesUsingAsset.scenePaths, foldoutState = true});
            }

            bool odd = true;
            foreach (var scenesUsingAssetGUI in scenesUsingAssetGUIs)
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
