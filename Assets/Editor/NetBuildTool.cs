using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// macOS 테스트 빌드 생성. AutoPilot menu.request("Game/빌드 (macOS)")로 원격 구동 가능.
/// 산출물: <프로젝트>/Builds/Preternatural.app
public static class NetBuildTool
{
    [MenuItem("Game/빌드 (macOS)")]
    public static void BuildMac()
    {
        var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, "Preternatural.app");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/01.Scenes/Homescreen.unity", "Assets/01.Scenes/GameScene.unity" },
            locationPathName = outPath,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[NetBuild] result={summary.result} size={summary.totalSize / 1048576}MB " +
            $"errors={summary.totalErrors} output={outPath}");
    }
}
