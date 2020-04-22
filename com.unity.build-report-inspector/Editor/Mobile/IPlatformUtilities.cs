namespace Unity.BuildReportInspector.Mobile
{
    internal interface IPlatformUtilities
    {
        MobileArchInfo[] GetArchitectureInfo(string applicationPath);
    }
}
