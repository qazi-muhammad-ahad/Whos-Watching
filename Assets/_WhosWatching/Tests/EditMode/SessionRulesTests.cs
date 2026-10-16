using NUnit.Framework;
using UnityEngine;

namespace WhosWatching.Tests
{
    public class SessionRulesTests
    {
        [TestCase(0,true)] [TestCase(3,true)] [TestCase(4,false)] [TestCase(5,false)]
        public void CapacityCountsReservations(int occupied, bool allowed) => Assert.That(SessionRules.Reject(SessionRules.Payload, occupied, true) == null, Is.EqualTo(allowed));
        [Test] public void InvalidPayloadsAndPhaseRejected()
        {
            Assert.That(SessionRules.Reject(null,0,true), Is.Not.Null);
            Assert.That(SessionRules.Reject(new byte[1024],0,true), Is.Not.Null);
            var bytes = SessionRules.Payload; bytes[0] ^= 1;
            Assert.That(SessionRules.Reject(bytes,0,true), Is.Not.Null);
            Assert.That(SessionRules.Reject(SessionRules.Payload,0,false), Is.Not.Null);
        }
        [TestCase("127.0.0.1","7777",true)] [TestCase("192.168.1.12","65535",true)]
        [TestCase("0.0.0.0","7777",false)] [TestCase("garbage","7777",false)]
        [TestCase("127.0.0.1","0",false)] [TestCase("127.0.0.1","65536",false)]
        public void ValidateEndpoint(string host,string port,bool valid) => Assert.That(SessionRules.Endpoint(host,port,out _),Is.EqualTo(valid));
        [Test] public void SequenceWrapAndReplay()
        {
            Assert.That(SessionRules.Newer(0,uint.MaxValue), Is.True);
            Assert.That(SessionRules.Newer(15,15), Is.False);
            Assert.That(SessionRules.Newer(14,15), Is.False);
            Assert.That(SessionRules.Newer(uint.MaxValue,0), Is.False);
        }
        [Test] public void NonFiniteAndExcessiveInputRejected()
        {
            Assert.That(SessionRules.ValidInput(Vector2.one,359,-80),Is.True);
            Assert.That(SessionRules.ValidInput(new Vector2(float.NaN,0),0,0),Is.False);
            Assert.That(SessionRules.ValidInput(Vector2.zero,float.PositiveInfinity,0),Is.False);
            Assert.That(SessionRules.ValidInput(Vector2.one*100,0,0),Is.False);
            Assert.That(SessionRules.ValidInput(Vector2.zero,0,91),Is.False);
        }
    }
}
