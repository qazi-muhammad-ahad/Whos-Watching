using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WhosWatching.Tests
{
    public sealed class JumpCollisionTests
    {
        const string Root="Assets/_WhosWatching/";
        [Test] public void EveryTeamPrefabInheritsPhysicalCollision()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Root+"Prefabs/TeamReady"}))
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var colliders=prefab.GetComponentsInChildren<Collider>(true);
                Assert.That(colliders.Length,Is.GreaterThan(0),prefab.name);
                Assert.That(colliders.All(c=>!c.isTrigger && c.enabled),Is.True,prefab.name);
                if(prefab.name.StartsWith("Crate_")) Assert.That(colliders.Single(),Is.TypeOf<BoxCollider>());
                foreach(var mesh in colliders.OfType<MeshCollider>()) Assert.That(mesh.sharedMesh,Is.Not.Null,prefab.name);
                Assert.That(colliders.Any(c=>c.name.Contains("accent badge") || c.name.Contains("fabric")),Is.False,prefab.name);
            }
        }
        [TestCase("Devil")][TestCase("Ghost")]
        public void CharactersHaveAnAirbornePoseWithMatchingBonePaths(string name)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Character/"+name+".prefab");
            Assert.That(prefab.GetComponentsInChildren<Collider>(true),Is.Empty);
            var animator=prefab.GetComponent<Animator>();
            var clip=animator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name==name+"_Jump");
            Assert.That(clip,Is.Not.Null);
            foreach(var binding in AnimationUtility.GetCurveBindings(clip)) Assert.That(prefab.transform.Find(binding.path),Is.Not.Null,binding.path);
        }
        [TestCase("Crate_Medium")][TestCase("Ramp_Standard")][TestCase("Step_Module_2_Tier")][TestCase("Wall_Corner_90deg")]
        public void SolidPropsHaveRaycastableSurfaces(string name)
        {
            var obj=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/TeamReady/TeamB/"+name+"_TeamB.prefab"));
            try
            {
                obj.transform.position=new Vector3(100,0,100); Physics.SyncTransforms();
                var colliders=obj.GetComponentsInChildren<Collider>(); int hits=0;
                foreach(var collider in colliders)
                {
                    var b=collider.bounds;
                    if(collider.Raycast(new Ray(b.center+Vector3.up*(b.extents.y+1),Vector3.down),out _,b.size.y+2)) hits++;
                }
                Assert.That(hits,Is.GreaterThan(0),name);
            }
            finally { Object.DestroyImmediate(obj); }
        }
    }
}
