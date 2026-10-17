using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace WhosWatching
{
    // LAN session and pre-game appearance lobby.
    public sealed class NetworkGameManager : MonoBehaviour
    {
        public NetworkManager Network;
        public Camera MenuCamera;
        public GazeSystem Gaze;
        public string Status = "Offline";
        public CharacterId SelectedCharacter;
        Vector2 lobbyScroll;
        string address = "127.0.0.1", port = "7777";
        readonly Dictionary<ulong, double> reservations = new();
        bool connecting, stopping;
        double deadline;
        public static NetworkGameManager Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            if (!Gaze) Gaze = GetComponent<GazeSystem>();
            Application.runInBackground = true;
            Time.fixedDeltaTime = 1f / 30;
            Network.ConnectionApprovalCallback += Approve;
            Network.OnClientConnectedCallback += Connected;
            Network.OnClientDisconnectCallback += Disconnected;
        }
        void OnDestroy()
        {
            if (Network != null)
            {
                Network.ConnectionApprovalCallback -= Approve;
                Network.OnClientConnectedCallback -= Connected;
                Network.OnClientDisconnectCallback -= Disconnected;
            }
            if (Instance == this) Instance = null;
        }
        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            Prune();
            int occupied = Network.ConnectedClientsIds.Count;
            foreach (var id in reservations.Keys)
                if (!Network.ConnectedClients.ContainsKey(id)) occupied++;
            string rejection = SessionRules.Reject(request.Payload, occupied, !TeamLobby.Instance || !TeamLobby.Instance.IsSpawned || !TeamLobby.Instance.Playing.Value);
            response.Approved = rejection == null;
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            response.Reason = rejection ?? "";
            if (response.Approved)
            {
                reservations[request.ClientNetworkId] = Time.realtimeSinceStartupAsDouble + 10;
                response.Position = new Vector3((occupied % 2 == 0 ? -1 : 1) * 3, 1.1f, (occupied / 2) * 3);
            }
        }
        void Prune()
        {
            var expired = new List<ulong>();
            foreach (var pair in reservations)
                if (pair.Value <= Time.realtimeSinceStartupAsDouble) expired.Add(pair.Key);
            foreach (var id in expired) reservations.Remove(id);
        }
        void Connected(ulong id)
        {
            reservations.Remove(id);
            if (id == Network.LocalClientId) { connecting = false; Status = "Connected — choose your character and ready up"; }
            Debug.Log($"WW_CONNECTED id={id}");
        }
        void Disconnected(ulong id)
        {
            reservations.Remove(id);
            Debug.Log($"WW_DISCONNECTED id={id}");
            if (!Network.IsServer || id == Network.LocalClientId)
            {
                Status = string.IsNullOrEmpty(Network.DisconnectReason) ? "Disconnected." : Network.DisconnectReason;
                Stop(false);
            }
        }
        public void StartSession(bool host, string ip = null, string portText = null)
        {
            if (Network.IsListening || Network.ShutdownInProgress || connecting || stopping) return;
            address = ip ?? address; port = portText ?? port;
            if (!SessionRules.Endpoint(address, port, out var number)) { Status = "Enter a valid IPv4 address and port 1–65535."; return; }
            try
            {
                Network.GetComponent<UnityTransport>().SetConnectionData(address, number, host ? "0.0.0.0" : null);
                Network.NetworkConfig.ConnectionData = SessionRules.Payload;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-wwBadToken") >= 0)
                    Network.NetworkConfig.ConnectionData = new byte[] { 0 };
#endif
                connecting = true; deadline = Time.realtimeSinceStartupAsDouble + 10;
                Status = host ? "Hosting..." : "Connecting...";
                bool started = host ? Network.StartHost() : Network.StartClient();
                if (!started) { Status = "Could not start transport. Check port availability."; Stop(false); }
            }
            catch (Exception ex) { Status = "Connection failed: " + ex.Message; Stop(false); }
        }
        public void Stop(bool user = true)
        {
            if (user) Status = "Left session.";
            connecting = false; stopping = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (!Network.ShutdownInProgress) Network.Shutdown();
        }
        void Update()
        {
            if (connecting && Time.realtimeSinceStartupAsDouble >= deadline) { Status = "Connection timed out."; Stop(false); }
            if (stopping && !Network.IsListening && !Network.ShutdownInProgress)
            { stopping = false; reservations.Clear(); }
            if (MenuCamera) MenuCamera.gameObject.SetActive(!Network.IsConnectedClient || !TeamLobby.GameplayAllowed);
            var local = Network.LocalClient?.PlayerObject?.GetComponent<NetworkPlayer>();
            if (local && local.View) local.View.gameObject.SetActive(TeamLobby.GameplayAllowed);
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 380, Mathf.Min(Screen.height - 24, 720)), GUI.skin.box);
            lobbyScroll = GUILayout.BeginScrollView(lobbyScroll);
            GUILayout.Label("WHO'S WATCHING?  /  TEAM LOBBY");
            GUILayout.Label(Status);
            if (!Network.IsListening && !connecting && !stopping)
            {
                GUILayout.Label("Host IPv4 (127.0.0.1 for same-PC tests)"); address = GUILayout.TextField(address);
                GUILayout.Label("UDP port"); port = GUILayout.TextField(port);
                DrawCharacterChoice(null);
                if (GUILayout.Button("Host LAN")) StartSession(true);
                if (GUILayout.Button("Join LAN")) StartSession(false);
            }
            else if (GUILayout.Button(connecting ? "Cancel" : "Leave")) Stop();
            var lobby = TeamLobby.Instance;
            var local = Network.LocalClient?.PlayerObject?.GetComponent<NetworkPlayer>();
            if (local && lobby && lobby.InLobby)
            {
                DrawCharacterChoice(local);
                GUILayout.Space(8);
                GUILayout.Label("CHOOSE YOUR TEAM (2 players maximum)");
                GUILayout.BeginHorizontal();
                foreach (var team in new[] { TeamId.A, TeamId.B })
                {
                    int count = lobby.Count(team);
                    GUI.enabled = local.Team.Value != team && TeamLobby.HasTeamSpace(team,count);
                    if (GUILayout.Button($"{(local.Team.Value == team ? "✓ " : "")}Team {team}  {count}/2")) local.SelectTeamRpc(team);
                }
                GUI.enabled = true; GUILayout.EndHorizontal();
                GUILayout.Label("Switching teams clears everyone's Ready state.");
                GUILayout.Label($"TEAM {local.Team.Value}  /  {(lobby.IsLeader(local) ? "You are the team leader" : "Leader: Player " + lobby.Leader(local.Team.Value)?.OwnerClientId)}");
                GUILayout.Label("Team color — each team uses a different color");
                for (int row = 0; row < 2; row++)
                {
                    GUILayout.BeginHorizontal();
                    for (byte i = (byte)(row*3); i < row*3+3; i++)
                    {
                        byte opponent = lobby.Index(local.Team.Value == TeamId.A ? TeamId.B : TeamId.A);
                        GUI.enabled = lobby.IsLeader(local) && i != opponent;
                        GUI.backgroundColor = TeamPalette.Color(i);
                        if (GUILayout.Button((lobby.Index(local.Team.Value) == i ? "✓ " : "") + TeamPalette.Names[i])) local.SelectColorRpc(i);
                    }
                    GUI.backgroundColor = Color.white; GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
                GUILayout.Label("Changing a team's color clears that team's Ready state.");
                if (GUILayout.Button(local.Ready.Value ? "Ready ✓ — click to change" : "Ready up")) local.SetReadyRpc(!local.Ready.Value);
                if (Network.IsServer)
                {
                    GUI.enabled = lobby.CanStart;
                    if (GUILayout.Button("Start game")) lobby.StartGame();
                    GUI.enabled = true;
                }
                GUILayout.Label("Both teams need a player. Everyone must be ready.");
            }
            else if (Network.IsServer && lobby && lobby.Playing.Value && GUILayout.Button("Return everyone to lobby")) lobby.ReturnToLobby();
            GUILayout.Label("WASD move • Space jump • click arena • Esc releases");
            if (Gaze && Network.IsConnectedClient)
            {
                foreach (var p in Gaze.Players)
                {
                    GUILayout.BeginHorizontal();
                    string color = lobby ? TeamPalette.Names[lobby.Index(p.Team.Value)] : "";
                    GUILayout.Label($"P{p.OwnerClientId} · {p.Character.Value} · {p.Team.Value}/{color} {(lobby && lobby.IsLeader(p) ? "★" : "")} {(p.Ready.Value ? "Ready" : "")} ");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (Network.IsServer && TeamLobby.ProbeBypass && GUILayout.Button("Switch team (test)")) Gaze.SetTeam(p, p.Team.Value == TeamId.A ? TeamId.B : TeamId.A);
#endif
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        void DrawCharacterChoice(NetworkPlayer player)
        {
            GUILayout.Space(8); GUILayout.Label("CHOOSE YOUR CHARACTER");
            var selected = player ? player.Character.Value : SelectedCharacter;
            int next = GUILayout.SelectionGrid((int)selected, new[] { "Devil", "Ghost" }, 2);
            GUILayout.Label(next == 0 ? "A horned puff with a bouncy walk." : "A floating wisp with a rippling hem.");
            if (next != (int)selected)
            {
                SelectedCharacter = (CharacterId)next;
                if (player) player.SelectCharacterRpc(SelectedCharacter);
            }
        }
    }
}
