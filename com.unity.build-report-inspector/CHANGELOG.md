# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.4.4-preview] - 2025-03-18

### Added
- Use AssetBundle name instead of internal file name in SourceAssets tab when sorting by output file path
- CSV format exports both AssetBundle file name and internal name.  Issue #43
- New column in CSV format for file extension of source path (e.g. ".unity", ".jpg").  Useful for analyzing by file type.
- Ability to sort by file path on File tab

## [0.4.3-preview] - 2025-02-25

### Added
- Add ability to export SourceAsset data to CVS
- Expose the maximum SourceAsset row count in the UI to remove hard coded internal limit.

## [0.4.2-preview] - 2025-02-21

### Added
- Add Content Summary tab

### Fixed
- Fix bug when Asset Bundle / Player build type was not changing when switching selected BuildReport
- Fix for issue #21 - exception related to SpriteAltas path parsing.

## [0.4.1-preview] - 2025-02-18

### Added
- Show BuildType and Build output directory

### Fixed
- Fix SourceAsset performance, fold-out issue
- Fix menu item behavior when BuildReport was already copied into project
- Fix test project compilation.  Test project has menu item for building with "Detailed Build Report" option enabled.

### Deprecated
- Drop support for versions older than 2019.3.  Tested on the oldest version that is still LTS, 2021.3.

## [0.4.0-preview] - 2025-02-17

### Added
- Support for sorting on the OutputFiles tab #23

### Fix 
- Fix mismatched name warning #30

## [0.3.0-preview] - 2022-07-22

### Fix
- Fixed failure to estimate Android APK size due to mismatch in apkanalyzer version

## [0.2.2-preview] - 2020-05-07

### Added
Add improved mobile support, which includes:
- List the files inside the application bundle (.apk, .aab, .obb (Android), .ipa (iOS/tvOS)).
- Show architectures supported by the build.
- Show estimated App Store download size (iOS/tvOS/Android) per-architecture.

### Fixed
- Fix #1 - Assets tab not showing assets after 60+ entries.
- Fix BuildSteps entry text overflowing vertically.
- Fix blank entries in the Assets tab.
- Fix .NET 3.5 support on 2018.4.

## [0.1.2-preview] - 2019-12-18

*Remove warning messages reported in https://issuetracker.unity3d.com/issues/errors-regarding-missing-meta-files-when-installing-build-report-inspector-package*

## [0.1.1-preview] - 2019-12-18

*Add fix for https://issuetracker.unity3d.com/issues/errors-regarding-missing-meta-files-when-installing-build-report-inspector-package*

## [0.1.0] - 2019-10-10

### This is the first release of *Unity Package Build Report Inspector*.

*This is a preview release*.  
*It contains code from https://github.com/Unity-Technologies/BuildReportInspector,*  
*which is an update of https://assetstore.unity.com/packages/tools/utilities/build-report-inspector-for-unity-119923*
