using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

// Statistics for a specific type
public class TypeStats
{
    // Pointer or reference to the type (in C#, this would likely refer to a class or type object)
    public Type type = null;

    // Object + resource data size
    public ulong size = 0;

    // Number of objects
    public int objectCount = 0;

    // Number of resources
    public int resourceCount = 0;
}

// Information about all objects from a specific asset
public class AssetStats
{
    // Asset's unique identifier (GUID)
    public GUID sourceAssetGUID;

    // Path to the corresponding asset
    public string sourceAssetPath;

    // Total size of the asset (including objects and resources)
    public ulong size = 0;

    // Number of objects associated with the asset
    public int objectCount = 0;

    // Number of resources associated with the asset
    public int resourceCount = 0;
}

// Summary of the size of the build output
public class BuildOutputStatistics
{
    // Fields
    public int serializedFileCount = 0;
    public int generatedFileCount = 0; // Number of serialized files and resource files that are created in this build
    public int resourceFileCount = 0;
    public int objectCount = 0;
    public int internalObjectCount = 0; // This is an object we cannot map back to an object in the project.
                                        // Expected for certain objects that are generated during the build for AssetBundle.

    // All this data is the uncompressed size (e.g. not accounting compression if the file is inside a Unity Archive file)

    // Include headers. Includes most padding but there may be extra padding at the very end of the file
    public ulong totalSerializedFileSize = 0;

    // Sum of sizes of serialized files and resource files that are created in this build
    public ulong totalGeneratedFileSize = 0;

    // Totals of just the header portion of the Serialized files (Resource files don't have any header)
    public ulong totalHeaderSize = 0;

    // .resS, .resource files. Actual size can be a bit larger because of padding
    public ulong totalResourceSize = 0;

    // Sizes include Resource file content if it is owned by the object
    public Dictionary<Type, TypeStats> statsPerType = new Dictionary<Type, TypeStats>();

    public TypeStats[] sortedTypeStats; // The contents with statsPerType sorted in descending order by size

    // AssetDatabase GUID to stats
    public Dictionary<GUID, AssetStats> assetStats = new Dictionary<GUID, AssetStats>();

    public AssetStats[] sortedAssetStats; // The contents with assetStats sorted in descending order by size

    // Notes on further statistics that could be added:
    // - Calculate total compression "win" (comparing total content size with actual archive size).  Overall and per-archive
    // - Scripting types (size and counts per MonoBehaviour/ScriptableObject derived class)
}

public class ContentSummary
{
    public BuildOutputStatistics m_Stats = new();

    public ContentSummary(BuildReport report)
    {
        CalculateStats(report);
    }

    private void CalculateStats(BuildReport report)
    {
        PackedAssets[] packedAssets = report.packedAssets;

        foreach(var packedAsset in packedAssets)
        {
            // Resource files contain blobs of data referenced from objects in the serialized files (e.g. audio/video/texture/mesh)
            bool isResourceFile = packedAsset.shortPath.EndsWith(".resS") || packedAsset.shortPath.EndsWith(".resource");

            if (isResourceFile)
            {
                m_Stats.resourceFileCount++;
                m_Stats.totalResourceSize += packedAsset.overhead; // Currently 0 because there is no header for these files
            }
            else
            {
                m_Stats.serializedFileCount++;
                m_Stats.totalSerializedFileSize += packedAsset.overhead; // Header of the serialized file
                m_Stats.totalHeaderSize += packedAsset.overhead; // Header of the serialized file
            }

            // The PackedAssetInfo describe each object inside an SerializedFile, and each blob of data in resource files
            var packedAssetInfoArray = packedAsset.contents;
            foreach(var packedAssetInfo in packedAssetInfoArray)
            {
                if (isResourceFile)
                {
                    m_Stats.totalResourceSize += packedAssetInfo.packedSize;
                }
                else
                {
                    m_Stats.totalSerializedFileSize += packedAssetInfo.packedSize;
                    m_Stats.objectCount++;
                }

                if (m_Stats.statsPerType.ContainsKey(packedAssetInfo.type))
                {
                    var stats = m_Stats.statsPerType[packedAssetInfo.type];
                    UpdateTypeStats(stats, packedAssetInfo, isResourceFile);
                }
                else
                {
                    var stats = new TypeStats();
                    stats.type = packedAssetInfo.type;
                    UpdateTypeStats(stats, packedAssetInfo, isResourceFile);
                    m_Stats.statsPerType.Add(packedAssetInfo.type, stats);
                }

                if (string.IsNullOrEmpty(packedAssetInfo.sourceAssetPath))
                {
                    m_Stats.internalObjectCount++;
                }
                else if (m_Stats.assetStats.ContainsKey(packedAssetInfo.sourceAssetGUID))
                {
                    var stats = m_Stats.assetStats[packedAssetInfo.sourceAssetGUID];
                    UpdateAssetStats(stats, packedAssetInfo, isResourceFile);
                }
                else
                {
                    var stats = new AssetStats();
                    stats.sourceAssetPath = packedAssetInfo.sourceAssetPath;
                    UpdateAssetStats(stats, packedAssetInfo, isResourceFile);
                    m_Stats.assetStats.Add(packedAssetInfo.sourceAssetGUID, stats);
                }
            }
        }

        m_Stats.sortedTypeStats = m_Stats.statsPerType.Values.OrderByDescending(stats => stats.size).ToArray();
        m_Stats.sortedAssetStats = m_Stats.assetStats.Values.OrderByDescending(stats => stats.size).ToArray();
    }

    private void UpdateTypeStats(TypeStats stats, PackedAssetInfo packedInfo, bool isResourceFile)
    {
        stats.size += packedInfo.packedSize;
        if (isResourceFile)
            stats.resourceCount += 1;
        else
            stats.objectCount += 1;
    }

    private void UpdateAssetStats(AssetStats stats, PackedAssetInfo packedInfo, bool isResourceFile)
    {
        stats.size += packedInfo.packedSize;
        if (isResourceFile)
            stats.resourceCount += 1;
        else
            stats.objectCount += 1;
    }
}