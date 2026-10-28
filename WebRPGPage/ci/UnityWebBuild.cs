// 檢查 Unity 與 WebGL 建置設定，產生供持續整合部署的遊戲檔案。
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WebRPG.ContinuousIntegration
{
    public static class UnityWebBuild
    {
        public static void Build()
        {
            if (Application.unityVersion != "6000.3.11f1")
            {
                throw new InvalidOperationException("Unity 6000.3.11f1 is required.");
            }

            if (PlayerSettings.WebGL.compressionFormat != WebGLCompressionFormat.Disabled ||
                PlayerSettings.WebGL.decompressionFallback)
            {
                throw new InvalidOperationException("Hosting requires uncompressed Web output without fallback.");
            }

            string[] scenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenePaths.Length == 0)
            {
                throw new InvalidOperationException("At least one enabled build scene is required.");
            }

            string originalTemplate = PlayerSettings.WebGL.template;

            try
            {
                PlayerSettings.WebGL.template = "PROJECT:WebRPGContinuousIntegration";
                BuildReport buildReport = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenePaths,
                    locationPathName = Path.GetFullPath(
                        Path.Combine(Application.dataPath, "../../artifacts/unity-web")),
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });

                if (buildReport.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Unity Web build failed: {buildReport.summary.result}.");
                }
            }
            finally
            {
                PlayerSettings.WebGL.template = originalTemplate;
            }
        }
    }
}
