using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;

namespace Unity.BuildReportInspector.Mobile
{
    internal static class MobileHelper
    {
        internal static IPlatformUtilities s_PlatformUtilities;
        internal static string AppendixSavePath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library/MobileReports");

        internal static void RegisterPlatformUtilities(IPlatformUtilities utilities)
        {
            if (!Directory.Exists(AppendixSavePath))
                Directory.CreateDirectory(AppendixSavePath);

            if (s_PlatformUtilities != null)
                throw new Exception("IPlatformUtilities already registered!");

            s_PlatformUtilities = utilities;
        }

        internal static void GenerateAndroidAppendix(string applicationPath, string guid)
        {
            var mobileAppendix = new MobileAppendix(applicationPath);
            SaveMobileAppendix(mobileAppendix, guid);
        }

        internal static void GenerateAppleAppendix(string applicationPath, string guid)
        {
            using (var archive = ZipFile.OpenRead(applicationPath))
            {
                var guidFile = archive.Entries.FirstOrDefault(x => x.Name == "UnityBuildGuid.txt");
                if (guidFile == null)
                {
                    Debug.LogError("The provided application was built before BuildReportInspector package was added to the project.");
                    return;
                }

                using (var reader = new StreamReader(guidFile.Open()))
                {
                    var applicationGuid = reader.ReadToEnd();
                    if (applicationGuid != guid)
                    {
                        Debug.LogError("The GUID of the selected report does not match the GUID of the provided application.\n" +
                                       $"Expected: {guid} but got: {applicationGuid}.");
                        return;
                    }
                }

                var mobileAppendix = new MobileAppendix(applicationPath);
                SaveMobileAppendix(mobileAppendix, guid);
            }
        }

        private static void SaveMobileAppendix(MobileAppendix appendix, string guid)
        {
            var appendixFilePath = Path.Combine(AppendixSavePath, guid);
            appendix.Save(appendixFilePath);
        }

        internal static MobileAppendix LoadMobileAppendix(string guid)
        {
            var appendixFile = Path.Combine(AppendixSavePath, guid);
            return File.Exists(appendixFile) ? MobileAppendix.Load(appendixFile) : null;
        }
    }
}
