using System.Collections.Generic;
using UnityEngine;

namespace WhosWatching
{
    public sealed class GazeSystem : MonoBehaviour
    {
        public GazeConfig Config;
        readonly List<NetworkPlayer> players = new();
        readonly GazePairs pairs = new();
        public IReadOnlyList<NetworkPlayer> Players => players;
        double next;
        public void Register(NetworkPlayer player)
        {
            if (players.Contains(player)) return;
            players.Add(player);
            if (player.IsServer)
            {
                int a = 0, b = 0;
                foreach (var p in players) { if (p.Team.Value == TeamId.A) a++; if (p.Team.Value == TeamId.B) b++; }
                player.Team.Value = a <= b ? TeamId.A : TeamId.B;
            }
        }
        public void Unregister(NetworkPlayer player)
        {
            players.Remove(player); pairs.Remove(player.NetworkObjectId);
            if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.Network.IsServer) Evaluate(Time.realtimeSinceStartupAsDouble);
        }
        public void ResetEffects()
        {
            pairs.Clear();
            foreach (var p in players)
            {
                if (p.IsSpawned && p.IsServer) p.Gaze.Value = GazeState.Neutral;
            }
        }
        public void SetTeam(NetworkPlayer player, TeamId team)
        {
            if (!player.IsServer || !players.Contains(player) || (team != TeamId.A && team != TeamId.B)) return;
            player.Team.Value = team; ResetEffects();
        }
        void Update()
        {
            var session = NetworkGameManager.Instance;
            if (!Config || session == null || !session.Network.IsServer) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < next) return;
            next = now + 1.0 / Config.EvaluationsPerSecond;
            Evaluate(now);
        }
        public void Evaluate(double now)
        {
            if (NetworkGameManager.Instance == null || !NetworkGameManager.Instance.Network.IsServer) return;
            Physics.SyncTransforms();
            var states = new Dictionary<NetworkPlayer, GazeState>();
            foreach (var player in players) states[player] = GazeState.Neutral;
            foreach (var observer in players)
            {
                foreach (var target in players)
                {
                    if (target == observer) continue;
                    bool visible = observer.IsSpawned && target.IsSpawned && observer.FreshInput &&
                        observer.Team.Value != TeamId.None && target.Team.Value != TeamId.None &&
                        GazeVisibility.CanSeeBody(observer.transform, observer.Eye.position, observer.AcceptedRotation,
                            target.transform, observer.CameraFieldOfView.Value, observer.AcceptedAspect, Config.Range, Config.Blockers);
                    if (!pairs.Step(observer.NetworkObjectId, target.NetworkObjectId, visible, now, Config.LockSeconds)) continue;
                    bool ally = observer.Team.Value == target.Team.Value;
                    var outgoing = states[observer];
                    if (!outgoing.AddTarget(target.NetworkObjectId, ally)) continue;
                    states[observer] = outgoing;
                    var incoming = states[target];
                    if (ally) incoming.Allies++; else incoming.Enemies++;
                    states[target] = incoming;
                }
            }
            foreach (var player in players)
            {
                var state = states[player];
                state.Move = GazeRules.Multiplier(state.Allies, state.Enemies, Config.MoveSupport, Config.MoveDisruption, Config.Minimum, Config.Maximum);
                state.Work = GazeRules.Multiplier(state.Allies, state.Enemies, Config.WorkSupport, Config.WorkDisruption, Config.Minimum, Config.Maximum);
                player.Gaze.Value = state;
            }
        }
    }
}
