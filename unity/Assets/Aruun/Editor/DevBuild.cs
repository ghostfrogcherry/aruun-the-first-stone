using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Aruun.EditorTools
{
    /// <summary>
    /// Builds a development player from the command line, so the game can be run and
    /// screenshotted without anyone pressing buttons in the editor:
    ///
    ///   Unity -batchmode -nographics -projectPath unity -executeMethod Aruun.EditorTools.DevBuild.Linux -quit
    ///
    /// The player goes to unity/Builds/linux/Aruun. The repo's content/ folder is copied into
    /// StreamingAssets for the build and removed again afterwards.
    /// </summary>
    public static class DevBuild
    {
        [MenuItem("Aruun/Build Linux dev player")]
        public static void Linux() { Build(BuildTarget.StandaloneLinux64, "linux/Aruun"); }

        [MenuItem("Aruun/Build Windows dev player")]
        public static void Windows() { Build(BuildTarget.StandaloneWindows64, "windows/Aruun.exe"); }

        static void Build(BuildTarget target, string relative)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string output = Path.Combine(project, "Builds", relative);
            string content = Path.GetFullPath(Path.Combine(project, "..", "content"));
            string streaming = Path.Combine(Application.dataPath, "StreamingAssets");
            string copied = Path.Combine(streaming, "content");
            bool madeStreaming = !Directory.Exists(streaming);

            // Builds need at least one scene. Make an empty one if there is none: Bootstrap
            // builds the whole game from code.
            string scene = "Assets/Aruun/Boot.unity";
            if (!File.Exists(Path.Combine(project, scene)))
            {
                var s = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s, scene);
            }

            try
            {
                CopyDirectory(content, copied);
                AssetDatabase.Refresh();
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { scene },
                    locationPathName = output,
                    target = target,
                    options = BuildOptions.Development,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log("Aruun build " + report.summary.result + ": " + output +
                          " (" + report.summary.totalErrors + " errors, " + report.summary.totalWarnings + " warnings)");
                if (report.summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
            }
            finally
            {
                if (Directory.Exists(copied)) Directory.Delete(copied, true);
                File.Delete(copied + ".meta");
                if (madeStreaming && Directory.Exists(streaming))
                {
                    Directory.Delete(streaming, true);
                    File.Delete(streaming + ".meta");
                }
                AssetDatabase.Refresh();
            }
        }

        static void CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            foreach (string dir in Directory.GetDirectories(from))
                CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)));
        }
    }
}
