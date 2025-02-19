Build Report Inspector for Unity
================================

This package contains an Editor script which implements an inspector for BuildReport files.

The BuildReport file is generated for Player builds ( [BuildPipeline.BuildPlayer](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildPlayer.html)), as well as [BuildPipeline.BuildAssetBundles](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildPlayer.html). 

This file records information about your last build, and helps you profile the time spent building your project and to understand the disk size footprint of the build output.

This package adds UI support for inspecting this information graphically in the Unity Editor Inspector view.  

Note: The Addressables and Scriptable Build Pipeline packages do not generate a BuildReport file, but Addressables has its own UI for showing build results.

Installation and Usage
======================

For documentation see [Build Report Inspector documentation](Documentation~/com.unity.build-report-inspector.md).
