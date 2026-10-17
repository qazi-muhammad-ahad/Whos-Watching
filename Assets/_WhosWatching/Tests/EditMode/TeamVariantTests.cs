using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WhosWatching.Tests
{
    public sealed class TeamVariantTests
    {
        const string Root="Assets/_WhosWatching/Prefabs/TeamReady";
        [Test] public void EveryBaseHasTwoLinkedIndependentlyTintedVariants()
        {
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{Root}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>Path.GetDirectoryName(p).Replace('\\','/')==Root).ToArray();
            Assert.That(paths.Length,Is.EqualTo(27));
            foreach(var path in paths)
            {
                var basePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var parent=new GameObject("Mixed team map");
                try
                {
                    var parentTint=parent.AddComponent<TeamTint>(); parentTint.Team=TeamId.A;
                    foreach(var team in new[]{TeamId.A,TeamId.B})
                    {
                        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Team{team}/{basePrefab.name}_Team{team}.prefab");
                        Assert.That(prefab,Is.Not.Null,path);
                        Assert.That(PrefabUtility.GetPrefabAssetType(prefab),Is.EqualTo(PrefabAssetType.Variant));
                        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(prefab),Is.EqualTo(basePrefab));
                        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent.transform);
                        var tint=instance.GetComponent<TeamTint>(); Assert.That(tint.Team,Is.EqualTo(team)); tint.Apply();
                        // A larger map binding must never overwrite its nested Team B variant.
                        parentTint.Slots=tint.Slots; parentTint.Apply();
                        foreach(var slot in tint.Slots)
                        {
                            var block=new MaterialPropertyBlock(); slot.Renderer.GetPropertyBlock(block,slot.MaterialIndex);
                            var expected=Color.Lerp(slot.BaseColor,TeamPalette.Color((byte)(team==TeamId.A ? 0 : 1)),slot.Strength);
                            Assert.That(Vector4.Distance(block.GetColor("_BaseColor"),expected),Is.LessThan(0.00001f),prefab.name);
                        }
                    }
                }
                finally { Object.DestroyImmediate(parent); }
            }
        }
        [TestCase(false)][TestCase(true)]
        public void CartoonMotionIsSmoothBoundedAndDoesNotMoveGameplayRoot(bool floating)
        {
            var parent=new GameObject("Gameplay root");
            var visual=new GameObject("Visual"); visual.transform.SetParent(parent.transform);
            var motion=visual.AddComponent<CharacterPresentation>(); motion.Floating=floating;
            visual.transform.localRotation=Quaternion.Euler(-90,0,0); visual.transform.localScale=Vector3.one*100;
            try
            {
                Vector3 previous=Vector3.zero;
                float minY=float.MaxValue,maxY=float.MinValue;
                for(int i=0;i<=120;i++)
                {
                    motion.SampleMotion(i/120f*Mathf.PI*2,5);
                    var position=visual.transform.localPosition;
                    minY=Mathf.Min(minY,position.y); maxY=Mathf.Max(maxY,position.y);
                    if(i>0) Assert.That(Vector3.Distance(position,previous),Is.LessThan(0.01f));
                    previous=position;
                    Assert.That(position.magnitude,Is.LessThan(0.07f));
                    Assert.That(parent.transform.position,Is.EqualTo(Vector3.zero));
                    Assert.That(visual.transform.localScale.x,Is.InRange(95,105));
                }
                Assert.That(maxY-minY,Is.GreaterThan(0.03f));
                motion.SampleMotion(0,5); var first=visual.transform.localPosition;
                motion.SampleMotion(Mathf.PI*2,5); Assert.That(Vector3.Distance(first,visual.transform.localPosition),Is.LessThan(0.00001f));
            }
            finally { Object.DestroyImmediate(parent); }
        }
        [Test] public void TeamCapacityAndIdsAreBounded()
        {
            Assert.That(TeamLobby.HasTeamSpace(TeamId.A,1),Is.True);
            Assert.That(TeamLobby.HasTeamSpace(TeamId.B,2),Is.False);
            Assert.That(TeamLobby.HasTeamSpace(TeamId.None,0),Is.False);
            Assert.That(TeamLobby.HasTeamSpace((TeamId)255,0),Is.False);
        }
    }
}
