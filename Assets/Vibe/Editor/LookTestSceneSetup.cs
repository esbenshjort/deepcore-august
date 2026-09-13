using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepCore.Vibe.Editor
{
    public static class LookTestSceneSetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("DeepCore Vibe/Setup Look Test Scene")]
        public static void SetupFromMenu()
        {
            Setup();
        }

        public static void SetupFromCommandLine()
        {
            Setup();
            EditorApplication.Exit(0);
        }

        static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Remove template global lights / clutter that fight the mood
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name.Contains("Global Light") || root.name == "Light 2D")
                    Object.DestroyImmediate(root);
            }

            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.backgroundColor = Color.black;
                cam.clearFlags = CameraClearFlags.SolidColor;
            }

            var existing = Object.FindAnyObjectByType<LookTestBootstrap>();
            if (existing == null)
            {
                var go = new GameObject("LookTestBootstrap");
                go.AddComponent<LookTestBootstrap>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            EditorBuildSettings.scenes = scenes;

            Debug.Log("[Vibe] Look test scene ready. Open SampleScene and press Play.");
        }
    }
}
