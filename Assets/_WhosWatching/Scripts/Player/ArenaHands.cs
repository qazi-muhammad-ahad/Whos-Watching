using UnityEngine;
namespace WhosWatching {
 // Camera-local hands make every action readable to its owner as well as remote players.
 public sealed class ArenaHands : MonoBehaviour {
 NetworkPlayer player; Transform left,right; Material material; uint punched,hit,respawn; float punchTime,hitTime,spawnTime;
 void Start() {
 player=GetComponentInParent<NetworkPlayer>();
 material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
 left=Hand("Left hand",-1); right=Hand("Right hand",1);
 }
 Transform Hand(string label,int side) {
 var go=GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name=label; go.transform.SetParent(transform,false);
 go.GetComponent<Collider>().enabled=false; Destroy(go.GetComponent<Collider>());
 go.GetComponent<Renderer>().sharedMaterial=material; go.transform.localScale=new Vector3(0.13f,0.15f,0.22f); return go.transform;
 }
 void LateUpdate() {
 if(!player || !player.IsSpawned || !left) return;
 left.gameObject.SetActive(TeamLobby.GameplayAllowed); right.gameObject.SetActive(TeamLobby.GameplayAllowed);
 if(punched!=player.PunchSequence.Value) { punched=player.PunchSequence.Value; punchTime=1; }
 if(hit!=player.HitSequence.Value) { hit=player.HitSequence.Value; hitTime=1; }
 if(respawn!=player.RespawnSequence.Value) { respawn=player.RespawnSequence.Value; spawnTime=1; }
 punchTime=Mathf.Max(0,punchTime-Time.deltaTime*3.5f); hitTime=Mathf.Max(0,hitTime-Time.deltaTime*3); spawnTime=Mathf.Max(0,spawnTime-Time.deltaTime*2);
 float punch=Mathf.Sin(punchTime*Mathf.PI), work=player.Working.Value?Mathf.Sin(Time.time*15*player.Gaze.Value.Work)*0.055f:0;
 bool carrying=ArenaGameplay.Instance && ArenaGameplay.Instance.Carrying(player)>=0;
 float bob=Mathf.Sin(Time.time*9)*Mathf.Clamp01(player.AnimationSpeed.Value/5)*0.012f;
 left.localPosition=new Vector3(-0.24f,-0.23f+bob+work-spawnTime*0.3f,0.45f+(carrying?0.12f:0));
 right.localPosition=new Vector3(0.24f-punch*0.14f,-0.23f+bob-work+punch*0.12f-spawnTime*0.3f,0.45f+punch*0.38f+(carrying?0.12f:0));
 material.color=Color.Lerp(TeamLobby.Instance.Color(player.Team.Value),Color.red,hitTime*0.7f);
 }
 void OnDestroy() { if(material) Destroy(material); }
 }
}
