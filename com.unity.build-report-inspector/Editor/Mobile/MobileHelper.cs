using System;
using System.IO;
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

        internal static void GenerateMobileAppendix(string applicationPath, string fileName)
        {
            var mobileAppendix = new MobileAppendix(applicationPath);
            var appendixFilePath = Path.Combine(AppendixSavePath, fileName);
            mobileAppendix.Save(appendixFilePath);
        }

        internal static MobileAppendix LoadMobileAppendix(string guid)
        {
            var appendixFile = Path.Combine(AppendixSavePath, guid);
            return File.Exists(appendixFile) ? MobileAppendix.Load(appendixFile) : null;
        }
    }
}
