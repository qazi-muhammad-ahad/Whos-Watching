using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WhosWatching
{
    // LAN session manager with console-quality title screen, lobby, settings, and how-to-play.
    public sealed class NetworkGameManager : MonoBehaviour
    {
        public NetworkManager Network;
        public Camera MenuCamera;
        public GazeSystem Gaze;
        public string Status = "Offline";
        public CharacterId SelectedCharacter;
        public string PlayerName = "Player";
        Vector2 lobbyScroll, settingsScroll, htpScroll;
        string address = "127.0.0.1", port = "7777";
        readonly Dictionary<ulong, double> reservations = new();
        bool connecting, stopping;
        double deadline;
        public static NetworkGameManager Instance { get; private set; }

        // 0=Title, 1=Lobby, 2=Settings, 3=HowToPlay
        int screen = 0;
        bool pauseSettingsOpen;
        int settingsTab; // 0=Graphics, 1=Audio, 2=Controls

        void Awake()
        {
            Instance = this;
            PlayerName = NetworkPlayer.CleanName(PlayerPrefs.GetString("WW.PlayerName", "Player"));
            if (!Gaze) Gaze = GetComponent<GazeSystem>();
            Application.runInBackground = true;
            Time.fixedDeltaTime = 1f / 30;
            GameSettings.Initialize();
            if (!FindAnyObjectByType<ObjectiveGuidance>())
            {
                var go = new GameObject("ObjectiveGuidanceManager");
                go.AddComponent<ObjectiveGuidance>();
            }
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
            if (id == Network.LocalClientId)
            { connecting = false; Status = "Connected — choose your character and ready up"; }
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
            if (!SessionRules.Endpoint(address, port, out var number))
            { Status = "Enter a valid IPv4 address and port 1–65535."; return; }
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
            pauseSettingsOpen = false;
            screen = 0;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (!Network.ShutdownInProgress) Network.Shutdown();
        }

        void Update()
        {
            if (connecting && Time.realtimeSinceStartupAsDouble >= deadline) { Status = "Connection timed out."; Stop(false); }
            if (stopping && !Network.IsListening && !Network.ShutdownInProgress) { stopping = false; reservations.Clear(); }
            if (MenuCamera) MenuCamera.gameObject.SetActive(!Network.IsConnectedClient || !TeamLobby.GameplayAllowed);
            var local = Network.LocalClient?.PlayerObject?.GetComponent<NetworkPlayer>();
            if (local && local.View) local.View.gameObject.SetActive(TeamLobby.GameplayAllowed);

            // Escape returns to Title screen when not in active match
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                var lobby = TeamLobby.Instance;
                bool inMatch = lobby && lobby.IsSpawned && lobby.Playing.Value;
                if (!inMatch && screen != 0)
                {
                    if (screen == 1 && (Network.IsListening || connecting)) Stop();
                    screen = 0;
                }
            }
        }

        // ═══════════════════════════════════════════════════
        //  OnGUI
        // ═══════════════════════════════════════════════════
        void OnGUI()
        {
            using (GameUI.Begin())
            {
                GUI.depth = -10;
                var lobby = TeamLobby.Instance;
                var local = Network.LocalClient?.PlayerObject?.GetComponent<NetworkPlayer>();

                // ── IN-MATCH: pause menu ──
                if (lobby && lobby.IsSpawned && lobby.Playing.Value)
                {
                    DrawPauseMenu(lobby, local);
                    return;
                }

                // ── PRE-GAME SCREENS ──
                switch (screen)
                {
                    case 0: DrawTitle(); break;
                    case 1: DrawLobby(lobby, local); break;
                    case 2: DrawSettings(); break;
                    case 3: DrawHowToPlay(); break;
                }
            }
        }

        // ═══════════════════════════════════════════════════
        //  TITLE SCREEN  (matches reference image exactly 100%)
        //  - Displays exact title artwork
        //  - Interactive buttons with glowing hover pulse
        //  - Click "START GAME" -> opens Lobby
        //  - Click "SETTINGS"   -> opens Settings
        //  - Click "HOW TO PLAY"-> opens How To Play
        // ═══════════════════════════════════════════════════
        void DrawTitle()
        {
            float W = GameUI.Width, H = GameUI.Height;
            var titleTex = GameUI.TitleScreenTexture;

            if (titleTex != null)
            {
                // Fit 1024:571 aspect ratio seamlessly across screen
                float targetAspect = 1024f / 571f;
                float screenAspect = W / H;
                Rect bgRect;
                if (screenAspect > targetAspect)
                {
                    float bh = W / targetAspect;
                    bgRect = new Rect(0, (H - bh) * 0.5f, W, bh);
                }
                else
                {
                    float bw = H * targetAspect;
                    bgRect = new Rect((W - bw) * 0.5f, 0, bw, H);
                }

                // Render the exact artwork from reference image
                GUI.DrawTexture(bgRect, titleTex, ScaleMode.StretchToFill);

                // Precise button locations from 1024x571 source image:
                // START GAME:   (192, 448) -> w: 201, h: 97
                // SETTINGS:     (400, 448) -> w: 226, h: 97
                // HOW TO PLAY:  (632, 448) -> w: 216, h: 97
                Rect rStart = new Rect(bgRect.x + bgRect.width * (192f / 1024f),
                                       bgRect.y + bgRect.height * (448f / 571f),
                                       bgRect.width * (201f / 1024f),
                                       bgRect.height * (97f / 571f));

                Rect rSettings = new Rect(bgRect.x + bgRect.width * (400f / 1024f),
                                          bgRect.y + bgRect.height * (448f / 571f),
                                          bgRect.width * (226f / 1024f),
                                          bgRect.height * (97f / 571f));

                Rect rHow = new Rect(bgRect.x + bgRect.width * (632f / 1024f),
                                     bgRect.y + bgRect.height * (448f / 571f),
                                     bgRect.width * (216f / 1024f),
                                     bgRect.height * (97f / 571f));

                if (GameUI.DrawTitleButton(rStart, GameUI.BtnStartTexture, new Color(0.2f, 0.7f, 1f)))
                    screen = 1;

                if (GameUI.DrawTitleButton(rSettings, GameUI.BtnSettingsTexture, new Color(1f, 0.45f, 0.2f)))
                    screen = 2;

                if (GameUI.DrawTitleButton(rHow, GameUI.BtnHowTexture, new Color(0.25f, 0.85f, 0.95f)))
                    screen = 3;
            }
            else
            {
                // Fallback procedural title screen
                GameUI.Fill(new Rect(0, H * 0.55f, W, H * 0.45f), new Color(0f, 0f, 0f, 0.35f), false);
                float logoW = 520, logoH = 150;
                float logoX = W * 0.5f - logoW * 0.5f;
                float logoY = H * 0.08f;
                GameUI.DrawLogo(new Rect(logoX, logoY, logoW, logoH), true);
                GUI.Label(new Rect(0, logoY + logoH + 8, W, 26), "WATCH YOUR BACK. STEAL THEIR STACK.", GameUI.Subtitle);

                float btnW = 280, btnH = 74, gap = 24;
                float totalW = btnW * 3 + gap * 2;
                float bx = W * 0.5f - totalW * 0.5f;
                float by = H - btnH - 55;

                if (GameUI.MenuButton(new Rect(bx, by, btnW, btnH), "\u0398", "START\nGAME", GameUI.HotOrange))
                    screen = 1;
                if (GameUI.MenuButton(new Rect(bx + btnW + gap, by, btnW, btnH), "\u2699", "SETTINGS", GameUI.NeonBlue))
                    screen = 2;
                if (GameUI.MenuButton(new Rect(bx + (btnW + gap) * 2, by, btnW, btnH), "?", "HOW TO\nPLAY", GameUI.Mint))
                    screen = 3;
            }

            // Keyboard navigation shortcuts
            if (Keyboard.current != null)
            {
                if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
                    screen = 1;
            }
        }

        // ═══════════════════════════════════════════════════
        //  LOBBY / PLAY
        // ═══════════════════════════════════════════════════
        void DrawLobby(TeamLobby lobby, NetworkPlayer local)
        {
            float W = GameUI.Width, H = GameUI.Height;

            // 1. Live 3D Scene Backdrop: Subtle cinematic vignette so 3D arena is fully visible
            GameUI.Fill(new Rect(0, 0, W, H), new Color(0.015f, 0.025f, 0.05f, 0.65f), false);

            // 2. Top Header Bar:
            // Back button in top-left
            if (GameUI.ModernButton(new Rect(28, 16, 120, 42), "<  BACK", GameUI.PanelLight))
            {
                if (!Network.IsListening && !connecting && !stopping) screen = 0;
                else Stop();
            }

            // Clean centered logo at top center
            GameUI.DrawLogo(new Rect(W * 0.5f - 160, 10, 320, 52), false);

            // Top-right LAN status badge
            float statW = 180, statH = 38;
            Rect rStat = new Rect(W - statW - 28, 18, statW, statH);
            GameUI.Fill(new Rect(rStat.x + 2, rStat.y + 2, rStat.width, rStat.height), new Color(0, 0, 0, 0.45f), false);
            GameUI.Fill(rStat, new Color(0.02f, 0.04f, 0.08f, 0.88f), false);
            GameUI.Fill(new Rect(rStat.x, rStat.y, 3, rStat.height), GameUI.Mint, false);
            GameUI.Fill(new Rect(rStat.x + 14, rStat.y + 14, 10, 10), GameUI.Mint, false);
            var statStyle = new GUIStyle(GameUI.BoldLabel) { fontSize = 12, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
            statStyle.normal.textColor = GameUI.Cream;
            GUI.Label(new Rect(rStat.x + 32, rStat.y + 9, rStat.width - 36, 20), "LAN MULTIPLAYER", statStyle);

            // 3. Two Main Modern Glass Cards (Left: Session / Crew, Right: Objectives & Roster)
            float totalW = 1040;
            float colGap = 26;
            float colW = (totalW - colGap) * 0.5f;
            float leftX = W * 0.5f - totalW * 0.5f;
            float rightX = leftX + colW + colGap;
            float panelY = 74;
            float panelH = H - panelY - 48;

            if (!Network.IsListening && !connecting && !stopping)
            {
                DrawPreSessionLobby(leftX, colW, rightX, colW, panelY, panelH);
            }
            else
            {
                DrawConnectedPanels(lobby, local, leftX, colW, rightX, colW, panelY, panelH);
            }

            // 4. Bottom Controls Strip
            float footerY = H - 32;
            GameUI.Fill(new Rect(W * 0.5f - 380, footerY, 760, 24), new Color(0.02f, 0.03f, 0.06f, 0.85f), false);
            GUI.Label(new Rect(0, footerY + 2, W, 20),
                "[WASD] MOVE   •   [SPACE] JUMP   •   [F] PICK UP   •   [E] WORK / SABOTAGE   •   [L-CLICK] PUNCH   •   [ESC] BACK",
                GameUI.Center);
        }

        void DrawPreSessionLobby(float lx, float lw, float rx, float rw, float py, float ph)
        {
            // ── LEFT PANEL: SESSION DEPLOYMENT ──
            GameUI.ModernPanel(new Rect(lx, py, lw, ph), GameUI.HotOrange);

            float cx = lx + 28;
            float cw = lw - 56;
            float cy = py + 22;

            // Header
            GUI.Label(new Rect(cx, cy, cw, 20), "OPERATIVE DEPLOYMENT", GameUI.Overline); cy += 24;
            GUI.Label(new Rect(cx, cy, cw, 28), "START A SESSION", GameUI.Heading); cy += 32;
            GUI.Label(new Rect(cx, cy, cw, 20), "Choose your hero and host or connect to a LAN server.", GameUI.Small); cy += 32;

            // Status notice (if connecting or error)
            if (!string.IsNullOrEmpty(Status) && Status != "Offline" && Status != "Left session.")
            {
                GameUI.Fill(new Rect(cx, cy, cw, 28), new Color(0.9f, 0.35f, 0.1f, 0.22f), false);
                GameUI.Fill(new Rect(cx, cy, 3, 28), GameUI.HotOrange, false);
                GUI.Label(new Rect(cx + 10, cy + 4, cw - 20, 22), Status, GameUI.BoldLabel);
                cy += 36;
            }

            // Section 1: YOUR IDENTITY
            GUI.Label(new Rect(cx, cy, cw, 20), "1. YOUR IDENTITY", GameUI.Overline); cy += 24;

            // Player Name Field
            Rect rName = new Rect(cx, cy, cw, 40);
            GameUI.Fill(new Rect(rName.x + 1, rName.y + 1, rName.width, rName.height), new Color(0, 0, 0, 0.45f), false);
            GameUI.Fill(rName, new Color(0.02f, 0.04f, 0.08f, 0.95f), false);
            Color inBorder = new Color(0.2f, 0.35f, 0.5f, 0.45f);
            GameUI.Fill(new Rect(rName.x, rName.y, rName.width, 1), inBorder, false);
            GameUI.Fill(new Rect(rName.x, rName.yMax - 1, rName.width, 1), inBorder, false);
            GameUI.Fill(new Rect(rName.x, rName.y, 1, rName.height), inBorder, false);
            GameUI.Fill(new Rect(rName.xMax - 1, rName.y, 1, rName.height), inBorder, false);

            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = Color.clear;
            string newName = GUI.TextField(new Rect(rName.x + 12, rName.y + 7, rName.width - 24, 26), PlayerName, 20, GameUI.ModernInput);
            if (newName != PlayerName)
            {
                PlayerName = newName;
                PlayerPrefs.SetString("WW.PlayerName", NetworkPlayer.CleanName(newName));
            }
            GUI.backgroundColor = oldBg;
            cy += 50;

            // Section 2: Choose Character
            GUI.Label(new Rect(cx, cy, cw, 20), "2. SELECT YOUR HERO", GameUI.Overline); cy += 24;
            float btnCharW = (cw - 14) * 0.5f;
            Rect rDevil = new Rect(cx, cy, btnCharW, 58);
            Rect rGhost = new Rect(cx + btnCharW + 14, cy, btnCharW, 58);

            if (GameUI.CharacterCard(rDevil, "DEVIL (SPEEDY)", "+Speed & Dash Burst", GameUI.HotOrange, SelectedCharacter == CharacterId.Devil))
            {
                SelectedCharacter = CharacterId.Devil;
                PlayerPrefs.SetInt("WW.SelectedChar", (int)SelectedCharacter);
            }

            if (GameUI.CharacterCard(rGhost, "GHOST (FLOATY)", "+Floaty High Jump", GameUI.Mint, SelectedCharacter == CharacterId.Ghost))
            {
                SelectedCharacter = CharacterId.Ghost;
                PlayerPrefs.SetInt("WW.SelectedChar", (int)SelectedCharacter);
            }
            cy += 68;

            // Section 3: HOST A GAME
            GUI.Label(new Rect(cx, cy, cw, 20), "3. HOST A GAME (LAN)", GameUI.Overline); cy += 24;
            Rect rHost = new Rect(cx, cy, cw, 46);
            if (GameUI.ModernButton(rHost, "HOST A GAME  (LAN)", GameUI.HotOrange))
            {
                StartSession(true);
            }
            cy += 56;

            // Section 4: DIRECT CONNECT
            GUI.Label(new Rect(cx, cy, cw, 20), "4. DIRECT CONNECT VIA IP", GameUI.Overline); cy += 24;

            float ipW = cw * 0.68f - 8;
            float portW = cw - ipW - 14;

            // IP
            Rect rIp = new Rect(cx, cy, ipW, 38);
            GameUI.Fill(rIp, new Color(0.02f, 0.04f, 0.08f, 0.95f), false);
            GameUI.Fill(new Rect(rIp.x, rIp.y, rIp.width, 1), inBorder, false);
            GameUI.Fill(new Rect(rIp.x, rIp.yMax - 1, rIp.width, 1), inBorder, false);
            GameUI.Fill(new Rect(rIp.x, rIp.y, 1, rIp.height), inBorder, false);
            GameUI.Fill(new Rect(rIp.xMax - 1, rIp.y, 1, rIp.height), inBorder, false);
            GUI.backgroundColor = Color.clear;
            address = GUI.TextField(new Rect(rIp.x + 10, rIp.y + 6, rIp.width - 20, 26), address, 24, GameUI.ModernInput);
            GUI.backgroundColor = oldBg;

            // Port
            Rect rPort = new Rect(cx + ipW + 14, cy, portW, 38);
            GameUI.Fill(rPort, new Color(0.02f, 0.04f, 0.08f, 0.95f), false);
            GameUI.Fill(new Rect(rPort.x, rPort.y, rPort.width, 1), inBorder, false);
            GameUI.Fill(new Rect(rPort.x, rPort.yMax - 1, rPort.width, 1), inBorder, false);
            GameUI.Fill(new Rect(rPort.x, rPort.y, 1, rPort.height), inBorder, false);
            GameUI.Fill(new Rect(rPort.xMax - 1, rPort.y, 1, rPort.height), inBorder, false);
            GUI.backgroundColor = Color.clear;
            port = GUI.TextField(new Rect(rPort.x + 10, rPort.y + 6, rPort.width - 20, 26), port, 8, GameUI.ModernInput);
            GUI.backgroundColor = oldBg;
            cy += 48;

            // Join Button
            Rect rJoin = new Rect(cx, cy, cw, 46);
            if (GameUI.ModernButton(rJoin, "JOIN GAME", GameUI.NeonBlue))
            {
                StartSession(false);
            }

            // ── RIGHT PANEL: MATCH OBJECTIVES & INTEL ──
            GameUI.ModernPanel(new Rect(rx, py, rw, ph), GameUI.Mint);

            float rcx = rx + 28;
            float rcw = rw - 56;
            float rcy = py + 22;

            GUI.Label(new Rect(rcx, rcy, rcw, 20), "TACTICAL INTEL", GameUI.Overline); rcy += 24;
            GUI.Label(new Rect(rcx, rcy, rcw, 28), "MATCH OBJECTIVES", GameUI.Heading); rcy += 32;
            GUI.Label(new Rect(rcx, rcy, rcw, 20), "Deliver 2 crates to stations then race to the center crown!", GameUI.Small); rcy += 32;

            // 4 Step Cards
            float stepH = 58, stepGap = 10;
            DrawStepCard(new Rect(rcx, rcy, rcw, stepH), "01", "WATCH & BOOST", "Stare at allies \u2192 grant 1.8x speed boost!", GameUI.Mint);
            rcy += stepH + stepGap;
            DrawStepCard(new Rect(rcx, rcy, rcw, stepH), "02", "DELIVER CRATES", "Pick up [F], carry to base, hold [E] to deposit.", GameUI.Gold);
            rcy += stepH + stepGap;
            DrawStepCard(new Rect(rcx, rcy, rcw, stepH), "03", "SABOTAGE RIVALS", "Sneak to enemy stations and hold [E] to sabotage!", GameUI.Coral);
            rcy += stepH + stepGap;
            DrawStepCard(new Rect(rcx, rcy, rcw, stepH), "04", "CLAIM THE CROWN", "When 2 crates are in, center crown unlocks. First wins!", GameUI.HotOrange);
            rcy += stepH + stepGap + 8;

            // HUD Toggle Card
            Rect rHud = new Rect(rcx, rcy, rcw, 44);
            string hudText = GameUI.Details ? "HUD MODE: FULL TACTICAL (FULL METERS)" : "HUD MODE: ESSENTIALS (CLEAN SCREEN)";
            Color hudCol = GameUI.Details ? GameUI.Mint : GameUI.PanelLight;
            if (GameUI.ModernButton(rHud, hudText, hudCol))
            {
                GameUI.Details = !GameUI.Details;
            }
            rcy += 54;

            // Pro Tip Banner
            Rect rTip = new Rect(rcx, rcy, rcw, 44);
            GameUI.Fill(rTip, new Color(0.04f, 0.08f, 0.12f, 0.88f), false);
            GameUI.Fill(new Rect(rTip.x, rTip.y, 3, rTip.height), GameUI.Mint, false);
            var tipStyle = new GUIStyle(GameUI.Small) { fontSize = 12, clipping = TextClipping.Overflow };
            tipStyle.normal.textColor = GameUI.Cream;
            GUI.Label(new Rect(rTip.x + 12, rTip.y + 12, rTip.width - 24, 20),
                "PRO TIP: Gaze speed boost makes carrying heavy crates much faster!", tipStyle);
        }

        void DrawConnectedPanels(TeamLobby lobby, NetworkPlayer local, float lx, float lw, float rx, float rw, float py, float ph)
        {
            // ── LEFT PANEL: READY ROOM ──
            GameUI.ModernPanel(new Rect(lx, py, lw, ph), GameUI.HotOrange);

            float cx = lx + 24;
            float cw = lw - 48;
            float cy = py + 16;

            GUI.Label(new Rect(cx, cy, cw, 26), "READY ROOM", GameUI.Heading);
            GUI.Label(new Rect(cx, cy + 24, cw, 20),
                connecting ? "Connecting to host session..." : "Pick your character, choose a side, then hit READY!", GameUI.Small);
            cy += 48;

            if (!string.IsNullOrEmpty(Status) && Status != "Offline")
            {
                GameUI.Fill(new Rect(cx, cy, cw, 26), new Color(0.9f, 0.35f, 0.1f, 0.22f), false);
                GameUI.Fill(new Rect(cx, cy, 3, 26), GameUI.HotOrange, false);
                GUI.Label(new Rect(cx + 8, cy + 3, cw - 16, 20), Status, GameUI.BoldLabel);
                cy += 32;
            }

            // Character choice in ready room
            GUI.Label(new Rect(cx, cy, cw, 20), "YOUR CHARACTER", GameUI.Overline);
            cy += 24;
            float btnCharW = (cw - 14) * 0.5f;
            var curChar = local ? local.Character.Value : SelectedCharacter;

            if (GameUI.CharacterCard(new Rect(cx, cy, btnCharW, 58), "DEVIL (SPEEDY)", "+Speed & Agility", GameUI.HotOrange, curChar == CharacterId.Devil))
            {
                SelectedCharacter = CharacterId.Devil;
                if (local) local.SelectCharacterRpc(SelectedCharacter);
            }

            if (GameUI.CharacterCard(new Rect(cx + btnCharW + 14, cy, btnCharW, 58), "GHOST (FLOATY)", "+Floaty High Jump", GameUI.Mint, curChar == CharacterId.Ghost))
            {
                SelectedCharacter = CharacterId.Ghost;
                if (local) local.SelectCharacterRpc(SelectedCharacter);
            }
            cy += 68;

            if (local && lobby && lobby.InLobby)
            {
                // Team Selection
                GUI.Label(new Rect(cx, cy, cw, 20), "PICK YOUR SIDE", GameUI.Overline);
                cy += 24;
                float teamW = (cw - 14) * 0.5f;
                foreach (var team in new[] { TeamId.A, TeamId.B })
                {
                    int cnt = lobby.Count(team);
                    bool hasSpace = TeamLobby.HasTeamSpace(team, cnt);
                    bool isMyTeam = local.Team.Value == team;
                    Rect rTeam = new Rect(team == TeamId.A ? cx : cx + teamW + 14, cy, teamW, 44);

                    Color tColor = lobby.Color(team);
                    string marker = isMyTeam ? "★ " : "";
                    if (GameUI.ModernButton(rTeam, $"{marker}TEAM {team}  ({cnt}/2)", isMyTeam ? tColor : GameUI.PanelLight, isMyTeam))
                    {
                        if (!isMyTeam && hasSpace) local.SelectTeamRpc(team);
                    }
                }
                cy += 54;

                // Color selector
                GUI.Label(new Rect(cx, cy, cw, 20),
                    lobby.IsLeader(local) ? "TEAM LEADER — PICK CREW COLOUR" : "Leader picks team colour", GameUI.Small);
                cy += 24;
                float chipW = (cw - 30) / 6f;
                for (byte i = 0; i < TeamPalette.Count; i++)
                {
                    Rect rChip = new Rect(cx + i * (chipW + 6), cy, chipW, 28);
                    bool isCur = lobby.Index(local.Team.Value) == i;
                    bool canPick = lobby.IsLeader(local) && i != lobby.Index(local.Team.Value == TeamId.A ? TeamId.B : TeamId.A);
                    Color c = TeamPalette.Color(i);

                    GameUI.Fill(rChip, c, false);
                    if (isCur)
                    {
                        GameUI.Fill(new Rect(rChip.x - 2, rChip.y - 2, rChip.width + 4, 2), Color.white, false);
                        GameUI.Fill(new Rect(rChip.x - 2, rChip.yMax, rChip.width + 4, 2), Color.white, false);
                        GameUI.Fill(new Rect(rChip.x - 2, rChip.y - 2, 2, rChip.height + 4), Color.white, false);
                        GameUI.Fill(new Rect(rChip.xMax, rChip.y - 2, 2, rChip.height + 4), Color.white, false);
                    }
                    if (canPick)
                    {
                        var oldBg = GUI.backgroundColor; GUI.backgroundColor = Color.clear;
                        if (GUI.Button(rChip, GUIContent.none, GUIStyle.none)) local.SelectColorRpc(i);
                        GUI.backgroundColor = oldBg;
                    }
                }
                cy += 38;

                // Ready Button
                bool isReady = local.Ready.Value;
                Rect rReady = new Rect(cx, cy, cw, 48);
                if (GameUI.ModernButton(rReady, isReady ? "✔  READY! (CLICK TO CANCEL)" : "★  I'M READY!",
                    isReady ? GameUI.Mint : GameUI.HotOrange))
                {
                    local.SetReadyRpc(!isReady);
                }
                cy += 54;

                // Start Match Button (Server)
                if (Network.IsServer)
                {
                    GUI.enabled = lobby.CanStart;
                    Rect rStart = new Rect(cx, cy, cw, 46);
                    if (GameUI.ModernButton(rStart, "▶  START MATCH!", GameUI.Gold))
                    {
                        lobby.StartGame();
                    }
                    GUI.enabled = true;
                    cy += 52;
                }
            }

            // Leave Button
            Rect rLeave = new Rect(cx, py + ph - 54, cw, 40);
            if (GameUI.ModernButton(rLeave, connecting ? "CANCEL CONNECTION" : "LEAVE LOBBY", GameUI.Coral))
            {
                Stop();
            }

            // ── RIGHT PANEL: MATCH BRIEFING & ROSTER ──
            GameUI.ModernPanel(new Rect(rx, py, rw, ph), GameUI.Mint);

            float rcx = rx + 22;
            float rcw = rw - 44;
            float rcy = py + 18;

            GUI.Label(new Rect(rcx, rcy, rcw, 16), "MATCH BRIEFING", GameUI.Overline);
            GUI.Label(new Rect(rcx, rcy + 18, rcw, 24), "EYES UP, CREW!", GameUI.Heading);
            rcy += 48;

            // Roster section
            GUI.Label(new Rect(rcx, rcy, rcw, 18), "PLAYERS IN ROOM", GameUI.Overline);
            rcy += 22;

            if (Gaze && Network.IsConnectedClient)
            {
                foreach (var p in Gaze.Players)
                {
                    Rect rPlayer = new Rect(rcx, rcy, rcw, 32);
                    Color tCol = lobby ? lobby.Color(p.Team.Value) : GameUI.Mint;
                    GameUI.Fill(rPlayer, new Color(0.04f, 0.07f, 0.12f, 0.85f), false);
                    GameUI.Fill(new Rect(rPlayer.x, rPlayer.y, 4, rPlayer.height), tCol, false);

                    string readyIcon = p.Ready.Value ? "✔ READY" : "WAITING";
                    Color readyCol = p.Ready.Value ? GameUI.Mint : GameUI.Muted;

                    var pStyle = new GUIStyle(GameUI.BoldLabel);
                    pStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(rPlayer.x + 12, rPlayer.y + 6, rPlayer.width - 100, 20),
                        $"{p.DisplayName.Value} (Team {p.Team.Value})", pStyle);

                    var rStyle = new GUIStyle(GameUI.Small);
                    rStyle.normal.textColor = readyCol;
                    rStyle.alignment = TextAnchor.MiddleRight;
                    GUI.Label(new Rect(rPlayer.xMax - 90, rPlayer.y + 6, 80, 20), readyIcon, rStyle);

                    rcy += 38;
                }
            }

            // Quick rules
            rcy += 10;
            GUI.Label(new Rect(rcx, rcy, rcw, 18), "CORE OBJECTIVES", GameUI.Overline);
            rcy += 22;
            DrawStepCard(new Rect(rcx, rcy, rcw, 44), "01", "WATCH & BOOST", "Look at allies for 1.8x speed!", GameUI.Mint);
            rcy += 50;
            DrawStepCard(new Rect(rcx, rcy, rcw, 44), "02", "DELIVER & BUILD", "Pick up [F], hold [E] at station.", GameUI.Gold);
            rcy += 50;
            DrawStepCard(new Rect(rcx, rcy, rcw, 44), "03", "CLAIM THE CROWN", "First team to crown WINS!", GameUI.HotOrange);
        }

        void DrawStepCard(Rect r, string num, string title, string desc, Color accent)
        {
            GameUI.Fill(new Rect(r.x + 2, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.40f), false);
            GameUI.Fill(r, new Color(0.04f, 0.07f, 0.12f, 0.90f), false);

            float bw = 46, bh = r.height;
            GameUI.Fill(new Rect(r.x, r.y, bw, bh), new Color(accent.r * 0.22f, accent.g * 0.22f, accent.b * 0.22f, 0.95f), false);
            GameUI.Fill(new Rect(r.x, r.y, 3, bh), accent, false);

            var badgeStyle = new GUIStyle(GameUI.Center)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Overflow
            };
            badgeStyle.normal.textColor = accent;
            GUI.Label(new Rect(r.x, r.y, bw, bh), num, badgeStyle);

            var titleStyle = new GUIStyle(GameUI.BoldLabel)
            {
                fontSize = 14,
                clipping = TextClipping.Overflow
            };
            titleStyle.normal.textColor = GameUI.Cream;
            GUI.Label(new Rect(r.x + bw + 14, r.y + 8, r.width - bw - 20, 22), title, titleStyle);

            var descStyle = new GUIStyle(GameUI.Small)
            {
                fontSize = 12,
                clipping = TextClipping.Overflow
            };
            descStyle.normal.textColor = GameUI.Muted;
            GUI.Label(new Rect(r.x + bw + 14, r.y + 30, r.width - bw - 20, 20), desc, descStyle);
        }

        void StepCard(float x, float y, float w, string num, string title, string desc, Color accent)
        {
            GameUI.Fill(new Rect(x, y, 36, 36), new Color(accent.r, accent.g, accent.b, 0.15f), false);
            GameUI.Fill(new Rect(x, y, 4, 36), accent, false);
            GUI.Label(new Rect(x, y, 36, 36), num, GameUI.Step);
            GUI.Label(new Rect(x + 44, y + 1, w - 48, 20), title, GameUI.BoldLabel);
            GUI.Label(new Rect(x + 44, y + 22, w - 48, 34), desc, GameUI.Small);
        }

        // ═══════════════════════════════════════════════════
        //  SETTINGS
        // ═══════════════════════════════════════════════════
        void DrawSettings()
        {
            float W = GameUI.Width, H = GameUI.Height;
            GameUI.Fill(new Rect(0, 0, W, H), new Color(0.015f, 0.025f, 0.05f, 0.70f), false);

            if (GameUI.ModernButton(new Rect(28, 16, 120, 42), "<  BACK", GameUI.PanelLight)) screen = 0;
            GameUI.DrawLogo(new Rect(W * 0.5f - 160, 10, 320, 52), false);

            float pw = 900, ph = H - 110;
            float px = W * 0.5f - pw * 0.5f, py = 74;
            GameUI.ModernPanel(new Rect(px, py, pw, ph), GameUI.Gold);

            // ── Sub-tabs ──
            float tw = 160, th = 42, tsx = px + 24, tsy = py + 16;
            if (GameUI.ModernButton(new Rect(tsx, tsy, tw, th), "GRAPHICS", settingsTab == 0 ? GameUI.Gold : GameUI.PanelLight, settingsTab == 0)) settingsTab = 0;
            if (GameUI.ModernButton(new Rect(tsx + tw + 10, tsy, tw, th), "AUDIO", settingsTab == 1 ? GameUI.Mint : GameUI.PanelLight, settingsTab == 1)) settingsTab = 1;
            if (GameUI.ModernButton(new Rect(tsx + (tw + 10) * 2, tsy, tw, th), "CONTROLS", settingsTab == 2 ? GameUI.NeonBlue : GameUI.PanelLight, settingsTab == 2)) settingsTab = 2;

            float cw = pw - 56;
            float ctx = px + 28, cty = py + 72, cth = ph - 130;

            GUILayout.BeginArea(new Rect(ctx, cty, cw, cth));
            settingsScroll = GUILayout.BeginScrollView(settingsScroll);

            if (settingsTab == 0) DrawGraphicsSettings(cw);
            else if (settingsTab == 1) DrawAudioSettings(cw);
            else DrawControlsSettings(cw);

            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            if (GameUI.Button("APPLY & SAVE", GameUI.Mint, GUILayout.Height(46), GUILayout.Width(220))) GameSettings.ApplyAllSettings();
            GUILayout.Space(12);
            if (GameUI.Button("RESTORE DEFAULTS", GameUI.Coral, GUILayout.Height(46), GUILayout.Width(220))) GameSettings.RestoreDefaults();
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawGraphicsSettings(float w)
        {
            GUILayout.Label("GRAPHICS CONFIGURATION", GameUI.Heading);
            GUILayout.Label("Resolution, display mode, and rendering quality.", GameUI.Small);
            GUILayout.Space(14);

            string[] resNames = new string[GameSettings.AvailableResolutions?.Length ?? 1];
            for (int i = 0; i < resNames.Length; i++)
            {
                if (GameSettings.AvailableResolutions != null && i < GameSettings.AvailableResolutions.Length)
                { var r = GameSettings.AvailableResolutions[i]; resNames[i] = $"{r.width} x {r.height}"; }
                else resNames[i] = $"{Screen.width} x {Screen.height}";
            }
            Rect rr = GUILayoutUtility.GetRect(w, 42);
            GameUI.CycleButton(rr, "Resolution", resNames, GameSettings.SelectedResolutionIndex, out int nr);
            if (nr != GameSettings.SelectedResolutionIndex) { GameSettings.SelectedResolutionIndex = nr; GameSettings.ApplyGraphics(); }

            GUILayout.Space(6);
            int cm = GameSettings.DisplayMode == FullScreenMode.ExclusiveFullScreen ? 0 :
                     GameSettings.DisplayMode == FullScreenMode.FullScreenWindow ? 1 : 2;
            Rect mr = GUILayoutUtility.GetRect(w, 42);
            GameUI.CycleButton(mr, "Display Mode", new[] { "Exclusive FS", "Borderless", "Windowed" }, cm, out int nm);
            if (nm != cm)
            {
                GameSettings.DisplayMode = nm == 0 ? FullScreenMode.ExclusiveFullScreen :
                                           nm == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                GameSettings.ApplyGraphics();
            }

            GUILayout.Space(6);
            Rect qr = GUILayoutUtility.GetRect(w, 42);
            GameUI.CycleButton(qr, "Quality", QualitySettings.names, GameSettings.QualityLevel, out int nq);
            if (nq != GameSettings.QualityLevel) { GameSettings.QualityLevel = nq; GameSettings.ApplyGraphics(); }

            GUILayout.Space(6);
            Rect vr = GUILayoutUtility.GetRect(w, 38);
            bool nv = GameUI.Toggle(vr, "VSync", GameSettings.VSync);
            if (nv != GameSettings.VSync) { GameSettings.VSync = nv; GameSettings.ApplyGraphics(); }
        }

        void DrawAudioSettings(float w)
        {
            GUILayout.Label("AUDIO CONFIGURATION", GameUI.Heading);
            GUILayout.Label("Adjust volume levels for sound effects and music.", GameUI.Small);
            GUILayout.Space(14);

            Rect sm = GUILayoutUtility.GetRect(w, 40);
            GameUI.Slider(sm, "Master Volume", GameSettings.MasterVolume, 0f, 1f, out float nmv);
            if (Mathf.Abs(nmv - GameSettings.MasterVolume) > 0.001f) { GameSettings.MasterVolume = nmv; GameSettings.ApplyAudio(); }

            GUILayout.Space(6);
            Rect mu = GUILayoutUtility.GetRect(w, 40);
            GameUI.Slider(mu, "Music Volume", GameSettings.MusicVolume, 0f, 1f, out float nmu);
            if (Mathf.Abs(nmu - GameSettings.MusicVolume) > 0.001f) { GameSettings.MusicVolume = nmu; GameSettings.ApplyAudio(); }

            GUILayout.Space(6);
            Rect sf = GUILayoutUtility.GetRect(w, 40);
            GameUI.Slider(sf, "SFX Volume", GameSettings.SFXVolume, 0f, 1f, out float nsf);
            if (Mathf.Abs(nsf - GameSettings.SFXVolume) > 0.001f) { GameSettings.SFXVolume = nsf; GameSettings.ApplyAudio(); }
        }

        void DrawControlsSettings(float w)
        {
            GUILayout.Label("CONTROLS & MOUSE", GameUI.Heading);
            GUILayout.Label("Fine-tune mouse sensitivity and review full combat controls.", GameUI.Small);
            GUILayout.Space(14);

            Rect sr = GUILayoutUtility.GetRect(w, 42);
            GameUI.Slider(sr, "Mouse Sensitivity", GameSettings.MouseSensitivity, 0.02f, 0.50f, out float ns);
            if (Mathf.Abs(ns - GameSettings.MouseSensitivity) > 0.001f) { GameSettings.MouseSensitivity = ns; GameSettings.SaveSettings(); }

            GUILayout.Space(8);
            Rect ir = GUILayoutUtility.GetRect(w, 40);
            bool ni = GameUI.Toggle(ir, "Invert Look Y", GameSettings.InvertY);
            if (ni != GameSettings.InvertY) { GameSettings.InvertY = ni; GameSettings.SaveSettings(); }

            GUILayout.Space(18);
            GameUI.SectionHeader("KEYBOARD & MOUSE BINDINGS", GameUI.NeonBlue, w);

            string[][] keys = {
                new[]{ "[W A S D]",      "Move Forward / Back / Strafe" },
                new[]{ "[SPACE]",        "Jump / High Leap" },
                new[]{ "[F]",            "Pick Up / Carry Energy Crate" },
                new[]{ "[Q]",            "Return Carried Crate To Base" },
                new[]{ "[E] Hold",       "Charge Base Station / Sabotage Enemy Base" },
                new[]{ "[L-CLICK]",      "Punch Knockback (Knocks crate from rivals)" },
                new[]{ "[H]",            "Toggle HUD Mode (Full Tactical / Minimal)" },
                new[]{ "[ESC]",          "Pause Menu / Unlock Cursor" },
            };

            foreach (var k in keys)
            {
                Rect kr = GUILayoutUtility.GetRect(w, 38);
                GameUI.Fill(new Rect(kr.x, kr.y + 2, kr.width, kr.height - 4), new Color(0.035f, 0.06f, 0.10f, 0.85f), false);
                GameUI.Fill(new Rect(kr.x, kr.y + 2, 3, kr.height - 4), GameUI.NeonBlue, false);

                var keyBadgeStyle = new GUIStyle(GameUI.BoldLabel)
                {
                    fontSize = 14,
                    clipping = TextClipping.Overflow,
                    alignment = TextAnchor.MiddleLeft
                };
                keyBadgeStyle.normal.textColor = GameUI.Gold;
                GUI.Label(new Rect(kr.x + 16, kr.y + 2, 160, kr.height - 4), k[0], keyBadgeStyle);

                var keyDescStyle = new GUIStyle(GameUI.Small)
                {
                    fontSize = 13,
                    clipping = TextClipping.Overflow,
                    alignment = TextAnchor.MiddleLeft
                };
                keyDescStyle.normal.textColor = GameUI.Cream;
                GUI.Label(new Rect(kr.x + 180, kr.y + 2, kr.width - 195, kr.height - 4), k[1], keyDescStyle);
                GUILayout.Space(4);
            }
        }

        // ═══════════════════════════════════════════════════
        //  HOW TO PLAY
        // ═══════════════════════════════════════════════════
        void DrawHowToPlay()
        {
            float W = GameUI.Width, H = GameUI.Height;
            GameUI.Fill(new Rect(0, 0, W, H), new Color(0.015f, 0.025f, 0.05f, 0.70f), false);

            if (GameUI.ModernButton(new Rect(28, 16, 120, 42), "<  BACK", GameUI.PanelLight)) screen = 0;
            GameUI.DrawLogo(new Rect(W * 0.5f - 160, 10, 320, 52), false);

            float pw = 900, ph = H - 110;
            float px = W * 0.5f - pw * 0.5f, py = 74;
            GameUI.ModernPanel(new Rect(px, py, pw, ph), GameUI.Mint);

            float cw = pw - 56;
            GUILayout.BeginArea(new Rect(px + 28, py + 20, cw, ph - 40));
            htpScroll = GUILayout.BeginScrollView(htpScroll);

            GUILayout.Label("HOW TO PLAY & WIN", GameUI.Heading);
            GUILayout.Label("A fast-paced LAN arena of speed, sabotage, and staring.", GameUI.Small);
            GUILayout.Space(14);

            DrawHowToPlaySection(cw, "1. THE GAZE SYSTEM", GameUI.Mint, new[] {
                "• ALLY BOOST — Look at your teammate to grant a massive 1.8x movement speed boost!",
                "• RIVAL STARE — Keep eyes on enemies to slow their sprint and track their moves.",
                "• Line of sight is essential — looking away cancels the speed boost instantly."
            });

            DrawHowToPlaySection(cw, "2. SUPPLY CRATES & BASE STATIONS", GameUI.Gold, new[] {
                "• Approach team energy crates and press [F] to hoist and carry them.",
                "• Haul crates to your team's base stations and HOLD [E] to charge and build.",
                "• Each team must charge 2 base stations to unlock the center crown forcefield."
            });

            DrawHowToPlaySection(cw, "3. INFILTRATION & SABOTAGE", GameUI.Coral, new[] {
                "• Sneak across the arena to enemy base stations and HOLD [E] to dismantle their power!",
                "• Sabotaged crates are kicked back out to the dispenser, setting rivals back.",
                "• In sudden-death overtime, a successful sabotage wins the match on the spot!"
            });

            DrawHowToPlaySection(cw, "4. RACE TO THE GOLDEN CROWN", GameUI.HotOrange, new[] {
                "• Once both base stations are fully powered, the arena center crown unlocks.",
                "• Sprint into the center platform — first player to reach the crown WINS!",
                "• If time expires with bases tied, sudden-death overtime begins immediately."
            });

            DrawHowToPlaySection(cw, "5. COMBAT & DEFENSE", GameUI.NeonBlue, new[] {
                "• [LEFT CLICK] to punch — a direct hit knocks opponents back and forces them to drop crates!",
                "• Punching has a short cooldown, so time your strikes carefully during pursuits.",
                "• Press [Q] to voluntarily drop your crate if you need to sprint or defend yourself."
            });

            GUILayout.Space(16);
            if (GameUI.Button("<  BACK TO MENU", GameUI.Mint, GUILayout.Height(46), GUILayout.Width(220))) screen = 0;

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawHowToPlaySection(float w, string title, Color accent, string[] bullets)
        {
            float cardH = 30 + bullets.Length * 24;
            Rect r = GUILayoutUtility.GetRect(w, cardH);
            GameUI.Fill(new Rect(r.x, r.y, r.width, r.height), new Color(0.03f, 0.05f, 0.09f, 0.85f), false);
            GameUI.Fill(new Rect(r.x, r.y, 3, r.height), accent, false);

            var titleStyle = new GUIStyle(GameUI.BoldLabel)
            {
                fontSize = 15,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = accent;
            GUI.Label(new Rect(r.x + 14, r.y + 6, r.width - 24, 20), title, titleStyle);

            float by = r.y + 28;
            var bulletStyle = new GUIStyle(GameUI.Small)
            {
                fontSize = 13,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.MiddleLeft
            };
            bulletStyle.normal.textColor = GameUI.Cream;

            foreach (var b in bullets)
            {
                GUI.Label(new Rect(r.x + 16, by, r.width - 28, 20), b, bulletStyle);
                by += 24;
            }
            GUILayout.Space(10);
        }

        // ═══════════════════════════════════════════════════
        //  PAUSE MENU (in-match)
        // ═══════════════════════════════════════════════════
        void DrawPauseMenu(TeamLobby lobby, NetworkPlayer local)
        {
            if (ArenaGameplay.Instance && ArenaGameplay.Instance.Finished.Value) return;
            if (Cursor.lockState == CursorLockMode.Locked) return;

            GameUI.Fill(new Rect(0, 0, GameUI.Width, GameUI.Height), new Color(0.01f, 0.02f, 0.04f, 0.55f), false);

            if (pauseSettingsOpen) { DrawPauseSettings(); return; }

            float mw = 340, mh = 400;
            Rect mr = new Rect(GameUI.Width * 0.5f - mw * 0.5f, GameUI.Height * 0.5f - mh * 0.5f, mw, mh);
            GameUI.Card(mr);
            GameUI.Fill(new Rect(mr.x, mr.y, mr.width, 4), GameUI.Mint, false);

            GameUI.DrawLogo(new Rect(mr.x + 18, mr.y + 10, mw - 36, 48));

            GUILayout.BeginArea(new Rect(mr.x + 22, mr.y + 66, mw - 44, mh - 82));
            GUILayout.Label("MATCH PAUSED", GameUI.Heading);
            GUILayout.Space(6);

            if (GameUI.Button("RESUME", GameUI.Mint, GUILayout.Height(44)))
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            GUILayout.Space(4);
            if (GameUI.Button("SETTINGS", GameUI.Gold, GUILayout.Height(44))) pauseSettingsOpen = true;
            GUILayout.Space(4);
            GameUI.HudChoice();
            GUILayout.Space(8);
            GUILayout.Label("[H] toggle HUD  \u2022  Click arena to resume", GameUI.Small);
            GUILayout.Space(8);

            if (Network.IsServer && GameUI.Button("BACK TO LOBBY", GameUI.Gold, GUILayout.Height(42)))
                lobby.ReturnToLobby();
            if (GameUI.Button("LEAVE MATCH", GameUI.Coral, GUILayout.Height(42))) Stop();
            GUILayout.EndArea();
        }

        void DrawPauseSettings()
        {
            float mw = 620, mh = 480;
            Rect r = new Rect(GameUI.Width * 0.5f - mw * 0.5f, GameUI.Height * 0.5f - mh * 0.5f, mw, mh);
            GameUI.Card(r);
            GameUI.Fill(new Rect(r.x, r.y, r.width, 4), GameUI.Gold, false);

            float w = r.width - 48;
            GUILayout.BeginArea(new Rect(r.x + 24, r.y + 16, w, r.height - 32));
            settingsScroll = GUILayout.BeginScrollView(settingsScroll);

            GUILayout.Label("IN-GAME SETTINGS", GameUI.Heading);
            GUILayout.Space(8);

            GameUI.SectionHeader("AUDIO", GameUI.Mint, w);
            Rect sm = GUILayoutUtility.GetRect(w, 32);
            GameUI.Slider(sm, "Master Volume", GameSettings.MasterVolume, 0f, 1f, out float nmv);
            if (Mathf.Abs(nmv - GameSettings.MasterVolume) > 0.001f) { GameSettings.MasterVolume = nmv; GameSettings.ApplyAudio(); }

            Rect mu = GUILayoutUtility.GetRect(w, 32);
            GameUI.Slider(mu, "Music Volume", GameSettings.MusicVolume, 0f, 1f, out float nmu);
            if (Mathf.Abs(nmu - GameSettings.MusicVolume) > 0.001f) { GameSettings.MusicVolume = nmu; GameSettings.ApplyAudio(); }

            Rect sf = GUILayoutUtility.GetRect(w, 32);
            GameUI.Slider(sf, "SFX Volume", GameSettings.SFXVolume, 0f, 1f, out float nsf);
            if (Mathf.Abs(nsf - GameSettings.SFXVolume) > 0.001f) { GameSettings.SFXVolume = nsf; GameSettings.ApplyAudio(); }

            GUILayout.Space(8);
            GameUI.SectionHeader("CONTROLS", GameUI.NeonBlue, w);
            Rect sr = GUILayoutUtility.GetRect(w, 32);
            GameUI.Slider(sr, "Mouse Sensitivity", GameSettings.MouseSensitivity, 0.02f, 0.50f, out float ns);
            if (Mathf.Abs(ns - GameSettings.MouseSensitivity) > 0.001f) { GameSettings.MouseSensitivity = ns; GameSettings.SaveSettings(); }

            Rect ir = GUILayoutUtility.GetRect(w, 32);
            bool ni = GameUI.Toggle(ir, "Invert Y", GameSettings.InvertY);
            if (ni != GameSettings.InvertY) { GameSettings.InvertY = ni; GameSettings.SaveSettings(); }

            GUILayout.Space(14);
            if (GameUI.Button("CLOSE & RESUME", GameUI.Gold, GUILayout.Height(44)))
            { GameSettings.SaveSettings(); pauseSettingsOpen = false; }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ═══════════════════════════════════════════════════
        //  CHARACTER CHOICE widget
        // ═══════════════════════════════════════════════════
        void DrawCharacterChoice(NetworkPlayer player)
        {
            GameUI.SectionHeader("YOUR IDENTITY", GameUI.Gold, 500);
            GUILayout.Label("PLAYER NAME", GameUI.Small);
            GUI.backgroundColor = GameUI.PanelLight;
            string next = GUILayout.TextField(PlayerName, 16);
            GUI.backgroundColor = Color.white;
            if (next != PlayerName)
            {
                PlayerName = next;
                PlayerPrefs.SetString("WW.PlayerName", NetworkPlayer.CleanName(next));
                if (player) player.SetDisplayNameRpc(next);
            }
            GUILayout.Space(6);
            GUILayout.Label("CHOOSE YOUR CHARACTER", GameUI.BoldLabel);
            var sel = player ? player.Character.Value : SelectedCharacter;
            int idx = (int)sel;
            GUILayout.BeginHorizontal();
            if (GameUI.Button((idx == 0 ? "\u25B6 " : "") + "DEVIL (SPEEDY)", idx == 0 ? GameUI.HotOrange : GameUI.PanelLight, GUILayout.Height(44))) idx = 0;
            if (GameUI.Button((idx == 1 ? "\u25B6 " : "") + "GHOST (FLOATY)", idx == 1 ? GameUI.NeonBlue : GameUI.PanelLight, GUILayout.Height(44))) idx = 1;
            GUILayout.EndHorizontal();
            if (idx != (int)sel)
            {
                SelectedCharacter = (CharacterId)idx;
                if (player) player.SelectCharacterRpc(SelectedCharacter);
            }
        }
    }
}
