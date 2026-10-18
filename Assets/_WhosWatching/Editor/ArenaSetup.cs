using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace WhosWatching.Editor {
public static class ArenaSetup {
 public static void Inspect() {
 if(Application.isBatchMode) EditorSceneManager.OpenScene("Assets/_WhosWatching/Scenes/Arena.unity");
 if (SceneManager.GetActiveScene().name != "Arena") throw new System.Exception("Open Arena first");
 var lines = new System.Collections.Generic.List<string>();
 foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name=="MainObjective" || t.name.StartsWith("Checkpoint") || t.name.StartsWith("Base_") || t.name.StartsWith("Crate"))) {
 var bounds = new Bounds(t.position,Vector3.zero); foreach(var r in t.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
 lines.Add($"ARENA {t.name} pos={t.position} parent={t.parent?.name} bounds={bounds} tint={t.GetComponent<TeamTint>()?.Team}");
 }
 File.WriteAllLines("Logs/ArenaLayout.txt",lines);
 }
 [MenuItem("Who's Watching/Install Arena gameplay")]
 public static void Install() {
 if(Application.isBatchMode) EditorSceneManager.OpenScene("Assets/_WhosWatching/Scenes/Arena.unity");
 var scene=SceneManager.GetActiveScene();
 if(scene.name!="Arena" || EditorApplication.isPlaying) throw new System.Exception("Open Arena in edit mode first");
 var lobby=Object.FindFirstObjectByType<TeamLobby>();
 var arena=lobby.GetComponent<ArenaGameplay>(); if(!arena) arena=Undo.AddComponent<ArenaGameplay>(lobby.gameObject);
 Undo.RecordObject(arena,"Configure Arena gameplay");
 var tints=Object.FindObjectsByType<TeamTint>(FindObjectsInactive.Include,FindObjectsSortMode.None);
 var checkpoints=tints.Where(t=>t.name=="Checkpoint" || t.name.StartsWith("Checkpoint (")).OrderBy(t=>t.Team).ThenBy(t=>t.transform.position.z).ToArray();
 if(checkpoints.Length!=4) throw new System.Exception("Expected four authored checkpoints, found "+checkpoints.Length);
 var final=GameObject.Find("MainObjective").transform;
 arena.Checkpoints=checkpoints.Select(t=>t.transform).Concat(new[]{final}).ToArray(); arena.Teams=checkpoints.Select(t=>t.Team).Concat(new[]{TeamId.None}).ToArray();
 arena.FinalCheckpoint=4;
 arena.CrateHomes=new Vector3[6]; arena.CrateTeams=new[]{TeamId.A,TeamId.A,TeamId.A,TeamId.B,TeamId.B,TeamId.B};
 foreach(var team in new[]{TeamId.A,TeamId.B}) {
 var floors=tints.Where(t=>t.Team==team && t.name.StartsWith("Floor_Plate_Square")).OrderBy(t=>Mathf.Abs(t.transform.position.z)).ToArray();
 if(floors.Length==0) throw new System.Exception("Missing base floor for "+team);
 Vector3 point=floors[0].transform.position;
 var spawn=lobby.transform.Find("Spawn_"+team);
 if(!spawn) { var go=new GameObject("Spawn_"+team); Undo.RegisterCreatedObjectUndo(go,"Create spawn"); spawn=go.transform; spawn.SetParent(lobby.transform); }
 spawn.SetPositionAndRotation(new Vector3(point.x,1.2f,point.z),Quaternion.Euler(0,team==TeamId.A?270:90,0));
 if(team==TeamId.A) arena.SpawnA=spawn; else arena.SpawnB=spawn;
 var homes=tints.Where(t=>t.Team==team && t.name.StartsWith("Crate_Small")).OrderByDescending(t=>Mathf.Abs(t.transform.position.x)).ThenBy(t=>t.transform.position.z).ToArray();
 int slot=0;
 for(int i=0;i<arena.CrateHomes.Length;i++) if(arena.CrateTeams[i]==team) {
 arena.CrateHomes[i]=homes[slot++ % homes.Length].transform.position + Vector3.up*0.08f;
 }
 foreach(var home in homes) { Undo.RecordObject(home.gameObject,"Replace decorative crates with supply"); home.gameObject.SetActive(false); }
 }
 arena.CratePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_WhosWatching/Prefabs/TeamReady/Crate_Small.prefab");
 var death=lobby.transform.Find("Deathbox");
 if(!death) { var go=new GameObject("Deathbox"); Undo.RegisterCreatedObjectUndo(go,"Create deathbox"); death=go.transform; death.SetParent(lobby.transform); }
 death.position=new Vector3(0,-7,0);
 var collider=death.GetComponent<BoxCollider>(); if(!collider) collider=death.gameObject.AddComponent<BoxCollider>(); collider.isTrigger=true; collider.size=new Vector3(160,2,160);
 if(!death.GetComponent<ArenaDeathbox>()) death.gameObject.AddComponent<ArenaDeathbox>();
 EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scene.path,true)};
 EditorUtility.SetDirty(arena); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
 File.WriteAllText("Logs/ArenaInstalled.txt",$"Checkpoints: {checkpoints.Length}, A spawn {arena.SpawnA.position}, B spawn {arena.SpawnB.position}\n"+string.Join("\n",arena.CrateHomes.Select(p=>p.ToString())));
 }
 public static void Build() { ProjectSetup.Build(); }
}}


