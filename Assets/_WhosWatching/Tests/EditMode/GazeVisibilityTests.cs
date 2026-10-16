using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WhosWatching.Tests
{
    public class GazeVisibilityTests
    {
        readonly List<GameObject> objects = new();
        readonly Vector3 origin = new(1000,1000,1000);
        Transform observer, target;
        GameObject Create(string name, Vector3 offset)
        {
            var obj = new GameObject(name); obj.layer = 30; obj.transform.position = origin + offset; objects.Add(obj); return obj;
        }
        [SetUp] public void Setup()
        {
            observer = Create("Observer",Vector3.zero).transform;
            var body = Create("Body",new Vector3(0,0,5)); target = body.transform;
            var capsule = body.AddComponent<CharacterController>(); capsule.height=2; capsule.radius=0.4f;
            body.layer = 29; // Movement query shape is deliberately excluded from the gaze mask.
            var gazeBody = Create("GazeBody",new Vector3(0,0,5)); gazeBody.transform.SetParent(body.transform,true);
            var gazeCollider = gazeBody.AddComponent<CapsuleCollider>(); gazeCollider.height=2; gazeCollider.radius=0.4f;
        }
        [TearDown] public void Cleanup() { foreach(var obj in objects) Object.DestroyImmediate(obj); objects.Clear(); }
        bool Visible(float aspect=16f/9)
        {
            Physics.SyncTransforms();
            return GazeVisibility.CanSeeBody(observer,origin+Vector3.up*0.65f,Quaternion.identity,target,60,aspect,15,1<<30);
        }
        GameObject Wall(float height=4)
        {
            var obj=Create("Wall",new Vector3(0,0,2.5f)); obj.AddComponent<BoxCollider>().size=new Vector3(4,height,0.2f); return obj;
        }
        [Test] public void OffCenterAndScreenEdgeBodyQualify()
        {
            target.position=origin+new Vector3(3,0,5); Assert.That(Visible(),Is.True);
            Assert.That(Visible(9f/16),Is.False);
            target.position=origin+new Vector3(5.3f,0,5); Assert.That(Visible(),Is.True,"A body edge can qualify when its center is offscreen");
            target.position=origin+new Vector3(6,0,5); Assert.That(Visible(),Is.False);
            target.position=origin+new Vector3(0,0,-5); Assert.That(Visible(),Is.False);
        }
        [Test] public void VisibleHeadCountsButFullWallBlocks()
        {
            var wall=Wall(1.5f);
            Assert.That(Visible(),Is.True,"Visible head qualifies through the dedicated body collider, even when chest is covered.");
            wall.GetComponent<BoxCollider>().size=new Vector3(4,4,0.2f); Assert.That(Visible(),Is.False);
            wall.GetComponent<BoxCollider>().isTrigger=true; Assert.That(Visible(),Is.True);
        }
        [Test] public void InterveningBodyBlocksAndSourceColliderDoesNot()
        {
            var source=observer.gameObject.AddComponent<CapsuleCollider>(); source.height=2; source.radius=0.4f;
            Assert.That(Visible(),Is.True);
            var blocker=Create("Intervening player body",new Vector3(0,0,2.5f)).AddComponent<CapsuleCollider>(); blocker.height=2; blocker.radius=0.4f;
            Assert.That(Visible(),Is.False);
            blocker.enabled=false; Assert.That(Visible(),Is.True);
        }
        [Test] public void CosmeticLayerCannotBlockGaze()
        {
            var wall=Wall(); wall.layer=29; Assert.That(Visible(),Is.True);
        }
    }
}
