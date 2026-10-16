using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WhosWatching
{
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public Camera View;
        public Transform VisualRoot;
        public Transform Eye, Chest;
        public NetworkVariable<TeamId> Team = new(TeamId.None);
        public NetworkVariable<GazeState> Gaze = new(GazeState.Neutral);
        public NetworkVariable<float> WorkProgress = new(0);
        public NetworkVariable<uint> WorkCycles = new(0);
        public bool FreshInput => inputEnabled && Time.realtimeSinceStartupAsDouble - lastInput <= 0.25;
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
            controller = GetComponent<CharacterController>();
            controller.enabled = IsServer;
            if (!Eye) Eye = transform.Find("EyeAnchor");
            if (!Chest) Chest = transform.Find("ChestAnchor");
            Team.OnValueChanged += TeamChanged;
            NetworkGameManager.Instance.Gaze.Register(this);
            ApplyTeam();
            View.gameObject.SetActive(IsOwner);
            if (IsOwner) VisualRoot.gameObject.SetActive(false);
            Debug.Log($"WW_SPAWN owner={OwnerClientId} local={IsOwner} server={IsServer}");
        }
        public override void OnNetworkDespawn()
        {
            Team.OnValueChanged -= TeamChanged;
            if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.Gaze.Unregister(this);
            if (IsOwner) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (SmokeProbe.Active || GazeProbe.Active || ScreenGazeProbe.Active) return;
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
            vertical = controller.isGrounded ? -2 : vertical + Physics.gravity.y * Time.fixedDeltaTime;
            velocity.y = vertical;
            controller.Move(velocity * Time.fixedDeltaTime);
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
        void TeamChanged(TeamId oldValue, TeamId newValue) => ApplyTeam();
        void ApplyTeam()
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", Team.Value == TeamId.A ? new Color(0.1f,0.7f,1) : new Color(1,0.4f,0.1f));
            foreach (var renderer in VisualRoot.GetComponentsInChildren<Renderer>(true)) renderer.SetPropertyBlock(block);
        }
        void OnGUI()
        {
            if (!IsSpawned || !IsOwner) return;
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
