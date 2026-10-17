#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;

namespace WhosWatching
{
    // Opt-in network integration test. No scene mutation or automatic input in ordinary gameplay.
    public sealed class ScreenGazeProbe : MonoBehaviour
    {
        public static bool Active { get; private set; }
        const int Stages = 18, CompleteMask = (1 << Stages) - 1;
        string role;
        int count = 4, previous = -1, passed, logged;
        double deadline, began, stageStart, sendAt, finishAt;
        NetworkPlayer finishPlayer;
        GameObject wall;
        static readonly Vector3 FixtureOffset = new(100,0,0);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args,"-wwScreenGaze");
            if (index < 0 || index + 1 >= args.Length) return;
            Active = true;
            var probe = new GameObject("Screen gaze integration probe").AddComponent<ScreenGazeProbe>();
            probe.role = args[index+1]; index = Array.IndexOf(args,"-wwExpected");
            if (index >= 0) probe.count = int.Parse(args[index+1]);
        }
        void Start()
        {
            // Art now has real collision. Keep synthetic sightlines independent of map edits.
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name="Screen gaze isolated test floor";
            floor.transform.position=FixtureOffset+new Vector3(0,-0.5f,0);
            floor.transform.localScale=new Vector3(40,1,40);
            deadline = Time.realtimeSinceStartupAsDouble + 85;
            NetworkGameManager.Instance.StartSession(role == "host");
        }
        void Update()
        {
            var session = NetworkGameManager.Instance; double now = Time.realtimeSinceStartupAsDouble;
            if (finishAt > 0)
            {
                if (now >= finishAt)
                {
                    var state = finishPlayer.Gaze.Value;
                    bool clean = state.TargetCount == 0 && state.Allies == 0 && state.Enemies == 0;
                    Debug.Log($"WW_SCREEN_DISCONNECT_CLEAN {clean}"); Finish(passed == CompleteMask && clean);
                }
                return;
            }
            if (now > deadline) { Finish(false); return; }
            var players = session.Gaze.Players.OrderBy(p => p.OwnerClientId).ToArray();
            if (players.Length != count) return;
            var host = players[0]; var local = Array.Find(players,p => p.IsOwner);
            if (!local) return;
            if (session.Network.IsServer)
            {
                if (began == 0) began = now;
                int next = Mathf.Min(Stages,(int)((now-began)/3));
                if (host.ProbeStage.Value != next)
                {
                    host.ProbeStage.Value = next;
                    // Stage 9 hides only one target without resetting the other established links.
                    if (next != 9) session.Gaze.ResetEffects();
                    Configure(players,next);
                }
            }
            int stage = host.ProbeStage.Value;
            if (stage < 0) return;
            if (stage == Stages)
            {
                if (session.Network.IsServer) { finishPlayer = host; finishAt = now + 2; }
                else Finish(passed == CompleteMask);
                return;
            }
            if (stage != previous) { previous = stage; stageStart = now; }
            if (now >= sendAt)
            {
                sendAt = now + 0.05;
                bool remoteObserver = stage >= 1 && stage <= 3;
                bool active = remoteObserver ? local != host : local == host && stage != 0 && stage != 10;
                float yaw = 0, pitch = 0;
                if (remoteObserver && local != host)
                {
                    var angles = Quaternion.LookRotation(host.Chest.position-local.Eye.position).eulerAngles;
                    yaw = angles.y + 25; pitch = Mathf.DeltaAngle(0,angles.x);
                }
                else if (remoteObserver) pitch = -80;
                if (stage == 12) yaw = 180;
                float aspect = stage == 7 ? 9f/16 : 16f/9;
                if (stage == 16) aspect = float.NaN; // Invalid packet must not refresh intent.
                if (stage != 11) local.SendTestInput(Vector2.zero,yaw,pitch,active,false,aspect);
            }
            if (now-stageStart < 2) return;
            bool valid = Check(players,stage);
            if (valid) passed |= 1 << stage;
            if ((logged & (1 << stage)) == 0)
            {
                logged |= 1 << stage;
                Debug.Log($"WW_SCREEN_STAGE stage={stage} valid={valid} hostTargets={host.Gaze.Value.TargetCount} hostIncoming={host.Gaze.Value.Allies}/{host.Gaze.Value.Enemies}");
            }
        }
        void Configure(NetworkPlayer[] players, int stage)
        {
            if (!wall)
            {
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Screen gaze probe wall";
            }
            wall.SetActive(stage == 9 || stage == 13 || stage == 14);
            wall.transform.position = new Vector3(-1.5f,1.5f,-4); wall.transform.localScale = new Vector3(1.2f,3,0.3f);
            if (stage == 13 || stage == 14)
            {
                float height = stage == 13 ? 1.7f : 3;
                wall.transform.position = new Vector3(0,height/2,-4); wall.transform.localScale = new Vector3(3,height,0.3f);
            }
            wall.transform.position += FixtureOffset;
            for (int i=0; i<players.Length; i++)
            {
                Vector3 position = new[] {new Vector3(0,1.05f,-7),new Vector3(-3,1.05f,-1),new Vector3(0,1.05f,1),new Vector3(3,1.05f,-1)}[i];
                if (stage >= 1 && stage <= 3)
                    position = new[] {new Vector3(-3,1.05f,0),new Vector3(-3,1.05f,-5),new Vector3(-7,1.05f,0),new Vector3(-3,1.05f,5)}[i];
                if (stage == 5 || stage == 6 || stage >= 13)
                {
                    if (i == 1) position = new Vector3(stage == 5 ? 6.45f : stage == 6 ? 7.5f : 0,1.05f,stage == 17 ? 10 : -1);
                    if (i > 1) position = new Vector3(i*2,1.05f,-10);
                }
                if (stage == 15)
                {
                    if (i == 1) position = new Vector3(0,1.05f,-4);
                    if (i == 2) position = new Vector3(0,1.05f,-1);
                }
                if (stage != 9) players[i].TestTeleport(position+FixtureOffset);
                players[i].Team.Value = i == 0 || (i == 1 && stage != 2) || stage == 1 ? TeamId.A : TeamId.B;
            }
        }
        bool Check(NetworkPlayer[] players, int stage)
        {
            var host = players[0]; var state = host.Gaze.Value;
            if (stage >= 1 && stage <= 3)
            {
                int allies = stage == 1 ? count-1 : stage == 3 ? 1 : 0;
                int enemies = count-1-allies;
                if (state.Allies != allies || state.Enemies != enemies || !EffectsMatch(state)) return false;
                foreach (var observer in players.Skip(1)) if (!observer.Gaze.Value.ContainsTarget(host.NetworkObjectId)) return false;
                return true;
            }
            int outgoingAllies = 0, outgoingEnemies = 0, total = 0;
            for (int i=1; i<players.Length; i++)
            {
                bool visible = stage == 4 || stage == 8 || (stage == 9 && i != 1) ||
                    ((stage == 5 || stage == 13 || stage == 15) && i == 1) || (stage == 7 && i == 2);
                bool ally = players[i].Team.Value == host.Team.Value;
                var targetState = players[i].Gaze.Value;
                if (state.ContainsTarget(players[i].NetworkObjectId) != visible || targetState.TargetCount != 0 ||
                    targetState.Allies != (visible && ally ? 1 : 0) || targetState.Enemies != (visible && !ally ? 1 : 0) || !EffectsMatch(targetState)) return false;
                if (visible) { total++; if (ally) outgoingAllies++; else outgoingEnemies++; }
            }
            return state.TargetCount == total && state.OutgoingAllies == outgoingAllies && state.OutgoingEnemies == outgoingEnemies && state.Allies == 0 && state.Enemies == 0;
        }
        bool EffectsMatch(GazeState state)
        {
            var config = NetworkGameManager.Instance.Gaze.Config;
            return Mathf.Abs(state.Move-GazeRules.Multiplier(state.Allies,state.Enemies,config.MoveSupport,config.MoveDisruption,config.Minimum,config.Maximum)) < 0.001f &&
                Mathf.Abs(state.Work-GazeRules.Multiplier(state.Allies,state.Enemies,config.WorkSupport,config.WorkDisruption,config.Minimum,config.Maximum)) < 0.001f;
        }
        void Finish(bool success)
        {
            Debug.Log($"WW_SCREEN {(success ? "PASS" : "FAIL")} role={role} players={count} stages={passed}/{CompleteMask}");
            NetworkGameManager.Instance.Network.Shutdown(); Application.Quit(success ? 0 : 2); enabled = false;
        }
    }
}
#endif
