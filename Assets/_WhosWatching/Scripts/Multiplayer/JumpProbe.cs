#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;

namespace WhosWatching
{
    public sealed class JumpProbe : MonoBehaviour
    {
        public static bool Active { get; private set; }
        string role;
        double began, stageAt, sendAt, deadline;
        int previous=-1,mask;
        float baseline,peak,ceilingPeak;
        bool sawAir,sawAnimation;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"-wwJump");
            if(i<0 || i+1>=args.Length) return;
            Active=true; new GameObject("Jump network acceptance").AddComponent<JumpProbe>().role=args[i+1];
        }
        void Start()
        {
            deadline=Time.realtimeSinceStartupAsDouble+45;
            NetworkGameManager.Instance.SelectedCharacter=role=="host" ? CharacterId.Devil : CharacterId.Ghost;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="Jump test floor";
            floor.transform.position=new Vector3(50,-0.5f,50); floor.transform.localScale=new Vector3(12,1,8);
            NetworkGameManager.Instance.StartSession(role=="host");
        }
        void Update()
        {
            double now=Time.realtimeSinceStartupAsDouble;
            if(now>deadline) { Finish(false); return; }
            var session=NetworkGameManager.Instance; var lobby=TeamLobby.Instance;
            var players=session.Gaze.Players.OrderBy(p=>p.OwnerClientId).ToArray();
            if(players.Length!=2 || !lobby || !lobby.IsSpawned) return;
            var local=players.FirstOrDefault(p=>p.IsOwner); if(!local) return;
            if(session.Network.IsServer)
            {
                if(began==0) { began=now; for(int i=0;i<2;i++) players[i].TestTeleport(new Vector3(48+i*4,1.05f,50)); }
                players[0].ProbeStage.Value=Mathf.Min(7,(int)((now-began)/3));
            }
            int stage=players[0].ProbeStage.Value;
            if(stage!=previous)
            {
                previous=stage; stageAt=now;
                if(stage==0) local.SetReadyRpc(true);
                if(stage==1 && session.Network.IsServer) lobby.StartGame();
                if(stage==2 || stage==4) local.JumpRpc();
                if(stage==3)
                {
                    var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube); ceiling.name="Jump test ceiling";
                    ceiling.transform.position=new Vector3(50,2.85f,50); ceiling.transform.localScale=new Vector3(12,0.2f,8);
                }
                if(stage==6 && session.Network.IsServer) lobby.ReturnToLobby();
            }
            if(now>=sendAt)
            {
                sendAt=now+0.05; local.SendTestInput(Vector2.zero);
                if(stage==0 || stage==6 || (stage==2 && now-stageAt<0.4)) local.JumpRpc();
            }
            if(stage==1) baseline=local.transform.position.y;
            if(stage==2)
            {
                peak=Mathf.Max(peak,local.transform.position.y-baseline); sawAir|=local.Airborne.Value;
                var remote=players.First(p=>!p.IsOwner);
                var animator=remote.VisualRoot.GetComponentInChildren<Animator>();
                sawAnimation|=animator && animator.GetCurrentAnimatorStateInfo(0).IsName("Jump");
            }
            if(stage==4) ceilingPeak=Mathf.Max(ceilingPeak,local.transform.position.y-baseline);
            if(now-stageAt<2) return;
            bool valid=stage switch {
                0=>!local.Airborne.Value && !lobby.Playing.Value && lobby.CanStart,
                1=>lobby.Playing.Value && !local.Airborne.Value,
                2=>sawAir && sawAnimation && peak>0.9f && peak<1.5f && !local.Airborne.Value,
                3=>!local.Airborne.Value && Mathf.Abs(local.transform.position.y-baseline)<0.1f,
                4=>ceilingPeak>0.3f && ceilingPeak<0.85f && !local.Airborne.Value,
                5=>Mathf.Abs(local.transform.position.y-baseline)<0.1f && !local.Airborne.Value,
                6=>!lobby.Playing.Value && !local.Airborne.Value && Mathf.Abs(local.transform.position.y-baseline)<0.1f,
                _=>false
            };
            if(valid) mask|=1<<stage;
            if(stage==7) Finish(mask==127);
        }
        void Finish(bool passed)
        {
            Debug.Log($"WW_JUMP {(passed ? "PASS":"FAIL")} mask={mask} peak={peak:F3} ceilingPeak={ceilingPeak:F3} airborne={sawAir} animation={sawAnimation}");
            NetworkGameManager.Instance.Network.Shutdown(); Application.Quit(passed ? 0:2); enabled=false;
        }
    }
}
#endif
