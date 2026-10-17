using Unity.Netcode;
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
        public NetworkVariable<bool> Ready = new(false);
        public NetworkVariable<float> AnimationSpeed = new(0);
        public NetworkVariable<bool> Airborne = new(false);
        public const float JumpHeight = 1.25f;
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
            }
            if (!TeamLobby.GameplayAllowed) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (SmokeProbe.Active || GazeProbe.Active || ScreenGazeProbe.Active || AppearanceProbe.Active || JumpProbe.Active) return;
#endif
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            // Avoid capturing clicks on the temporary menu.
            if (mouse.leftButton.wasPressedThisFrame && mouse.position.ReadValue().x > 400)
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
            }
            else vertical = controller.isGrounded ? -2 : vertical + Physics.gravity.y * Time.fixedDeltaTime;
            jumpRequested = false;
            velocity.y = vertical;
            var collision = controller.Move(velocity * Time.fixedDeltaTime);
            if ((collision & CollisionFlags.Above) != 0 && vertical > 0) vertical = 0;
            Airborne.Value = !controller.isGrounded;
            var actual = controller.velocity; actual.y = 0;
            AnimationSpeed.Value = Mathf.Clamp(actual.magnitude, 0, 10);
            // Development work harness. Production objectives will validate range/team/LOS separately.
            if (fresh && heldWork)
            {
                WorkProgress.Value += Time.fixedDeltaTime * Gaze.Value.Work / 5;
                if (WorkProgress.Value >= 1) { WorkProgress.Value = 0; WorkCycles.Value++; }
            }
            if (transform.position.y < -10)
            {
                controller.enabled = false; transform.position = new Vector3(0, 2, 0); controller.enabled = true;
                move = Vector2.zero; vertical = 0; inputEnabled = false; lastInput = double.NegativeInfinity;
                NetworkGameManager.Instance.Gaze.ResetEffects();
            }
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SelectCharacterRpc(CharacterId character)
        {
            if (!TeamLobby.Instance || !TeamLobby.Instance.InLobby || !TeamPalette.Valid(character)) return;
            Character.Value = character; Ready.Value = false;
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
            if (IsServer) Ready.Value = false;
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
            if (!IsSpawned || !IsOwner || !TeamLobby.GameplayAllowed) return;
            var state = Gaze.Value;
            string outgoing = $"Outgoing: +{state.OutgoingAllies} supported / -{state.OutgoingEnemies} disrupted";
            GUI.Label(new Rect(Screen.width / 2 - 8, Screen.height / 2 - 12, 30, 30), "+");
            GUILayout.BeginArea(new Rect(12, Screen.height - 185, 530, 175), GUI.skin.box);
            GUILayout.Label($"You: {Team.Value} / Player {OwnerClientId}   |   {outgoing}");
            string targets = "Visible targets:";
            foreach (var target in NetworkGameManager.Instance.Gaze.Players)
                if (state.ContainsTarget(target.NetworkObjectId)) targets += $" {target.OwnerClientId}({target.Team.Value})";
            GUILayout.Label(state.TargetCount > 0 ? targets : "No confirmed visible targets");
            GUILayout.Label($"Incoming: +{state.Allies} allies / -{state.Enemies} enemies");
            GUILayout.Label($"Movement {state.Move:F2}x   Work {state.Work:F2}x");
            GUILayout.Label($"Hold E: five-second work test {WorkProgress.Value:P0} | completed {WorkCycles.Value}");
            GUILayout.Label("Gaze needs captured mouse; Esc suspends your movement/work/gaze.");
            GUILayout.EndArea();
        }
    }
}
