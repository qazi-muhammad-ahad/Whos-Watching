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
                    if(ArenaGameplay.Instance && ArenaGameplay.Instance.Finished.Value) return;
                    if(Cursor.lockState==CursorLockMode.Locked) return;
                    var menu=new Rect(GameUI.Width-300,20,280,310);GameUI.Card(menu);
                    GameUI.Fill(new Rect(menu.x,menu.y,menu.width,4),GameUI.Mint,false);
                    GUILayout.BeginArea(new Rect(menu.x+16,menu.y+12,248,286));
                    GUILayout.Label("PAUSED",GameUI.Overline);
                    GUILayout.Label("TAKE A BREATHER",GameUI.Heading);
                    GameUI.HudChoice();
                    GUILayout.Label("H toggles HUD   /   click arena to resume",GameUI.Small);
                    if(Network.IsServer && GameUI.Button("BACK TO LOBBY",GameUI.Gold)) lobby.ReturnToLobby();
                    if(GameUI.Button("LEAVE MATCH",GameUI.Cream)) Stop();
                    GUILayout.EndArea();return;
                }
                GameUI.Fill(new Rect(0,0,GameUI.Width,GameUI.Height),new Color(0.015f,0.035f,0.065f,0.34f),false);
                float left=GameUI.Width/2-480;
                GameUI.Fill(new Rect(left,25,8,78),GameUI.Gold,false);
                GUI.Label(new Rect(left+22,19,540,22),"A TEAM ARENA GAME",GameUI.Overline);
                GUI.Label(new Rect(left+20,36,650,64),"WHO'S WATCHING?",GameUI.Title);
                GUI.Label(new Rect(left+23,96,600,23),"LOOK OUT FOR YOUR CREW. KEEP AN EYE ON THE RIVALS.",GameUI.Small);
                GameUI.Card(new Rect(left+737,36,223,48),new Color(0.035f,0.065f,0.09f,0.92f));
                GameUI.Fill(new Rect(left+737,36,4,48),GameUI.Mint,false);
                GUI.Label(new Rect(left+746,43,205,34),"LOCAL MULTIPLAYER",GameUI.Center);
                GameUI.Card(new Rect(left,120,590,555));GameUI.Card(new Rect(left+610,120,350,555));
                GameUI.Fill(new Rect(left,120,590,3),GameUI.Gold,false);
                GameUI.Fill(new Rect(left+610,120,350,3),GameUI.Mint,false);
                GUILayout.BeginArea(new Rect(left+24,139,542,524));
                lobbyScroll=GUILayout.BeginScrollView(lobbyScroll);
                GUILayout.Label(local?"READY ROOM":"START A SESSION",GameUI.Heading);
                GUILayout.Label(local?"Get your look sorted, choose a side and ready up.":"Host a room for your crew or join a friend.",GameUI.Small);
                if(!string.IsNullOrEmpty(Status) && Status!="Offline") GUILayout.Label(Status,GameUI.Small);
                if(!Network.IsListening && !connecting && !stopping)
                {
                    DrawCharacterChoice(null);
                    GUILayout.Space(10);GUILayout.Label("JOIN A FRIEND",GameUI.Heading);
                    GUILayout.Label("HOST ADDRESS",GameUI.Small);address=GUILayout.TextField(address);
                    GUILayout.Label("PORT",GameUI.Small);port=GUILayout.TextField(port);
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
                }
                if(Network.IsListening || connecting || stopping) if(GameUI.Button(connecting?"CANCEL":"LEAVE LOBBY",GameUI.Cream)) Stop();
                GUILayout.EndScrollView();GUILayout.EndArea();
                float guideX=left+632,guideW=306;
                GUI.Label(new Rect(guideX,140,guideW,22),"HOW TO PLAY",GameUI.Overline);
                GUI.Label(new Rect(guideX,160,guideW,34),"EYES UP, CREW.",GameUI.Heading);
                GUI.Label(new Rect(guideX,194,guideW,38),"Work together. Watch everything.",GameUI.Small);
                GameUI.Fill(new Rect(guideX,239,guideW,1),GameUI.Line,false);
                GameUI.Fill(new Rect(guideX,253,36,36),new Color(0.10f,0.20f,0.22f,1f));
                GUI.Label(new Rect(guideX,252,36,36),"01",GameUI.Step);
                GUI.Label(new Rect(guideX+48,249,258,32),"WATCH YOUR TEAM",GameUI.Heading);
                GUI.Label(new Rect(guideX+48,276,258,48),"Look at allies to boost them. Stare down rivals to slow them.",GameUI.Small);
                GameUI.Fill(new Rect(guideX,331,36,36),new Color(0.10f,0.20f,0.22f,1f));
                GUI.Label(new Rect(guideX,330,36,36),"02",GameUI.Step);
                GUI.Label(new Rect(guideX+48,327,258,32),"BUILD & BOTHER",GameUI.Heading);
                GUI.Label(new Rect(guideX+48,354,258,48),"Finish your checkpoints. Sabotage theirs. Save a crate for the middle.",GameUI.Small);
                GameUI.Fill(new Rect(guideX,409,36,36),new Color(0.10f,0.20f,0.22f,1f));
                GUI.Label(new Rect(guideX,408,36,36),"03",GameUI.Step);
                GUI.Label(new Rect(guideX+48,405,258,32),"CLAIM THE CROWN",GameUI.Heading);
                GUI.Label(new Rect(guideX+48,432,258,48),"The middle wins it. Five minutes, then sudden-death overtime.",GameUI.Small);
                GameUI.Fill(new Rect(guideX,493,guideW,1),GameUI.Line,false);
                if(GameUI.Button(new Rect(guideX,504,guideW,46),GameUI.Details?"HUD  /  DETAILED":"HUD  /  ESSENTIALS",GameUI.Cream)) GameUI.Details=!GameUI.Details;
                GUI.Label(new Rect(guideX,554,guideW,42),"Press H in a match to switch HUD detail.",GameUI.Small);
                if(Gaze && Network.IsConnectedClient) {
                    GUI.Label(new Rect(guideX,596,guideW,22),"IN THE ROOM",GameUI.Overline);
                    float rosterY=620;
                    foreach(var p in Gaze.Players) {
                        if(rosterY>651) break;
                        GUI.Label(new Rect(guideX,rosterY,guideW,22),$"{p.DisplayName.Value}  /  TEAM {p.Team.Value}  /  {(p.Ready.Value?"READY":"CHOOSING")}",GameUI.Small);
                        rosterY+=23;
                    }
                }
                GUI.Label(new Rect(0,GameUI.Height-31,GameUI.Width,24),"WASD MOVE     SPACE JUMP     F PICK UP     E WORK     CLICK PUNCH",GameUI.Center);
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
            if (next != (int)selected)
            {
                SelectedCharacter = (CharacterId)next;
                if (player) player.SelectCharacterRpc(SelectedCharacter);
            }
        }
    }
}


