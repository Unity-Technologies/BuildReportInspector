using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace Unity.BuildReportInspector.Mobile
{
    [Serializable]
    internal class MobileFile
    {
        internal string Path { get; }
        internal long CompressedSize { get; }
        internal long UncompressedSize { get; }

        internal MobileFile(string path, long compressedSize, long uncompressedSize)
        {
            Path = path;
            CompressedSize = compressedSize;
            UncompressedSize = uncompressedSize;
        }
    }

    [Serializable]
    internal class MobileArchInfo
    {
        [Serializable]
        internal class ExecutableSegments
        {
            internal long TextSize { get; set; }
            internal long DataSize { get; set; }
            internal long LlvmSize { get; set; }
            internal long LinkeditSize { get; set; }
        }

        internal string Name { get; set; }
        internal long DownloadSize { get; set; }
        internal ExecutableSegments Segments { get; set; }

        internal MobileArchInfo() { }
        
        internal MobileArchInfo(string name)
        {
            Name = name;
        }
    }

    [Serializable]
    internal class MobileAppendix
    {
        internal long BuildSize { get; }
        internal MobileFile[] Files { get; }
        internal MobileArchInfo[] Architectures { get; }

        internal MobileAppendix(string applicationPath)
        {
            if (!IsBuildValid(applicationPath))
            {
                Debug.LogError("Couldn't collect report data from application bundle: bundle invalid.");
                return;
            }

            // Get the actual size of the app bundle on disk
            BuildSize = new FileInfo(applicationPath).Length;
            
            // Get the list of files inside of the app bundle from the zip header
            using (var archive = ZipFile.OpenRead(applicationPath))
            {
                Files = new MobileFile[archive.Entries.Count];
                for (var i = 0; i < archive.Entries.Count; i++)
                {
                    // Skip iOS directory meta files
                    if (archive.Entries[i].Length == 0)
                        continue;

                    Files[i] = new MobileFile(
                        archive.Entries[i].FullName, 
                        archive.Entries[i].CompressedLength, 
                        archive.Entries[i].Length);
                }
            }
            
            if (MobileHelper.s_PlatformUtilities == null)
                return;

            // Extract the data about the different architectures comprising the build
            if (MobileHelper.s_PlatformUtilities.GetArchitectureInfo(applicationPath, out var architectureInfos))
            {
                Architectures = architectureInfos;
            }
        }

        private static bool IsBuildValid(string buildPath)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(buildPath))
                {
                    return archive.Entries.Any(x =>
                        x.FullName == "AndroidManifest.xml" ||
                        x.FullName == "BundleConfig.pb" ||
                        x.FullName == "Info.plist");
                }
            }
            catch
            {
                return false;
            }
        }

        internal void Save(string path)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, this);
            }
        }

        internal static MobileAppendix Load(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open))
            {
                var formatter = new BinaryFormatter();
                return formatter.Deserialize(stream) as MobileAppendix;
            }
        }
    }
}
