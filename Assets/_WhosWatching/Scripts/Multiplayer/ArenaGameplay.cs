using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace WhosWatching
{
    public struct ArenaCrate : INetworkSerializable, IEquatable<ArenaCrate>
    {
        public bool Active;
        public ulong Carrier;
        public const ulong Nobody = ulong.MaxValue;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref Active); s.SerializeValue(ref Carrier); }
        public bool Equals(ArenaCrate other) => Active == other.Active && Carrier == other.Carrier;
    }

    // Scene-authored locations, server-owned state. Visual replicas never run gameplay physics.
    public sealed class ArenaGameplay : NetworkBehaviour
    {
        public static ArenaGameplay Instance { get; private set; }
        public Transform SpawnA, SpawnB;
        public Transform[] Checkpoints = Array.Empty<Transform>();
        public TeamId[] Teams = Array.Empty<TeamId>();
        public TeamId[] CrateTeams = Array.Empty<TeamId>();
        public int FinalCheckpoint = 4;
        public NetworkVariable<double> EndsAt = new(0);
        public NetworkVariable<bool> Finished = new(false);
        public NetworkVariable<bool> Overtime = new(false);
        public NetworkVariable<TeamId> Winner = new(TeamId.None);
        public const double RoundSeconds = 300;
        public const double OvertimeSeconds = 180;
        public Vector3[] CrateHomes = Array.Empty<Vector3>();
        public GameObject CratePrefab;
        public float DeathHeight = -8, WorkSeconds = 3, SabotageSeconds = 4;
        public NetworkList<bool> Completed;
        public NetworkList<ArenaCrate> Crates;
        readonly Dictionary<ulong, int> workTargets = new();
        readonly List<Transform> boxes = new(), markers = new();
        Material visualMaterial;
        public const float Reach = 2.8f;
        void Awake() { Instance = this; Completed = new(); Crates = new(); }
        public override void OnDestroy() { if (Instance == this) Instance = null; base.OnDestroy(); }
        public override void OnNetworkSpawn()
        {
            if (IsServer) SpawnEveryone();
            MakeVisuals();
        }
        public override void OnNetworkDespawn()
        {
            foreach (var t in boxes) if (t) Destroy(t.gameObject);
            foreach (var t in markers) if (t) Destroy(t.gameObject);
            boxes.Clear(); markers.Clear();
            if (visualMaterial) Destroy(visualMaterial);
        }
        public void ResetRound()
        {
            if (!IsServer) return;
            Completed.Clear(); Crates.Clear(); workTargets.Clear();
            Finished.Value = false; Overtime.Value = false; Winner.Value = TeamId.None;
            EndsAt.Value = NetworkManager.ServerTime.Time + RoundSeconds;
            for (int i = 0; i < Checkpoints.Length; i++)
                Completed.Add(false);
            for (int i = 0; i < CrateHomes.Length; i++)
                Crates.Add(new ArenaCrate { Active = true, Carrier = ArenaCrate.Nobody });
            foreach (var p in NetworkGameManager.Instance.Gaze.Players) { p.WorkProgress.Value = 0; p.Working.Value = false; }
        }
        public int Carrying(NetworkPlayer p)
        {
            for (int i = 0; i < Crates.Count; i++)
                if (Crates[i].Active && Crates[i].Carrier == p.OwnerClientId) return i;
            return -1;
        }
        public void Release(NetworkPlayer p)
        {
            if (!IsServer) return;
            int i = Carrying(p);
            if (i >= 0) {
                Crates[i] = new ArenaCrate { Active = true, Carrier = ArenaCrate.Nobody };
                SoundRpc(ArenaSound.CrateReturn, p.transform.position);
            }
            workTargets.Remove(p.OwnerClientId); p.WorkProgress.Value = 0; p.Working.Value = false;
        }
        public Vector3 SpawnPosition(NetworkPlayer p)
        {
            var anchor = p.Team.Value == TeamId.B ? SpawnB : SpawnA;
            int slot = 0;
            foreach (var other in NetworkGameManager.Instance.Gaze.Players)
                if (other.Team.Value == p.Team.Value && other.OwnerClientId < p.OwnerClientId) slot++;
            return anchor.position + anchor.right * (slot == 0 ? -0.7f : 0.7f);
        }
        public void Respawn(NetworkPlayer p)
        {
            if (!IsServer) return;
            Release(p);
            var anchor = p.Team.Value == TeamId.B ? SpawnB : SpawnA;
            p.ServerRespawn(SpawnPosition(p), anchor.eulerAngles.y);
        }
        public void SpawnEveryone()
        {
            if (!IsServer) return;
            ResetRound();
            foreach (var p in NetworkGameManager.Instance.Gaze.Players) Respawn(p);
        }
        public bool CanReach(NetworkPlayer p, Vector3 point, Transform target = null)
        {
            Vector3 eye = p.Eye.position, delta = point - eye;
            if (delta.sqrMagnitude > Reach * Reach) return false;
            // Require looking toward the interaction, but allow looking slightly above low crates.
            if (Vector3.Dot(p.AcceptedRotation * Vector3.forward, delta.normalized) < 0.25f) return false;
            foreach (var hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore))
                if (!target || !hit.transform.IsChildOf(target)) return false;
            return true;
        }
        public void Pickup(NetworkPlayer p)
        {
            if (!IsServer || !p.FreshInput || Carrying(p) >= 0) return;
            int best = -1; float distance = float.MaxValue;
            for (int i = 0; i < Crates.Count; i++)
            {
                if (!Crates[i].Active || Crates[i].Carrier != ArenaCrate.Nobody || CrateTeams[i] != p.Team.Value || !CanReach(p, CrateHomes[i] + Vector3.up * 0.35f)) continue;
                float d = (CrateHomes[i] - p.transform.position).sqrMagnitude;
                if (d < distance) { best = i; distance = d; }
            }
            if (best >= 0) {
                Crates[best] = new ArenaCrate { Active = true, Carrier = p.OwnerClientId };
                SoundRpc(ArenaSound.CratePickup, p.transform.position);
            }
        }
        [Rpc(SendTo.ClientsAndHost)]
        void SoundRpc(ArenaSound sound, Vector3 position)
        {
            if (ArenaAudio.Instance) ArenaAudio.Instance.PlayEvent(sound, position);
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            if (TeamLobby.Instance.Playing.Value && !Finished.Value && NetworkManager.ServerTime.Time >= EndsAt.Value)
            {
                if (Overtime.Value) { Winner.Value = TeamId.None; Finished.Value = true; }
                else
                {
                    Winner.Value = RegulationWinner(CompletedCount(TeamId.A), CompletedCount(TeamId.B));
                    if (Winner.Value != TeamId.None) Finished.Value = true;
                    else { Overtime.Value = true; EndsAt.Value += OvertimeSeconds; }
                }
            }
            // Returning a disconnected player's crate maintains supply without spawning duplicates.
            for (int i = 0; i < Crates.Count; i++)
                if (Crates[i].Carrier != ArenaCrate.Nobody && !NetworkManager.ConnectedClients.ContainsKey(Crates[i].Carrier))
                    Crates[i] = new ArenaCrate { Active = true, Carrier = ArenaCrate.Nobody };
            foreach (var p in NetworkGameManager.Instance.Gaze.Players)
            {
                if (!p || !p.IsSpawned) continue;
                if (p.transform.position.y < DeathHeight) { Respawn(p); continue; }
                int target = -1, carried = Carrying(p);
                if (p.FreshInput && p.HeldWork)
                    for (int i = 0; i < Completed.Count; i++)
                    {
                        bool own = Teams[i] == p.Team.Value || i == FinalCheckpoint;
                        if (i == FinalCheckpoint && !FinalUnlocked(p.Team.Value)) continue;
                        if ((own ? !Completed[i] && carried >= 0 : Completed[i]) && CanReach(p, Checkpoints[i].position + Vector3.up * 0.6f, Checkpoints[i]))
                        { target = i; break; }
                    }
                p.Working.Value = target >= 0;
                if (target < 0) { p.WorkProgress.Value = 0; workTargets.Remove(p.OwnerClientId); continue; }
                if (!workTargets.TryGetValue(p.OwnerClientId, out int previous) || previous != target) p.WorkProgress.Value = 0;
                workTargets[p.OwnerClientId] = target;
                bool placing = Teams[target] == p.Team.Value || target == FinalCheckpoint;
                p.WorkProgress.Value += Time.fixedDeltaTime * p.Gaze.Value.Work / (placing ? WorkSeconds : SabotageSeconds);
                if (p.WorkProgress.Value < 1) continue;
                if (placing)
                {
                    Completed[target] = true;
                    SoundRpc(ArenaSound.CheckpointComplete, Checkpoints[target].position);
                    Crates[carried] = new ArenaCrate { Active = false, Carrier = ArenaCrate.Nobody };
                    if (target == FinalCheckpoint) { Winner.Value = p.Team.Value; Finished.Value = true; }
                }
                else
                {
                    Completed[target] = false;
                    SoundRpc(ArenaSound.CheckpointSabotage, Checkpoints[target].position);
                    for (int j = 0; j < Crates.Count; j++)
                        if (CrateTeams[j] == Teams[target] && !Crates[j].Active)
                        { Crates[j] = new ArenaCrate { Active = true, Carrier = ArenaCrate.Nobody }; break; }
                    if (Overtime.Value) { Winner.Value = p.Team.Value; Finished.Value = true; }
                }
                p.WorkProgress.Value = 0; p.WorkCycles.Value++; workTargets.Remove(p.OwnerClientId);
            }
        }
        public bool FinalUnlocked(TeamId team)
        {
            for (int i = 0; i < Completed.Count; i++) if (Teams[i] == team && !Completed[i]) return false;
            return team != TeamId.None;
        }
        public static TeamId RegulationWinner(int a, int b) => a > b ? TeamId.A : b > a ? TeamId.B : TeamId.None;
        public int CompletedCount(TeamId team)
        {
            int count = 0;
            for (int i = 0; i < Completed.Count; i++) if (Teams[i] == team && Completed[i]) count++;
            return count;
        }
        void MakeVisuals()
        {
            visualMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            for (int i = 0; i < CrateHomes.Length; i++)
            {
                var box = Instantiate(CratePrefab, transform); box.name = "Supply crate " + i;
                foreach (var c in box.GetComponentsInChildren<Collider>()) { c.enabled = false; Destroy(c); }
                foreach (var tint in box.GetComponentsInChildren<TeamTint>()) tint.Team = CrateTeams[i];
                // Normalize imported props to a readable, carryable size.
                var b = new Bounds(box.transform.position, Vector3.zero);
                foreach (var r in box.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                box.transform.localScale *= 0.7f / Mathf.Max(b.size.x, b.size.y, b.size.z, 0.01f);
                boxes.Add(box.transform);
            }
            for (int i = 0; i < Checkpoints.Length; i++)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "Checkpoint status " + i; marker.transform.SetParent(transform);
                marker.GetComponent<Collider>().enabled = false; Destroy(marker.GetComponent<Collider>());
                marker.GetComponent<Renderer>().sharedMaterial = visualMaterial;
                marker.transform.position = Checkpoints[i].position + Vector3.up * (i == FinalCheckpoint ? 0.52f : 0.12f);
                markers.Add(marker.transform);
            }
        }
        void Update()
        {
            if (!IsSpawned || Crates.Count != boxes.Count) return;
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < boxes.Count; i++)
            {
                var state = Crates[i]; var box = boxes[i];
                box.gameObject.SetActive(state.Active && TeamLobby.GameplayAllowed);
                Vector3 position = CrateHomes[i]; Quaternion rotation = Quaternion.Euler(0, Time.time * 25, 0);
                if (state.Carrier != ArenaCrate.Nobody)
                    foreach (var p in NetworkGameManager.Instance.Gaze.Players)
                        if (p.OwnerClientId == state.Carrier)
                        { position = p.transform.position + p.transform.forward * 0.95f + Vector3.down * 0.15f; rotation = p.transform.rotation; break; }
                else position += Vector3.up * (Mathf.Sin(Time.time * 2 + i) * 0.06f);
                box.SetPositionAndRotation(position, rotation);
            }
            for (int i = 0; i < markers.Count; i++)
            {
                Color color = i == FinalCheckpoint ? new Color(1,0.75f,0.15f) : TeamLobby.Instance.Color(Teams[i]);
                block.SetColor("_BaseColor", Completed[i] ? Color.Lerp(color, Color.white, 0.5f) : color * 0.55f);
                markers[i].GetComponent<Renderer>().SetPropertyBlock(block);
                float pulse = Completed[i] ? 1 : 0.94f + Mathf.Sin(Time.time * 3 + i) * 0.06f;
                markers[i].localScale = new Vector3(pulse, Completed[i] ? 0.24f : 0.035f, pulse);
            }
        }
        public string Score(TeamId team)
        {
            int done = 0, total = 0;
            for (int i = 0; i < Completed.Count; i++) if (Teams[i] == team) { total++; if (Completed[i]) done++; }
            return $"{done}/{total}";
        }
        void OnGUI()
        {
            if (!IsSpawned || !TeamLobby.Instance.Playing.Value) return;
            using (GameUI.Begin())
            {
                if (!Finished.Value)
                {
                    float w = 620, h = 60;
                    float x = GameUI.Width * 0.5f - w * 0.5f;
                    int remaining = Mathf.Max(0, Mathf.CeilToInt((float)(EndsAt.Value - NetworkManager.ServerTime.Time)));
                    bool isUrgent = remaining < 60 || Overtime.Value;

                    // Modern cartoon floating scoreboard
                    GameUI.Card(new Rect(x, 16, w, h));
                    GameUI.Fill(new Rect(x, 16, w, 3), isUrgent ? GameUI.Coral : GameUI.Mint, false);

                    // Team A capsule
                    Color colA = TeamLobby.Instance.Color(TeamId.A);
                    GameUI.Card(new Rect(x + 10, 22, 160, 48), Color.Lerp(colA, GameUI.Ink, 0.4f));
                    GameUI.Fill(new Rect(x + 10, 22, 6, 48), colA, false);
                    GUI.Label(new Rect(x + 22, 26, 140, 22), "TEAM A", GameUI.BoldLabel);
                    GUI.Label(new Rect(x + 22, 46, 140, 20), $"{Score(TeamId.A)} BUILT", GameUI.Small);

                    // Team B capsule
                    Color colB = TeamLobby.Instance.Color(TeamId.B);
                    GameUI.Card(new Rect(x + w - 170, 22, 160, 48), Color.Lerp(colB, GameUI.Ink, 0.4f));
                    GameUI.Fill(new Rect(x + w - 170, 22, 6, 48), colB, false);
                    GUI.Label(new Rect(x + w - 158, 26, 140, 22), "TEAM B", GameUI.BoldLabel);
                    GUI.Label(new Rect(x + w - 158, 46, 140, 20), $"{Score(TeamId.B)} BUILT", GameUI.Small);

                    // Center Round Timer & Subtitle
                    Color timeColor = isUrgent ? (Mathf.Sin(Time.time * 8f) > 0 ? GameUI.Coral : GameUI.Gold) : GameUI.Cream;
                    GUI.color = timeColor;
                    GUI.Label(new Rect(x + 180, 18, 260, 36), $"{remaining / 60:00}:{remaining % 60:00}", GameUI.Display);
                    GUI.color = Color.white;

                    string modeTitle = Overtime.Value ? "★ SUDDEN-DEATH OVERTIME ★" : "RACE TO THE CROWN";
                    GUI.Label(new Rect(x + 180, 52, 260, 20), modeTitle, GameUI.Center);
                }

                if (Finished.Value)
                {
                    // Dark celebration vignette
                    GameUI.Fill(new Rect(0, 0, GameUI.Width, GameUI.Height), new Color(0.015f, 0.035f, 0.065f, 0.85f), false);
                    float w = 620, h = 420;
                    float x = GameUI.Width * 0.5f - w * 0.5f, y = GameUI.Height * 0.5f - h * 0.5f;

                    GameUI.Card(new Rect(x, y, w, h));
                    GameUI.Fill(new Rect(x, y, w, 4), Winner.Value == TeamId.None ? GameUI.Mint : GameUI.Gold, false);

                    // Draw Logo on top of result card
                    GameUI.DrawLogo(new Rect(x + w * 0.5f - 140, y + 16, 280, 64));

                    GUI.Label(new Rect(x + 20, y + 84, w - 40, 26), "ROUND COMPLETE", GameUI.Overline);
                    string winText = Winner.Value == TeamId.None ? "IT'S A DRAW!" : $"TEAM {Winner.Value} WINS!";
                    GUI.Label(new Rect(x + 20, y + 108, w - 40, 56), winText, GameUI.Display);

                    string subText = Winner.Value == TeamId.None ?
                        "Three minutes of overtime. Both sides equally mischievous!" :
                        "Incredible teamwork and watchful coordination took the crown!";
                    GUI.Label(new Rect(x + 30, y + 168, w - 60, 40), subText, GameUI.Center);

                    // Scoreboard recap card
                    float scoreCardW = 400;
                    GameUI.Card(new Rect(x + w * 0.5f - scoreCardW * 0.5f, y + 215, scoreCardW, 70), GameUI.PanelLight);
                    GUI.Label(new Rect(x + w * 0.5f - scoreCardW * 0.5f, y + 225, scoreCardW, 24), $"FINAL SCORE: TEAM A [{Score(TeamId.A)}]  vs  TEAM B [{Score(TeamId.B)}]", GameUI.BoldLabel);
                    GUI.Label(new Rect(x + w * 0.5f - scoreCardW * 0.5f, y + 250, scoreCardW, 22), Overtime.Value ? "Decided in Overtime" : "Regulation Match", GameUI.Small);

                    float btnY = y + 330;
                    if (NetworkManager.IsServer && GameUI.Button(new Rect(x + 35, btnY, 260, 48), "BACK TO LOBBY", GameUI.Gold))
                        TeamLobby.Instance.ReturnToLobby();
                    if (GameUI.Button(new Rect(NetworkManager.IsServer ? x + 325 : x + 180, btnY, 260, 48), "LEAVE MATCH", GameUI.Cream))
                        NetworkGameManager.Instance.Stop();
                }
            }
        }
    }
}
