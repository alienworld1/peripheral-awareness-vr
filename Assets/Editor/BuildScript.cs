using UnityEngine;
using UnityEditor;
using System.IO;

public class BuildScript
{
    [MenuItem("Build/Build Android APK")]
    public static void BuildAPK()
    {
        string buildPath = Path.GetDirectoryName(Application.dataPath) + "/vrletter.apk";
        
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = new[] { "Assets/Scenes/SampleScene.unity" };
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.options = BuildOptions.None;
        
        Debug.Log("Building APK to: " + buildPath);
        
        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("Build succeeded: " + report.summary.totalSize + " bytes");
        }
        else
        {
            Debug.LogError("Build failed");
        }
    }
}
