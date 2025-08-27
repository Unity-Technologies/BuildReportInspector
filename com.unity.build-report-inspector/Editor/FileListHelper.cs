using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Reporting;

namespace Unity.BuildReportInspector
{
    /// <summary>
    /// Helper class for processing file-related operations from the BuildReport.
    /// </summary>
    public class FileListHelper
    {
        private Dictionary<string, string> internalNameToArchiveMapping = new Dictionary<string, string>();

        public FileListHelper(BuildReport report)
        {
            CalculateAssetBundleMapping(report);
        }

        /// <summary>
        /// Given a name of a content file in the build output this will return the Archive name, if it is inside an AssetBundle.
        /// The archive name is the name of the AssetBundle file so this is a more user friendly name than the internal name.
        /// For example GetArchiveNameForInternalName("CAB-76a378bdc9304bd3c3a82de8dd97981a.resource") might return "audio.bundle"
        /// For files in an Player build this always returns null.
        /// </summary>
        /// <param name="internalName">The internal name to map.</param>
        /// <returns>The corresponding archive name, or null if not found.</returns>
        public string GetArchiveNameForInternalName(string internalName)
        {
            return internalNameToArchiveMapping.TryGetValue(internalName, out var archiveName) ? archiveName : null;
        }

        /// <summary>
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
        /// </summary>
        private void CalculateAssetBundleMapping(BuildReport report)
        {
            internalNameToArchiveMapping.Clear();

#if UNITY_6000_0_OR_NEWER
            if (report.summary.buildType == BuildType.Player)
                return;
#endif

#if UNITY_2022_1_OR_NEWER
            var files = report.GetFiles();
#else
            var files = report.files;
#endif // UNITY_2022_1_OR_NEWER

            // Track archive paths and their base filenames for AssetBundle or manifest files
            var archivePathToFileName = new Dictionary<string, string>();
            foreach (var file in files)
            {
                if (file.role == CommonRoles.assetBundle)
                {
                    var justFileName = Path.GetFileName(file.path);
                    archivePathToFileName[file.path] = justFileName;
                }
            }

            if (archivePathToFileName.Count == 0)
                return;

            // Map internal file names to their corresponding archive filenames
            foreach (var file in files)
            {
                // Assumes internal files are not in subdirectories inside the archive
                var justPath = Path.GetDirectoryName(file.path)?.Replace('\\', '/');
                var justFileName = Path.GetFileName(file.path);

                if (!string.IsNullOrEmpty(justPath) && archivePathToFileName.ContainsKey(justPath))
                {
                    internalNameToArchiveMapping[justFileName] = archivePathToFileName[justPath];
                }
            }
        }
    }
}
