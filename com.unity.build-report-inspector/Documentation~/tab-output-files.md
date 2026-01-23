# Output Files Tab

A list of all files written by the build  

![OutputFiles](images/OutputFiles.png)


An example for an AssetBundle build:
![OutputFiles](images/OutputFiles_AssetBundle.png)

Note: in the case of AssetBundles Unity will report the contents of the Archive file in the file list.  E.g. these files are inside the AssetBundle and not visible in the file system.  In the case of a compressed Player build Unity does not currently report the files inside the data.unity3d archive.  In both cases, the files inside the archive can be extracted with the WebExtract tool if you want to examine them.

For the API equivalent see [BuildReport.GetFiles](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.GetFiles.html).
