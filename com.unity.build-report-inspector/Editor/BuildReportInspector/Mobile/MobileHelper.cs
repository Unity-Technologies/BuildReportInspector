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

        internal static string AppendixSavePath
        {
            get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library/MobileReports"); }
        }

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
            GenerateMobileAppendix(applicationPath, guid);
        }

        internal static void GenerateAppleAppendix(string applicationPath, string guid)
        {
            try
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
                            Debug.LogErrorFormat("The GUID of the selected report does not match the GUID of the provided application.\nExpected: {0} but got: {1}.", guid, applicationGuid);
                            return;
                        }
                    }
                }
                
                GenerateMobileAppendix(applicationPath, guid);
            }
            catch
            {
                Debug.LogError("Could not open the application archive. Please provide a valid .ipa bundle.");
            }

        }

        private static void GenerateMobileAppendix(string applicationPath, string guid)
        {
            try
            {
                var appendix = new MobileAppendix(applicationPath);
                var appendixFilePath = Path.Combine(AppendixSavePath, guid);
                appendix.Save(appendixFilePath);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        internal static MobileAppendix LoadMobileAppendix(string guid)
        {
            var appendixFile = Path.Combine(AppendixSavePath, guid);
            return File.Exists(appendixFile) ? MobileAppendix.Load(appendixFile) : null;
        }
    }
}
