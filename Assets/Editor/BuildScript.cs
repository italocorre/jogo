using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarAberto.EditorTools
{
    /// <summary>
    /// Gera a cena e o .exe. Usado pelo GitHub Actions (GameCI) e também pelo menu "Mar Aberto" no editor.
    /// O jogo é todo montado por código, então a cena só precisa existir.
    /// </summary>
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string ProductName = "Mar Aberto";

        [MenuItem("Mar Aberto/Gerar Cena")]
        public static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Neblina ligada na cena para o Unity manter as variantes de shader com neblina.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 180f;
            RenderSettings.fogEndDistance = 1400f;
            RenderSettings.ambientMode = AmbientMode.Trilight;

            // Uma luz direcional na cena garante as variantes de sombra (o jogo cria a sua própria).
            var lightGo = new GameObject("LuzDeReferencia");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[MarAberto] Cena criada em " + ScenePath);
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = "MarAberto";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            // Garante o sistema de input clássico (Input.GetKey) — "Both" também funciona.
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets != null && assets.Length > 0)
                {
                    var so = new SerializedObject(assets[0]);
                    var prop = so.FindProperty("activeInputHandler");
                    if (prop != null && prop.intValue == 1)
                    {
                        prop.intValue = 2;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MarAberto] Não foi possível ajustar o input: " + e.Message);
            }
        }

        [MenuItem("Mar Aberto/Gerar .exe")]
        public static void BuildFromMenu()
        {
            Build("Build/MarAberto/MarAberto.exe", false);
        }

        /// <summary>Chamado pelo GitHub Actions.</summary>
        public static void BuildWindows()
        {
            string path = GetArg("-customBuildPath");
            if (string.IsNullOrEmpty(path)) path = "build/StandaloneWindows64/MarAberto.exe";
            if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                path = Path.Combine(path, "MarAberto.exe");
            Build(path, true);
        }

        [MenuItem("Mar Aberto/Tirar prints")]
        public static void CapturePreviews()
        {
            ConfigurePlayer();
            try { Preview.CaptureAll("screens"); }
            catch (Exception e) { Debug.LogWarning("[MarAberto] Prints falharam: " + e); }
        }

        static void Build(string path, bool exitOnFinish)
        {
            ConfigurePlayer();
            if (exitOnFinish) CapturePreviews();
            CreateScene();
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("[MarAberto] Build: " + s.result + " — " + s.totalSize / (1024 * 1024) + " MB — " + path);
            if (exitOnFinish) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
            else if (s.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(path);
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
