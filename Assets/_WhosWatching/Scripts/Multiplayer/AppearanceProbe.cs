#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;

namespace WhosWatching
{
    // Opt-in, real four-peer RPC/replication acceptance. Does not bypass the lobby.
    public sealed class AppearanceProbe : MonoBehaviour
    {
        public static bool Active { get; private set; }
        string role;
        double began, stageAt, nextSend, deadline;
        int previous=-1, mask, originalIndex=-1, reported=-1;
        Vector3 lobbyPosition;
        bool sawInitialSelection;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"-wwAppearance");
            if(i<0 || i+1>=args.Length) return;
            Active=true; new GameObject("Appearance acceptance").AddComponent<AppearanceProbe>().role=args[i+1];
        }
        void Start() { deadline=Time.realtimeSinceStartupAsDouble+75; NetworkGameManager.Instance.SelectedCharacter=CharacterId.Ghost; NetworkGameManager.Instance.StartSession(role=="host"); }
        void Update()
        {
            double now=Time.realtimeSinceStartupAsDouble;
            if(now>deadline) { Finish(false); return; }
            var session=NetworkGameManager.Instance; var lobby=TeamLobby.Instance;
            var players=session.Gaze.Players.OrderBy(p=>p.OwnerClientId).ToArray();
            if((players.Length!=4 && !(previous>=7 && players.Length==3)) || !lobby || !lobby.IsSpawned) return;
            var local=players.FirstOrDefault(p=>p.IsOwner); if(!local) return;
            if(originalIndex<0) originalIndex=Array.IndexOf(players,local);
            int index=originalIndex;
            if(session.Network.IsServer)
            {
                if(began==0)
                {
                    began=now;
                    // Connection completion order can differ from assigned client IDs.
                    for(int i=0;i<players.Length;i++) session.Gaze.SetTeam(players[i],i%2==0 ? TeamId.A : TeamId.B);
                }
                players[0].ProbeStage.Value=now-began<2 ? -1 : Mathf.Min(14,(int)((now-began-2)/3));
            }
            int stage=players[0].ProbeStage.Value;
            if(stage<0) { sawInitialSelection=players.All(p=>p.Character.Value==CharacterId.Ghost); return; }
            if(stage!=previous)
            {
                previous=stage; stageAt=now;
                if(stage==0) { local.SelectCharacterRpc(index%2==0 ? CharacterId.Devil : CharacterId.Ghost); if(index<2) local.SelectColorRpc((byte)(index==0 ? 2 : 3)); }
                if(stage==1) { local.SelectCharacterRpc((CharacterId)255); local.SelectColorRpc(index<2 ? (byte)255 : (byte)4); }
                if(stage==2) { local.SelectColorRpc((byte)(local.Team.Value==TeamId.A ? 3 : 2)); local.SetReadyRpc(true); lobbyPosition=local.transform.position; }
                if(stage==3 && session.Network.IsServer) lobby.StartGame();
                if(stage==4) { local.SelectCharacterRpc(index%2==0 ? CharacterId.Ghost : CharacterId.Devil); local.SelectColorRpc(5); local.SetReadyRpc(false); }
                if(stage==5 && session.Network.IsServer) lobby.ReturnToLobby();
                if(stage==6) { local.SelectCharacterRpc(index%2==0 ? CharacterId.Ghost : CharacterId.Devil); }
                if(stage==8 && index==3) local.SelectColorRpc(5);
                if(stage==9) local.SelectTeamRpc(index==3 ? TeamId.A : TeamId.None);
                if(stage==10) local.SetReadyRpc(true);
                if(stage==11 && index==0) local.SelectTeamRpc(TeamId.B);
                if(stage==12) local.SelectTeamRpc(index==2 ? TeamId.B : (TeamId)255);
                if(stage==13 && index==0) local.SelectTeamRpc(TeamId.A);
            }
            if(stage==2 && now>=nextSend) { nextSend=now+0.05; local.SendTestInput(Vector2.up); }
            if(now-stageAt<1.5) return;
            bool colors=lobby.ColorA.Value==2 && lobby.ColorB.Value==3;
            bool characters=players.All(p=>p.Character.Value==(Array.IndexOf(players,p)%2==0 ? CharacterId.Devil : CharacterId.Ghost));
            bool leaders=lobby.Leader(TeamId.A)==players[0] && lobby.Leader(TeamId.B)==players[1];
            bool valid=stage switch {
                0 => sawInitialSelection && colors && characters && leaders && !players.Any(p=>p.Ready.Value) && CheckVisuals(players,lobby),
                1 => colors && characters,
                2 => colors && lobby.CanStart && Vector3.Distance(local.transform.position,lobbyPosition)<0.1f && !local.FreshInput,
                3 => lobby.Playing.Value && colors && characters,
                4 => lobby.Playing.Value && colors && characters && players.All(p=>p.Ready.Value),
                5 => !lobby.Playing.Value && !players.Any(p=>p.Ready.Value),
                6 => players.All(p=>p.Character.Value==(Array.IndexOf(players,p)%2==0 ? CharacterId.Ghost : CharacterId.Devil)) && CheckVisuals(players,lobby),
                7 => players.Length==3 && lobby.Leader(TeamId.B).OwnerClientId==3,
                8 => lobby.ColorA.Value==2 && lobby.ColorB.Value==5 && CheckVisuals(players,lobby),
                9 => lobby.Count(TeamId.A)==2 && lobby.Count(TeamId.B)==1,
                10 => lobby.CanStart,
                11 => players[0].Team.Value==TeamId.B && lobby.Count(TeamId.B)==2 && lobby.Leader(TeamId.B)==players[0] && !players.Any(p=>p.Ready.Value) && CheckVisuals(players,lobby),
                12 => players[0].Team.Value==TeamId.B && lobby.Count(TeamId.A)==1 && lobby.Count(TeamId.B)==2,
                13 => players[0].Team.Value==TeamId.A && lobby.Count(TeamId.A)==2 && lobby.Leader(TeamId.B).OwnerClientId==3 && CheckVisuals(players,lobby),
                _ => false
            };
            if(valid && (mask & 1<<stage)==0) { mask|=1<<stage; Debug.Log($"WW_APPEARANCE_STAGE {stage} PASS"); }
            if(stage<14 && now-stageAt>2.5 && (mask & 1<<stage)==0 && reported!=stage)
            {
                reported=stage;
                Debug.Log($"WW_APPEARANCE_WAIT stage={stage} initial={sawInitialSelection} colors={lobby.ColorA.Value}/{lobby.ColorB.Value} playing={lobby.Playing.Value} visuals={CheckVisuals(players,lobby)} roster={string.Join(";",players.Select(p=>$"{p.OwnerClientId}:{p.Team.Value}:{p.Character.Value}:{p.Ready.Value}"))}");
            }
            if(stage==7 && index==1) Finish(mask==127);
            if(stage==14) Finish(mask==16383);
        }
        static bool CheckVisuals(NetworkPlayer[] players,TeamLobby lobby)
        {
            var block=new MaterialPropertyBlock();
            foreach(var player in players.Where(p=>!p.IsOwner))
            {
                if(player.VisualRoot.childCount!=1) return false;
                var tint=player.VisualRoot.GetComponentInChildren<TeamTint>();
                var animator=player.VisualRoot.GetComponentInChildren<Animator>();
                if(!tint || !animator || !animator.runtimeAnimatorController) return false;
                foreach(var slot in tint.Slots)
                {
                    slot.Renderer.GetPropertyBlock(block,slot.MaterialIndex);
                    if(Vector4.Distance(block.GetColor("_BaseColor"),Color.Lerp(slot.BaseColor,lobby.Color(player.Team.Value),slot.Strength))>0.001f) return false;
                }
            }
            foreach(var tint in FindObjectsByType<TeamTint>(FindObjectsSortMode.None).Where(t=>!t.GetComponentInParent<NetworkPlayer>()))
            foreach(var slot in tint.Slots)
            {
                slot.Renderer.GetPropertyBlock(block,slot.MaterialIndex);
                if(Vector4.Distance(block.GetColor("_BaseColor"),Color.Lerp(slot.BaseColor,lobby.Color(tint.Team),slot.Strength))>0.001f) return false;
            }
            return true;
        }
        void Finish(bool passed)
        {
            Debug.Log($"WW_APPEARANCE {(passed ? "PASS" : "FAIL")} role={role} mask={mask}");
            NetworkGameManager.Instance.Network.Shutdown(); Application.Quit(passed ? 0 : 2); enabled=false;
        }
    }
}
#endif

