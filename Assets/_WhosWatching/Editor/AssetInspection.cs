using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WhosWatching.Editor
{
    public static class AssetInspection
    {
        public static void Run()
        {
            var report = new StringBuilder();
            foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/_WhosWatching/Prefabs/") && (p.EndsWith(".fbx") || p.EndsWith("Devil.prefab") || p.EndsWith("Ghost.prefab"))))
            {
                report.AppendLine(path);
                foreach (var material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>()) report.AppendLine($"  MATERIAL {material.name} {material.color} shader={material.shader.name}");
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
                {
                    report.AppendLine($"  CLIP {clip.name} length={clip.length} legacy={clip.legacy}");
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip).Take(8)) report.AppendLine($"    {binding.path}: {binding.propertyName}");
                }
                if (path.EndsWith(".prefab") || path.Contains("Blue_"))
                {
                    var obj = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    foreach (var t in obj.GetComponentsInChildren<Transform>(true)) report.AppendLine($"  NODE {AnimationUtility.CalculateTransformPath(t,obj.transform)} pos={t.localPosition} scale={t.localScale}");
                    foreach (var r in obj.GetComponentsInChildren<Renderer>(true)) report.AppendLine($"  RENDERER {r.name} bounds={r.bounds} mats={string.Join(",",r.sharedMaterials.Select(m=>m.name))}");
                    Object.DestroyImmediate(obj);
                }
            }
            EditorSceneManager.OpenScene("Assets/_WhosWatching/Scenes/Main.unity");
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) report.AppendLine($"SCENE {root.name} pos={root.transform.position} scale={root.transform.localScale}");
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/AssetInspection.txt",report.ToString());
        }
    }
}
