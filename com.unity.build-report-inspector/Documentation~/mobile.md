# Mobile

The mobile appendix was introduced, starting with Unity 2019.3, to report additional data for mobile builds.  

When installed, the BuildReportInspector package includes additional mobile-specific entries into the BuildReport, such as architectures inside the build, App Store download sizes and the list of files inside the application bundle (.apk, .obb, .aab for Android and .ipa for iOS/tvOS). 

<img src="images/MobileAppendix.png" width="400">

## Android
When this package is installed the mobile appendix is generated automatically for Android builds using build callbacks, immediately after Unity exports the application bundle.

This appendix information is saved in a file in the "Assets/BuildReports/Mobile" directory named after the build's unique GUID.

## iOS
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
