# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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
