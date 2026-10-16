using System;
using Unity.Netcode;
using UnityEngine;

namespace WhosWatching
{
    public static class GazeRules
    {
        public static float Multiplier(int allies, int enemies, float support = 0.35f, float disruption = 0.25f, float min = 0.5f, float max = 1.5f) =>
            Mathf.Clamp(1 + Mathf.Max(0, allies) * support - Mathf.Max(0, enemies) * disruption, min, max);
        public const float NearClip = 0.05f;
        public const float MinAspect = 0.25f, MaxAspect = 4;
        public static bool ValidAspect(float aspect) => SessionRules.Finite(aspect) && aspect >= MinAspect && aspect <= MaxAspect;
        // Rectangular perspective frustum, not a circular aim cone. Position is relative to the authoritative eye/aim.
        public static bool InViewport(Vector3 local, float verticalFov, float aspect, float range)
        {
            if (!ValidAspect(aspect) || !SessionRules.Finite(verticalFov) || verticalFov <= 0 || verticalFov >= 180 ||
                !SessionRules.Finite(local.x) || !SessionRules.Finite(local.y) || !SessionRules.Finite(local.z) ||
                local.z < NearClip || local.sqrMagnitude > range * range) return false;
            float height = local.z * Mathf.Tan(verticalFov * Mathf.Deg2Rad * 0.5f);
            return Mathf.Abs(local.y) <= height && Mathf.Abs(local.x) <= height * aspect;
        }
    }
    // Separate acquisition timers: adding/removing one target must not reset the other visible targets.
    public sealed class GazePairs
    {
        readonly System.Collections.Generic.Dictionary<(ulong observer, ulong target), double> since = new();
        public bool Step(ulong observer, ulong target, bool visible, double now, float delay)
        {
            var pair = (observer, target);
            if (!visible || observer == target) { since.Remove(pair); return false; }
            if (!since.TryGetValue(pair, out double start)) since[pair] = start = now;
            return now - start >= delay;
        }
        public void Remove(ulong id)
        {
            var remove = new System.Collections.Generic.List<(ulong observer, ulong target)>();
            foreach (var pair in since.Keys) if (pair.observer == id || pair.target == id) remove.Add(pair);
            foreach (var pair in remove) since.Remove(pair);
        }
        public void Clear() => since.Clear();
    }
    public struct GazeState : INetworkSerializable, IEquatable<GazeState>
    {
        // Four-player admission cap means at most three outgoing targets. IDs may legitimately be zero.
        public int TargetCount, OutgoingAllies, OutgoingEnemies;
        public ulong Target0, Target1, Target2;
        public int Allies, Enemies;
        public float Move, Work;
        public static GazeState Neutral => new GazeState { Move = 1, Work = 1 };
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref TargetCount); s.SerializeValue(ref Target0); s.SerializeValue(ref Target1); s.SerializeValue(ref Target2);
            s.SerializeValue(ref OutgoingAllies); s.SerializeValue(ref OutgoingEnemies);
            s.SerializeValue(ref Allies); s.SerializeValue(ref Enemies);
            s.SerializeValue(ref Move); s.SerializeValue(ref Work);
        }
        public bool ContainsTarget(ulong id) => (TargetCount > 0 && Target0 == id) || (TargetCount > 1 && Target1 == id) || (TargetCount > 2 && Target2 == id);
        public bool AddTarget(ulong id, bool ally)
        {
            if (TargetCount >= 3 || ContainsTarget(id)) return false;
            if (TargetCount == 0) Target0 = id; else if (TargetCount == 1) Target1 = id; else Target2 = id;
            TargetCount++; if (ally) OutgoingAllies++; else OutgoingEnemies++;
            return true;
        }
        public bool Equals(GazeState other) => TargetCount == other.TargetCount && Target0 == other.Target0 && Target1 == other.Target1 && Target2 == other.Target2 &&
            OutgoingAllies == other.OutgoingAllies && OutgoingEnemies == other.OutgoingEnemies && Allies == other.Allies && Enemies == other.Enemies && Move == other.Move && Work == other.Work;
    }
}
