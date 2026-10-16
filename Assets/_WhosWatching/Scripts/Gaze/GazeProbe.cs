#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;

namespace WhosWatching
{
    // Real network RPC/replication test, explicitly enabled by command line only.
    public sealed class GazeProbe : MonoBehaviour
    {
        public static bool Active { get; private set; }
        string role;
        int population = 2, previous = -1, mask;
        double began, stageStart, sendAt, deadline;
        GameObject wall;
        float workStart;
        bool workPassed;
        double finishAt;
        NetworkPlayer finishTarget;
        float movementStart;
        int movementMask;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args,"-wwGaze");
            if (i < 0 || i+1 >= args.Length) return;
            Active = true;
            var probe = new GameObject("Gaze integration probe").AddComponent<GazeProbe>(); probe.role = args[i+1];
            i = Array.IndexOf(args,"-wwExpected"); if (i >= 0) probe.population = int.Parse(args[i+1]);
        }
        void Start()
        {
            deadline = Time.realtimeSinceStartupAsDouble + 50;
            NetworkGameManager.Instance.StartSession(role == "host");
        }
        void Update()
        {
            var session = NetworkGameManager.Instance;
            double now = Time.realtimeSinceStartupAsDouble;
            if (finishAt > 0)
            {
                if (now >= finishAt)
                {
                    bool clean = finishTarget && finishTarget.Gaze.Value.Allies == 0 && finishTarget.Gaze.Value.Enemies == 0;
                    Debug.Log($"WW_GAZE_DISCONNECT_CLEAN {clean}");
                    Finish(mask == 1023 && workPassed && movementMask == 3 && clean);
                }
                return;
            }
            if (now > deadline) { Finish(false); return; }
            var players = session.Gaze.Players.OrderBy(p => p.OwnerClientId).ToArray();
            if (players.Length != population) return;
            var target = players[0]; var local = Array.Find(players,p => p.IsOwner);
            if (!local) return;
            if (session.Network.IsServer)
            {
                if (began == 0) began = now;
                int next = Mathf.Min(10,(int)((now-began)/3));
                if (target.ProbeStage.Value != next)
                {
                    target.ProbeStage.Value = next;
                    session.Gaze.ResetEffects();
                    for (int i=0; i<players.Length; i++)
                    {
                        players[i].TestTeleport(new[] {new Vector3(-3,1.05f,0),new Vector3(-3,1.05f,-5),new Vector3(-7,1.05f,0),new Vector3(-3,1.05f,5)}[i]);
                        var team = next == 2 || next == 9 || (next == 7 && i>1) ? TeamId.B : TeamId.A;
                        players[i].Team.Value = i == 0 ? TeamId.A : team;
                    }
                    if (!wall) { wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Probe wall"; wall.transform.position = new Vector3(-3,1.5f,-2.5f); wall.transform.localScale = new Vector3(2,3,0.3f); }
                    wall.SetActive(next == 3);
                }
            }
            int stage = target.ProbeStage.Value;
            if (stage < 0) return;
            if (stage != previous) { previous = stage; stageStart = now; workStart = target.WorkCycles.Value + target.WorkProgress.Value; }
            if (stage == 10)
            {
                if (session.Network.IsServer) { finishAt = now + 2; finishTarget = target; }
                else Finish(mask == 1023 && workPassed && movementMask == 3);
                return;
            }
            if (now >= sendAt)
            {
                sendAt = now + 0.05;
                bool observer = local != target;
                bool enabled = stage != 5 && stage != 0;
                if (stage == 3 && local != players[1]) enabled = false;
                var aim = target.Chest.position - local.Eye.position;
                var rotation = Quaternion.LookRotation(aim.normalized).eulerAngles;
                float pitch = Mathf.DeltaAngle(0,rotation.x);
                if (!observer || stage == 4) { rotation.y = 0; pitch = -80; }
                var motion = local == target && stage >= 8 && now-stageStart >= 0.6 && now-stageStart < 1.1 ? Vector2.up : Vector2.zero;
                if (stage != 6 || !observer) local.SendTestInput(motion,rotation.y,pitch,enabled,local==target);
            }
            if (now-stageStart < 0.5) movementStart = target.transform.position.z;
            if (now-stageStart < 2) return;
            int allies = stage == 1 || stage == 8 ? population-1 : stage == 7 ? 1 : 0;
            int enemies = stage == 2 || stage == 9 ? population-1 : stage == 7 ? population-2 : 0;
            if (stage >= 8)
            {
                float moved = target.transform.position.z - movementStart;
                float expected = 2.5f * GazeRules.Multiplier(allies,enemies);
                if (Mathf.Abs(moved-expected) < 0.65f) movementMask |= 1 << (stage-8);
            }
            var state = target.Gaze.Value;
            if (state.Allies == allies && state.Enemies == enemies && Mathf.Abs(state.Move-GazeRules.Multiplier(allies,enemies)) < 0.001f && Mathf.Abs(state.Work-state.Move)<0.001f)
                mask |= 1 << stage;
            if (stage == 1 && target.WorkCycles.Value + target.WorkProgress.Value - workStart > 0.4f) workPassed = true;
        }
        void Finish(bool passed)
        {
            Debug.Log($"WW_GAZE {(passed ? "PASS" : "FAIL")} role={role} population={population} stages={mask} work={workPassed} movement={movementMask}");
            NetworkGameManager.Instance.Network.Shutdown(); Application.Quit(passed ? 0 : 2); enabled = false;
        }
    }
}
#endif
