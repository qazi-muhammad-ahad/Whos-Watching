# Multiplayer Setup — Who's Watching?

Implementation roadmap and continuation handoff for the Unity 3D URP project at this repository root. [Plan.md](Plan.md) defines behavior; [Todo.md](Todo.md) has task IDs; [progression.md](progression.md) records actual completion. This document covers the implemented LAN foundation and the remaining game milestones. Consult Docs/TestLog.md for actual validation.

## Baseline

Use **Unity 6000.3.23f1**, explicitly approved by the user. The requested 6000.3.24f1 is a future patch upgrade requiring verification. Editor and URP version numbers differ. Target Windows, keyboard/mouse, direct LAN, player-hosted server, 1v1 and 2v2, maximum four including host. No Relay/cloud login during the baseline.

The repository now contains a saved Main scene, registered primitive player prefab, resolved networking packages, host/join controls, server movement and EditMode tests. This is a development movement lobby; basic teams and gaze are implemented; production readiness, phase gating and objectives remain pending. The scene generator is an initial-creation utility and refuses to overwrite Main.

## 1. Finish the project baseline — SET

1. Install the pinned editor with Windows build support; add this repository root in Unity Hub.
2. Pin ProjectSettings/ProjectVersion.txt and package manifest. Pinned and resolved versions are URP 17.3.0, NGO 2.13.1, Transport 2.7.4 and Input System 1.20.0. Resolve compatibility through Package Manager; retain Test Framework/uGUI as needed. Commit the generated packages-lock.json.
3. Set product name to Who's Watching?, version 0.1.0, Windows target, text serialization, visible meta files and background execution. Commit Assets with .meta, Packages and ProjectSettings. Keep caches, logs and builds ignored.
4. Create Assets/_WhosWatching with Scripts/{Core,Multiplayer,Player,Teams,Gaze,Objectives,Lobby,UI}, Settings, Prefabs, Materials, Art and Tests/{EditMode,PlayMode}. Follow Plan section 15 for the final layout.
5. Create Scenes/Main.unity with floor, cover, walls, light, menu camera and team spawns. Make it the initial enabled build scene. Verify Graphics/Quality URP references and launch a blank Windows build.
6. Record versions and ownership in Docs/Decisions.md and test evidence in Docs/TestLog.md. Another teammate must reproduce from a clean clone before G0 is closed.

## 2. Direct networking — NET

- One NetworkManager and UnityTransport, client/server topology, 30 Hz network tick, connection approval and scene management. Register a primitive player prefab with one NetworkObject and server-authoritative NetworkTransform.
- NetworkGameManager owns Host/Join/Cancel/Leave, validation, transport and callbacks. Host binds 0.0.0.0:7777; clients enter the reachable host IPv4. Loopback 127.0.0.1 is only a same-machine test.
- Temporary controls show address/port, connection status, failures and cancellation. Disable duplicate starts. Use about ten seconds for join timeout. Await shutdown before locally reloading Main; prevent persistent duplicate managers.
- Approval validates a small exact protocol token, payload size, Lobby phase and four-player cap. Include pending reservations in capacity without counting a client twice. Release reservations on failure/disconnect/timeout. Protocol metadata is not authentication.
- NGO creates and despawns one player per connection. Maintain a dictionary by actual client ID; never index arrays by client ID. Register callbacks once and unregister on teardown.
- Test two processes, disconnect/rejoin, bad token, fifth join, timeout/cancel and retries. Then test a second PC on the same LAN and document its actual result.

## 3. Server movement and input — PLY

- Player root: NetworkObject, server NetworkTransform, CharacterController, identity/input adapter and motor. Children: VisualRoot, fixed EyeAnchor/ChestAnchor and cosmetic socket. Only owner enables input, Camera and AudioListener.
- WASD/mouse/E/Escape actions. Send input at 20 Hz: sequence, round ID, movement, yaw/pitch, input-enabled, held-interact, optional objective ID plus presence flag. Host uses the same validation route.
- Validate ownership from RPC metadata, finite values, round, sequence and rate. Clamp movement/pitch and normalize yaw. Define wrap-safe ordering; duplicates and reordered inputs cannot overwrite newer state.
- Server moves once at 30 Hz with 5 m/s base speed, gravity and world collision. Disable player/player movement collision. No jump/sprint/pushing. Server owns speed modifiers and transform.
- Expire movement/work/gaze after 0.25 seconds without fresh accepted input. Idle clients send heartbeats. Menus and non-playing phases suppress gameplay; camera look remains immediate locally.
- Server handles spawn/reset/out-of-bounds recovery. G1 requires independent host/client movement and a recorded separate-PC attempt.

## 4. Gaze vertical slice — GAZ/EFF

Use the full rectangular camera viewport, server-owned vertical FOV (default 60 degrees), validated owner aspect, 15 m range, 0.15 second independent pair lock and 20 Hz server evaluation. Affect ALL visible players simultaneously. Fixed body samples must be both inside the viewport and unobstructed. World and GazeTarget layers block rays; source colliders/triggers/cosmetics do not. The dedicated GazeBody capsule matches the standard visible body and is excluded from physical collisions; do not use the movement CharacterController query shape for visibility.

Maintain separate timers per observer-target pair. Release invalid pairs without restarting unrelated targets. Sum every incoming ally/enemy once per target, then apply configurable final bounds: `clamp(1 + support*allies - disruption*enemies, Minimum, Maximum)`. Replicate all outgoing IDs/counts and incoming counts/multipliers. Support and disruption can occur simultaneously in both directions.

Unity tuning: Project window > Assets > _WhosWatching > Settings > GazeConfig. Inspector sections **Per-observer contributions** and **Stacking limits** control the individual amounts and final bounds. Defaults remain 0.35/0.25 and 0.50–1.50; users can change them. Stop Play mode, save settings and rebuild for distribution. Main > Networking > Gaze System references this asset.
Use a development-only team override and five-second work harness. Verify allies, enemies, mixed stacks, walls, corners, switching, stale input, disconnect and reset before content work. G2 requires actual networked movement/work effects.

## 5. Complete the match — LOB/OBJ/MAT

Server validates balanced 1v1/2v2 teams, ready states and host start. Extra joiners in a 1v1 lobby wait unassigned. Selection changes clear all readiness. Reject joins outside Lobby.

Implement Lobby -> Countdown -> Playing -> Results -> Lobby using server deadlines and round IDs. Three sequential stations per team; 12 seconds base duration, 2 m range, one worker, retained progress. Server checks team/order/range/LOS/held input. After initial aim acquisition, workers may look away while holding valid work.

Advance all stations before resolving results; same-step final completions draw. At 180 seconds compare completed count, then current progress within 0.001 tolerance. Clamp final work interval to remaining time. Disconnect during countdown/play aborts; host loss returns clients to menu. Rematch resets transient state/readiness while retaining the session.

## 6. Tests and evidence

| Test layer | Required coverage |
| --- | --- |
| EditMode | Payload/capacity/phase rejection; IPv4/port validation; sequence wrap/replay; input freshness; gaze selection/stack caps; team eligibility; work integration and results |
| PlayMode | NGO ownership/spawn; reservation release; one owner camera; shutdown/retry; replicated variables; movement/occlusion |
| Multi-process | Host + one/three clients; fifth rejection; invalid build; cancel/timeout; host loss; three consecutive reset cycles |
| Separate PCs | Both host roles, full 1v1/2v2, independent inputs; explicitly record machine/process counts |
| Adverse network | Delay/loss/reordering; stale and prior-round input cannot reactivate work |
| Release | Clean clone build, extracted archive on another PC, identical builds on peers, Plan T01–T17 |

Run tests through Window > General > Test Runner. For batch EditMode, close the open Editor, then use the installed Unity executable with `-batchmode -nographics -projectPath <repo> -runTests -testPlatform EditMode -testResults Logs/EditMode.xml -logFile Logs/EditMode.log`. Do not add -quit; allow the test runner to finish. Confirm a completed report, not merely process launch.

Each TestLog entry records date, revision/build, editor/packages, topology, scenario, steps, expected/actual, pass/fail and defects. Pure unit tests do not verify transport or LAN. All 13 initial EditMode policy tests passed; see Docs/TestLog.md for subsequent build/process evidence.

## 7. Integrate art later

Artists deliver URP-compatible visual prefabs/materials under Art or isolated third-party folders. Preserve root NetworkObject, controller, transform, fixed anchors, references and GUIDs; replace only VisualRoot children. Keep metre scale and team tint. Hats have no colliders, NetworkObjects or gameplay logic. Record licenses. Recheck collision/LOS/readability after every visual replacement. Primitive art remains sufficient for network tests.

## 8. Upgrade and handoff

Upgrade to 6000.3.24f1 on a branch after preserving the baseline. Inspect serialized/package changes, compile, run tests and compare URP visuals plus two/four-process sessions. Standardize the team on one editor patch and identical builds. Compatibility is expected for a patch update but cannot be guaranteed without these runs. [Unity release notes](https://unity.com/releases/editor/whats-new/6000.3.24f1).

Use the pinned package APIs: [NGO 2.13](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/index.html). Prefer its local Documentation~ over unrelated tutorial versions. Relay stays gated until G5 and tested LAN exists.

After each milestone update progression.md and check only evidence-backed Todo tasks. Observe the account reserve before long runs. The gaze slice is now implemented. Next actions are interactive gaze playtests, separate-PC LAN and lifecycle acceptance, then objectives and round state. The initial host/client movement harness and two/four-process smoke tests pass; it is not yet the full game.

## Implemented gaze workflow

Open Main and host/join with the same new build. Capture mouse, keep players in the camera view and watch the confirmed HUD. Host development buttons can put two players on the same team. Hold E for the five-second work demonstration; use central cover to remove gaze. Escape disables outgoing intent. Inspector tuning lives in Assets/_WhosWatching/Settings/GazeConfig.asset. After rebuilding, run Tools/GazeSmoke.ps1 with -Players 2 or 4. See Docs/TestLog.md for evidence. This is the gaze milestone, not completed objective rounds.

## Screen-wide rule change — implemented

The user explicitly requested all visible players in an observer camera FOV/screen count simultaneously. This supersedes the narrow 8-degree, one-outgoing-target design. Implemented with independent pair locks, full viewport checks, standard-body LOS and multi-target HUD/state. Protocol is now 3-screen-gaze. Run Tools/ScreenGazeSmoke.ps1 after building; see Docs/TestLog.md and progression.md for actual validation results.
