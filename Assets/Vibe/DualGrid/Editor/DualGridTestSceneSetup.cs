using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepCore.DualGrid.Editor
{
    public static class DualGridTestSceneSetup
    {
        const string ScenePath = "Assets/Scenes/DualGridTest.unity";

        [MenuItem("DeepCore Vibe/Create DualGridTest Scene")]
        public static void CreateFromMenu()
        {
            Create();
        }

        public static void CreateFromCommandLine()
        {
            Create();
            EditorApplication.Exit(0);
        }

        static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 5.5f;
            cam.backgroundColor = new Color(0.05f, 0.055f, 0.06f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(4.5f, 3.2f, -10f);
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            light.intensity = 0.9f;

            var runner = new GameObject("DualGridTestRunner");
            runner.AddComponent<DualGridTestRunner>();

            // Ensure folder
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, ScenePath);

            // Add to build settings without removing SampleScene
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            foreach (var s in scenes)
            {
                if (s.path == ScenePath) { found = true; break; }
            }
            if (!found)
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.Refresh();
            Debug.Log($"[DualGrid] Scene created: {ScenePath}. Open it and press Play.");
        }
    }
}
