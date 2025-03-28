# About Build Report Inspector

This package contains an Editor script which implements an inspector for BuildReport files.

The BuildReport file is generated for Player builds ( [BuildPipeline.BuildPlayer](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildPlayer.html)), as well as [BuildPipeline.BuildAssetBundles](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildPlayer.html). 

This file records information about your last build, and helps you profile the time spent building your project and to understand the disk size footprint of the build output.

This package adds UI support for inspecting this information graphically in the Unity Editor Inspector view.  

Note: The Addressables and Scriptable Build Pipeline packages do not generate a BuildReport file, but Addressables has its own UI for showing build results.

## Alternatives to using this package

Another way to view the build report is using the [Project Auditor package](https://docs.unity3d.com/Packages/com.unity.project-auditor@1.0/manual/build-view-reference.html).

And you can also write your own custom script to access data about your builds, using the [BuildReport](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.html) scripting API


## Preview package
This package is available as a preview.

**This package is provided as-is, with no support from Unity Technologies.** 

It serves as a demonstration of the information available in the BuildReport file.  It can be a useful tool "as-is" and continues to be functional in recent versions of Unity, for example Unity 6.

## Package contents

The following table describes the package folder structure:

|**Location**|**Description**|
|---|---|
|*Editor*|Contains the package scripts.|

<a name="Installation"></a>

## Installation

To install this package, follow the instructions in the [Package Manager documentation](https://docs.unity3d.com/Manual/upm-ui-install.html).

A recommended way is to install the package from github:

Clone this [repository](git@github.com:Unity-Technologies/BuildReportInspector.git). Alternatively download it as a zip file and expand it to a location on your local hard drive.  Typically it is best to use the "main" branch which has the latest recommended version.

In the Unity Package Manager Window select "Add package from disk" and select the `package.json` file inside the `com.unity.build-report-inspector` folder in your copy of this project.

Once the package is added the custom view will appear any time you use the Inspector to view a BuildReport file.
  
This script adds a convenient menu shortcut (_Window/Open Last Build Report_), to copy that file to the **Assets** folder and select it, so you can inspect it using the Build Report Inspector.

## Requirements

This version of Build Report Inspector is compatible with the following versions of the Unity Editor:

* 2021.3 and later.  It may also be functional in 2019 and 2020 versions.

---

<a name="UsingBuildReportInspector"></a>
# Using Build Report Inspector

Unity will write the BuildReport to `Library/LastBuild.buildreport` when making a build.  This location is cannot be reached from the Project view, but the file can manually be copied somewhere inside the Assets folder to view it.

This package adds a menu item `Window/Open Last Build Report` which will take care of copying the last build report file to the Assets/BuildReports folder and select it so that it can be viewed in the Inspector.  The file will be renamed to include a time stamp so that you can have multiple build reports in the same folder.

Note: By default the `Library/LastBuild.buildreport` file is in binary serialization format.  But when copied into the Assets folder it will be converted to yaml text format (provided the Asset Serialization project setting is set to "Text").

The Inspector Window includes have several tabs, which are described in the following sections:

### Build steps
The different steps involved in making you build, how long they took, and what messages were printed during those steps (if any).  

<img src="images/BuildSteps.png" width="600">

The equivalent information is exposed through the API by [BuildReport.steps](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport-steps.html).

Note: when [BuildOptions.DetailedBuildReport](https://docs.unity3d.com/ScriptReference/BuildOptions.DetailedBuildReport.html) is used then the build steps will include much more information. The amount of build step data generated by this flag is not appropriate for large builds, but can be useful when diagnosing issues with smaller player builds.

### Content Summary
A summary of the content of the build, including some overall statistics and then sizes and object counts aggregated by Type and Source Asset.

Note: **The sizes reported are prior to any compression**.  If you build AssetBundles with compression, or use the BuildOptions.CompressWithLz4HC flag for a Player build, then the size on disk can be smaller than what is reported on this tab.  Even when compression is not used, extra padding bytes between objects and resource blobs can mean that the actual size of disk is different from the sum of the sizes of the individual objects.

This information is calculated from the same PackedAsset information from the BuildReport that is used to populate the SourceAssets tab. But this page focuses on summarizing key information.

The data shown in this view is only calculated on-demand, because the PackedAsset information can grow very large for large builds.

![ContentSummary](images/ContentSummary.png)

Here is another example, from a much larger build:

![ContentSummary](images/ContentSummary-largeBuild.png)

The following section explains some details about what each statistic means:

**Serialized File Size** - This is the sum of the size of all the Serialized Files in the build output.  Serialized Files refers the binary file format that Unity users to store Unity Objects.  In the build output this is the format used for scenes (e.g. level files) and sharedAssets.  AssetBundles also include Serialized Files, which have a name like CAB-<hash> and no file extension.  

**Serialized File Headers** - This is the sum of the header sections of all the Serialized Files.  The header is extra information at the start of the file, prior to the actual serialized Objects.  The size of this data relative to the total serialized file size is something to keep an eye on, especially if you split your Player content into many scenes, or your AssetBundle into many AssetBundles.  In some cases it can become a significant overhead.  The header is where TypeTrees are stored.  By default TypeTrees are excluded from Player builds, but they are included in AssetBundles unless you specify the flag  [BuildAssetBundleOptions.DisableWriteTypeTree](https://docs.unity3d.com/ScriptReference/BuildAssetBundleOptions.DisableWriteTypeTree.html).

**Resource File Count** - "Resource" is rather an overloaded term in Unity.  But in this context it is the count of all .resS or .resource files in the build output.  There are companion files to the Serialized Files, containing Texture, Mesh, Audio and Video data as blobs.  These files do not have a header.

**Object Count** - This is the count of all objects in the build output.  Note: This does not include objects inside Scene files (because they are not currently included in the PackedAsset output).

**Object Type Count** - This is the count of different Unity Object types in the build output.  See [Unity Object Types](https://docs.unity3d.com/Manual/ClassIDReference.html) for a list of all the different types.  Note: this count does not include different Scripting Types (e.g. different classes derived from MonoBehaviour or ScriptableObject).  All such objects in the build output are classified as "MonoBehaviour".  

**Source Asset Count** - This is the total number of Assets in the project that contributed objects to the build output.  

At the bottom of the tab there are foldouts that show the total list of different types and the list of source assets.  For each element the total size is shown - this is the sum of the size of the objects and resource blobs (.resS or .resource content) attributed to that Type or Source Asset.  

Note: in the case of AssetBundles the objects from a source asset can get duplicated into multiple bundles.  This will result in a larger size in the build output.  So studying the size information recorded on this tab can be useful to get a sense of the impact of any duplication, especially for larger Assets.


### Source assets

This page displays information about the objects and resources (audio clips, meshes and textures) and how they contribute to the build size.   It shows details than the Content Summary tab.

Warning: Objects inside Scene files are currently not reported in the BuildReport (e.g. no PackedAssets are generated for level files).

![SourceAssets](images/SourceAssets.png)

There are three available views:

**Size** shows the objects grouped by their source asset and Type.  If you hover over the entry the number of objects of the type inside the Asset is shown.  Note: The same Asset can show up multiple times, because may contain multiple types of objects.  And in the case of a Texture there will be an entry for the Texture object and another for the texture data (from the .resS file).

**Output Data Files** This show information about the content of the output files, based on the source asset and type.  Hover over the entry to see the number of objects (or resources).

**Importer Type** This shows information about grouped by object type.  This is useful to see the size of all objects and resources of a particular type, and which Assets contributed them.

For the API equivalent see [PackedAssets](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/Build.Reporting.PackedAssets.html).

In the case of AssetBundle builds the AssetBundle name is shown instead of the internal archive filename when you sort by Output Data File.

![](images/sourceassets-assetbundle.png)

Warning: This view aggregates information about every single object in the build.  Currently this view is so slow that it is unusable for large builds (e.g. large numbers of Assets or prefabs with large GameObject hierarchies).

**Export to CSV**

See [Exporting and Analysis](./exporting-and-analysis.md) for more details.

### Output files
A list of all files written by the build  

![OutputFiles](images/OutputFiles.png)


An example for an AssetBundle build:
![OutputFiles](images/OutputFiles_AssetBundle.png)

Note: in the case of AssetBundles Unity will report the contents of the Archive file in the file list.  E.g. these files are inside the AssetBundle and not visible in the file system.  In the case of a compressed Player build Unity does not currently report the files inside the data.unity3d archive.  In both cases, the files inside the archive can be extracted with the WebExtract tool if you want to examine them.

For the API equivalent see [BuildReport.GetFiles](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.GetFiles.html).

### Duplicated Assets

This tab is only shown for AssetBundle builds (because Player builds automatically avoid duplicating content).

With AssetBundles the layout of Assets into bundles is up to the users control.  In practice it is quite easy to accidentally duplicate data because assets that are not explicitly assigned to a bundle can be copied inside multiple bundled, if those assets are referenced from multiple bundles.

This tab reports the overall size and percentage of duplication, and makes it possible to see the worst offenders (by overall size).  Solving duplication is typically a question of adding additional AssetBundles with the Assets that are listed, or rearranging Assets between AssetBundles, so that they can share content inside the same AssetBundle.

This screen shot shows an example for a simple scenario that is included in the TestProject.  You can try it out yourself, but here is a quick summary.

There are three scenes that all reference the same ImageList.asset.  The ImageList.asset in turn references two textures.

When the scenes are each assigned to their own AssetBundle the ImageList and textures are repeated 3 times in the output build.

When viewing the output in the Source Assets tab it may not be immediate evident that something is wrong:

![DuplicateAssets](images/assetbundle-bad-layout.png)

But the Duplicate Asset tab reports that the same Assets are appearing in more than one AssetBundle.

![DuplicateAssets](images/assetbundle-duplication-tab.png)

In this case the duplication can fully resolved, by assigning the ImageList.asset to its own bundle.  This automatically solves the problem of the repeated Textures because they are only referenced from that asset.

![DuplicateAssets](images/assetbundle-duplication-tab-noduplicates.png)

The details of the new layout can be viewed on the Source Assets tab:

![DuplicateAssets](images/assetbundle-fixed-layout.png)

Note: Another way to resolve the duplicates in this case is to put all three scenes into the same AssetBundle.

Note: sometimes some degree of duplication is desirable in AssetBundles. For small Assets that are not used universally then some degree of repeats can be more efficient than having many tiny AssetBundles.  It can also be a way to avoid dependencies between AssetBundles, when working on optimizing distribution and download patterns.

### Stripping
For platforms which support engine code stripping, a list of all engine modules added to the build, and what caused them to be included in the build.  

![Stripping](images/Stripping.png)

For the API equivalent see [StrippingInfo](https://docs.unity3d.com/ScriptReference/Build.Reporting.StrippingInfo.html).

This tab is only shown for Player builds because it is not relevant for AssetBundle builds.

### Scenes using Assets
[Available from Unity 2020.1.0a6]

This tab is only populated when you use the [BuildOptions.DetailedBuildReport](https://docs.unity3d.com/ScriptReference/BuildOptions.DetailedBuildReport.html) build option when calling [BuildPipeline.BuildPlayer](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildPlayer.html) in a custom build script.  It is not available for AssetBundle builds.

This shows a list describing which scenes are using each asset of the build.

<img src="images/ScenesUsingAssets.png" width="400">

For the API equivalent see [ScenesUsingAssets](https://docs.unity3d.com/ScriptReference/Build.Reporting.ScenesUsingAssets.html).

### Mobile

The mobile appendix was introduced, starting with Unity 2019.3, to report additional data for mobile builds.  

When present, the BuildReportInspector UI includes additional mobile-specific entries, such as architectures inside the build, App Store download sizes and the list of files inside the application bundle (.apk, .obb, .aab for Android and .ipa for iOS/tvOS). 

<img src="images/MobileAppendix.png" width="400">

#### Android
The mobile appendix is generated automatically for Android builds using build callbacks, immediately after Unity exports the application bundle.

This appendix information is saved in a file in the "Assets/BuildReports/Mobile" directory named after the build's unique GUID.

#### iOS
Because Unity does not export .ipa bundles directly, they need to be generated manually by the user. When an iOS build report is opened in Unity, the BuildReportInspector UI will display a prompt to open an .ipa bundle for more detailed information about the build, as shown in the image below.

<img src="images/MobileiOSPrompt.png" width="400">

To generate a development .ipa bundle:

1. Open the Xcode project exported by Unity.
2. In the menu bar, go to `Product > Archive`.
3. Once Xcode finishes archiving, click `Distribute App`.
4. Select `Development` distribution method, go to next step.
5. Select desired App Thinning and Bitcode options, go to next step.
6. Set valid signing credentials and click `Next`.
7. Once Xcode finishes distributing, click `Export` and select where to save the distributed files.

Once these steps are complete, an .ipa bundle will be inside the directory, saved in step 7.  
This process can also be automated using the `xcodebuild` command line tool.  
After the .ipa bundle is provided, the iOS-specific information is added to the BuildReportInspector UI automatically.

---

# Contributing

The source for Build Report Inspector package is available at https://github.com/Unity-Technologies/BuildReportInspector  
For contributions, please refer to the repository's [CONTRIBUTING.md](https://github.com/Unity-Technologies/BuildReportInspector/blob/master/com.unity.build-report-inspector/CONTRIBUTING.md) file.
