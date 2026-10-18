using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace WhosWatching.Editor
{
    public static class JumpAndCollisionSetup
    {
        const string Root="Assets/_WhosWatching/";
        [MenuItem("Who's Watching/Install jump animations and environment collision")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach(var name in new[]{"Devil","Ghost"}) InstallJump(name);
            foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Root+"Prefabs/TeamReady"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>Path.GetDirectoryName(p).Replace('\\','/')==Root+"Prefabs/TeamReady").ToArray())
            {
                var contents=PrefabUtility.LoadPrefabContents(path);
                try { AddColliders(contents); PrefabUtility.SaveAsPrefabAsset(contents,path); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            var scene=EditorSceneManager.OpenScene(Root+"Scenes/Main.unity");
            foreach(var obj in scene.GetRootGameObjects())
            {
                string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
                if(path.StartsWith(Root+"Prefabs/") && !path.Contains("/Character/") && !path.Contains("/Player/")) AddColliders(obj);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("WW_JUMP_COLLISION_INSTALLED: jump controllers, shared base colliders and existing scene prop colliders.");
        }
        public static void AddColliders(GameObject root)
        {
            var meshes=root.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.sharedMesh).ToArray();
            if(root.name.StartsWith("Crate_"))
            {
                if(root.GetComponentInChildren<Collider>()) return;
                var bounds=new Bounds(); bool initialized=false;
                foreach(var mesh in meshes)
                {
                    var b=mesh.sharedMesh.bounds;
                    for(int i=0;i<8;i++)
                    {
                        var corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0 ? -1:1,(i&2)==0 ? -1:1,(i&4)==0 ? -1:1));
                        var point=root.transform.InverseTransformPoint(mesh.transform.TransformPoint(corner));
                        if(!initialized) { bounds=new Bounds(point,Vector3.zero); initialized=true; } else bounds.Encapsulate(point);
                    }
                }
                if(initialized) { var box=root.AddComponent<BoxCollider>(); box.center=bounds.center; box.size=bounds.size; }
                return;
            }
            foreach(var mesh in meshes)
            {
                string name=mesh.name.ToLowerInvariant();
                if(new[]{"fabric","hem","emblem","eye","accent","badge","stripe","energy","glow","bolt","screw","rivet","arrow","symbol","marker"}.Any(name.Contains)) continue;
                if(mesh.GetComponent<Collider>()) continue;
                // Static triangle surfaces preserve ramps, stair treads, corners and openings.
                var collider=mesh.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh=mesh.sharedMesh; collider.convex=false;
            }
        }
        public static void InstallJump(string name)
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"Animations/"+name+".controller");
            if(!controller.parameters.Any(p=>p.name=="Airborne")) controller.AddParameter("Airborne",AnimatorControllerParameterType.Bool);
            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"Animations/"+name+(name=="Devil" ? "_Idle.anim" : "_Move.anim"));
            var clip=new AnimationClip { name=name+"_Jump",frameRate=30 };
            foreach(var binding in AnimationUtility.GetCurveBindings(source))
            {
                var curve=AnimationUtility.GetEditorCurve(source,binding);
                AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,1,curve.Evaluate(0.2f)));
            }
            // Raised arms and tucked feet distinguish the airborne silhouette.
            foreach(var group in AnimationUtility.GetCurveBindings(clip).Where(b=>b.propertyName.StartsWith("m_LocalRotation.")).GroupBy(b=>b.path))
            {
                if(!group.Key.EndsWith("Arm.L") && !group.Key.EndsWith("Arm.R") && !group.Key.EndsWith("Leg.L") && !group.Key.EndsWith("Leg.R")) continue;
                var bindings=group.OrderBy(b=>b.propertyName).ToArray();
                float Read(string axis)=>AnimationUtility.GetEditorCurve(clip,group.First(b=>b.propertyName.EndsWith(axis))).Evaluate(0);
                Quaternion rest=new Quaternion(Read("x"),Read("y"),Read("z"),Read("w"));
                Quaternion pose=rest*Quaternion.Euler(group.Key.Contains("Leg.") ? 28:0,0,group.Key.Contains("Arm.") ? (group.Key.EndsWith(".L") ? -22:22):0);
                foreach(var binding in bindings)
                {
                    float value=binding.propertyName.EndsWith("x") ? pose.x : binding.propertyName.EndsWith("y") ? pose.y : binding.propertyName.EndsWith("z") ? pose.z : pose.w;
                    AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,1,value));
                }
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=true; AnimationUtility.SetAnimationClipSettings(clip,settings);
            string path=Root+"Animations/"+name+"_Jump.anim";
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing) { EditorUtility.CopySerialized(clip,existing); Object.DestroyImmediate(clip); clip=existing; } else AssetDatabase.CreateAsset(clip,path);
            var machine=controller.layers[0].stateMachine;
            var jump=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Jump");
            if(!jump)
            {
                jump=machine.AddState("Jump");
                var enter=machine.AddAnyStateTransition(jump); enter.hasExitTime=false; enter.duration=0.1f; enter.canTransitionToSelf=false; enter.AddCondition(AnimatorConditionMode.If,0,"Airborne");
                var exit=jump.AddTransition(machine.states.First(s=>s.state.name=="Idle").state); exit.hasExitTime=false; exit.duration=0.15f; exit.AddCondition(AnimatorConditionMode.IfNot,0,"Airborne");
            }
            jump.motion=clip;
            foreach(var transition in machine.anyStateTransitions.Where(t=>t.destinationState.name=="Buff"))
                if(!transition.conditions.Any(c=>c.parameter=="Airborne")) transition.AddCondition(AnimatorConditionMode.IfNot,0,"Airborne");
            EditorUtility.SetDirty(controller);
        }
    }
}
