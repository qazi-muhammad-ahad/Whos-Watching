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
        public string PlayerName = "Player";
        Vector2 lobbyScroll;
        string address = "127.0.0.1", port = "7777";
        readonly Dictionary<ulong, double> reservations = new();
        bool connecting, stopping;
        double deadline;
        public static NetworkGameManager Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            PlayerName = NetworkPlayer.CleanName(PlayerPrefs.GetString("WW.PlayerName", "Player"));
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
            PlayerPrefs.Save();
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
            using (GameUI.Begin())
            {
                GUI.depth=-10;
                var lobby=TeamLobby.Instance;
                var local=Network.LocalClient?.PlayerObject?.GetComponent<NetworkPlayer>();
                if(lobby && lobby.IsSpawned && lobby.Playing.Value)
                {
                    if(Cursor.lockState==CursorLockMode.Locked) return;
                    var menu=new Rect(GameUI.Width-300,20,280,255);GameUI.Card(menu);
                    GUILayout.BeginArea(new Rect(menu.x+16,menu.y+10,248,235));
                    GUILayout.Label("TAKE A BREATHER",GameUI.Heading);
                    GameUI.HudChoice();
                    GUILayout.Label("H switches HUD   /   click arena to play",GameUI.Small);
                    if(Network.IsServer && GameUI.Button("BACK TO LOBBY",GameUI.Gold)) lobby.ReturnToLobby();
                    if(GameUI.Button("LEAVE MATCH",GameUI.Cream)) Stop();
                    GUILayout.EndArea();return;
                }
                GameUI.Fill(new Rect(0,0,GameUI.Width,GameUI.Height),new Color(0.035f,0.045f,0.10f,0.83f),false);
                GUI.Label(new Rect(0,20,GameUI.Width,60),"WHO'S WATCHING?",GameUI.Title);
                GUI.Label(new Rect(0,80,GameUI.Width,30),"A LITTLE TEAMWORK. A LITTLE TROUBLE.",GameUI.Center);
                float left=GameUI.Width/2-480;
                GameUI.Card(new Rect(left,130,590,545));GameUI.Card(new Rect(left+610,130,350,545));
                GUILayout.BeginArea(new Rect(left+24,147,542,510));
                lobbyScroll=GUILayout.BeginScrollView(lobbyScroll);
                GUILayout.Label(local?"YOUR CREW, YOUR CALL":"LET'S MAKE SOME MISCHIEF",GameUI.Heading);
                GUILayout.Label(Status,GameUI.Small);
                if(!Network.IsListening && !connecting && !stopping)
                {
                    DrawCharacterChoice(null);
                    GUILayout.Space(12);GUILayout.Label("JOIN A FRIEND",GameUI.Heading);
                    GUILayout.Label("Host address",GameUI.Small);address=GUILayout.TextField(address);
                    GUILayout.Label("Port",GameUI.Small);port=GUILayout.TextField(port);
                    GUILayout.BeginHorizontal();
                    if(GameUI.Button("HOST A GAME",GameUI.Gold)) StartSession(true);
                    if(GameUI.Button("JOIN GAME",GameUI.Mint)) StartSession(false);
                    GUILayout.EndHorizontal();
                }
                else if(local && lobby && lobby.InLobby)
                {
                    DrawCharacterChoice(local);
                    GUILayout.Label("PICK YOUR SIDE",GameUI.Heading);
                    GUILayout.BeginHorizontal();
                    foreach(var team in new[]{TeamId.A,TeamId.B}) {
                        int count=lobby.Count(team);GUI.enabled=local.Team.Value!=team && TeamLobby.HasTeamSpace(team,count);
                        if(GameUI.Button($"{(local.Team.Value==team?"> ":"")}TEAM {team}    {count}/2",Color.Lerp(lobby.Color(team),Color.white,0.35f))) local.SelectTeamRpc(team);
                    }
                    GUI.enabled=true;GUILayout.EndHorizontal();
                    GUILayout.Label(lobby.IsLeader(local)?"Team leader: choose your crew's color":"Your team leader chooses the color",GameUI.Small);
                    GUILayout.BeginHorizontal();
                    for(byte i=0;i<TeamPalette.Count;i++) {
                        if(i==3) { GUILayout.EndHorizontal();GUILayout.BeginHorizontal(); }
                        GUI.enabled=lobby.IsLeader(local) && i!=lobby.Index(local.Team.Value==TeamId.A?TeamId.B:TeamId.A);
                        if(GameUI.Button((lobby.Index(local.Team.Value)==i?">":"")+TeamPalette.Names[i],Color.Lerp(TeamPalette.Color(i),Color.white,0.3f),GUILayout.Height(35))) local.SelectColorRpc(i);
                    }
                    GUI.enabled=true;GUILayout.EndHorizontal();
                    if(GameUI.Button(local.Ready.Value?"READY!  /  click to unready":"I'M READY!",GameUI.Mint)) local.SetReadyRpc(!local.Ready.Value);
                    if(Network.IsServer) {
                        GUI.enabled=lobby.CanStart;if(GameUI.Button("LET'S GO!",GameUI.Gold)) lobby.StartGame();GUI.enabled=true;
                    }
                    GUILayout.Label("Both teams need a player. Everyone must be ready.",GameUI.Small);
                }
                if(Network.IsListening || connecting || stopping) if(GameUI.Button(connecting?"CANCEL":"LEAVE LOBBY",GameUI.Cream)) Stop();
                GUILayout.EndScrollView();GUILayout.EndArea();
                GUILayout.BeginArea(new Rect(left+632,148,306,508));
                GUILayout.Label("EYES ON THE PRIZE",GameUI.Heading);
                GUILayout.Label("Carry crates. Build checkpoints.\nSteal the spotlight.");GUILayout.Space(8);
                GUILayout.Label("01   BACK YOUR BUDDY",GameUI.Heading);
                GUILayout.Label("Look at allies to boost them. Stare down rivals to slow them.",GameUI.Small);
                GUILayout.Label("02   BUILD & BOTHER",GameUI.Heading);
                GUILayout.Label("Finish your two checkpoints. Sabotage theirs. Save a crate for the middle!",GameUI.Small);
                GUILayout.Label("03   CLAIM THE CROWN",GameUI.Heading);
                GUILayout.Label("The middle wins it. Five minutes, then score or sudden-death overtime.",GameUI.Small);
                GUILayout.Space(9);GameUI.HudChoice();
                GUILayout.Label("H switches HUD. Essentials keeps names, scores and the timer.",GameUI.Small);
                if(Gaze && Network.IsConnectedClient) {
                    GUILayout.Space(8);GUILayout.Label("ROLL CALL",GameUI.Heading);
                    foreach(var p in Gaze.Players) GUILayout.Label($"{p.DisplayName.Value}  /  TEAM {p.Team.Value}  /  {(p.Ready.Value?"READY":"choosing...")}",GameUI.Small);
                }
                GUILayout.EndArea();
                GUI.Label(new Rect(0,GameUI.Height-32,GameUI.Width,25),"WASD  MOVE     SPACE  JUMP     F  PICK UP     E  WORK     CLICK  PUNCH",GameUI.Center);
            }
        }
        void DrawCharacterChoice(NetworkPlayer player)
        {
            GUILayout.Label("YOUR NAME", GameUI.Small);
            string nextName = GUILayout.TextField(PlayerName,16);
            if(nextName != PlayerName) {
                PlayerName=nextName; PlayerPrefs.SetString("WW.PlayerName",NetworkPlayer.CleanName(nextName));
                if(player) player.SetDisplayNameRpc(nextName);
            }
            GUILayout.Space(8); GUILayout.Label("CHOOSE YOUR LITTLE MENACE",GameUI.Heading);
            var selected = player ? player.Character.Value : SelectedCharacter;
            int next=(int)selected;
            GUILayout.BeginHorizontal();
            if(GameUI.Button((next==0?"> ":"")+"DEVIL",next==0?GameUI.Gold:GameUI.Cream)) next=0;
            if(GameUI.Button((next==1?"> ":"")+"GHOST",next==1?GameUI.Mint:GameUI.Cream)) next=1;
            GUILayout.EndHorizontal();
            GUILayout.Label(next == 0 ? "A horned puff with a bouncy walk." : "A floating wisp with a rippling hem.");
            if (next != (int)selected)
            {
                SelectedCharacter = (CharacterId)next;
                if (player) player.SelectCharacterRpc(SelectedCharacter);
            }
        }
    }
}


