using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Unity.BuildReportInspector
{
    public class AssetInBundleStats
    {
        public string sourceAssetPath;
        public ulong totalSize = 0;
        public Dictionary<string, ulong> assetBundleSizes = new Dictionary<string, ulong>(); // AssetBundleName -> size
    }

    // Utility to analyze the PackedAsset information in the build BuildReport to discover duplicated Assets.
    // This is only relevant for AssetBundles because Player builds should always deduplicate any referenced content (in the .sharedAsset files).
    // Note: The TestProject, in the git repo for this package, includes an simple scenario for demonstrating this feature.
    public class DuplicateAssets
    {
        // Unity tracks MonoBehaviours and ScriptableObjects using small Monoscript objects.
        // These can be numerous and distracting, unless you specifically want to investigate
        // duplication for MonoScripts.
        const bool kSkipMonoScripts = true;

        // Map from sourceAssetPath to statistics how this asset appears in AssetBundles
        public Dictionary<string, AssetInBundleStats> m_AssetStats;

        // Total size of all reported content in the PackAssets (ignore Scene content and non-content output in the build)
        public ulong m_TotalSize = 0;

        // Size of just the extra copies.  E.g. if a 1MB Asset appears 4 times, when that will add 3MB to this statistic.
        public ulong m_DuplicateSize = 0;

        public DuplicateAssets(BuildReport report)
        {
            CalculateStats(report);
        }

        // Skip certain PackedAsset entries based on their source path
        private bool ShouldSkipAsset(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
                // Generated or internal object
                return true;

            if (kSkipMonoScripts && sourcePath.EndsWith(".cs"))
                return true;

            if (sourcePath == "AssetBundle Object")
                // This is a generated object and different inside each AssetBundle, so never report it as duplicated
                return true;

            if (sourcePath == "Resources/unity_builtin_extra")
                // This file contains multiple "built-in" shaders, and only the specific Shaders that are referenced will be
                // copied into an AssetBundle.  There can be duplicated data in the build, but without examining down to the level
                // of the individual Shader objects its not possible to estimate the duplication.
                // (The ContentSummary and SourceAssets tab can be used to see the total size associated with this source)
                return true;

            return false;
        }

        private void CalculateStats(BuildReport report)
        {
            m_AssetStats = new Dictionary<string, AssetInBundleStats>();
            var fileListHelper = new FileListHelper(report);

            foreach (var packedAsset in report.packedAssets)
            {
                foreach (var packedAssetInfo in packedAsset.contents)
                {
                    m_TotalSize += packedAssetInfo.packedSize;

                    // Use the actual filename of the AssetBundle, instead of the internal filename
                    // E.g. instead of "CAB-05ada3d0be5ce07b1347f149d9743cb5" report "Texture.bundle"
                    var archiveName = fileListHelper.GetArchiveNameForInternalName(packedAsset.shortPath);
                    if (string.IsNullOrEmpty(archiveName))
                        continue; // AssetBundleManifest

                    var sourcePath = packedAssetInfo.sourceAssetPath;

                    if (ShouldSkipAsset(sourcePath))
                        continue;

                    if (!m_AssetStats.TryGetValue(sourcePath, out var assetStats))
                    {
                        assetStats = new AssetInBundleStats
                        {
                            sourceAssetPath = sourcePath
                        };
                        m_AssetStats[sourcePath] = assetStats;
                    }

                    assetStats.totalSize += packedAssetInfo.packedSize;

                    if (!assetStats.assetBundleSizes.ContainsKey(archiveName))
                        assetStats.assetBundleSizes[archiveName] = 0;

                    assetStats.assetBundleSizes[archiveName] += packedAssetInfo.packedSize;
                }
            }

            // We only want entries that appear in more than one AssetBundle,
            // and want to show biggest assets first
            m_AssetStats = m_AssetStats
                .Where(pair => pair.Value.assetBundleSizes.Count > 1)
                .OrderByDescending(pair => pair.Value.totalSize)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            CalculateDuplicatedSize();
        }

        private void CalculateDuplicatedSize()
        {
            m_DuplicateSize = 0;
            foreach (var assetStat in m_AssetStats)
            {
                // Typically the size should be the same inside each AssetBundle.
                // But possibly for FBX and other types that have subassets the content could actually
                // be different, so average things out
                var countItems = assetStat.Value.assetBundleSizes.Count;
                ulong totalSizeFromAsset = 0;
                foreach(var assetBundleStat in assetStat.Value.assetBundleSizes)
                {
                    totalSizeFromAsset += assetBundleStat.Value;
                }
                m_DuplicateSize += ((ulong)(countItems - 1) * totalSizeFromAsset) / (ulong)countItems;
            }
        }
    }
}
