Build Report Inspector for Unity
================================

This package contains an Editor script which implements an inspector for the BuildReport class added in Unity 18.1.  
The BuildReport class lets you access information about your last build, and helps you profile the time spent building your project and the builds disk size footprint. This information may help you improving your build times and build sizes.  
This script allows you to inspect this information graphically in the Editor UI, making it more easily accessible than the script APIs would.  


Disclaimer
==========

**This package is provided as-is, with no support from Unity Technologies.** We plan to add a built-in and supported UI for the BuildReport feature in a future version of Unity, but until then, this package serves as a demonstration on how you can access the BuildReport information today.

In particular, this package gets the information it can from the BuildReport Scripting API (https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.html).  


Usage
=====

Add the [BuildReportInspector.cs](com.unity.build-report-inspector/Editor/BuildReportInspector.cs) editor script to an **Editor** folder in your project to install it.  
Alternatively, install it from Unity Editor's Package Manager window (check "Show preview packages" in the "Advanced" menu to have "Build Report Inspector" displayed in the list of available packages).  
Once installed, BuildReport objects can be viewed in the inspector.

You can obtain a BuildReport object as the return value of the BuildPlayer API (https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildPlayer.html) when making a build, or by selecting a file containing BuildReport data.  
Unity's default build setup will write such a file to **Library/LastBuild.buildreport** (this may change in the future) when making a build. 
This script adds a convenient menu shortcut (_Window/Open Last Build Report_), to copy that file to the **Assets** folder and select it, so you can inspect it using the Build Report Inspector.

Once open in the inspector, you can chose what data to view using the popup menu at the top of the window. The Build Report Inspector can show the following data:

### Build steps
The different steps involved in making you build, how long they took, and what messages were printed during those steps (if any).  

<img src="com.unity.build-report-inspector/Documentation~/images/BuildSteps.png" width="600">

### Source assets
A list of all assets which are used in the build, and how much they contribute to your build size  

![SourceAssets](com.unity.build-report-inspector/Documentation~/images/SourceAssets.png)

### Output files
A list of all files written by the build  

![OutputFiles](com.unity.build-report-inspector/Documentation~/images/OutputFiles.png)

### Stripping
For platforms which support engine code stripping, a list of all engine modules added to the build, and what caused them to be included in the build.  

![Stripping](com.unity.build-report-inspector/Documentation~/images/Stripping.png)

### Scenes using Assets
[Available from Unity 2020.1.0a6]  
When BuildOptions.DetailedBuildReport is passed to [BuildPipeline.BuildPlayer](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildPlayer.html), a list describing which scenes are using each asset of the build, is provided in the BuildReport.

<img src="com.unity.build-report-inspector/Documentation~/images/ScenesUsingAssets.png" width="400">

### Mobile
[Available from Unity 2019.3]  
The BuildReport API is not very good at reporting data from mobile builds. For this reason, starting at Unity 2019.3, mobile appendix was added to the BuildReportInspector. The mobile appendix expands the BuildReportInspector UI by adding mobile-specific entries, such as architectures inside the build, app store download sizes and the list of files inside the application bundle (.apk, .obb, .aab for Android and .ipa for iOS/tvOS).

<img src="com.unity.build-report-inspector/Documentation~/images/MobileAppendix.png" width="400">

#### Android
The mobile appendix is generated automatically for Android builds, right after Unity exports the application bundle.  

#### iOS
Because Unity does not export .ipa bundles directly, they need to be generated manually by the user. When an iOS build report is opened in Unity, the BuildReportInspector UI will display a prompt to open an .ipa bundle for more detailed information about the build, as shown in the image below.

<img src="com.unity.build-report-inspector/Documentation~/images/MobileiOSPrompt.png" width="400">

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
