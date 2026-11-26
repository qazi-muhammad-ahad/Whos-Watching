using System;
using UnityEngine;

namespace WhosWatching
{
    public enum ObjectiveType : byte
    {
        PickupCrate,
        DeliverToCheckpoint,
        SabotageEnemy,
        CaptureFinalCrown,
        DefendOrDistract
    }

    [DisallowMultipleComponent]
    public sealed class ObjectiveGuidance : MonoBehaviour
    {
        public static ObjectiveGuidance Instance { get; private set; }

        public ObjectiveType CurrentType { get; private set; }
        public string PrimaryText { get; private set; } = "GET READY!";
        public string SecondaryText { get; private set; } = "Follow markers to complete objectives";
        public Vector3 TargetWorldPosition { get; private set; }
        public float TargetDistance { get; private set; }
        public bool HasActiveTarget { get; private set; }

        // 3D visual beacon marker
        Transform beaconTransform;
        Renderer beaconRenderer;
        Material beaconMaterial;
        MaterialPropertyBlock propBlock;

        // Banner toast notification on objective completion
        string toastTitle = "";
        string toastSubtitle = "";
        float toastTimer = 0f;
        int lastCompletedA = 0, lastCompletedB = 0;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            propBlock = new MaterialPropertyBlock();
            CreateBeaconVisual();
        }

        void OnDestroy()
        {
            if (beaconTransform) Destroy(beaconTransform.gameObject);
            if (beaconMaterial) Destroy(beaconMaterial);
            if (Instance == this) Instance = null;
        }

        void CreateBeaconVisual()
        {
            var beaconObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beaconObj.name = "CartoonObjectiveBeacon";
            beaconObj.transform.SetParent(transform, false);
            var col = beaconObj.GetComponent<Collider>();
            if (col) Destroy(col);

            beaconTransform = beaconObj.transform;
            beaconRenderer = beaconObj.GetComponent<Renderer>();

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            beaconMaterial = new Material(shader);
            beaconRenderer.sharedMaterial = beaconMaterial;
            beaconObj.SetActive(false);
        }

        void Update()
        {
            var arena = ArenaGameplay.Instance;
            var lobby = TeamLobby.Instance;
            var session = NetworkGameManager.Instance;

            if (!arena || !arena.IsSpawned || !lobby || !lobby.Playing.Value)
            {
                HasActiveTarget = false;
                if (beaconTransform) beaconTransform.gameObject.SetActive(false);
                return;
            }

            var local = session && session.Network.LocalClient?.PlayerObject ?
                        session.Network.LocalClient.PlayerObject.GetComponent<NetworkPlayer>() : null;

            if (!local || !local.IsSpawned)
            {
                HasActiveTarget = false;
                if (beaconTransform) beaconTransform.gameObject.SetActive(false);
                return;
            }

            // Track completions for celebration toasts
            int countA = arena.CompletedCount(TeamId.A);
            int countB = arena.CompletedCount(TeamId.B);
            if (countA > lastCompletedA)
            {
                ShowToast(local.Team.Value == TeamId.A ? "CHECKPOINT SECURED!" : "ENEMY SCORED!", 
                          local.Team.Value == TeamId.A ? "Great teamwork! Keep advancing!" : "Watch out! Reclaim ground with sabotage!", 3.5f);
            }
            else if (countA < lastCompletedA)
            {
                ShowToast(local.Team.Value == TeamId.A ? "CHECKPOINT SABOTAGED!" : "SABOTAGE SUCCESSFUL!",
                          local.Team.Value == TeamId.A ? "Enemy dismantled your crate!" : "Enemy progress reset!", 3.5f);
            }
            if (countB > lastCompletedB)
            {
                ShowToast(local.Team.Value == TeamId.B ? "CHECKPOINT SECURED!" : "ENEMY SCORED!",
                          local.Team.Value == TeamId.B ? "Great teamwork! Keep advancing!" : "Watch out! Reclaim ground with sabotage!", 3.5f);
            }
            else if (countB < lastCompletedB)
            {
                ShowToast(local.Team.Value == TeamId.B ? "CHECKPOINT SABOTAGED!" : "SABOTAGE SUCCESSFUL!",
                          local.Team.Value == TeamId.B ? "Enemy dismantled your crate!" : "Enemy progress reset!", 3.5f);
            }
            lastCompletedA = countA;
            lastCompletedB = countB;

            if (toastTimer > 0) toastTimer -= Time.deltaTime;

            // Evaluate optimal active objective
            EvaluateObjective(arena, lobby, local);

            // Update 3D beacon visual
            UpdateBeacon(local, lobby);
        }

        void EvaluateObjective(ArenaGameplay arena, TeamLobby lobby, NetworkPlayer local)
        {
            var team = local.Team.Value;
            var enemyTeam = team == TeamId.A ? TeamId.B : TeamId.A;
            int carried = arena.Carrying(local);
            bool isCarrying = carried >= 0;

            // Check if own team checkpoints are all done
            bool ownDone = true;
            int nextOwnCheckpoint = -1;
            for (int i = 0; i < arena.Checkpoints.Length; i++)
            {
                if (arena.Teams[i] == team && !arena.Completed[i])
                {
                    ownDone = false;
                    nextOwnCheckpoint = i;
                    break;
                }
            }

            // Check enemy checkpoints
            int enemyCheckpointToSabotage = -1;
            for (int i = 0; i < arena.Checkpoints.Length; i++)
            {
                if (arena.Teams[i] == enemyTeam && arena.Completed[i])
                {
                    enemyCheckpointToSabotage = i;
                    break;
                }
            }

            bool finalUnlocked = arena.FinalUnlocked(team);

            if (arena.Overtime.Value)
            {
                if (isCarrying && finalUnlocked)
                {
                    CurrentType = ObjectiveType.CaptureFinalCrown;
                    TargetWorldPosition = arena.Checkpoints[arena.FinalCheckpoint].position;
                    PrimaryText = "OVERTIME: DELIVER TO THE CROWN TO WIN!";
                    SecondaryText = "Hold [E] at the center platform. Stare down rivals!";
                    HasActiveTarget = true;
                }
                else if (enemyCheckpointToSabotage >= 0)
                {
                    CurrentType = ObjectiveType.SabotageEnemy;
                    TargetWorldPosition = arena.Checkpoints[enemyCheckpointToSabotage].position;
                    PrimaryText = "OVERTIME: SABOTAGE ENEMY CHECKPOINT TO WIN!";
                    SecondaryText = "Hold [E] at their station to trigger sudden-death victory!";
                    HasActiveTarget = true;
                }
                else
                {
                    CurrentType = ObjectiveType.PickupCrate;
                    TargetWorldPosition = FindNearestCrate(arena, local, team);
                    PrimaryText = "OVERTIME: GRAB A CRATE TO WIN!";
                    SecondaryText = "Press [F] at your supply to grab a delivery crate";
                    HasActiveTarget = true;
                }
            }
            else if (isCarrying)
            {
                if (!ownDone && nextOwnCheckpoint >= 0)
                {
                    CurrentType = ObjectiveType.DeliverToCheckpoint;
                    TargetWorldPosition = arena.Checkpoints[nextOwnCheckpoint].position;
                    PrimaryText = $"DELIVER CRATE TO STATION {(nextOwnCheckpoint % 2) + 1}";
                    SecondaryText = "Hold [E] at the station. Have allies look at you for 1.8x build speed!";
                    HasActiveTarget = true;
                }
                else if (finalUnlocked)
                {
                    CurrentType = ObjectiveType.CaptureFinalCrown;
                    TargetWorldPosition = arena.Checkpoints[arena.FinalCheckpoint].position;
                    PrimaryText = "DELIVER CRATE TO CENTER CROWN PLATFORM!";
                    SecondaryText = "Hold [E] at the golden crown platform to win the match!";
                    HasActiveTarget = true;
                }
                else
                {
                    CurrentType = ObjectiveType.DefendOrDistract;
                    TargetWorldPosition = arena.Checkpoints[arena.FinalCheckpoint].position;
                    PrimaryText = "DEFEND SUPPLY & DELIVER TO CENTER";
                    SecondaryText = "Stay alert. Press [Q] if you need to return your crate.";
                    HasActiveTarget = true;
                }
            }
            else
            {
                if (!ownDone)
                {
                    CurrentType = ObjectiveType.PickupCrate;
                    TargetWorldPosition = FindNearestCrate(arena, local, team);
                    PrimaryText = "PICK UP A TEAM SUPPLY CRATE";
                    SecondaryText = "Walk up to your base crate and press [F] to pick it up";
                    HasActiveTarget = true;
                }
                else if (finalUnlocked)
                {
                    CurrentType = ObjectiveType.PickupCrate;
                    TargetWorldPosition = FindNearestCrate(arena, local, team);
                    PrimaryText = "GET FINAL CRATE FOR CENTER CROWN!";
                    SecondaryText = "Pick up crate [F] and rush to the center platform!";
                    HasActiveTarget = true;
                }
                else if (enemyCheckpointToSabotage >= 0)
                {
                    CurrentType = ObjectiveType.SabotageEnemy;
                    TargetWorldPosition = arena.Checkpoints[enemyCheckpointToSabotage].position;
                    PrimaryText = "SABOTAGE ENEMY CHECKPOINT!";
                    SecondaryText = "Sneak over and hold [E] to dismantle their crate";
                    HasActiveTarget = true;
                }
                else
                {
                    CurrentType = ObjectiveType.DefendOrDistract;
                    TargetWorldPosition = arena.Checkpoints[arena.FinalCheckpoint].position;
                    PrimaryText = "PATROL & PROTECT YOUR BASE";
                    SecondaryText = "Watch allies to boost them, punch rivals with [Left Click]";
                    HasActiveTarget = true;
                }
            }

            TargetDistance = Vector3.Distance(local.transform.position, TargetWorldPosition);
        }

        Vector3 FindNearestCrate(ArenaGameplay arena, NetworkPlayer player, TeamId team)
        {
            Vector3 best = player.transform.position;
            float minDist = float.MaxValue;
            for (int i = 0; i < arena.Crates.Count; i++)
            {
                if (arena.CrateTeams[i] == team && arena.Crates[i].Active && arena.Crates[i].Carrier == ArenaCrate.Nobody)
                {
                    float d = Vector3.Distance(player.transform.position, arena.CrateHomes[i]);
                    if (d < minDist) { minDist = d; best = arena.CrateHomes[i]; }
                }
            }
            return best;
        }

        void UpdateBeacon(NetworkPlayer local, TeamLobby lobby)
        {
            if (!beaconTransform || !HasActiveTarget)
            {
                if (beaconTransform) beaconTransform.gameObject.SetActive(false);
                return;
            }

            beaconTransform.gameObject.SetActive(true);

            float floatY = Mathf.Sin(Time.time * 3f) * 0.25f + 1.2f;
            beaconTransform.position = TargetWorldPosition + Vector3.up * floatY;
            beaconTransform.rotation = Quaternion.Euler(0, Time.time * 60f, 0);

            float pulse = 0.85f + Mathf.Sin(Time.time * 5f) * 0.15f;
            beaconTransform.localScale = new Vector3(pulse * 0.8f, 0.25f, pulse * 0.8f);

            Color targetColor = CurrentType switch
            {
                ObjectiveType.CaptureFinalCrown => GameUI.Gold,
                ObjectiveType.SabotageEnemy => new Color(1f, 0.35f, 0.35f),
                ObjectiveType.PickupCrate => GameUI.Mint,
                _ => lobby.Color(local.Team.Value)
            };

            propBlock.SetColor("_BaseColor", Color.Lerp(targetColor, Color.white, 0.3f));
            beaconRenderer.SetPropertyBlock(propBlock);
        }

        public void ShowToast(string title, string subtitle, float duration = 3.5f)
        {
            toastTitle = title;
            toastSubtitle = subtitle;
            toastTimer = duration;
        }

        public void DrawGUI(Camera playerCamera)
        {
            if (!HasActiveTarget) return;

            // Draw Toast alert if active
            if (toastTimer > 0)
            {
                float toastW = 440, toastH = 64;
                float toastX = GameUI.Width / 2 - toastW / 2;
                float toastY = 120;
                GameUI.Card(new Rect(toastX, toastY, toastW, toastH), new Color(0.04f, 0.08f, 0.12f, 0.95f));
                GameUI.Fill(new Rect(toastX, toastY, toastW, 3), GameUI.Gold, false);
                GUI.Label(new Rect(toastX, toastY + 8, toastW, 26), toastTitle, GameUI.Step);
                GUI.Label(new Rect(toastX, toastY + 34, toastW, 22), toastSubtitle, GameUI.Small);
            }

            // Draw Objective Guidance Banner (Top Center below scoreboard)
            float bannerW = 540, bannerH = 46;
            float bannerX = GameUI.Width / 2 - bannerW / 2;
            float bannerY = 82;

            GameUI.Card(new Rect(bannerX, bannerY, bannerW, bannerH), new Color(0.035f, 0.065f, 0.09f, 0.94f));
            Color accentColor = CurrentType == ObjectiveType.CaptureFinalCrown ? GameUI.Gold :
                               CurrentType == ObjectiveType.SabotageEnemy ? new Color(1f, 0.35f, 0.35f) : GameUI.Mint;
            GameUI.Fill(new Rect(bannerX, bannerY, 4, bannerH), accentColor, false);

            string distTag = TargetDistance < 2.8f ? " [WITHIN REACH]" : $" [{Mathf.RoundToInt(TargetDistance)}m]";
            GUI.Label(new Rect(bannerX + 16, bannerY + 4, bannerW - 32, 22), PrimaryText + distTag, GameUI.Heading);
            GUI.Label(new Rect(bannerX + 16, bannerY + 24, bannerW - 32, 18), SecondaryText, GameUI.Small);

            // Screen-Space Off-Screen Radar Arrow Pointer
            if (playerCamera != null)
            {
                Vector3 screenPos = playerCamera.WorldToScreenPoint(TargetWorldPosition + Vector3.up * 0.8f);
                bool isBehind = screenPos.z < 0;

                float screenX = screenPos.x / GameUI.Scale;
                float screenY = (Screen.height - screenPos.y) / GameUI.Scale;

                // Check if target is off-screen or behind camera
                bool offScreen = isBehind || screenX < 40 || screenX > GameUI.Width - 40 || screenY < 40 || screenY > GameUI.Height - 40;

                if (offScreen)
                {
                    // Clamp to screen bounds with padding
                    Vector2 screenCenter = new Vector2(GameUI.Width * 0.5f, GameUI.Height * 0.5f);
                    Vector2 dir = isBehind ? new Vector2(-screenX + screenCenter.x, -screenY + screenCenter.y) :
                                            new Vector2(screenX - screenCenter.x, screenY - screenCenter.y);
                    dir.Normalize();

                    float edgeDist = Mathf.Min(GameUI.Width * 0.44f, GameUI.Height * 0.42f);
                    Vector2 clampedPos = screenCenter + dir * edgeDist;

                    float arrowSize = 36;
                    GameUI.Card(new Rect(clampedPos.x - arrowSize * 0.5f, clampedPos.y - arrowSize * 0.5f, arrowSize, arrowSize), accentColor);
                    GUI.Label(new Rect(clampedPos.x - 30, clampedPos.y - 12, 60, 24), $"{Mathf.RoundToInt(TargetDistance)}m", GameUI.Center);
                }
                else
                {
                    // Draw on-screen floating 2D target pin
                    float pinW = 90, pinH = 26;
                    GameUI.Card(new Rect(screenX - pinW * 0.5f, screenY - 35, pinW, pinH), new Color(0.04f, 0.08f, 0.12f, 0.85f));
                    GameUI.Fill(new Rect(screenX - pinW * 0.5f, screenY - 35, pinW, 2), accentColor, false);
                    GUI.Label(new Rect(screenX - pinW * 0.5f, screenY - 34, pinW, pinH), $"{Mathf.RoundToInt(TargetDistance)}m", GameUI.Center);
                }
            }
        }
    }
}
