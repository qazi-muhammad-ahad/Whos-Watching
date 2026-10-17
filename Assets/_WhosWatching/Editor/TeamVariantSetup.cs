using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WhosWatching.Editor
{
    public static class TeamVariantSetup
    {
        const string Root="Assets/_WhosWatching/Prefabs/TeamReady";
        [MenuItem("Who's Watching/Generate Team A and B environment variants")]
        public static void Install()
        {
            Directory.CreateDirectory(Root+"/TeamA"); Directory.CreateDirectory(Root+"/TeamB"); AssetDatabase.Refresh();
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{Root}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>Path.GetDirectoryName(p).Replace('\\','/')==Root).ToArray();
            foreach(var path in paths)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach(var team in new[]{TeamId.A,TeamId.B})
                {
                    string output=$"{Root}/Team{team}/{prefab.name}_Team{team}.prefab";
                    var existing=AssetDatabase.LoadAssetAtPath<GameObject>(output);
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(existing ? existing : prefab);
                    try
                    {
                        instance.name=$"{prefab.name}_Team{team}";
                        foreach(var tint in instance.GetComponentsInChildren<TeamTint>(true))
                        {
                            tint.Team=team; tint.Apply();
                            PrefabUtility.RecordPrefabInstancePropertyModifications(tint);
                        }
                        PrefabUtility.SaveAsPrefabAsset(instance,output);
                    }
                    finally { Object.DestroyImmediate(instance); }
                }
            }
            AssetDatabase.SaveAssets(); Debug.Log($"WW_TEAM_VARIANTS {paths.Length*2} linked variants generated. Main scene untouched.");
        }
    }
}
