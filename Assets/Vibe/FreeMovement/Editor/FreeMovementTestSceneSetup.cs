using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepCore.FreeMovement.Editor
{
    public static class FreeMovementTestSceneSetup
    {
        const string ScenePath = "Assets/Scenes/FreeMovement_12x12TerrainTest.unity";

        [MenuItem("DeepCore Vibe/Create FreeMovement_12x12TerrainTest Scene")]
        public static void CreateFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[FreeMovement] Stop Play Mode before creating a scene.");
                return;
            }
            Create();
        }

        static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 7.5f;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(4f, 3f, -10f);
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            light.intensity = 0.85f;

            var runner = new GameObject("FreeMovementTestRunner");
            runner.AddComponent<FreeMovementTestRunner>();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            foreach (var s in scenes)
                if (s.path == ScenePath) { found = true; break; }
            if (!found)
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.Refresh();
            Debug.Log($"[FreeMovement] Scene ready: {ScenePath}. Open and Play.");
        }

        const string ScaleCompareScenePath = "Assets/Scenes/FreeMovement_ScaleCompare.unity";

        [MenuItem("DeepCore Vibe/Create FreeMovement_ScaleCompare Scene")]
        public static void CreateScaleCompareFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ScaleCompare] Stop Play Mode before creating a scene.");
                return;
            }
            CreateScaleCompare();
        }

        static void CreateScaleCompare()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 8.5f;
            cam.backgroundColor = new Color(0.015f, 0.015f, 0.02f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(22f, 3.2f, -10f);
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            light.intensity = 0.16f;
            light.color = new Color(0.5f, 0.58f, 0.75f);

            var runner = new GameObject("FreeMovementScaleCompareRunner");
            runner.AddComponent<FreeMovementScaleCompareRunner>();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, ScaleCompareScenePath);

            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            foreach (var s in scenes)
                if (s.path == ScaleCompareScenePath) { found = true; break; }
            if (!found)
                scenes.Add(new EditorBuildSettingsScene(ScaleCompareScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.Refresh();
            Debug.Log($"[ScaleCompare] Scene ready: {ScaleCompareScenePath}. Open and Play.");
        }

        const string GoldHuntScenePath = "Assets/Scenes/FreeMovement_GoldHunt.unity";

        [MenuItem("DeepCore Vibe/Create FreeMovement_GoldHunt Scene")]
        public static void CreateGoldHuntFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[GoldHunt] Stop Play Mode before creating a scene.");
                return;
            }
            CreateGoldHunt();
        }

        static void CreateGoldHunt()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 5.2f;
            cam.backgroundColor = new Color(0.012f, 0.012f, 0.018f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(3.6f, 1.4f, -10f);
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            light.intensity = 0.1f;
            light.color = new Color(0.45f, 0.52f, 0.7f);

            var runner = new GameObject("FreeMovementGoldHuntRunner");
            runner.AddComponent<FreeMovementGoldHuntRunner>();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, GoldHuntScenePath);

            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            foreach (var s in scenes)
                if (s.path == GoldHuntScenePath) { found = true; break; }
            if (!found)
                scenes.Add(new EditorBuildSettingsScene(GoldHuntScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.Refresh();
            Debug.Log($"[GoldHunt] Scene ready: {GoldHuntScenePath}. Open and Play.");
        }

        const string SocketMapScenePath = "Assets/Scenes/FreeMovement_SocketMap.unity";

        [MenuItem("DeepCore Vibe/Create FreeMovement_SocketMap Scene")]
        public static void CreateSocketMapFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SocketMap] Stop Play Mode before creating a scene.");
                return;
            }
            CreateSocketMap();
        }

        static void CreateSocketMap()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 7.5f;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.014f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(10f, 2.2f, -10f);
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            light.intensity = 0.025f;
            light.color = new Color(0.35f, 0.4f, 0.55f);

            var runner = new GameObject("FreeMovementSocketMapRunner");
            runner.AddComponent<FreeMovementSocketMapRunner>();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, SocketMapScenePath);

            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            foreach (var s in scenes)
                if (s.path == SocketMapScenePath) { found = true; break; }
            if (!found)
                scenes.Add(new EditorBuildSettingsScene(SocketMapScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.Refresh();
            Debug.Log($"[SocketMap] Scene ready: {SocketMapScenePath}. Open and Play.");
        }

        const string BalanceCompareScenePath = "Assets/Scenes/FreeMovement_BalanceCompare.unity";

        [MenuItem("DeepCore Vibe/Create FreeMovement_BalanceCompare Scene")]
        public static void CreateBalanceCompareFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[BalanceCompare] Stop Play Mode before creating a scene.");
                return;
            }
            CreateBalanceCompare();
        }

        static void CreateBalanceCompare()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 9.5f;
            cam.backgroundColor = new Color(0.01f, 0.012f, 0.018f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(5.5f, 3.2f, -10f);
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            light.intensity = 0.08f;
            light.color = new Color(0.45f, 0.55f, 0.7f);

            var runner = new GameObject("ExcavatorBalanceCompareRunner");
            runner.AddComponent<ExcavatorBalanceCompareRunner>();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, BalanceCompareScenePath);

            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            foreach (var s in scenes)
                if (s.path == BalanceCompareScenePath) { found = true; break; }
            if (!found)
                scenes.Add(new EditorBuildSettingsScene(BalanceCompareScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.Refresh();
            Debug.Log($"[BalanceCompare] Scene ready: {BalanceCompareScenePath}. Open and Play.");
        }
    }
}
