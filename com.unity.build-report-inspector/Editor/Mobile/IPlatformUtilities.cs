namespace Unity.BuildReportInspector.Mobile
{
    internal interface IPlatformUtilities
    {
        bool GetArchitectureInfo(string applicationPath, out MobileArchInfo[] architectures);
    }
}
