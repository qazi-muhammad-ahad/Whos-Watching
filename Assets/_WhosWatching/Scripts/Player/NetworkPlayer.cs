using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WhosWatching
{
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public Camera View;
        public Transform VisualRoot;
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
            View.gameObject.SetActive(IsOwner);
            if (IsOwner) VisualRoot.gameObject.SetActive(false);
            Debug.Log($"WW_SPAWN owner={OwnerClientId} local={IsOwner} server={IsServer}");
        }
        public override void OnNetworkDespawn()
        {
            if (IsOwner) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (SmokeProbe.Active) return;
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
                SubmitRpc(++sequence, direction, yaw, pitch);
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void SendTestInput(Vector2 direction)
        {
            if (IsSpawned && IsOwner) SubmitRpc(++sequence, direction, 0, 0);
        }
#endif
        void LateUpdate()
        {
            if (IsSpawned && IsOwner) View.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
        void SubmitRpc(uint serial, Vector2 direction, float lookYaw, float lookPitch, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || !SessionRules.ValidInput(direction, lookYaw, lookPitch)) return;
            if (received && !SessionRules.Newer(serial, lastSequence)) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - lastInput < 0.015) return;
            received = true; lastSequence = serial; lastInput = now;
            move = Vector2.ClampMagnitude(direction, 1); acceptedYaw = Mathf.Repeat(lookYaw, 360);
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            bool fresh = Time.realtimeSinceStartupAsDouble - lastInput <= 0.25;
            transform.rotation = Quaternion.Euler(0, acceptedYaw, 0);
            Vector3 velocity = fresh ? transform.rotation * new Vector3(move.x, 0, move.y) * 5 : Vector3.zero;
            vertical = controller.isGrounded ? -2 : vertical + Physics.gravity.y * Time.fixedDeltaTime;
            velocity.y = vertical;
            controller.Move(velocity * Time.fixedDeltaTime);
            if (transform.position.y < -10)
            {
                controller.enabled = false; transform.position = new Vector3(0, 2, 0); controller.enabled = true;
                move = Vector2.zero; vertical = 0; lastInput = double.NegativeInfinity;
            }
        }
    }
}
