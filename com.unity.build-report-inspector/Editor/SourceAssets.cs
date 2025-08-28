using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Text.RegularExpressions;

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
        public Dictionary<string, ulong> m_outputFiles; // Filepath -> size (Sorted biggest to smallest)
        public Dictionary<string, ulong> m_assetTypes;  // Type -> size (Sorted biggest to smallest)

        private static readonly Texture DefaultAssetIcon = EditorGUIUtility.IconContent("DefaultAsset Icon").image;

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

            // Initialize the FileListHelper
            var fileListHelper = new FileListHelper(report);

            foreach (var packedAsset in report.packedAssets)
            {
                string outputFile;
                string internalArchivePath = "";

                // Look up the archive name for the current internal file name
                var archiveName = fileListHelper.GetArchiveNameForInternalName(packedAsset.shortPath);
                if (!string.IsNullOrEmpty(archiveName))
                {
                    internalArchivePath = packedAsset.shortPath;
                    outputFile = archiveName;
                }
                else
                {
                    outputFile = packedAsset.shortPath;
                }

                if (!m_outputFiles.ContainsKey(outputFile))
                    m_outputFiles[outputFile] = 0;
                m_outputFiles[outputFile] += packedAsset.overhead;

                // Combine all objects that have the same type and source asset
                var assetTypesInFile = new Dictionary<string, ContentEntry>();
                foreach (var entry in packedAsset.contents)
                {
                    var type = entry.type.ToString();

                    if (type.EndsWith("Importer"))
                        type = type.Substring(0, type.Length - 8);

                    var key = type + entry.sourceAssetGUID.ToString();

                    if (assetTypesInFile.ContainsKey(key))
                    {
                        // Update existing entry
                        var existingEntry = assetTypesInFile[key];
                        existingEntry.size += entry.packedSize;
                        existingEntry.objectCount++;
                        assetTypesInFile[key] = existingEntry;
                    }
                    else
                    {
                        string path = entry.sourceAssetPath;
                        if (string.IsNullOrEmpty(path))
                            path = "Generated"; // Build output not associated with a source asset

                        string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                        Regex regex = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
                        string sanitizedPath = regex.Replace(path, "");

                        assetTypesInFile[key] = new ContentEntry
                        {
                            size = entry.packedSize,
                            icon = m_calculateIcon ? AssetDatabase.GetCachedIcon(entry.sourceAssetPath) ?? DefaultAssetIcon : null,
                            outputFile = outputFile,
                            internalArchivePath = internalArchivePath,
                            type = type,
                            path = path,
                            extension = Path.GetExtension(sanitizedPath),
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

            // Sort assets, output files, and asset types in descending order by size
            m_assets = m_assets.OrderBy(p => ulong.MaxValue - p.size).ToList();
            m_outputFiles = m_outputFiles.OrderBy(p => ulong.MaxValue - p.Value).ToDictionary(x => x.Key, x => x.Value);
            m_assetTypes = m_assetTypes.OrderBy(p => ulong.MaxValue - p.Value).ToDictionary(x => x.Key, x => x.Value);
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
