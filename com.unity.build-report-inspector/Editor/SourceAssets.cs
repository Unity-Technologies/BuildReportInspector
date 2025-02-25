using System;
using System.Collections.Generic;
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
        public string path;       // Source path in the project
        public ulong size;        // bytes
        public string outputFile; // name of file in the build output directory
        public string type;
        public int objectCount;

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
            foreach (var packedAsset in report.packedAssets)
            {
                m_outputFiles[packedAsset.shortPath] = packedAsset.overhead;

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
                        assetTypesInFile[key] = new ContentEntry
                        {
                            size = entry.packedSize,
                            icon = m_calculateIcon ? AssetDatabase.GetCachedIcon(entry.sourceAssetPath) : null,
                            outputFile = packedAsset.shortPath,
                            type = type,
                            path = entry.sourceAssetPath,
                            objectCount = 1
                        };
                    }
                }

                foreach (var entry in assetTypesInFile)
                {
                    m_assets.Add(entry.Value);

                    var sizeProp = entry.Value.size;
                    m_outputFiles[packedAsset.shortPath] += sizeProp;
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

    }
}
