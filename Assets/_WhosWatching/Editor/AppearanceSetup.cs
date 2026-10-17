using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WhosWatching.Editor
{
    public static class AppearanceSetup
    {
        const string Root = "Assets/_WhosWatching/";
        const string Animations = Root + "Animations/";
        const string Ready = Root + "Prefabs/TeamReady/";
        public static void InstallAndPreview()
        {
            Install(); AppearancePreview.Render();
            EditorSceneManager.OpenScene(Root+"Scenes/Main.unity");
        }
        [MenuItem("Who's Watching/Install character and team appearance")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Animations); Directory.CreateDirectory(Ready); AssetDatabase.Refresh();
            PrepareCharacter("Devil", "Blue_Female_Puff", false);
            PrepareCharacter("Ghost", "Blue_Male_Wisp_Cap", true);
            JumpAndCollisionSetup.InstallJump("Devil"); JumpAndCollisionSetup.InstallJump("Ghost");
            string playerPath = Root + "Prefabs/Player/NetworkPlayer.prefab";
            var playerRoot = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                var player = playerRoot.GetComponent<NetworkPlayer>();
                player.DevilPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Character/Devil.prefab");
                player.GhostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Character/Ghost.prefab");
                var renderer = player.VisualRoot.GetComponent<Renderer>(); if (renderer) Object.DestroyImmediate(renderer);
                var filter = player.VisualRoot.GetComponent<MeshFilter>(); if (filter) Object.DestroyImmediate(filter);
                PrefabUtility.SaveAsPrefabAsset(playerRoot,playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(playerRoot); }
            foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(Root+"Prefabs/") && p.EndsWith(".fbx") && !p.Contains("/Character/")).ToArray())
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var wrapper = new GameObject(Path.GetFileNameWithoutExtension(path));
                model.transform.SetParent(wrapper.transform,false);
                Bind(wrapper, DefaultTeam(wrapper.name,0), false);
                JumpAndCollisionSetup.AddColliders(wrapper);
                PrefabUtility.SaveAsPrefabAsset(wrapper,Ready+wrapper.name+".prefab");
                Object.DestroyImmediate(wrapper);
            }
            CreateCheckpoint();
            TeamVariantSetup.Install();
            var scene = EditorSceneManager.OpenScene(Root+"Scenes/Main.unity");
            var session = Object.FindFirstObjectByType<NetworkGameManager>();
            // NGO's manager must remain separate from replicated scene objects.
            if (session.GetComponent<TeamLobby>()) Object.DestroyImmediate(session.GetComponent<TeamLobby>());
            if (session.GetComponent<NetworkObject>()) Object.DestroyImmediate(session.GetComponent<NetworkObject>());
            var lobby = Object.FindFirstObjectByType<TeamLobby>();
            if (!lobby)
            {
                var lobbyRoot = new GameObject("TeamLobby");
                lobbyRoot.AddComponent<NetworkObject>(); lobbyRoot.AddComponent<TeamLobby>();
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if (source.StartsWith(Root+"Prefabs/") && !source.Contains("/Player/") && !source.Contains("/Character/"))
                {
                    // Preserve deliberate per-instance teams and nested A/B map variants.
                    if (!root.GetComponent<TeamTint>() && !root.GetComponentInChildren<TeamTint>())
                        Bind(root,DefaultTeam(root.name,root.transform.position.x),false);
                }
                else if (root.name == "West" || root.name == "East") AddBoundaryStripe(root,root.name == "West" ? TeamId.A : TeamId.B);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("WW_APPEARANCE_INSTALLED: two characters, 26 team-ready props, checkpoint and Main lobby.");
        }
        static TeamId DefaultTeam(string name, float x) => name.Contains("Red") ? TeamId.B : name.Contains("Blue") ? TeamId.A : x <= 0 ? TeamId.A : TeamId.B;

        static void PrepareCharacter(string name, string source, bool ghost)
        {
            string path = Root+"Prefabs/Character/"+name+".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Character/"+source+".fbx");
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = sourceRoot.transform.localRotation;
                root.transform.localScale = sourceRoot.transform.localScale;
                if (ghost)
                {
                    // Both the rig and its skinned mesh have an authoring-space X offset.
                    var rig = root.transform.Find("Blue_Male_Wisp_Cap | Armature");
                    float offset = rig.localPosition.x;
                    foreach (Transform child in root.transform) child.localPosition -= Vector3.right * offset;
                }
                var clips = AssetDatabase.LoadAllAssetsAtPath(Root+"Prefabs/Character/"+source+".fbx").OfType<AnimationClip>()
                    .Where(c=>!c.name.StartsWith("__preview") && !c.name.Contains("|Red_")).ToArray();
                var move = CopyClip(clips.First(c=>c.name.EndsWith(ghost ? "Float_Wave_Loop" : "Walk_Loop")),name+"_Move",root,ghost,true);
                var buff = CopyClip(clips.First(c=>c.name.EndsWith("Buff_Dance")),name+"_Buff",root,ghost,false);
                var idle = ghost ? move : Idle(move,name+"_Idle");
                string controllerPath = Animations+name+".controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (!controller)
                {
                    controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                    controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
                    controller.AddParameter("Buff",AnimatorControllerParameterType.Trigger);
                    var machine = controller.layers[0].stateMachine;
                    var rest = machine.AddState("Idle"); rest.motion = idle; rest.speed = ghost ? 0.65f : 1;
                    var moving = machine.AddState("Move"); moving.motion = move;
                    var dance = machine.AddState("Buff"); dance.motion = buff;
                    machine.defaultState = rest;
                    var start = rest.AddTransition(moving); start.hasExitTime = false; start.duration = 0.12f; start.AddCondition(AnimatorConditionMode.Greater,0.1f,"Speed");
                    var stop = moving.AddTransition(rest); stop.hasExitTime = false; stop.duration = 0.12f; stop.AddCondition(AnimatorConditionMode.Less,0.1f,"Speed");
                    var celebrate = machine.AddAnyStateTransition(dance); celebrate.hasExitTime = false; celebrate.duration = 0.12f; celebrate.canTransitionToSelf = false; celebrate.AddCondition(AnimatorConditionMode.If,0,"Buff");
                    var done = dance.AddTransition(rest); done.hasExitTime = true; done.exitTime = 1; done.duration = 0.15f;
                }
                var animator = root.GetComponent<Animator>(); if (!animator) animator = root.AddComponent<Animator>();
                animator.avatar = null; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                var presentation = root.GetComponent<CharacterPresentation>(); if (!presentation) presentation = root.AddComponent<CharacterPresentation>();
                presentation.Animator = animator; presentation.Floating = ghost;
                Bind(root,TeamId.A,true);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static AnimationClip CopyClip(AnimationClip source,string name,GameObject target,bool ghost,bool loop)
        {
            var clip = new AnimationClip { name = name, frameRate = source.frameRate };
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                // Keep the runtime root stationary and remove FBX authoring offsets.
                if (binding.path == "" || (ghost && !binding.path.Contains('/'))) continue;
                if (!target.transform.Find(binding.path)) throw new InvalidOperationException($"Missing bone {binding.path} in {target.name}");
                AnimationUtility.SetEditorCurve(clip,binding,AnimationUtility.GetEditorCurve(source,binding));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            return SaveClip(clip,name);
        }
        static AnimationClip Idle(AnimationClip move,string name)
        {
            var clip = new AnimationClip { name = name, frameRate = 30 };
            foreach (var binding in AnimationUtility.GetCurveBindings(move))
            {
                float value = AnimationUtility.GetEditorCurve(move,binding).Evaluate(0);
                AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,1,value));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip,settings);
            return SaveClip(clip,name);
        }
        static AnimationClip SaveClip(AnimationClip clip,string name)
        {
            string path = Animations+name+".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (!existing) { AssetDatabase.CreateAsset(clip,path); return clip; }
            EditorUtility.CopySerialized(clip,existing); Object.DestroyImmediate(clip); return existing;
        }
        public static void Bind(GameObject root, TeamId team, bool character)
        {
            var tint = root.GetComponent<TeamTint>(); if (!tint) tint = root.AddComponent<TeamTint>();
            tint.Team = team;
            var slots = new List<TeamTint.Slot>();
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bool painted = renderers.Any(r=>r.sharedMaterials.Any(m=>m && m.name.Contains("Light Painted")));
            foreach (var renderer in renderers)
            for (int i=0;i<renderer.sharedMaterials.Length;i++)
            {
                var material = renderer.sharedMaterials[i]; if (!material) continue;
                string label = material.name;
                bool primary = character && label.Contains("primary");
                bool accent = character ? label.Contains("accent") :
                    label.StartsWith("Team ") || label.StartsWith("Energy ") || label.Contains("Light Painted") || (!painted && label.Contains("Charcoal Metal"));
                if (!primary && !accent) continue;
                slots.Add(new TeamTint.Slot { Renderer=renderer, MaterialIndex=i, Strength=character && accent ? 0.55f : 1,
                    BaseColor=character ? Color.white : material.color, Emissive=material.IsKeywordEnabled("_EMISSION") });
            }
            if (slots.Count == 0 && !character)
            {
                // All-concrete pieces need a separate badge instead of tinting the whole wall.
                var bounds = renderers.Select(r=>r.bounds).Aggregate((a,b)=>{ a.Encapsulate(b); return a; });
                var badge = root.transform.Find("Team accent badge");
                if (!badge)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Team accent badge";
                    Object.DestroyImmediate(go.GetComponent<Collider>()); badge = go.transform; badge.SetParent(root.transform,false);
                }
                badge.position = new Vector3(bounds.center.x,bounds.center.y,bounds.max.z+0.012f);
                badge.rotation = Quaternion.identity;
                var scale = root.transform.lossyScale;
                badge.localScale = new Vector3(Mathf.Min(bounds.size.x*0.5f,0.8f)/scale.x,0.12f/scale.y,0.02f/scale.z);
                var renderer = badge.GetComponent<Renderer>(); renderer.sharedMaterial = AccentMaterial();
                slots.Add(new TeamTint.Slot { Renderer=renderer,MaterialIndex=0,Strength=1,BaseColor=Color.white });
            }
            tint.Slots = slots.ToArray();
            if (slots.Count == 0) throw new InvalidOperationException("No team accent binding for "+root.name);
            tint.Apply(); EditorUtility.SetDirty(tint);
        }
        static Material AccentMaterial()
        {
            string path = Root+"Materials/TeamAccent.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color=Color.white }; AssetDatabase.CreateAsset(material,path); }
            return material;
        }
        static void AddBoundaryStripe(GameObject wall,TeamId team)
        {
            var stripe = wall.transform.Find("Team accent stripe");
            if (!stripe)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Team accent stripe";
                Object.DestroyImmediate(go.GetComponent<Collider>()); stripe = go.transform; stripe.SetParent(wall.transform,false);
            }
            stripe.localPosition = new Vector3(team == TeamId.A ? 0.505f : -0.505f,0.25f,0);
            stripe.localScale = new Vector3(0.02f,0.08f,0.98f);
            var renderer = stripe.GetComponent<Renderer>(); renderer.sharedMaterial = AccentMaterial();
            var tint = stripe.GetComponent<TeamTint>(); if (!tint) tint = stripe.gameObject.AddComponent<TeamTint>();
            tint.Team = team; tint.Slots = new[] { new TeamTint.Slot { Renderer=renderer, MaterialIndex=0, Strength=1, BaseColor=Color.white } };
        }
        static void CreateCheckpoint()
        {
            var root = new GameObject("Checkpoint");
            var platform = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/06_Objectives/Objective_Platform_Base.fbx"));
            platform.transform.SetParent(root.transform,false);
            for (int i=0;i<2;i++)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube); post.name = "Checkpoint team marker";
                post.transform.SetParent(root.transform,false); post.transform.localPosition = new Vector3(i==0 ? -0.85f : 0.85f,0.65f,0);
                post.transform.localScale = new Vector3(0.12f,1.2f,0.12f); Object.DestroyImmediate(post.GetComponent<Collider>());
                post.GetComponent<Renderer>().sharedMaterial = AccentMaterial();
            }
            Bind(root,TeamId.A,false);
            var tint = root.GetComponent<TeamTint>();
            tint.Slots = tint.Slots.Concat(root.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Checkpoint team marker")
                .Select(r=>new TeamTint.Slot { Renderer=r, MaterialIndex=0, Strength=1, BaseColor=Color.white })).ToArray();
            JumpAndCollisionSetup.AddColliders(root);
            PrefabUtility.SaveAsPrefabAsset(root,Ready+"Checkpoint.prefab"); Object.DestroyImmediate(root);
        }
    }
}
