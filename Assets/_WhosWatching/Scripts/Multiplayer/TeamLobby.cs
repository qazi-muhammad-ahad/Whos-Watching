using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace WhosWatching
{
    // One scene NetworkObject owns shared choices. Only the server writes them.
    public sealed class TeamLobby : NetworkBehaviour
    {
        public static TeamLobby Instance { get; private set; }
        public NetworkVariable<byte> ColorA = new(0), ColorB = new(1);
        public NetworkVariable<bool> Playing = new(false);
        public bool InLobby => IsSpawned && !Playing.Value;
        public static bool GameplayAllowed => Instance && Instance.IsSpawned && (Instance.Playing.Value || ProbeBypass) &&
            (!ArenaGameplay.Instance || !ArenaGameplay.Instance.IsSpawned || !ArenaGameplay.Instance.Finished.Value);
        public static bool ProbeBypass {
            get {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return SmokeProbe.Active || GazeProbe.Active || ScreenGazeProbe.Active;
#else
                return false;
#endif
            }
        }
        void Awake() => Instance = this;
        public override void OnDestroy() { if (Instance == this) Instance = null; base.OnDestroy(); }
        public override void OnNetworkSpawn()
        {
            if (IsServer) { ColorA.Value = 0; ColorB.Value = 1; Playing.Value = false; }
        }
        public byte Index(TeamId team) => team == TeamId.B ? ColorB.Value : ColorA.Value;
        public Color Color(TeamId team) => team == TeamId.None ? TeamPalette.Neutral : TeamPalette.Color(Index(team));
        public NetworkPlayer Leader(TeamId team) => NetworkGameManager.Instance.Gaze.Players
            .Where(p => p && p.IsSpawned && p.Team.Value == team).OrderBy(p => p.OwnerClientId).FirstOrDefault();
        public bool IsLeader(NetworkPlayer player) => player && Leader(player.Team.Value) == player;
        public int Count(TeamId team) => NetworkGameManager.Instance.Gaze.Players.Count(p => p && p.IsSpawned && p.Team.Value == team);
        public static bool HasTeamSpace(TeamId team, int count) => (team == TeamId.A || team == TeamId.B) && count < 2;
        public bool ChangeTeam(NetworkPlayer player, TeamId team)
        {
            if (!IsServer || !InLobby || !player || !player.IsSpawned ||
                !NetworkGameManager.Instance.Gaze.Players.Contains(player)) return false;
            if (team == player.Team.Value) return true;
            if (!HasTeamSpace(team,Count(team))) return false;
            NetworkGameManager.Instance.Gaze.SetTeam(player,team);
            foreach (var p in NetworkGameManager.Instance.Gaze.Players) p.Ready.Value = false;
            return true;
        }
        public bool CanStart => InLobby && NetworkGameManager.Instance.Gaze.Players.Count >= 2 &&
            Leader(TeamId.A) && Leader(TeamId.B) && NetworkGameManager.Instance.Gaze.Players.All(p => p.Ready.Value);
        public bool ChooseColor(NetworkPlayer player, byte index)
        {
            if (!IsServer || !InLobby || !IsLeader(player) ||
                !TeamPalette.CanChoose(index, Index(player.Team.Value == TeamId.A ? TeamId.B : TeamId.A))) return false;
            if (Index(player.Team.Value) == index) return true;
            if (player.Team.Value == TeamId.A) ColorA.Value = index; else ColorB.Value = index;
            foreach (var p in NetworkGameManager.Instance.Gaze.Players.Where(p => p.Team.Value == player.Team.Value)) p.Ready.Value = false;
            return true;
        }
        public void StartGame()
        {
            if (!IsServer || !CanStart) return;
            Playing.Value = true;
            if (ArenaGameplay.Instance) ArenaGameplay.Instance.SpawnEveryone();
            NetworkGameManager.Instance.Gaze.ResetEffects();
        }
        public void ReturnToLobby()
        {
            if (!IsServer) return;
            Playing.Value = false;
            if (ArenaGameplay.Instance) ArenaGameplay.Instance.ResetRound();
            foreach (var p in NetworkGameManager.Instance.Gaze.Players) p.Ready.Value = false;
            NetworkGameManager.Instance.Gaze.ResetEffects();
        }
    }
}
