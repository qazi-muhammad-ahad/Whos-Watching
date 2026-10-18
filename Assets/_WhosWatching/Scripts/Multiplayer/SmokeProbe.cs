#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Unity.Netcode;
using UnityEngine;

namespace WhosWatching
{
    // Opt-in standalone process harness; never active in normal play or release builds.
    public sealed class SmokeProbe : MonoBehaviour
    {
        public static bool Active { get; private set; }
        string role;
        double start, next;
        float duration = 15;
        int expected = 2;
        bool sawPopulation, sawMovement, sawStaleStop;
        Vector3 initial, last;
        Vector3 settled;
        bool hasSettled;
        bool captured;
        double spawnedAt;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-wwSmoke");
            if (index < 0 || index + 1 >= args.Length) return;
            Active = true;
            var probe = new GameObject("DevelopmentSmokeProbe").AddComponent<SmokeProbe>();
            probe.role = args[index + 1];
            int count = Array.IndexOf(args, "-wwExpected");
            if (count >= 0 && count + 1 < args.Length) int.TryParse(args[count+1], out probe.expected);
            int seconds = Array.IndexOf(args, "-wwSeconds");
            if (seconds >= 0 && seconds + 1 < args.Length) float.TryParse(args[seconds+1], out probe.duration);
        }
        void Start()
        {
            start = Time.realtimeSinceStartupAsDouble;
            NetworkGameManager.Instance.StartSession(role == "host");
        }
        void Update()
        {
            double elapsed = Time.realtimeSinceStartupAsDouble - start;
            var manager = NetworkManager.Singleton;
            var player = manager.LocalClient?.PlayerObject?.GetComponent<NetworkPlayer>();
            if (NetworkGameManager.Instance.Gaze.Players.Count == expected) sawPopulation = true;
            if (player != null && elapsed >= next)
            {
                next = elapsed + 0.05;
                if (!captured) { initial = player.transform.position; captured = true; spawnedAt = elapsed; }
                // Move then stop sending entirely: proves the server expires stale input.
                if (elapsed - spawnedAt < 1) player.SendTestInput(Vector2.up);
                var position = player.transform.position;
                if (Mathf.Abs(position.z - initial.z) > 1) sawMovement = true;
                if (elapsed - spawnedAt > 3 && !hasSettled) { settled = position; hasSettled = true; }
                if (elapsed - spawnedAt > 6) sawStaleStop = Mathf.Abs(position.z - settled.z) < 0.05f && Mathf.Abs(position.z) < 11;
                last = position;
            }
            if (elapsed >= duration)
            {
                bool passed = sawPopulation && sawMovement && sawStaleStop;
                if (role == "reject") passed = !captured && NetworkGameManager.Instance.Status.Contains("Session full");
                if (role == "badtoken") passed = !captured && NetworkGameManager.Instance.Status.Contains("Incompatible build");
                Debug.Log($"WW_SMOKE {(passed ? "PASS" : "FAIL")} role={role} population={sawPopulation} movement={sawMovement} staleStop={sawStaleStop}");
                manager.Shutdown(); Application.Quit(passed ? 0 : 2);
            }
        }
    }
}
#endif
