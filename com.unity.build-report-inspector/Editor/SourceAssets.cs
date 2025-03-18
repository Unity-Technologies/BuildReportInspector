using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Unity.BuildReportInspector
{
    // Support code for calculated the datastructures shown on the Source Assets tab
    // This code is kept separate from the UI and could be used in isolation (e.g. from scriptings)

    // Size usage detail within the built data.
    // This records the total size of a particular type from a particular source asset within the specified output file
    public struct ContentEntry
    {
        public string path;        // Source path in the project
        public ulong size;         // bytes
        public string outputFile;  // name of file in the build output directory
        public string internalArchivePath; // name of file inside AssetBundle (empty for player build)
        public string type;
        public int objectCount;
        public string extension;   // File extension of the source path (e.g. ".jpg")

        public Texture icon; // Only required when displaying in the UI
    }

    public class ContentAnalysis
    {
        public List<ContentEntry> m_assets; // records contents of the build output.  Objects of the same type within the same file are collapsed together to single entry
        public Dictionary<string, ulong> m_outputFiles; // Filepath -> size
        public Dictionary<string, ulong> m_assetTypes;  // Type -> size

        bool m_calculateIcon = true;
        int m_maxEntries = 0;

        public ContentAnalysis(BuildReport report, int maxEntries, bool calculateIcon)
        {
            m_maxEntries = maxEntries;
            m_calculateIcon = calculateIcon;
            CalculateStats(report);
        }

        public bool HitMaximumEntries() { return m_assets.Count == m_maxEntries; }

        private void CalculateStats(BuildReport report)
        {
            m_assets = new List<ContentEntry>();
            m_outputFiles = new Dictionary<string, ulong>();
            m_assetTypes = new Dictionary<string, ulong>();

            var internalNameToArchiveMapping = new Dictionary<string, string>();
            CalculateAssetBundleMapping(report, internalNameToArchiveMapping);

            foreach (var packedAsset in report.packedAssets)
            {
                string outputFile = "";
                string internalArchivePath = "";
                if (internalNameToArchiveMapping.ContainsKey(packedAsset.shortPath))
                {
                    internalArchivePath = packedAsset.shortPath;
                    outputFile = internalNameToArchiveMapping[packedAsset.shortPath];
                }
                else
                {
                    outputFile = packedAsset.shortPath;
                }

                if (!m_outputFiles.ContainsKey(outputFile))
                    m_outputFiles[outputFile] = 0;
                m_outputFiles[outputFile] += packedAsset.overhead;

                // Combine all objects that have the same type and source asset to reduce the overhead,
                // e.g. no use reporting 1,000 individual gameobjects in the same prefab.
                // Note: this still won't scale for truly large builds, because of the underlying approach of recording
                // every single object in the build report.
                // For those cases the UnityDataTools Analyze tool, which uses an sqlite database, is recommended.
                var assetTypesInFile = new Dictionary<string, ContentEntry>();
                foreach (var entry in packedAsset.contents)
                {
                    var type = entry.type.ToString();

                    if (type.EndsWith("Importer"))
                        type = type.Substring(0, type.Length - 8);

                    // A single output file can contain objects from multiple source objects.
                    var key = type + entry.sourceAssetGUID.ToString();

                    if (assetTypesInFile.ContainsKey(key))
                    {
                        // update the statistics
                        var existingEntry = assetTypesInFile[key];
                        existingEntry.size += entry.packedSize;
                        existingEntry.objectCount++;
                        assetTypesInFile[key] = existingEntry;
                    }
                    else
                    {
                        string path = entry.sourceAssetPath;
                        if (string.IsNullOrEmpty(path))
                            path = "Generated"; // Some build output is generated and not associated with a source Asset

                        assetTypesInFile[key] = new ContentEntry
                        {
                            size = entry.packedSize,
                            icon = m_calculateIcon ? AssetDatabase.GetCachedIcon(entry.sourceAssetPath) : null,
                            outputFile = outputFile,
                            internalArchivePath = internalArchivePath,
                            type = type,
                            path = path,
                            extension = Path.GetExtension(path),
                            objectCount = 1
                        };
                    }
                }

                foreach (var entry in assetTypesInFile)
                {
                    m_assets.Add(entry.Value);

                    var sizeProp = entry.Value.size;
                    m_outputFiles[outputFile] += sizeProp;
                    if (!m_assetTypes.ContainsKey(entry.Value.type))
                        m_assetTypes[entry.Value.type] = 0;
                    m_assetTypes[entry.Value.type] += sizeProp;

                    if (m_assets.Count == m_maxEntries)
                        break;
                }

                if (m_assets.Count == m_maxEntries)
                    break;
            }

            // Sort m_assets in descending order by size
            m_assets = m_assets.OrderBy(p => ulong.MaxValue - p.size).ToList();
            m_outputFiles = m_outputFiles.OrderBy(p => ulong.MaxValue - p.Value).ToDictionary(x => x.Key, x => x.Value);
            m_assetTypes = m_assetTypes.OrderBy(p => ulong.MaxValue - p.Value).ToDictionary(x => x.Key, x => x.Value);
        }

        private void CalculateAssetBundleMapping(BuildReport report, Dictionary<string, string> mapping)
        {
            mapping.Clear();

#if UNITY_6000_0_OR_NEWER
            if (report.summary.buildType == BuildType.Player)
                return;
#endif
            var files = report.GetFiles();

            // Map between the internal file names inside Archive files back to the Archive filename.
            // Currently this only applies to AssetBundle builds, which can have many output files and which use hard to understand internal file names.
            // For compressed Player builds the PackedAssets reports the internal files, but the file list does not report the unity3d content,
            // so this code will not pick up the mapping.  However because there is only a single unity3d file on most platforms this is less important

            /*
            Example input:

            - path: C:/Src/TestProject/Build/AssetBundles/audio.bundle/CAB-76a378bdc9304bd3c3a82de8dd97981a.resource
              role: StreamingResourceFile
            ...
            - path: C:/Src/TestProject/Build/AssetBundles/audio.bundle
              role: AssetBundle
            ...

            Result:
            CAB-76a378bdc9304bd3c3a82de8dd97981a.resource -> audio.bundle
            */


            // Track full path to just the archive filename for any AssetBundles in the build output
            var archivePathToFileName = new Dictionary<string, string>();
            foreach (var file in files)
            {
                if (file.role == CommonRoles.assetBundle ||
                    file.role == CommonRoles.manifestAssetBundle)
                {
                    var justFileName = Path.GetFileName(file.path);
                    archivePathToFileName[file.path] = justFileName;
                }
            }

            if (archivePathToFileName.Count() == 0)
                return;

            // Find files that have paths inside one of the AssetBundle paths
            var internalNameToArchiveMapping = new Dictionary<string, string>();
            foreach (var file in files)
            {
                // This assumes that the files are not in subdirectory inside the archive
                var justPath = Path.GetDirectoryName(file.path).Replace('\\', '/');
                var justFileName = Path.GetFileName(file.path);
                if (archivePathToFileName.ContainsKey(justPath))
                {
                    mapping[justFileName] = archivePathToFileName[justPath];
                }
            }
        }

        // For larger builds it can be better to analyze using a pivot tables in a spreadsheet or a database.
        // So this method exports the raw analysis data to a CSV file.
        // On success this returns an empty string, otherwise it returns a description of the nature of the failure.
        public string SaveAssetsToCsv(string filePath)
        {
            string errorMessage = "";
            try
            {
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    // Header row
                    writer.WriteLine("SourceAssetPath,OutputFile,Type,Size,ObjectCount,Extension,AssetBundlePath");

                    foreach (var asset in m_assets)
                    {
                        writer.WriteLine($"{EscapeCsv(asset.path)},{EscapeCsv(asset.outputFile)},{EscapeCsv(asset.type)},{asset.size},{asset.objectCount},{asset.extension},{asset.internalArchivePath}");
                    }
                }
                Debug.Log($"Content analysis written to {filePath}");
            }
            catch (Exception e)
            {
                errorMessage = $"An error occurred while writing to CSV:\n{filePath}\n\n{e.Message}";
            }
            return errorMessage;
        }

        // Escapes a string for use in a CSV file, wrapping it in quotes if it contains special characters (commas, quotes, or newlines).
        // Quotes in the original string are escaped by doubling them(" -> "").
        private static string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return string.Empty;
            }

            // Check if the field contains special characters
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                // Escape double quotes by doubling them
                field = field.Replace("\"", "\"\"");
                // Wrap the field in double quotes
                return $"\"{field}\"";
            }

            return field;
        }

    }
}
