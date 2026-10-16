using NUnit.Framework;
using UnityEngine;

namespace WhosWatching.Tests
{
    public class GazeRulesTests
    {
        [TestCase(0,0,1)] [TestCase(1,0,1.35f)] [TestCase(0,1,0.75f)] [TestCase(0,2,0.5f)]
        [TestCase(1,1,1.1f)] [TestCase(1,2,0.85f)] [TestCase(20,0,1.5f)] [TestCase(0,20,0.5f)]
        public void BoundedAdditiveEffects(int allies, int enemies, float expected) => Assert.That(GazeRules.Multiplier(allies,enemies), Is.EqualTo(expected).Within(0.0001));
        [Test] public void RectangularScreenAndRangeBoundaries()
        {
            Assert.That(GazeRules.InViewport(new Vector3(5.1f,2.8f,5),60,16f/9,15),Is.True, "Screen corners count, not just a circular cone");
            Assert.That(GazeRules.InViewport(new Vector3(5.2f,0,5),60,16f/9,15),Is.False);
            Assert.That(GazeRules.InViewport(new Vector3(0,2.9f,5),60,16f/9,15),Is.False);
            Assert.That(GazeRules.InViewport(Vector3.forward*15,60,16f/9,15),Is.True);
            Assert.That(GazeRules.InViewport(Vector3.forward*15.01f,60,16f/9,15),Is.False);
            Assert.That(GazeRules.InViewport(Vector3.back,60,16f/9,15),Is.False);
            Assert.That(GazeRules.InViewport(Vector3.zero,60,16f/9,15),Is.False);
            Assert.That(GazeRules.InViewport(Vector3.forward*0.01f,60,16f/9,15),Is.False);
        }
        [Test] public void ViewportAspectChangesHorizontalCoverageOnly()
        {
            Assert.That(GazeRules.InViewport(new Vector3(3,0,5),60,9f/16,15),Is.False);
            Assert.That(GazeRules.InViewport(new Vector3(3,0,5),60,16f/9,15),Is.True);
            Assert.That(GazeRules.InViewport(new Vector3(7,0,5),60,32f/9,15),Is.True);
            Assert.That(GazeRules.InViewport(new Vector3(0,3,5),60,32f/9,15),Is.False);
            Assert.That(GazeRules.ValidAspect(float.NaN),Is.False);
            Assert.That(GazeRules.ValidAspect(float.PositiveInfinity),Is.False);
            Assert.That(GazeRules.ValidAspect(100),Is.False);
            Assert.That(GazeRules.ValidAspect(0),Is.False);
        }
        [Test] public void IndependentPairAcquisitionReleaseAndIdentityZero()
        {
            var pairs = new GazePairs();
            Assert.That(pairs.Step(1,0,true,0,0.15f),Is.False);
            Assert.That(pairs.Step(1,2,true,0.1,0.15f),Is.False);
            Assert.That(pairs.Step(1,0,true,0.16,0.15f),Is.True);
            Assert.That(pairs.Step(1,2,true,0.16,0.15f),Is.False);
            Assert.That(pairs.Step(1,2,true,0.26,0.15f),Is.True);
            Assert.That(pairs.Step(1,0,false,0.27,0.15f),Is.False);
            Assert.That(pairs.Step(1,2,true,0.27,0.15f),Is.True);
            Assert.That(pairs.Step(1,0,true,0.28,0.15f),Is.False);
            pairs.Remove(0);
            Assert.That(pairs.Step(1,2,true,0.3,0.15f),Is.True);
            pairs.Remove(1);
            Assert.That(pairs.Step(1,2,true,1,0.15f),Is.False);
            Assert.That(pairs.Step(2,2,true,1,0),Is.False);
        }
        [Test] public void MultiTargetStateCountsEachPlayerOnce()
        {
            var state = GazeState.Neutral;
            Assert.That(state.ContainsTarget(0),Is.False);
            Assert.That(state.AddTarget(0,true),Is.True);
            Assert.That(state.AddTarget(0,false),Is.False);
            Assert.That(state.AddTarget(42,false),Is.True);
            Assert.That(state.AddTarget(99,false),Is.True);
            Assert.That(state.TargetCount,Is.EqualTo(3));
            Assert.That(state.OutgoingAllies,Is.EqualTo(1));
            Assert.That(state.OutgoingEnemies,Is.EqualTo(2));
            Assert.That(state.ContainsTarget(42),Is.True);
            Assert.That(state.AddTarget(100,true),Is.False);
        }
        [Test] public void StackingLimitsApplyAfterAllContributionsAndAreConfigurable()
        {
            Assert.That(GazeRules.Multiplier(3,0,0.35f,0.25f,0.5f,3),Is.EqualTo(2.05f).Within(0.001));
            Assert.That(GazeRules.Multiplier(2,1),Is.EqualTo(1.45f).Within(0.001),"Do not cap buffs before subtracting enemies");
            Assert.That(GazeRules.Multiplier(1,2),Is.EqualTo(0.85f).Within(0.001));
            Assert.That(GazeRules.Multiplier(0,3,0.35f,0.25f,0.1f,3),Is.EqualTo(0.25f).Within(0.001));
        }
    }
}
