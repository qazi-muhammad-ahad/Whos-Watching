using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WhosWatching.Tests
{
    public sealed class AppearanceTests
    {
        const string Root = "Assets/_WhosWatching/";
        [Test] public void PaletteRejectsInvalidAndDuplicateChoices()
        {
            for (byte i=0;i<TeamPalette.Count;i++)
            {
                Assert.That(TeamPalette.Valid(i),Is.True);
                Assert.That(TeamPalette.CanChoose(i,i),Is.False);
                Assert.That(TeamPalette.CanChoose(i,(byte)((i+1)%TeamPalette.Count)),Is.True);
            }
            Assert.That(TeamPalette.Valid(255),Is.False);
            Assert.That(TeamPalette.Valid((CharacterId)2),Is.False);
            Assert.That(TeamPalette.Valid(CharacterId.Ghost),Is.True);
        }
        [TestCase("Devil")][TestCase("Ghost")]
        public void CopiedClipsAnimateMatchingRigWithoutMovingItsRoot(string character)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Character/"+character+".prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponent<Animator>();
                Assert.That(animator,Is.Not.Null); Assert.That(animator.runtimeAnimatorController,Is.Not.Null);
                Assert.That(animator.applyRootMotion,Is.False);
                var clips = animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
                Assert.That(clips.Length,Is.GreaterThanOrEqualTo(2));
                foreach (var clip in clips)
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    Assert.That(binding.path,Is.Not.Empty);
                    Assert.That(instance.transform.Find(binding.path),Is.Not.Null,binding.path);
                }
                var move = clips.First(c=>c.name.EndsWith("_Move"));
                Assert.That(AnimationUtility.GetAnimationClipSettings(move).loopTime,Is.True);
                Vector3 rootPosition=instance.transform.position, rootScale=instance.transform.localScale;
                Quaternion rootRotation=instance.transform.rotation;
                var bones=instance.GetComponentsInChildren<Transform>();
                move.SampleAnimation(instance,0);
                var matrices=bones.Select(t=>t.localToWorldMatrix).ToArray();
                move.SampleAnimation(instance,0.37f);
                Assert.That(bones.Where((t,i)=>t.localToWorldMatrix!=matrices[i]).Any(),Is.True,"Locomotion must move bones");
                Assert.That(instance.transform.position,Is.EqualTo(rootPosition));
                Assert.That(instance.transform.rotation,Is.EqualTo(rootRotation));
                Assert.That(instance.transform.localScale,Is.EqualTo(rootScale));
                var bounds=instance.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
                Assert.That(bounds.size.y,Is.InRange(1.2f,2.5f));
                Assert.That(Mathf.Abs(bounds.center.x),Is.LessThan(0.6f),"FBX authoring offset must be removed");
            }
            finally { Object.DestroyImmediate(instance); }
        }
        [Test] public void TintChangesOnlyBoundSlotAndNeverSharedMaterial()
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color=Color.white };
            try
            {
                var renderer=obj.GetComponent<Renderer>(); renderer.sharedMaterials=new[]{material,material};
                var tint=obj.AddComponent<TeamTint>(); tint.Team=TeamId.A;
                tint.Slots=new[]{new TeamTint.Slot { Renderer=renderer,MaterialIndex=1,Strength=1,BaseColor=Color.white }};
                tint.Apply();
                var block=new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block,1); Assert.That(Vector4.Distance(block.GetColor("_BaseColor"),TeamPalette.Color(0)),Is.LessThan(0.00001f));
                renderer.GetPropertyBlock(block,0); Assert.That(block.isEmpty,Is.True);
                tint.Team=TeamId.B; tint.Apply();
                renderer.GetPropertyBlock(block,1); Assert.That(Vector4.Distance(block.GetColor("_BaseColor"),TeamPalette.Color(1)),Is.LessThan(0.00001f));
                Assert.That(material.color,Is.EqualTo(Color.white));
            }
            finally { Object.DestroyImmediate(obj); Object.DestroyImmediate(material); }
        }
        [Test] public void AllTeamReadyPropsHaveValidAccentBindings()
        {
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{Root+"Prefabs/TeamReady"});
            Assert.That(paths.Length,Is.GreaterThanOrEqualTo(26));
            foreach(var guid in paths)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var tint=prefab.GetComponent<TeamTint>(); Assert.That(tint,Is.Not.Null,prefab.name);
                Assert.That(tint.Slots.Length,Is.GreaterThan(0),prefab.name);
                foreach(var slot in tint.Slots) { Assert.That(slot.Renderer,Is.Not.Null,prefab.name); Assert.That(slot.MaterialIndex,Is.LessThan(slot.Renderer.sharedMaterials.Length)); }
            }
        }
        [Test] public void GhostArmsMatchBodyAndDevilAccentsAreLighter()
        {
            var ghost=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Character/Ghost.prefab").GetComponent<TeamTint>();
            Assert.That(ghost.Slots.Length,Is.GreaterThanOrEqualTo(3));
            Assert.That(ghost.Slots.All(s=>s.Strength==1),Is.True,"Ghost arms and body must use the identical main color");
            var devil=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Character/Devil.prefab").GetComponent<TeamTint>();
            Assert.That(devil.Slots.Any(s=>s.Strength==1),Is.True);
            var accents=devil.Slots.Where(s=>s.Strength<1).ToArray();
            Assert.That(accents.Length,Is.GreaterThanOrEqualTo(4));
            for(byte i=0;i<TeamPalette.Count;i++)
            foreach(var slot in accents)
                Assert.That(Color.Lerp(slot.BaseColor,TeamPalette.Color(i),slot.Strength).grayscale,Is.GreaterThan(TeamPalette.Color(i).grayscale));
        }
    }
}
