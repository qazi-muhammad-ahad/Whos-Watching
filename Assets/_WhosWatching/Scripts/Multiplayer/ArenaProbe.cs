#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace WhosWatching {
 public sealed class ArenaProbe : MonoBehaviour {
 public static bool Active {get;private set;}
 string role; ArenaGameplay arena; NetworkPlayer host,client; double deadline,nextSend; int checks; bool done,started;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install() {
 var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"-wwArena"); if(i<0 || i+1>=args.Length)return;
 Active=true; new GameObject("Arena acceptance").AddComponent<ArenaProbe>().role=args[i+1];
 }
 void Start() { deadline=Time.realtimeSinceStartupAsDouble+100; NetworkGameManager.Instance.PlayerName=role=="host"?"Test Devil":"Test Ghost"; NetworkGameManager.Instance.StartSession(role=="host"); }
 void Update() {
 if(done)return;
 if(Time.realtimeSinceStartupAsDouble>deadline) { Finish(false,"timeout");return; }
 arena=ArenaGameplay.Instance;
 var players=NetworkGameManager.Instance.Gaze.Players.OrderBy(p=>p.OwnerClientId).ToArray();
 if(players.Length!=2 || !arena || !arena.IsSpawned || arena.Crates.Count!=6) return;
 host=players[0]; client=players[1]; var local=players.First(p=>p.IsOwner);
 if(!started) {started=true; local.SetReadyRpc(true); if(role=="host")StartCoroutine(Run());}
 int stage=host.ProbeStage.Value;
 if(stage==90 && role!="host") { Finish(arena.Finished.Value && arena.Winner.Value==TeamId.None,"replicated timeout");return; }
 if(Time.realtimeSinceStartupAsDouble<nextSend)return; nextSend=Time.realtimeSinceStartupAsDouble+0.05;
 if(TeamLobby.Instance.InLobby) local.SetReadyRpc(true);
 Vector3 point=local.transform.position+Vector3.forward*2;
 bool work=false;
 if(stage>=10 && stage<16) point=arena.CrateHomes[stage-10]+Vector3.up*0.35f;
 if(stage>=20 && stage<25) { point=arena.Checkpoints[stage-20].position+Vector3.up*0.6f; work=role=="host"; }
 if(stage==30) {point=arena.Checkpoints[0].position+Vector3.up*0.6f;work=role!="host";}
 if(stage==40 && role=="host")point=client.Chest.position;
 var delta=point-local.Eye.position; var rotation=Quaternion.LookRotation(delta);
 float pitch=Mathf.DeltaAngle(0,rotation.eulerAngles.x);
 local.SendTestInput(Vector2.zero,rotation.eulerAngles.y,pitch,true,work);
 if(stage>=10 && stage<16 && role=="host")local.PickupRpc();
 }
 int ActiveCrates() { int count=0; foreach(var crate in arena.Crates) if(crate.Active) count++; return count; }
 void Check(bool condition,string name) { if(!condition) {Debug.Log($"WW_ARENA_DIAGNOSTIC pos={host.transform.position} work={host.WorkProgress.Value} rate={host.Gaze.Value.Work} carrying={arena.Carrying(host)} stage={host.ProbeStage.Value}");Finish(false,name);throw new Exception("Arena acceptance: "+name);} checks++;Debug.Log("WW_ARENA_CHECK "+name); }
 void Near(NetworkPlayer p,Vector3 point) { p.TestTeleport(new Vector3(point.x,point.y+1.15f,point.z-1.5f)); }
 IEnumerator Run() {
 yield return new WaitForSeconds(1);
 Check(host.DisplayName.Value.ToString()=="Test Devil" && client.DisplayName.Value.ToString()=="Test Ghost","replicated player names");
 Check(TeamLobby.Instance.CanStart,"ready lobby"); TeamLobby.Instance.StartGame(); yield return new WaitForSeconds(0.8f);
 Check(Vector3.Distance(host.transform.position,arena.SpawnPosition(host))<0.5f && Vector3.Distance(client.transform.position,arena.SpawnPosition(client))<0.5f,"both base spawns");
 Check(ActiveCrates()==6 && !arena.FinalUnlocked(TeamId.A),"six crates / final locked");
 Check(Math.Abs(arena.EndsAt.Value-arena.NetworkManager.ServerTime.Time-299)<2,"five minute timer");
 Near(host,arena.CrateHomes[0]); host.ProbeStage.Value=10; yield return new WaitForSeconds(0.8f);
 Check(arena.Carrying(host)>=0,"pickup");
 Near(host,arena.Checkpoints[4].position); host.ProbeStage.Value=24; yield return new WaitForSeconds(0.7f);
 Check(!arena.Completed[4] && host.WorkProgress.Value==0,"cannot skip team checkpoints");
 Near(host,arena.Checkpoints[0].position); host.ProbeStage.Value=20; yield return new WaitForSeconds(6.5f);
 Check(arena.Completed[0] && ActiveCrates()==5,"placement consumes exactly one");
 arena.Respawn(host); Near(client,arena.Checkpoints[0].position); host.ProbeStage.Value=30; yield return new WaitForSeconds(8.5f);
 Check(!arena.Completed[0] && ActiveCrates()==6,"sabotage replenishes exactly one");
 arena.Respawn(client); Near(host,arena.CrateHomes[0]); host.ProbeStage.Value=10; yield return new WaitForSeconds(0.7f);
 uint before=host.RespawnSequence.Value; host.TestTeleport(new Vector3(0,-9,0)); host.ProbeStage.Value=0; yield return new WaitForSeconds(0.8f);
 Check(host.RespawnSequence.Value>before && arena.Carrying(host)<0 && ActiveCrates()==6,"fall respawns and returns crate");
 // Isolated collision pad lets knockback distance be measured without arena walls.
 var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position=new Vector3(50,-0.5f,50); floor.transform.localScale=new Vector3(16,1,16);
 host.TestTeleport(new Vector3(50,1.05f,50)); client.TestTeleport(new Vector3(50,1.05f,51.5f)); host.ProbeStage.Value=40;
 yield return new WaitForSeconds(1.5f); var start=client.transform.position; uint hits=client.HitSequence.Value;
 host.PunchRpc(); host.PunchRpc(); yield return new WaitForSeconds(0.5f);
 Check(client.HitSequence.Value==hits+1 && Vector3.Distance(start,client.transform.position)>0.4f,"punch knockback and cooldown");
 arena.Respawn(client);
 for(int i=0;i<2;i++) {
 Near(host,arena.CrateHomes[i]); host.ProbeStage.Value=10+i; yield return new WaitForSeconds(0.7f);
 Check(arena.Carrying(host)>=0,"refill pickup "+i);
 Near(host,arena.Checkpoints[i].position); host.ProbeStage.Value=20+i; yield return new WaitForSeconds(6.5f);
 Check(arena.Completed[i],"team checkpoint "+i);
 }
 Check(arena.FinalUnlocked(TeamId.A),"final unlock");
 Near(host,arena.CrateHomes[2]);host.ProbeStage.Value=12;yield return new WaitForSeconds(0.7f);
 Near(host,arena.Checkpoints[4].position);host.ProbeStage.Value=24;yield return new WaitForSeconds(6.5f);
 Check(arena.Finished.Value && arena.Winner.Value==TeamId.A && !TeamLobby.GameplayAllowed,"final delivery wins and freezes round");
 TeamLobby.Instance.ReturnToLobby();host.Ready.Value=true;client.Ready.Value=true;TeamLobby.Instance.StartGame();host.ProbeStage.Value=0;
 Check(ActiveCrates()==6 && !arena.Finished.Value,"new round reset");
 // Seed a mid-round score, then exercise the real deadline transition.
 arena.Completed[0]=true;
 arena.Crates[0]=new ArenaCrate { Active=false, Carrier=ArenaCrate.Nobody };
 arena.EndsAt.Value=arena.NetworkManager.ServerTime.Time+0.5;yield return new WaitForSeconds(1);
 Check(arena.Finished.Value && arena.Winner.Value==TeamId.A && !arena.Overtime.Value,"regulation leader wins");
 TeamLobby.Instance.ReturnToLobby();host.Ready.Value=true;client.Ready.Value=true;TeamLobby.Instance.StartGame();
 arena.Completed[0]=true;arena.Completed[2]=true;
 arena.Crates[0]=new ArenaCrate { Active=false, Carrier=ArenaCrate.Nobody };
 arena.Crates[3]=new ArenaCrate { Active=false, Carrier=ArenaCrate.Nobody };
 arena.EndsAt.Value=arena.NetworkManager.ServerTime.Time+0.5;yield return new WaitForSeconds(1);
 Check(arena.Overtime.Value && !arena.Finished.Value,"one-one tie enters overtime");
 Near(client,arena.Checkpoints[0].position);host.ProbeStage.Value=30;yield return new WaitForSeconds(8.5f);
 Check(arena.Finished.Value && arena.Winner.Value==TeamId.B && !arena.Completed[0],"overtime sabotage wins");
 TeamLobby.Instance.ReturnToLobby();host.Ready.Value=true;client.Ready.Value=true;TeamLobby.Instance.StartGame();host.ProbeStage.Value=0;
 arena.EndsAt.Value=arena.NetworkManager.ServerTime.Time+0.5;yield return new WaitForSeconds(1);
 Check(arena.Overtime.Value && !arena.Finished.Value,"tied regulation starts overtime");
 Check(arena.EndsAt.Value-arena.NetworkManager.ServerTime.Time>178,"three minute overtime");
 arena.EndsAt.Value=arena.NetworkManager.ServerTime.Time+0.5;yield return new WaitForSeconds(1);
 Check(arena.Finished.Value && arena.Winner.Value==TeamId.None,"overtime timeout draw");host.ProbeStage.Value=90;
 yield return new WaitForSeconds(0.7f);Finish(true,"all server checks");
 }
 void Finish(bool passed,string reason) { if(done)return;done=true;Debug.Log($"WW_ARENA {(passed?"PASS":"FAIL")} checks={checks} reason={reason}");Application.Quit(passed?0:2); }
 }
}
#endif





