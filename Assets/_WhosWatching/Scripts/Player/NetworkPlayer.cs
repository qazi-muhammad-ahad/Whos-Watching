using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WhosWatching
{
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public Camera View;
        public Transform VisualRoot;
        public GameObject DevilPrefab, GhostPrefab;
        public NetworkVariable<CharacterId> Character = new(CharacterId.Devil);
        public NetworkVariable<FixedString64Bytes> DisplayName = new(new FixedString64Bytes("Player"));
        public NetworkVariable<bool> Ready = new(false);
        public NetworkVariable<float> AnimationSpeed = new(0);
        public NetworkVariable<bool> Airborne = new(false);
        public const float JumpHeight = 1.25f;
        public NetworkVariable<bool> Working = new(false);
        public NetworkVariable<uint> PunchSequence = new(0), HitSequence = new(0), RespawnSequence = new(0);
        public NetworkVariable<uint> JumpSequence = new(0);
        public bool HeldWork => heldWork;
        public float PunchMultiplier => GazeRules.Multiplier(Gaze.Value.Allies, Gaze.Value.Enemies);
        Vector3 knockback;
        double lastPunch = double.NegativeInfinity, invulnerableUntil;
        bool jumpRequested;
        double lastJump = double.NegativeInfinity;
        GameObject characterVisual;
        bool submittedAppearance;
        public Transform Eye, Chest;
        public NetworkVariable<TeamId> Team = new(TeamId.None);
        public NetworkVariable<GazeState> Gaze = new(GazeState.Neutral);
        public NetworkVariable<float> WorkProgress = new(0);
        public NetworkVariable<uint> WorkCycles = new(0);
        public bool FreshInput => TeamLobby.GameplayAllowed && inputEnabled && Time.realtimeSinceStartupAsDouble - lastInput <= 0.25;
        public NetworkVariable<float> CameraFieldOfView = new(60);
        public Quaternion AcceptedRotation => Quaternion.Euler(acceptedPitch, acceptedYaw, 0);
        public float AcceptedAspect { get; private set; } = 16f / 9;
        bool inputEnabled, heldWork;
        float acceptedPitch;
        CharacterController controller;
        Vector2 move;
        float yaw, pitch, acceptedYaw, vertical;
        uint sequence, lastSequence;
        bool received;
        double nextSend, lastInput = double.NegativeInfinity;
        public override void OnNetworkSpawn()
        {
            submittedAppearance = false;
            controller = GetComponent<CharacterController>();
            controller.enabled = IsServer;
            if (!Eye) Eye = transform.Find("EyeAnchor");
            if (!Chest) Chest = transform.Find("ChestAnchor");
            Team.OnValueChanged += TeamChanged;
            Character.OnValueChanged += CharacterChanged;
            NetworkGameManager.Instance.Gaze.Register(this);
            RefreshCharacter();
            View.gameObject.SetActive(IsOwner);
            if (IsOwner) VisualRoot.gameObject.SetActive(false);
            if (IsOwner && !View.GetComponent<ArenaHands>()) View.gameObject.AddComponent<ArenaHands>();
            if (IsServer && ArenaGameplay.Instance && ArenaGameplay.Instance.IsSpawned) ArenaGameplay.Instance.Respawn(this);
            Debug.Log($"WW_SPAWN owner={OwnerClientId} local={IsOwner} server={IsServer}");
        }
        public override void OnNetworkDespawn()
        {
            Team.OnValueChanged -= TeamChanged;
            Character.OnValueChanged -= CharacterChanged;
            if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.Gaze.Unregister(this);
            if (IsOwner) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            // Scene objects can spawn after the host player. Wait for the lobby before
            // submitting the offline choice, otherwise its phase validation rejects it.
            if (!submittedAppearance && TeamLobby.Instance && TeamLobby.Instance.InLobby)
            {
                submittedAppearance = true;
                SelectCharacterRpc(NetworkGameManager.Instance.SelectedCharacter);
                SetDisplayNameRpc(NetworkGameManager.Instance.PlayerName);
            }
            if (!TeamLobby.GameplayAllowed) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ArenaProbe.Active || SmokeProbe.Active || GazeProbe.Active || ScreenGazeProbe.Active || AppearanceProbe.Active || JumpProbe.Active) return;
#endif
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            if (keyboard.hKey.wasPressedThisFrame) GameUI.Details = !GameUI.Details;
            if (keyboard.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            // Avoid capturing clicks on the temporary menu.
            if (mouse.leftButton.wasPressedThisFrame && !GameUI.PointerOverMenu())
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            bool active = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            Vector2 direction = Vector2.zero;
            if (active)
            {
                direction = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                var delta = mouse.delta.ReadValue();
                yaw = Mathf.Repeat(yaw + delta.x * 0.12f, 360);
                pitch = Mathf.Clamp(pitch - delta.y * 0.12f, -80, 80);
            }
            if (Time.unscaledTimeAsDouble >= nextSend)
            {
                nextSend = Time.unscaledTimeAsDouble + 0.05;
                SubmitRpc(++sequence, direction, yaw, pitch, active, active && keyboard.eKey.isPressed, LocalAspect());
            }
            if (active && keyboard.spaceKey.wasPressedThisFrame) JumpRpc();
            if (active && mouse.leftButton.wasPressedThisFrame) PunchRpc();
            if (active && keyboard.fKey.wasPressedThisFrame) PickupRpc();
            if (active && keyboard.qKey.wasPressedThisFrame) ReturnCrateRpc();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public NetworkVariable<int> ProbeStage = new(-1);
        public void SendTestInput(Vector2 direction, float lookYaw = 0, float lookPitch = 0, bool enabled = true, bool work = false, float aspect = 16f / 9)
        {
            if (IsSpawned && IsOwner) SubmitRpc(++sequence, direction, lookYaw, lookPitch, enabled, work, aspect);
        }
        public void TestTeleport(Vector3 position)
        {
            if (!IsServer) return;
            controller.enabled = false; transform.position = position; controller.enabled = true;
            GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(position, Quaternion.identity, Vector3.one);
            move = Vector2.zero; vertical = 0; inputEnabled = false; lastInput = double.NegativeInfinity;
        }
#endif
        void LateUpdate()
        {
            if (IsSpawned && IsOwner)
            {
                View.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
                View.fieldOfView = CameraFieldOfView.Value;
                View.nearClipPlane = GazeRules.NearClip;
                View.aspect = LocalAspect();
            }
        }
        float LocalAspect() => Mathf.Clamp((float)View.pixelWidth / Mathf.Max(1, View.pixelHeight), GazeRules.MinAspect, GazeRules.MaxAspect);
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void JumpRpc()
        {
            if (!FreshInput || !controller || !controller.isGrounded || Time.realtimeSinceStartupAsDouble-lastJump < 0.25) return;
            jumpRequested = true;
        }
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
        void SubmitRpc(uint serial, Vector2 direction, float lookYaw, float lookPitch, bool enabled, bool work, float aspect, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || !SessionRules.ValidInput(direction, lookYaw, lookPitch) || !GazeRules.ValidAspect(aspect)) return;
            if (received && !SessionRules.Newer(serial, lastSequence)) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - lastInput < 0.015) return;
            received = true; lastSequence = serial; lastInput = now;
            move = Vector2.ClampMagnitude(direction, 1); acceptedYaw = Mathf.Repeat(lookYaw, 360);
            acceptedPitch = Mathf.Clamp(lookPitch, -80, 80); inputEnabled = enabled; heldWork = enabled && work;
            AcceptedAspect = aspect;
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            CameraFieldOfView.Value = NetworkGameManager.Instance.Gaze.Config.VerticalFieldOfView;
            bool fresh = FreshInput;
            transform.rotation = Quaternion.Euler(0, acceptedYaw, 0);
            Vector3 velocity = fresh ? transform.rotation * new Vector3(move.x, 0, move.y) * (5 * Gaze.Value.Move) : Vector3.zero;
            if (jumpRequested && fresh && controller.isGrounded)
            {
                vertical = Mathf.Sqrt(-2 * Physics.gravity.y * JumpHeight);
                lastJump = Time.realtimeSinceStartupAsDouble;
                JumpSequence.Value++;
            }
            else vertical = controller.isGrounded && vertical <= 0 ? -2 : vertical + Physics.gravity.y * Time.fixedDeltaTime;
            jumpRequested = false;
            velocity += knockback;
            knockback = Vector3.MoveTowards(knockback, Vector3.zero, 18 * Time.fixedDeltaTime);
            velocity.y += vertical;
            var collision = controller.Move(velocity * Time.fixedDeltaTime);
            if ((collision & CollisionFlags.Above) != 0 && vertical > 0) vertical = 0;
            Airborne.Value = !controller.isGrounded;
            var actual = controller.velocity; actual.y = 0;
            AnimationSpeed.Value = Mathf.Clamp(actual.magnitude, 0, 10);
            // Development work harness. Production objectives will validate range/team/LOS separately.
            if (!ArenaGameplay.Instance && fresh && heldWork)
            {
                WorkProgress.Value += Time.fixedDeltaTime * Gaze.Value.Work / 5;
                if (WorkProgress.Value >= 1) { WorkProgress.Value = 0; WorkCycles.Value++; }
            }
            if (!ArenaGameplay.Instance && transform.position.y < -10) ServerRespawn(new Vector3(0, 2, 0), 0);
        }
        public void ServerRespawn(Vector3 position, float facing)
        {
            if (!IsServer || !controller) return;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, facing, 0));
            controller.enabled = true;
            GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(position, transform.rotation, Vector3.one);
            move = Vector2.zero; vertical = 0; knockback = Vector3.zero; jumpRequested = false;
            acceptedYaw = facing; inputEnabled = false; heldWork = false; lastInput = double.NegativeInfinity;
            Airborne.Value = false; WorkProgress.Value = 0; Working.Value = false;
            invulnerableUntil = Time.realtimeSinceStartupAsDouble + 1.25;
            RespawnSequence.Value++;
            FaceSpawnRpc(facing);
        }
        [Rpc(SendTo.Owner)]
        void FaceSpawnRpc(float facing) { yaw = facing; pitch = 0; }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void PickupRpc() { if (ArenaGameplay.Instance) ArenaGameplay.Instance.Pickup(this); }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void ReturnCrateRpc() { if (FreshInput && ArenaGameplay.Instance) ArenaGameplay.Instance.Release(this); }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void PunchRpc()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!FreshInput || now - lastPunch < 0.55) return;
            lastPunch = now; PunchSequence.Value++;
            NetworkPlayer closest = null; float distance = 2.3f;
            Vector3 forward = AcceptedRotation * Vector3.forward;
            foreach (var target in NetworkGameManager.Instance.Gaze.Players)
            {
                if (target == this || target.Team.Value == Team.Value || now < target.invulnerableUntil) continue;
                Vector3 delta = target.Chest.position - Eye.position;
                if (delta.magnitude > distance || Vector3.Dot(forward, delta.normalized) < 0.5f) continue;
                if (Physics.Raycast(Eye.position, delta.normalized, delta.magnitude, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore)) continue;
                closest = target; distance = delta.magnitude;
            }
            if (!closest) return;
            Vector3 direction = closest.transform.position - transform.position; direction.y = 0;
            closest.knockback = direction.normalized * (9 * PunchMultiplier);
            closest.vertical = Mathf.Max(closest.vertical, 2.5f * PunchMultiplier);
            closest.HitSequence.Value++;
            if (ArenaGameplay.Instance) ArenaGameplay.Instance.Release(closest);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SelectCharacterRpc(CharacterId character)
        {
            if (!TeamLobby.Instance || !TeamLobby.Instance.InLobby || !TeamPalette.Valid(character)) return;
            Character.Value = character; Ready.Value = false;
        }
        public static string CleanName(string value)
        {
            var result = new System.Text.StringBuilder();
            foreach (char c in value ?? "")
            {
                if (result.Length >= 16) break;
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-') result.Append(c);
            }
            string name = result.ToString().Trim();
            return name.Length == 0 ? "Player" : name;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SetDisplayNameRpc(string value)
        {
            if (!TeamLobby.Instance || !TeamLobby.Instance.InLobby || value == null || value.Length > 64) return;
            DisplayName.Value = new FixedString64Bytes(CleanName(value));
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SelectColorRpc(byte index) => TeamLobby.Instance.ChooseColor(this,index);
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SelectTeamRpc(TeamId team)
        {
            if (TeamLobby.Instance) TeamLobby.Instance.ChangeTeam(this,team);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SetReadyRpc(bool ready)
        {
            if (TeamLobby.Instance && TeamLobby.Instance.InLobby) Ready.Value = ready;
        }
        void TeamChanged(TeamId oldValue, TeamId newValue)
        {
            if (IsServer) {
                Ready.Value = false;
                if (ArenaGameplay.Instance && ArenaGameplay.Instance.IsSpawned) ArenaGameplay.Instance.Respawn(this);
            }
            foreach (var tint in VisualRoot.GetComponentsInChildren<TeamTint>(true)) tint.Apply();
        }
        void CharacterChanged(CharacterId oldValue, CharacterId newValue) => RefreshCharacter();
        void RefreshCharacter()
        {
            if (characterVisual) { characterVisual.SetActive(false); Destroy(characterVisual); }
            var prefab = Character.Value == CharacterId.Ghost ? GhostPrefab : DevilPrefab;
            if (!prefab) return;
            characterVisual = Instantiate(prefab, VisualRoot);
            characterVisual.transform.localPosition = new Vector3(0,-1,0);
            characterVisual.transform.localRotation = prefab.transform.localRotation;
            characterVisual.transform.localScale = prefab.transform.localScale;
        }
        void OnGUI()
        {
            if (!IsSpawned || !TeamLobby.GameplayAllowed) return;
            if (!IsOwner) { DrawNameplate(); return; }
            using (GameUI.Begin())
            {
                GameUI.Reticle();if(!GameUI.Details)return;
                var state=Gaze.Value;var arena=ArenaGameplay.Instance;
                float y=GameUI.Height-150;
                GameUI.Card(new Rect(24,y,300,136));
                GUILayout.BeginArea(new Rect(40,y+9,268,118));
                GUILayout.Label($"{DisplayName.Value}  /  TEAM {Team.Value}",GameUI.Heading);
                GUILayout.Label($"{state.Allies} CHEERING YOU ON   /   {state.Enemies} WATCHING",GameUI.Small);
                GUILayout.Label($"MOVE {state.Move:F1}x   WORK {state.Work:F1}x   PUNCH {PunchMultiplier:F1}x",GameUI.Small);
                GUILayout.Label($"Supporting {state.OutgoingAllies}  /  Disrupting {state.OutgoingEnemies}",GameUI.Small);
                GUILayout.EndArea();
                GameUI.Card(new Rect(GameUI.Width/2-225,y+7,450,108));
                bool carrying=arena && arena.Carrying(this)>=0;
                GUI.Label(new Rect(GameUI.Width/2-210,y+13,420,26),Working.Value?"KEEP GOING!":carrying?"SPECIAL DELIVERY!":"MAKE YOUR MOVE",GameUI.Center);
                GUI.Label(new Rect(GameUI.Width/2-210,y+42,420,24),carrying?"Hold E at your checkpoint  /  Q returns crate":"F picks up a crate  /  hold E to sabotage",GameUI.Small);
                GameUI.Fill(new Rect(GameUI.Width/2-206,y+81,412,10),GameUI.Ink);
                if(WorkProgress.Value>0)GameUI.Fill(new Rect(GameUI.Width/2-206,y+81,412*Mathf.Clamp01(WorkProgress.Value),10),GameUI.Gold);
                GameUI.Card(new Rect(GameUI.Width-224,y,200,136));
                GUILayout.BeginArea(new Rect(GameUI.Width-208,y+9,168,118));
                  GUILayout.Label("CONTROLS",GameUI.Heading);
                  GUILayout.Label("H  HUD   /   ESC  PAUSE\nSPACE  JUMP   /   CLICK  PUNCH",GameUI.Small);
                GUILayout.EndArea();
            }
        }
        void DrawNameplate()
        {
            var local = NetworkManager.LocalClient?.PlayerObject;
            var owner = local ? local.GetComponent<NetworkPlayer>() : null;
            var camera = owner ? owner.View : null;
            if (!camera || !camera.isActiveAndEnabled) return;
            Vector3 point = transform.position + Vector3.up * 1.4f;
            Vector3 target = Chest ? Chest.position : transform.position;
            if ((camera.transform.position-target).sqrMagnitude > 28*28 ||
                Physics.Linecast(camera.transform.position,target,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore)) return;
            Vector3 screen = camera.WorldToScreenPoint(point);
            if (screen.z <= 0) return;
            using (GameUI.Begin())
            {
                float x = screen.x / GameUI.Scale, y = (Screen.height-screen.y) / GameUI.Scale;
                GameUI.Card(new Rect(x-95,y-30,190,29));
                GameUI.Fill(new Rect(x-89,y-23,6,15),TeamLobby.Instance.Color(Team.Value));
                GUI.Label(new Rect(x-79,y-29,166,27),DisplayName.Value.ToString(),GameUI.Center);
            }
        }
    }
}
