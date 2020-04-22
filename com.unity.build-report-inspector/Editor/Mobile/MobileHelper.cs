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

        internal static string AppendixSavePath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Assets/BuildReports/Mobile");

        internal static void RegisterPlatformUtilities(IPlatformUtilities utilities)
        {
            if (!Directory.Exists(AppendixSavePath))
                Directory.CreateDirectory(AppendixSavePath);

            s_PlatformUtilities = utilities;
        }

        internal static void GenerateAndroidAppendix(string applicationPath, string guid)
        {
            MobileHelper.RegisterPlatformUtilities(new AndroidUtilities());
            GenerateMobileAppendix(applicationPath, guid);
        }

        internal static void GenerateAppleAppendix(string applicationPath, string guid)
        {
            try
            {
                MobileHelper.RegisterPlatformUtilities(new AppleUtilities());
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
                            Debug.LogError($"The GUID of the selected report does not match the GUID of the provided application.\nExpected: {guid} but got: {applicationGuid}.");
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
                if (!Directory.Exists(AppendixSavePath))
                    Directory.CreateDirectory(AppendixSavePath);

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
