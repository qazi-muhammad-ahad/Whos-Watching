using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;

namespace WhosWatching.Editor
{
    public static class ProjectSetup
    {
        const string Root = "Assets/_WhosWatching/";
        // Explicit command only: never rebuild a teammate's scene automatically on import.
        [MenuItem("Who's Watching/Create initial LAN scene")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Root + "Scenes/Main.unity") != null)
                throw new System.InvalidOperationException("Main already exists. Preserve it; setup is only for initial creation.");
            PlayerSettings.productName = "Who's Watching?";
            PlayerSettings.companyName = "WhosWatching";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            Time.fixedDeltaTime = 1f / 30;
            var time = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0]);
            time.FindProperty("Fixed Timestep").floatValue = 1f / 30; time.ApplyModifiedProperties();
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            tags.FindProperty("layers").GetArrayElementAtIndex(8).stringValue = "Player";
            tags.ApplyModifiedProperties(); Physics.IgnoreLayerCollision(8, 8, true);

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = new Color(0.15f, 0.65f, 0.9f);
            AssetDatabase.CreateAsset(material, Root + "Materials/Player.mat");
            var root = new GameObject("NetworkPlayer"); root.layer = 8;
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            var controller = root.AddComponent<CharacterController>(); controller.height = 2; controller.radius = 0.4f;
            var player = root.AddComponent<NetworkPlayer>();
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule); visual.name = "VisualRoot";
            visual.transform.SetParent(root.transform, false); Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = material; player.VisualRoot = visual.transform;
            var eye = new GameObject("EyeAnchor"); eye.transform.SetParent(root.transform, false); eye.transform.localPosition = Vector3.up * 0.65f;
            var chest = new GameObject("ChestAnchor"); chest.transform.SetParent(root.transform, false); chest.transform.localPosition = Vector3.up * 0.3f;
            var camera = new GameObject("OwnerCamera"); camera.transform.SetParent(eye.transform, false);
            player.View = camera.AddComponent<Camera>(); player.View.nearClipPlane = 0.05f;
            camera.AddComponent<AudioListener>(); camera.AddComponent<UniversalAdditionalCameraData>(); camera.SetActive(false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "Prefabs/Player/NetworkPlayer.prefab");
            Object.DestroyImmediate(root);
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            list.Add(new NetworkPrefab { Prefab = prefab });
            AssetDatabase.CreateAsset(list, Root + "Settings/NetworkPrefabs.asset");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Cube("Floor", new Vector3(0,-0.5f,0), new Vector3(24,1,24));
            Cube("Cover", new Vector3(0,1,4), new Vector3(5,2,1));
            Cube("North", new Vector3(0,1.5f,12), new Vector3(24,3,1));
            Cube("South", new Vector3(0,1.5f,-12), new Vector3(24,3,1));
            Cube("East", new Vector3(12,1.5f,0), new Vector3(1,3,24));
            Cube("West", new Vector3(-12,1.5f,0), new Vector3(1,3,24));
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(50,-30,0);
            var menu = new GameObject("MenuCamera").AddComponent<Camera>();
            menu.transform.position = new Vector3(0,16,-14); menu.transform.rotation = Quaternion.Euler(45,0,0);
            menu.gameObject.AddComponent<AudioListener>(); menu.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var networkRoot = new GameObject("Networking");
            var transport = networkRoot.AddComponent<UnityTransport>();
            var manager = networkRoot.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, PlayerPrefab = prefab, ConnectionApproval = true, TickRate = 30, ClientConnectionBufferTimeout = 10 };
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            var session = networkRoot.AddComponent<NetworkGameManager>(); session.Network = manager; session.MenuCamera = menu;
            EditorSceneManager.SaveScene(scene, Root + "Scenes/Main.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Root + "Scenes/Main.unity", true) };
            AssetDatabase.SaveAssets();
        }
        static void Cube(string name, Vector3 position, Vector3 scale)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
            obj.transform.position = position; obj.transform.localScale = scale;
        }
        [MenuItem("Who's Watching/Build Standalone Windows (.exe)")]
        public static void Build()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { Root + "Scenes/Arena.unity" },
                locationPathName = "Builds/Windows/WhosWatching.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception("Windows build failed: " + report.summary.result);

            Debug.Log("<color=#00FFCC><b>[Who's Watching]</b> Windows .exe built successfully at: Builds/Windows/WhosWatching.exe</color>");
            EditorUtility.RevealInFinder("Builds/Windows/WhosWatching.exe");
        }
        public static void InstallGaze()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layer = tags.FindProperty("layers").GetArrayElementAtIndex(9);
            if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != "GazeTarget")
                throw new System.InvalidOperationException("Layer 9 is occupied; choose a free gaze layer before migration.");
            layer.stringValue = "GazeTarget"; tags.ApplyModifiedProperties();
            for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(9,i,true);
            string prefabPath = Root + "Prefabs/Player/NetworkPlayer.prefab";
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var body = contents.transform.Find("GazeBody");
                if (!body)
                {
                    body = new GameObject("GazeBody").transform; body.SetParent(contents.transform,false);
                    var collider = body.gameObject.AddComponent<CapsuleCollider>(); collider.height = 2; collider.radius = 0.4f;
                }
                body.gameObject.layer = 9;
                PrefabUtility.SaveAsPrefabAsset(contents,prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var scene = EditorSceneManager.OpenScene(Root + "Scenes/Main.unity");
            var session = Object.FindFirstObjectByType<NetworkGameManager>();
            var gaze = session.GetComponent<GazeSystem>();
            if (!gaze) gaze = session.gameObject.AddComponent<GazeSystem>();
            var config = AssetDatabase.LoadAssetAtPath<GazeConfig>(Root + "Settings/GazeConfig.asset");
            if (!config) { config = ScriptableObject.CreateInstance<GazeConfig>(); AssetDatabase.CreateAsset(config, Root + "Settings/GazeConfig.asset"); }
            gaze.Config = config; session.Gaze = gaze;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
    }
}
