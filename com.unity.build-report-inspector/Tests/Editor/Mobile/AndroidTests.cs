using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.BuildReportInspector.Mobile;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine;

[TestFixture]
[RequirePlatformSupport(BuildTarget.Android)]
public class AndroidTests
{
    private string m_BuildPath;
    private List<string> m_AppendixGuids;

    [Test]
    public void Android_CanGenerateApkAppendix()
    {
        var appendix = BuildPlayer(ScriptingImplementation.Mono2x, AndroidArchitecture.ARMv7, false);

        Assert.AreEqual(1, appendix.Architectures.Length, "Appendix contains unexpected architectures.");
        Assert.AreEqual("armeabi-v7a", appendix.Architectures[0].Name, "Architecture name parsed incorrectly for architecture armeabi-v7a");

        VerifyGenericAppendixData(appendix);
    }

    [Test]
    public void Android_CanGenerateAabAppendix()
    {
        var appendix = BuildPlayer(ScriptingImplementation.IL2CPP, AndroidArchitecture.All, true);

        Assert.AreEqual(2, appendix.Architectures.Length, "Appendix contains unexpected architectures.");
        foreach (var arch in appendix.Architectures)
        {
            Debug.Log(arch.Name);
        }
        Assert.That(appendix.Architectures.Any(x => x.Name == "armeabi-v7a"), "Architecture armeabi-v7a not found in the appendix.");
        Assert.That(appendix.Architectures.Any(x => x.Name == "arm64-v8a"), "Architecture arm64-v8a not found in the appendix.");

        VerifyGenericAppendixData(appendix);
    }

    private static void VerifyGenericAppendixData(MobileAppendix appendix)
    {
        Assert.Greater(appendix.BuildSize, 0, "Build size not calculated correctly.");
        foreach (var arch in appendix.Architectures)
        {
            Assert.Greater(arch.DownloadSize, 0, $"Download size not estimated for architecture {arch.Name}");
        }
    }
    
    [OneTimeSetUp]
    public void Setup()
    {
        m_BuildPath = Utilities.GetTemporaryFolder();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        if (Directory.Exists(m_BuildPath))
        {
            Directory.Delete(m_BuildPath, true);
        }

        if (m_AppendixGuids == null) return;
        foreach (var guid in m_AppendixGuids)
        {
            var appendixPath = Path.Combine(MobileHelper.AppendixSavePath, guid);
            var appendixMeta = $"{appendixPath}.meta";
            Debug.Log(appendixPath);
            File.Delete(appendixPath);
            File.Delete(appendixMeta);
        }
    }

    private MobileAppendix BuildPlayer(ScriptingImplementation backend, AndroidArchitecture architecture, bool buildAab)
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, backend);
        EditorUserBuildSettings.buildAppBundle = buildAab;
        PlayerSettings.Android.targetArchitectures = architecture;
        var buildName = "test" + (buildAab ? ".aab" : string.Empty);
        var options = new BuildPlayerOptions
        {
            target = BuildTarget.Android,
            locationPathName = Path.Combine(m_BuildPath, buildName),
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (m_AppendixGuids == null)
            m_AppendixGuids = new List<string>();
        var appendixGuid = report.summary.guid.ToString();
        m_AppendixGuids.Add(appendixGuid);
        return MobileHelper.LoadMobileAppendix(appendixGuid);
    }
}
