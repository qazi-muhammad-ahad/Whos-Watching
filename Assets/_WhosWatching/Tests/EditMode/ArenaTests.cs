using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace WhosWatching.Tests {
 public sealed class ArenaTests {
 [Test] public void ArenaHasThreeCratesPerTeamAndSharedFinal() {
 var scene=EditorSceneManager.OpenScene("Assets/_WhosWatching/Scenes/Arena.unity");
 var arena=Object.FindFirstObjectByType<ArenaGameplay>(); Assert.That(arena,Is.Not.Null);
 Assert.That(arena.Checkpoints.Length,Is.EqualTo(5)); Assert.That(arena.Teams.Count(t=>t==TeamId.A),Is.EqualTo(2)); Assert.That(arena.Teams.Count(t=>t==TeamId.B),Is.EqualTo(2));
 Assert.That(arena.Teams[arena.FinalCheckpoint],Is.EqualTo(TeamId.None));
 Assert.That(arena.CrateTeams.Count(t=>t==TeamId.A),Is.EqualTo(3)); Assert.That(arena.CrateTeams.Count(t=>t==TeamId.B),Is.EqualTo(3));
 Assert.That(arena.CrateHomes.Length,Is.EqualTo(6)); Assert.That(ArenaGameplay.RoundSeconds,Is.EqualTo(300));
 Assert.That(arena.GetComponent<Unity.Netcode.NetworkObject>(),Is.Not.Null);
 Assert.That(arena.GetComponentInChildren<ArenaDeathbox>().GetComponent<BoxCollider>().isTrigger,Is.True);
 Physics.SyncTransforms();
 foreach(var spawn in new[]{arena.SpawnA,arena.SpawnB}) {
 Assert.That(Physics.Raycast(spawn.position,Vector3.down,3,~0,QueryTriggerInteraction.Ignore),Is.True,"Spawn must have a floor");
 Assert.That(Physics.CheckCapsule(spawn.position+Vector3.up*0.55f,spawn.position-Vector3.up*0.55f,0.38f,~0,QueryTriggerInteraction.Ignore),Is.False,"Spawn must not overlap a wall");
 }
 foreach(var home in arena.CrateHomes) Assert.That(Physics.Raycast(home+Vector3.up,Vector3.down,3,~0,QueryTriggerInteraction.Ignore),Is.True,"Crate home must have a floor");
 Assert.That(Object.FindObjectsByType<TeamTint>(FindObjectsSortMode.None).Any(t=>t.name.StartsWith("Crate_Small")),Is.False,"Decorative crates must not inflate supply");
 }
 [TestCase(2,1,TeamId.A)][TestCase(0,2,TeamId.B)][TestCase(1,1,TeamId.None)]
 public void RegulationScoresChooseLeaderOrOvertime(int a,int b,TeamId expected) { Assert.That(ArenaGameplay.RegulationWinner(a,b),Is.EqualTo(expected)); Assert.That(ArenaGameplay.OvertimeSeconds,Is.EqualTo(180)); }
 [Test] public void PunchStrengthRespondsToSupportAndDisruption() {
 Assert.That(GazeRules.Multiplier(1,0),Is.GreaterThan(GazeRules.Multiplier(0,0)));
 Assert.That(GazeRules.Multiplier(0,1),Is.LessThan(GazeRules.Multiplier(0,0)));
 }
 }
}

