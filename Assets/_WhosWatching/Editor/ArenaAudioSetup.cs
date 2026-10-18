using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WhosWatching.Editor
{
    public static class ArenaAudioSetup
    {
        [MenuItem("Who's Watching/Install Arena audio slots")]
        public static void Install()
        {
            if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/_WhosWatching/Scenes/Arena.unity");
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "Arena" || EditorApplication.isPlaying) throw new System.InvalidOperationException("Open Arena in edit mode first.");
            var audio = Object.FindFirstObjectByType<ArenaAudio>(FindObjectsInactive.Include);
            if (!audio)
            {
                var root = new GameObject("Arena Audio"); Undo.RegisterCreatedObjectUndo(root, "Add Arena audio");
                audio = Undo.AddComponent<ArenaAudio>(root);
            }
            Selection.activeGameObject = audio.gameObject;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("WW_AUDIO_INSTALLED: select Arena Audio and drag clips into the Inspector. Existing assignments preserved.");
        }
        // Repair only incorrect root ownership; keep meshes, colliders and all other overrides.
        public static void RepairVariantOwnership()
        {
            int repaired = 0;
            foreach (var team in new[] { TeamId.A, TeamId.B })
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_WhosWatching/Prefabs/TeamReady/Team" + team }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab.GetComponent<TeamTint>().Team == team) continue;
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try {
                        var tint = root.GetComponent<TeamTint>(); tint.Team = team;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(tint);
                        PrefabUtility.SaveAsPrefabAsset(root, path); repaired++;
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
            AssetDatabase.SaveAssets(); Debug.Log("WW_TEAM_OWNERSHIP_REPAIRS " + repaired);
        }
        public static void Prepare() { RepairVariantOwnership(); Install(); }
    }
}
