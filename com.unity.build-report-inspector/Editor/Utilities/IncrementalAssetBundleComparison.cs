using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

// Based on example script originally published on https://discussions.unity.com/t/about-incremental-build-and-asset-bundle-hashes

// Utility showing how to use existing Unity and System APIs to monitor the output of an AssetBundle Incremental Build,

// The Test project has an example of how to incorporate it into a build script.
// The output is written to the Console window.

struct BundleBuildInfo
{
    public Hash128 bundleHash; // As calculated by Unity
    public uint crc;           // As calculated by Unity
    public DateTime timeStamp; // Can be used to detect if the bundle was rebuilt or the previous file was reused
    public string contentHash; // MD5, Expected to always change when the CRC changes and vis versa

    public override string ToString()
    {
        return $"Unity hash: {bundleHash} Content MD5: {contentHash} CRC: {crc.ToString("X8")} Write time: {timeStamp}";
    }
}

// Usage:
// In your build script create an instance of this object and call CollectBuildInfo() + ReportToConsole() prior to running an incremental build.
// Call DetectBuildResults() and ReportToConsole() after the build to get a report about what was changed by the incremental build.
public class IncrementalBuildReporter
{
    Dictionary<string, BundleBuildInfo> m_previousBuildInfo; // map from path to BuildInfo
    string m_buildPath; // Typically a relative path within the Unity project
    string m_manifestAssetBundlePath;

    StringBuilder m_report;

    public IncrementalBuildReporter(string buildPath)
    {
        m_report = new StringBuilder();
        m_buildPath = buildPath;
        m_previousBuildInfo = new();

        var directoryName = Path.GetFileName(buildPath);

        // Special AssetBundle that stores the AssetBundleManifest follows this naming convention
        m_manifestAssetBundlePath = buildPath + "/" + directoryName;

        if (!File.Exists(m_manifestAssetBundlePath))
        {
            // Expected on the first build
            m_report.AppendLine("No Previous Build Found.  All AssetBundles will be rebuilt.");
            return;
        }

        m_report.AppendLine("Collecting info from previous build");
        CollectBuildInfo(m_previousBuildInfo);
    }

    public void ReportToConsole()
    {
        Debug.Log(m_report.ToString());
        m_report.Clear();
    }

    private void CollectBuildInfo(Dictionary<string, BundleBuildInfo> bundleInfos)
    {
        var manifestAssetBundle = AssetBundle.LoadFromFile(m_manifestAssetBundlePath);

        try
        {
            var assetBundleManifest = manifestAssetBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");

            var allBundles = assetBundleManifest.GetAllAssetBundles();
            foreach (var bundleRelativePath in allBundles)
            {
                // bundle is the AssetBundle's path relative to the root build folder
                var bundlePath = m_buildPath + "/" + bundleRelativePath;

                if (!File.Exists(bundlePath))
                {
                    // Bundles may have been manually erased or moved after the build
                    m_report.AppendLine("AssetBundle " + bundlePath + " is missing from disk");
                    continue;
                }

                // Get the CRC from the bundle's .manifest file
                if (!BuildPipeline.GetCRCForAssetBundle(bundlePath, out uint crc))
                {
                    m_report.AppendLine("Failed to read CRC from manifest file of " + bundlePath);
                    continue;
                }

                var fileInfo = new FileInfo(bundlePath);

                var bundleInfo = new BundleBuildInfo()
                {
                    bundleHash = assetBundleManifest.GetAssetBundleHash(bundleRelativePath), //
                    crc = crc,
                    timeStamp = fileInfo.LastWriteTime,
                    contentHash = GetMD5HashFromAssetBundle(bundlePath)
                };

                bundleInfos.Add(bundleRelativePath, bundleInfo);
            }
        }
        finally
        {
            manifestAssetBundle.Unload(true);
        }
    }

    public void DetectBuildResults()
    {
        m_report.AppendLine().AppendLine("Collecting results of new Build:");

        var newBuildInfo = new Dictionary<string, BundleBuildInfo>();
        CollectBuildInfo(newBuildInfo);

        foreach (KeyValuePair<string, BundleBuildInfo> dictionaryEntry in newBuildInfo)
        {
            string bundlePath = dictionaryEntry.Key;
            BundleBuildInfo newBundleInfo = dictionaryEntry.Value;

            if (m_previousBuildInfo.TryGetValue(bundlePath, out BundleBuildInfo previousBundleInfo))
            {
                if (previousBundleInfo.timeStamp == newBundleInfo.timeStamp)
                {
                    // Bundle was not rebuilt.  Do some sanity checking just in case the timestamp is misleading
                    if (previousBundleInfo.crc != newBundleInfo.crc ||
                        previousBundleInfo.contentHash != newBundleInfo.contentHash)
                    {
                        m_report.AppendLine($"*UNEXPECTED* [Timestamp match with new content]: {bundlePath}\n\tNow:  {newBundleInfo} \n\tWas: {previousBundleInfo}");
                    }
                    else
                    {
                        // Incremental build decided not to build this bundle
                        m_report.AppendLine($"[Not rebuilt]: {bundlePath}\n\t{newBundleInfo}");
                    }
                }
                else if (previousBundleInfo.bundleHash == newBundleInfo.bundleHash)
                {
                    if (previousBundleInfo.crc != newBundleInfo.crc)
                    {
                        // Hash is the same, but according to the CRC, the bundle has new content, so this is a "hash conflict".
                        // This is problematic if the hash is used to distinguish different versions of the AssetBundle (e.g. along with the AssetBundle cache)
                        // If this occurs be wary of releasing this build.
                        m_report.AppendLine($"*WARNING* [New CRC content, but unchanged hash]: {bundlePath}\n\tNow: {newBundleInfo}\n\tWas: {previousBundleInfo}");
                    }
                    else if (previousBundleInfo.contentHash != newBundleInfo.contentHash)
                    {
                        // Normally shouldn't happen, because the CRC check above should also trigger
                        m_report.AppendLine($"*WARNING* [New file content, unchanged hash]: {bundlePath}");
                    }
                    else
                    {
                        // Expected with ForceRebuildAssetBundle or if Unity is being conservative and rebuilding something that might have changed
                        m_report.AppendLine($"[Rebuilt, identical content]: {bundlePath}\n\tNow: {newBundleInfo}\n\tWas: {previousBundleInfo}");
                    }
                }
                else
                {
                    if (previousBundleInfo.contentHash == newBundleInfo.contentHash)
                    {
                        // Expected if the incremental build heuristic has changed, e.g. when upgrading Unity
                        m_report.AppendLine($"[Rebuilt, new hash produced identical content]:{bundlePath}\n\tNow: {newBundleInfo}\n\tWas: {previousBundleInfo}");
                    }
                    else
                    {
                        // The normal case for a AssetBundle that required rebuild
                        m_report.AppendLine($"[Rebuilt, new content]: {bundlePath}\n\tNow: {newBundleInfo}\n\tWas: {previousBundleInfo}");
                    }
                }

                // Clear it out so that we can detect obsolete AssetBundles
                m_previousBuildInfo.Remove(bundlePath);
            }
            else
            {
                m_report.AppendLine($"[Brand new]: {bundlePath}\n\t{newBundleInfo}");
            }
        }

        // Anything left in this structure was not matched with an AssetBundle from the new build (so potentially can be erased)
        foreach (KeyValuePair<string, BundleBuildInfo> dictionaryEntry in m_previousBuildInfo)
        {
            m_report.AppendLine($"[Obsolete bundle]: {dictionaryEntry.Key}\n\t{dictionaryEntry.Value}");
        }
    }

    private static string GetMD5HashFromAssetBundle(string fileName)
    {
        //Note: The file content will change if the compression is changed (e.g. the LZMA -> LZ4 conversion done for AssetBundle cache)
        //Tip: the AssetBundleStripUnityVersion flag is recommended if you use hashing to track AssetBundle versions.
        FileStream file = new FileStream(fileName, FileMode.Open);

        var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(file);
        file.Close();

        // Convert to string
        var sb = new StringBuilder();
        for (int i = 0; i < hash.Length; i++)
            sb.Append(hash[i].ToString("x2"));
        return sb.ToString();
    }
}