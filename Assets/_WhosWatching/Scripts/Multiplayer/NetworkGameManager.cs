using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace WhosWatching
{
    // Development connectivity harness. Match/lobby readiness is the next milestone.
    public sealed class NetworkGameManager : MonoBehaviour
    {
        public NetworkManager Network;
        public Camera MenuCamera;
        public GazeSystem Gaze;
        public string Status = "Offline";
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
            string rejection = SessionRules.Reject(request.Payload, occupied, true);
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
            if (id == Network.LocalClientId) { connecting = false; Status = "Connected — gaze test arena"; }
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
            if (MenuCamera) MenuCamera.gameObject.SetActive(!Network.IsConnectedClient);
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 370, 340), GUI.skin.box);
            GUILayout.Label("Who's Watching? — LAN development harness");
            GUILayout.Label(Status);
            if (!Network.IsListening && !connecting && !stopping)
            {
                GUILayout.Label("Host IPv4 (127.0.0.1 for same-PC tests)"); address = GUILayout.TextField(address);
                GUILayout.Label("UDP port"); port = GUILayout.TextField(port);
                if (GUILayout.Button("Host LAN")) StartSession(true);
                if (GUILayout.Button("Join LAN")) StartSession(false);
            }
            else if (GUILayout.Button(connecting ? "Cancel" : "Leave")) Stop();
            GUILayout.Label("WASD move • click arena to capture mouse • Esc releases");
            GUILayout.Label("All visible players receive gaze; walls block it.");
            if (Gaze && Network.IsConnectedClient)
            {
                foreach (var p in Gaze.Players)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Player {p.OwnerClientId}: Team {p.Team.Value}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (Network.IsServer && GUILayout.Button("Switch team (test)")) Gaze.SetTeam(p, p.Team.Value == TeamId.A ? TeamId.B : TeamId.A);
#endif
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndArea();
        }
    }
}
