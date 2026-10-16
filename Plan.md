# Who's Watching? — Game and Technical Plan

**Document purpose:** define what the game contains, how its systems fit together, and what the team is committing to deliver. [Todo.md](Todo.md) is the ordered implementation checklist. [progression.md](progression.md) records actual project status.

**Planning baseline:** 16 October 2026. Approximately five beginner-to-intermediate Unity developers; a possible 48-hour development window. A Unity URP LAN development harness now exists. This document describes the full target design; consult progression.md for actual implemented scope. Time estimates are targets and assume the team can install a common editor and test on separate PCs early.

## 1. Executive summary

Who's Watching? is a small competitive multiplayer objective race where attention is a resource. Looking at a teammate makes that teammate faster; looking at an opponent makes that opponent slower. Players choose where to stand, which objective to advance, whom to support, and whom to obstruct with their gaze.

The first release supports **1v1 and 2v2, with four total players including the host**. One player's computer runs the server and also participates. A compact symmetrical arena, readable team colors, and one reusable hold-to-complete objective type are enough to demonstrate the concept. LAN connections are the delivery baseline. Internet play through Relay is a gated stretch goal.

The first playable milestone must prove two connected players, movement, looking, teams, ally buffs, and enemy debuffs with primitive geometry. A temporary developer team selector permits testing two allies even though a normal two-player match is 1v1. Cosmetics, decorative environments, and polished menus follow that milestone.

The 48-hour target is a playable jam prototype, not a commercial multiplayer launch. Reserve the final 12 hours for testing and fixes, and the final 4 hours for release packaging and contingency. Cut optional work when gates slip; do not label a build that lacks 2v2 or either gaze effect as the full requested MVP.

## 2. Fixed pillars and explicit design assumptions

### Fixed requirements

1. Two opposing teams compete through objectives.
2. Normal match formats are 1v1 and 2v2; maximum population is four.
3. Looking at an ally buffs the ally; looking at an enemy debuffs the enemy.
4. Gaze remains the defining mechanic and cannot pass through solid walls.
5. Multiplayer uses a player-hosted match; dedicated servers are unnecessary.
6. Team colors remain clearly visible regardless of cosmetics.
7. Cosmetics are visual only and selected before play.
8. The architecture may accommodate more players later, but 3v3 is outside initial implementation and testing.

### Defaults chosen to make implementation concrete

These are recommendations to playtest, not extra requirements from the original concept.

| Decision | Initial rule | Reason |
| --- | --- | --- |
| Platform | Windows desktop, keyboard and mouse | One build target reduces integration risk |
| Perspective | First person | Camera aim maps directly to gaze |
| Movement | Walk, look, ground collision; no jump, sprint, crouch, combat, or health | Keeps authoritative movement small |
| Mode | Race to finish three sequential team-owned stations | One objective implementation produces a complete round |
| Arena | One mirrored arena with six station placements, cover, and cross-sightlines | Supports support, disruption, and repositioning |
| Round duration | 180 seconds initially | Prevents sessions without an ending |
| Gaze | Automatic while looking; one outgoing target per player | No extra ability button or targeting menu |
| Effects | Short acquisition delay, bounded additive stacking, immediate release on next server evaluation | Predictable and easy to debug |
| Interaction | Hold E while within range and unobstructed | Familiar, low-cost objective mechanic |
| Persistence | Settings and preferred cosmetic only, stored locally | No accounts or inventory needed for LAN |
| Session topology | One persistent scene with menu/lobby/game/results panels | Avoids early synchronized scene transitions |

In 1v1, teammate support naturally cannot occur during a normal round because there is no teammate. Enemy disruption still functions. Ally support is verified in the early debug setup and in real 2v2 matches. Do not add bots or a replacement support mechanic to compensate.

## 3. Scope and release priorities

| Tier | Contents | Shipping rule |
| --- | --- | --- |
| **P0 — MVP** | Host/join over LAN; two/four-player matches; authoritative movement and gaze; walls block effects; bounded movement/task multipliers; one objective type; score/timer/results; simple lobby and readiness; selectable team-color pair; minimal gaze HUD; rematch through lobby; graceful session failure; tested standalone builds | All required for the requested minimum prototype |
| **P1 — important** | None plus two simple hats; local settings; basic sound cues; small results statistics; improved objective signs and help text | Complete after the core loop; cut only with the cut documented |
| **P2 — optional polish** | Character animation, small particles, richer environment art, nicer results transitions, extra cosmetic variants | Never delay a P0 gate |
| **P3 — stretch** | Relay join codes, a second objective type, another map | Only after tested P0/P1 work and before freeze; choose at most one substantial stretch feature |
| **Post-jam** | 3v3, prediction/reconciliation, host migration, live reconnect, public matchmaking, rankings, inventory, progression, complex physics | No implementation in the 48-hour scope |

Lightweight cosmetics are part of the intended first version, but are an explicit deadline fallback rather than a prerequisite for proving the gameplay. The default cosmetic is always valid. Color selection is implemented as selection of a distinct color **pair**, not arbitrary RGB values that could make teams indistinguishable.

## 4. Recommended Unity technology stack

| Area | Choice | Setup constraint |
| --- | --- | --- |
| Editor | One mutually available, supported Unity 6 LTS editor patch | Verify the selected package versions against that editor, then pin it for everyone |
| Rendering | The team's familiar 3D template; prefer URP if no established preference | Keep the selected pipeline for the jam; use simple opaque materials |
| Networking | Netcode for GameObjects (NGO), client-server topology | Do not select distributed authority |
| Transport | Unity Transport (UTP) | Direct IP/port first; Relay transport configuration only if approved by the milestone gate |
| Input | Unity Input System with a small action asset | One input backend for movement, look, interact, and menu |
| Player physics | CharacterController; static environment colliders | Server moves players; no networked rigidbody gameplay |
| UI | Canvas/uGUI and TextMeshPro | Use the team's familiar workflow; no UI framework migration |
| Configuration | Small ScriptableObject assets and serialized prefab fields | Inspector-tunable values with one authoritative source |
| Version control | Git, text serialization, visible meta files | Commit Assets with their .meta files, Packages, ProjectSettings |
| Tests | Unity Test Framework for pure rules plus manual network sessions | Test actual host/client behavior in builds early |

The initial project now pins Unity 6000.3.23f1, URP 17.3.0, NGO 2.13.1 and Transport 2.7.4; consult Packages and Docs/TestLog.md for resolved versions and validation. Setup must record the selected editor in `ProjectSettings/ProjectVersion.txt` and resolved packages in `Packages/manifest.json` and `Packages/packages-lock.json`. Do not assume the newest NGO release fits every Unity 6 LTS patch. Use documentation matching the installed package.

NGO's official package documentation is the API reference; the older multiplayer documentation site identifies itself as no longer updated. Avoid combining RPC or authority examples from unrelated NGO versions. [Unity NGO package documentation](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@latest/), [older documentation notice and NetworkTransform overview](https://docs-multiplayer.unity3d.com/netcode/current/components/networktransform/).

## 5. Player experience and core loop

1. Host creates a LAN session; other players enter its address and port.
2. Players select an available team slot and a cosmetic. The host chooses 1v1 or 2v2 and a team-color pair.
3. Everyone readies; the host starts when the roster is valid.
4. A three-second countdown places players at their team's spawn points and introduces the first station.
5. Players travel to stations, hold E to work, support an ally by looking at them, or interrupt an enemy's efficiency through gaze.
6. Finishing a station unlocks that team's next station. Both teams can work independently.
7. The first team to finish all three stations wins, subject to the same-tick draw rule. The timer provides a second ending condition.
8. Results display; the host returns everyone to the lobby for another ready/start cycle, or players leave.

There is no damage, death, elimination, weapon system, respawn economy, or physical pushing. World collisions block walking; players do not physically block each other. This prevents door griefing and avoids networked player-pushing physics.

## 6. Multiplayer architecture and authority

### 6.1 Topology

The host runs NGO's server and a local client. Other clients send input and requests to that server. The server decides gameplay state and replicates it. The host is trusted; the prototype does not defend against a malicious host or implement production anti-cheat.

Direct LAN means players can reach the host's local address on the same network. An address field is not an automatic Internet NAT-traversal solution. Explain LAN requirements in the build instructions; do not make router port forwarding a dependency of the jam submission.

### 6.2 Authority matrix

| Concern | Local client | Host/server | Replication |
| --- | --- | --- | --- |
| Keyboard/mouse | Read owned player's input | Validate received values and sender | Small input snapshots |
| Camera | Render immediate local look and sensitivity | No active gameplay Camera needed for validation | Only aim angles, never the Camera object |
| Movement | Submit direction; display replicated motion | Run CharacterController, gravity, speed modifiers, and bounds reset | Server-authoritative NetworkTransform |
| Aim | Submit yaw/pitch | Normalize yaw, clamp pitch, reject invalid/stale input | Accepted body yaw and aim pitch for presentation |
| Teams/ready/colors/cosmetics | Request changes in lobby | Validate slots, IDs, phase, and authority | Server-written state |
| Gaze | Optional candidate preview | Select target, validate LOS/lock, calculate effects | Confirmed target plus incoming counts/multipliers |
| Objectives | Request/refresh held interaction | Validate target, range, LOS, phase, worker, and advance progress | Progress, completion, worker |
| Match/timer/results | Draw current state | Own state machine, clock deadlines, scores, winner, reset | Phase, deadlines, scores, result |
| UI/audio/particles | Render from accepted state | No per-frame presentation decisions | Occasional meaningful events only |

Network object ownership gives the client its input identity; it does not give permission to write objective progress, movement transforms, speed effects, or results. State changes must originate from the server. NGO NetworkVariables hold durable replicated state; RPCs carry requests or one-time events. Initialize views from the current value as well as subscribing to changes, so a newly spawned view is immediately correct. [Unity NetworkVariables](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@3.1/manual/basics/networkvariable.html), [Unity RPCs](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@3.1/manual/advanced-topics/message-system/rpc.html).

### 6.3 Movement and timing contract

- Start with 30 Hz host simulation and approximately 20 input snapshots per second; profile and tune rather than synchronizing input every render frame.
- A snapshot contains sequence number, round ID, normalized movement vector, view yaw/pitch, held-interact flag, and optional requested objective ID. Use an explicit target-present flag, not zero as a universal invalid network ID.
- Use a replaceable/unreliable snapshot stream where supported by the selected NGO version. Later snapshots supersede older ones. A repeated held flag avoids depending on a single release packet.
- Server obtains sender identity from RPC metadata/ownership, not a player ID supplied by the client. Reject out-of-order sequence numbers, non-finite numbers, invalid IDs, wrong-round input, and excessive request rates.
- Expire movement, interaction, and outgoing gaze when no fresh input arrives for 0.25 seconds. Give network-stress tests a clear expected stop behavior.
- Local camera rotation is immediate; local position follows authoritative movement with presentation smoothing. This introduces client movement latency. LAN is the baseline, so full prediction is deferred.
- Keep the physics root separate from any visual/camera smoothing. Gaze uses the server's actual eye anchor and accepted aim, never the smoothed presentation transform.
- Host input travels through the same validated input path as remote input. Do not advance a host character once as client and again as server.
- Movement uses server-computed multipliers. An owner-authoritative movement shortcut would weaken enforcement and is not the planned fallback.
- At each gameplay step: consume input, move players with the previous computed modifier, evaluate gaze when due, aggregate effects, advance all objectives, then resolve wins/timeouts. A modifier can affect movement on the next simulation step; this is intentional.

### 6.4 Admission and session rules

Connection approval rejects a fifth player, incompatible build/protocol IDs, oversized/invalid payloads, and join requests outside Lobby. Reserve pending approved slots so simultaneous requests cannot overfill the session. Never send passwords or other secrets in an unprotected connection payload. [Unity connection approval](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@3.1/manual/basics/connection-approval.html).

Use a short connect timeout, a Cancel button, and human-readable errors. On a failed start or join, shut down the incomplete session and restore the menu. Do not strand disabled controls or create a second NetworkManager when retrying.

## 7. Player/controller architecture

Each player prefab has one NetworkObject, a server-authoritative NetworkTransform, CharacterController, identity state, input adapter, movement code, eye/aim anchors, a primitive body renderer, and a local camera rig. Avoid nested network objects for hats and body parts.

Only the owning player enables input, Camera, and AudioListener. Remote players render a body and team/name indicator. Disable player-player movement collision using dedicated layers while retaining world collisions. Gaze queries use their own player-target layers and remain independent of movement collision rules.

Initial bindings are WASD to move, mouse to look, E to interact, Escape for a local menu, and mouse buttons for menus. Opening a menu releases held input and gaze until the menu closes; multiplayer simulation continues. Base speed is 5 m/s. The server clamps movement magnitude so diagonals are not faster. No jumping means the arena needs ramps or flat routes.

Spawn points belong to teams and use separate positions for each teammate. Players cannot move during countdown/results/lobby. An out-of-bounds player is returned by the server to their assigned spawn, with interaction and outgoing gaze cleared. No score penalty is required in the prototype.

## 8. Core gaze-system design

### 8.1 Recommended targeting rules

| Parameter | Initial value | Inspector control |
| --- | --- | --- |
| Maximum gaze range | 15 m | `GazeConfig.MaxDistance` |
| Cone half-angle | 8 degrees from accepted aim forward; 16-degree full cone | `GazeConfig.HalfAngleDegrees` |
| Acquisition time | 0.15 s of continuous validity | `GazeConfig.LockSeconds` |
| Server gaze evaluations | 20 Hz | `GazeConfig.EvaluationsPerSecond` |
| Outgoing targets | Exactly zero or one | Fixed MVP rule |
| Release | First evaluation finding invalid LOS, aim, range, or player state | No gameplay fade/grace |
| Target anchor | Consistent upper-chest anchor on the standard body | Prefab reference |

The gameplay cone is not the Camera's visual field of view. The HUD reticle should communicate the narrower targeting tolerance. Looking toward a visible chest within the cone qualifies; seeing only a hat or a tiny limb does not. All characters use the same anchors and collision size, including cosmetic variants.

### 8.2 Host evaluation algorithm

1. Skip observers outside Playing, disconnected/despawned players, stale input, or players whose menu suppresses input. Clear their outgoing state.
2. Reconstruct the observer's eye position from its server character transform and fixed anchor; construct forward from accepted yaw/pitch. Never accept a camera world position from the client.
3. Iterate the at-most-three other players. Reject self, missing/invalid targets, targets beyond range, and targets outside the cone using a dot-product threshold.
4. Cast from the observer's eye to the candidate's chest. Query solid-world and player-body layers, ignore triggers and observer-owned colliders, and use the nearest remaining hit. The first blocker must belong to the candidate. Another player's body blocks gaze to somebody behind them; decorative hats and particles never block it.
5. If a non-allocating multi-hit query is used, sort/select hits by distance explicitly; returned ordering is not assumed. Buffer saturation must fail closed or retry with a larger buffer, never imply visibility. For four players, a simple correctly filtered query is preferable to complex optimization.
6. Select the visible candidate with the smallest angular error, then distance, then stable player identity to break ties. Candidate switches reset acquisition.
7. Require continuous qualification for 0.15 seconds of server time. If validity breaks, clear the old confirmed target immediately and reset acquisition. There is no retained buff while locking onto a new target.
8. Build the complete set of confirmed directed gaze relationships for this evaluation. Aggregate all targets' effects only after all observers are evaluated, avoiding order-dependent stacking.
9. Replicate changes in confirmed targets and effects. Clients may preview candidates but label effects as active only after host confirmation.

Do not use a broad sphere cast that can pass around a wall edge. A single chest anchor plus real obstruction ray is a deliberate, explainable compromise. If it feels too strict, first tune angle, range, and map cover; do not remove LOS validation. A second anchor is a later tuning option only if every accepted anchor still has a real clear ray.

### 8.3 Visibility boundaries and expected delay

Walls stop gameplay effects at the next host gaze evaluation, normally within 50 ms of the host observing the break at 20 Hz. Network delivery adds visible client delay. Do not describe this as zero-latency wall detection. A local icon may fade visually after the authoritative effect reaches zero, but gameplay must already be unaffected.

On disconnect, reset, phase change, or despawn, remove all outgoing and incoming contributions from that player. Recompute from current valid relationships instead of increment/decrement counters that can leak modifiers.

## 9. Buff/debuff architecture and balance

`GazeEffectController` is the one owner of movement and work modifiers. It receives confirmed gaze relationships; it never searches for targets itself. Avoid a second overlapping `BuffDebuffSystem` for the same mechanic.

Let `A` be the number of unique allied observers and `E` the number of unique enemy observers. For the prototype:

```text
MoveMultiplier = clamp(1 + 0.35*A - 0.25*E, 0.50, 1.50)
WorkMultiplier = clamp(1 + 0.35*A - 0.25*E, 0.50, 1.50)
ActualMoveSpeed = BaseMoveSpeed * MoveMultiplier
ProgressDelta = deltaSeconds * WorkMultiplier / BaseDurationSeconds
```

Expose separate movement and work coefficients/clamps, initialized to the same values. The original prompt's 5/3.5/7-second durations are illustrative, not exact required ratios. The defaults above produce:

| Observers | Multiplier | A 5-second task takes, if constant |
| --- | --- | --- |
| None | 1.00 | 5.00 s |
| One ally | 1.35 | 3.70 s |
| One enemy | 0.75 | 6.67 s |
| Two enemies | 0.50 | 10.00 s |
| One ally and one enemy | 1.10 | 4.55 s |
| One ally and two enemies | 0.85 | 5.88 s |

Effects never compound multiplicatively each frame. Recalculate from base values and current observer counts. One observer contributes once. Self-gaze is invalid. Mutual gaze is valid: allies can buff each other; enemies can debuff each other.

If effects change during work, integrate progress using the new multiplier; do not restart the task or recalculate a single finish timestamp. The minimum multiplier prevents an enemy team from freezing a player or stopping all objective progress indefinitely. Mixed effects display both incoming icons and the net multiplier, so support is visible even when the net result remains a slowdown.

Balance constants are starting hypotheses. Tune through 1v1 and 2v2 sessions, especially whether moving to disrupt is worth abandoning work and whether a supported worker is too hard to challenge. Do not add stamina, weapons, resource costs, or a different core ability to solve early balance problems.

## 10. Team identity and lobby selections

Use stable `TeamId` values for Team A and Team B; never infer team membership from a material or color. In 1v1, each team has one slot; in 2v2, each has two. Three players may wait in a 2v2 lobby, but a 2v1 match cannot start. The host cannot switch to 1v1 while more than two players are connected.

The session hard cap remains four even while 1v1 is selected. If a third or fourth player joins that lobby, place them in an unassigned waiting state, clear readiness, and prevent starting until the host selects 2v2 or extra players leave. Unassigned players are not spectators in a live match. When 2v2 is selected, fill available slots deterministically before allowing readiness.

The host chooses from curated pairs such as red/blue, green/purple, and orange/cyan. Everyone sees both assigned colors. Host choice is the simplest implementation of selectable team colors and prevents conflicting choices. Add permanent A/B labels and distinct team symbols so meaning is not conveyed by color alone.

Tint the primary body using a renderer property block or another small, pipeline-tested material override. Keep a team-colored body region and nameplate visible from normal viewing angles. Cosmetic meshes use separate renderers and cannot overwrite the team's primary tint. Reserve shader outlines for optional polish.

Any successful team, cosmetic, mode, or palette change clears **all** ready states. Selection requests are accepted only in Lobby and validated on the server. The start button is enabled only for the host, with exactly two or four connected, initialized players in the selected balanced mode and all players ready.

## 11. Objective mode and reusable framework

### 11.1 First mode: station race

Place three stations for each team in mirrored positions. Only a station's owning team can use it. Each team follows its own ordered sequence; both teams can advance independently, and completed stations cannot be stolen or reset by enemies. Enemy influence comes from gaze and sightline positioning.

Each station takes **12 unmodified seconds** initially. Tune this in the Inspector. Keep progress when a worker stops, moves away, or changes target. A station has at most one active worker; a second teammate is encouraged to support them. The second player's attempted interaction shows a clear busy message rather than contributing double progress.

To acquire a station, a player aims at its interaction anchor, is within 2 m, has unobstructed sight to that anchor, and holds E. After acquisition, holding E continues work while range and LOS remain valid; continuous facing is not required. This lets players look at people while working. Cover placement and limited interaction range provide the tradeoff, rather than an invisible prohibition on gaze during work.

The server validates held intent and eligibility continuously. Release, stale input, leaving range, obstruction, another phase, disconnect, completion, or a wrong round ID stops work and releases the slot. Competing requests are resolved by server receipt order with stable identity as a same-step tie-breaker. A waiting player retries while holding; no permanent queue is needed.

### 11.2 Data and responsibilities

`ObjectiveConfig` stores base duration, interaction range, display label, and presentation references. `ObjectiveBase` owns networked progress, completion, team ownership, sequence index, current worker identity, and validation/reset operations. `HoldObjective` supplies the actual per-step work behavior. `ObjectiveManager` registers objectives, tracks each team's current index, and emits exactly one completion notification per station per round.

Use a short contract such as `CanInteract(player)`, `TryBegin(player)`, `ServerTick(dt)`, `Stop(player)`, and `ResetForRound(roundId)`. These are project contracts, not Unity API signatures. Do not implement a generic quest graph, task scripting language, or dependency injection framework.

Host progress is full precision and clamped to [0,1]. Replicate a display snapshot at about 10 Hz or when completion/worker changes; smooth the bar locally. Score comes from confirmed completed stations. A completion guard prevents duplicate scoring from repeated ticks or requests.

### 11.3 Future objective types

| Type | Reuse | Extra work; outside MVP |
| --- | --- | --- |
| Switch | Same validation and completion state | Immediate activation and reset presentation |
| Repair/assemble | Same hold progress | Different model, label, audio |
| Capture point | Same team/progress reporting | Explicit contested/decay rules and occupancy validation |
| Collect/deliver | Same scoring and phase integration | Server-held carrier/item IDs and pickup/delivery validation |
| Transport object | Same destination and completion contract | Prefer logical carried state and visual attachment; avoid networked free physics |

Design room for these types, but implement only HoldObjective during the initial loop. Future carry tasks benefit from movement modifiers; future timed tasks benefit from work modifiers. Instant switches should not invent an artificial speed bonus where no duration exists.

## 12. Match lifecycle and edge cases

```text
Offline menu -> Connecting/Hosting -> Lobby -> Countdown -> Playing -> Results
                                         ^                              |
                                         +---- host returns/rematches --+
Any connected phase -> session shutdown/error -> Offline menu
```

`MatchManager` owns a replicated phase and round ID. Local connecting state belongs to `NetworkGameManager`; it is not a server phase before a connection exists.

| Phase | Server behavior | Client presentation |
| --- | --- | --- |
| Lobby | Allow valid joins/selections; no movement/work/gaze; track initialized players | Roster, mode, palette, cosmetics, readiness |
| Countdown | Lock admission; increment round ID; clear state; teleport players; publish start deadline | Countdown and team objective prompt |
| Playing | Enable validated gameplay; publish end deadline; resolve objectives and results | Crosshair, progress, effects, score, timer |
| Results | Freeze gameplay; store final score/result once; clear effects and workers | Winner/draw/abort, scores, optional statistics |
| Return to lobby | Clear readiness; reset transient input; preserve connected roster and valid preferences | Ready/start flow for another round |

Use synchronized server time for deadlines, with local countdown display derived from those deadlines. The server decides expiration. Do not decrement independent client timers and let them announce winners.

After advancing **all** objectives for a simulation step, resolve completion: if both teams finish their last station in that step, declare a draw; otherwise the finishing team wins. At timeout, rank by completed stations, then normalized progress on each team's current station. Differences within 0.001 count as tied. A final completion on the ending step takes precedence over timeout comparison; clamp that step's work interval to the time remaining before the deadline.

A non-host disconnect during Countdown or Playing aborts the round to Results with a clear reason and no competitive winner. This avoids unplanned 2v1 rules. The remaining host returns players to Lobby. A disconnect in Lobby simply frees a slot; in Results it removes the player but keeps the already determined result. A host disconnect ends the session on everyone else's computer and returns them to the menu.

Joining during Countdown, Playing, or Results is rejected with a retry-in-lobby message. Live reconnect and host migration are out of scope. After returning to Lobby, a former client may join again as a fresh player. Rematch means a full reset and ready/start cycle in the same session, not reconnecting to a half-finished round.

## 13. Lightweight cosmetics

The intended first version has a catalog containing None and two simple hats. The catalog assigns stable integer IDs to local visual prefabs and preview icons. Players choose in Lobby; the server validates the ID, stores it on the player, and clients instantiate the matching visual at a head socket. Save the preferred ID locally and revalidate it on every new session.

Hats have no colliders, NetworkObjects, gameplay scripts, alternate eye positions, or movement modifiers. Every option uses the same player silhouette bounds for collision and targeting. Keep accessories small enough that they do not obscure team color or an opponent's actual body. Do not implement unlocking, currencies, transactions, inventory, or asset downloads.

Invalid or unavailable IDs resolve to None. A missing hat must never prevent a player from spawning. If cosmetics break integration near the deadline, disable the selector and ship None for everyone, recording the reduced scope.

## 14. UI, player feedback, and audio

Use one Canvas with separate panels. Presenters subscribe to state changes; they do not own rules or set progress. Labels use Who's Watching? consistently, including the window/build name where supported.

| Surface | Required controls/information | Ownership |
| --- | --- | --- |
| Main menu | Title, Host, address/port and Join, Settings, Quit, connection status/error, Cancel | Local view -> connection commands |
| Lobby | Roster, A/B team slots/colors, ready markers, own team/cosmetic choice, host mode/palette, Ready, host Start, Leave | Server state with local requests |
| HUD | Team label/symbol, station instruction, interaction prompt and bar, both team completion counts, timer, crosshair | Read replicated state |
| Outgoing gaze | Distinct Support/Disrupt reticle and target label; acquiring versus confirmed state | Local preview plus host confirmation |
| Incoming gaze | Ally and enemy observer icons/counts; net movement/work multiplier | Authoritative effect snapshot |
| Results | Winner/draw/aborted reason, final station scores; host Rematch/Return to Lobby, local Leave | Replicated result; host transition request |
| Settings/menu | Sensitivity, master volume, local close/back controls | Local preferences only |

Rematch and Return to Lobby may be one clearly labeled host button because both use the same safe reset flow. Non-host players see Waiting for host and a Leave option.

Use text and shapes alongside color: a plus/support symbol and a slowdown/disrupt symbol. Optional small sounds play when a confirmed outgoing effect begins, when an incoming state changes, and when a station completes. Rate-limit repeated cues so aim jitter cannot produce constant audio. Avoid large opaque overlays, rapid flashes, or FOV changes that make aiming harder.

P1 results statistics are completed stations, active work seconds, support seconds, and disruption seconds per player. Accumulate these on the host only while Playing, once per valid interval; do not derive competitive statistics from local HUD timers. If time is short, final team station scores alone satisfy the basic results requirement.

## 15. Scene, prefab, and code structure

### 15.1 Minimal scene arrangement

Ship one scene, `Main.unity`, containing the persistent networking root, camera/menu canvas, spawn markers, arena geometry, and manager/objective network objects. UI panels represent menu/lobby/game/results. Keep the arena and in-scene network objects active for stable registration; hide it with a menu camera/UI or presentation controls, and gate interaction by phase. Do not destroy/recreate NetworkManager on panel changes.

On a failed connection or normal Leave, wait for NGO shutdown to complete, then reload Main locally to restore a clean session shell. This local reload occurs only after leaving the network session. Do not use it for a live rematch. Ensure the old network root cannot persist and duplicate the new one.

Developer-only `NetworkTest.unity` and `GazeTest.unity` scenes are optional temporary harnesses, excluded from the release scene list. Splitting lobby/gameplay scenes is post-jam unless a proven existing team template already handles NGO scene management.

### 15.2 Folder proposal

```text
Assets/
  _WhosWatching/
    Art/Characters/ Environment/ UI/
    Audio/
    Materials/
    Prefabs/Player/ Objectives/ UI/ Environment/
    Scenes/Main.unity
    Scripts/
      Core/            # MatchManager, configs, shared small enums
      Multiplayer/     # NetworkGameManager, session callbacks, snapshots
      Player/          # NetworkPlayer, input, motor, camera
      Teams/           # TeamManager, team presentation
      Gaze/            # GazeDetector, GazeEffectController, feedback
      Objectives/      # ObjectiveBase, HoldObjective, ObjectiveManager
      Lobby/           # LobbyManager and request validation
      Cosmetics/       # CosmeticManager and catalog
      UI/              # panel presenters and HUD
      Debug/           # development overlay and gizmos
    Settings/          # input actions, balance configs, catalogs
    Tests/EditMode/ PlayMode/
Packages/
ProjectSettings/
Docs/                  # decisions, test evidence, release notes
Plan.md
Todo.md
progression.md
README.md
```

The folder listing is organizational shorthand, not a command that creates combined directory names. Keep third-party assets separate from team-owned code. Use one small runtime assembly if useful; add test assemblies when tests need them, not a large assembly hierarchy.

### 15.3 System responsibilities and dependencies

| Component | Owns | Depends on / communicates with |
| --- | --- | --- |
| `NetworkGameManager` | Host/join/shutdown, transport configuration, admission callbacks | NGO NetworkManager, MatchManager phase, lobby population |
| `NetworkPlayer` | Replicated identity, team, cosmetic, input snapshot acceptance | TeamManager/LobbyManager validation; player components |
| `PlayerInputReader` | Owner-only action sampling and input messages | NetworkPlayer; local menu state |
| `PlayerMotor` | Server movement/gravity and safe teleport | CharacterController, accepted input, effect multipliers |
| `LocalPlayerView` | Owner camera, immediate look, cursor/menu behavior | Input reader and authoritative root presentation |
| `TeamManager` | Team slots, team palette, spawn lookup | Player roster and lobby requests |
| `GazeDetector` | Host candidate selection, LOS, acquisition state | Player registry, accepted aim, GazeConfig |
| `GazeEffectController` | Aggregate confirmed relationships and modifiers | GazeDetector outputs, team IDs, EffectConfig |
| `ObjectiveBase` / `HoldObjective` | One station's validation, worker, progress, reset | Player state, WorkMultiplier, config |
| `ObjectiveManager` | Station registration/order and completion counts | Objectives, MatchManager phase; emits completion events |
| `MatchManager` | Phase, round ID, deadlines, result, reset ordering | Team readiness, objectives, player/gaze reset hooks |
| `LobbyManager` | Ready selections and start eligibility | TeamManager, cosmetic catalog, MatchManager |
| `CosmeticManager` | Apply allowed visual prefab and fallback | Player CosmeticId, local catalog, head socket |
| UI presenters | Menu/lobby/HUD/results rendering and button requests | Read accepted state; never own authoritative gameplay |

Use explicit Inspector references and small C# events for local presentation updates. Avoid `FindObjectsOfType` every frame, cross-scene singleton chains, and a global event bus. A manager may hold a four-player registry updated at network spawn/despawn. Keep network serialization in a few simple structs with explicit fields.

The shared contracts to agree before parallel coding are `TeamId`, `MatchPhase`, `RoundId`, `InputSnapshot`, `GazeSnapshot`, `EffectSnapshot`, `ObjectiveSnapshot`, and `MatchResult`. They need only the fields described in this plan, not a generic gameplay framework. Team/phase IDs have documented enum values; object references use NGO-safe IDs or references validated before dereferencing.

## 16. Network state and performance budget

| State | Writer | Delivery/update policy |
| --- | --- | --- |
| Input/aim/held intent | Owner sends; host accepts | About 20 Hz snapshots, bounded rate and freshness |
| Position/body rotation | Host | NetworkTransform, roughly 30 Hz baseline, interpolation on clients |
| Aim pitch for other players | Host | Small accepted state update; optional visual head pitch |
| Team, ready, palette, cosmetic, mode | Host | Reliable state on change |
| Confirmed gaze target and incoming effects | Host | On change, at most gaze evaluation frequency |
| Objective progress | Host | About 10 Hz display updates; immediate completion/worker state |
| Phase, round, start/end deadlines, result | Host | Reliable state on transition |
| Team scores | Host | On station completion/reset |
| Statistics | Host | Results snapshot; no per-frame HUD replication required |
| Reticle, UI layout, camera effects, cosmetic particles, audio playback | Local | No continuous network updates |

At four players there are at most 12 directed observer-target pairs per evaluation, or 240 candidate evaluations/second at 20 Hz before range/cone rejection. This small bounded workload does not need spatial partitioning or jobs. Count actual casts and messages in a development overlay before optimizing.

Keep server progress separate from quantized or throttled presentation snapshots. Do not stream strings or full catalogs continuously. Store shared configuration in matching builds and check build/protocol identity; copy selected configuration into round state on the host, freeze it during play, and replicate the actual modifiers used by clients. Client-local Inspector edits never change host rules.

Aim for stable 60 FPS on the weakest available target PC with four players and primitive art. Record measured frame time and network traffic; no numeric bandwidth guarantee is made before profiling. A fixed step must not run multiple times from unrelated MonoBehaviours without a clear order. Extra catch-up work must be bounded to avoid a spiral after a long frame.

## 17. Five-person responsibility split and workflow

| Role | Primary ownership | Independent early work | Integration handoff |
| --- | --- | --- | --- |
| A — networking/integration lead | NGO setup, NetworkGameManager, identity, lobby admission, build pipeline | Minimal host/join and player prefab scaffold | Stable input/identity API; imports tested prefabs into Main |
| B — player/gaze | Input, server motor, view, target detection, modifiers | Pure effect math and gaze harness with dummy players | Working player/gaze prefab plus tests and config |
| C — objectives/match | Objective framework, score/timer/result/reset | Offline hold objective against a fake modifier provider | Objective prefab and phase contracts wired to host |
| D — arena/identity | Graybox, colliders/layers, spawn/station markers, team materials, tiny hats | Symmetrical primitive arena prefab and body visuals | Arena prefab with validated references; no shared-scene edits |
| E — UI/audio/QA | Panels, input prompts, feedback, test records, sound | Mock roster/HUD views against temporary data | Presenter bindings after contracts stabilize; leads multi-PC tests |

The table describes the human development team, not separate AI tasks. Each role has responsibility for testing its own changes; E coordinates tests but is not the sole tester. A second developer reviews networking changes and helps the lead during integration gates.

One person owns `Main.unity` integration. Contributors edit isolated prefabs/scenes and scripts rather than simultaneously rewriting the same serialized scene. A owns the player network root; B edits its movement/gaze child configuration by agreement. D hands visual prefab updates to A/B. Shared enum/contract changes are announced before merging.

Use short-lived branches, small reviewable commits, and integration every two to four hours. Commit .meta files with assets; never regenerate another person's GUIDs to resolve a merge. Keep a known-good build at every gate. If stubs are used, mark them clearly and remove/disable them before evaluating a multiplayer gate. No mock data may drive release scores or effects.

## 18. 48-hour roadmap and gates

Hours are elapsed wall-clock windows with parallel work, not 240 guaranteed productive person-hours. Schedule breaks and handoffs; do not assume five people code continuously for two days. Each gate is an observable build outcome. If a gate slips, use the fallback table immediately.

| Window | Main outcome and role emphasis | Exit gate |
| --- | --- | --- |
| H0–2 | A pins editor/packages and networking scaffold; B/C agree contracts; D grayboxes; E drafts mock panels/test sheet | **G0:** every developer opens the same project; contracts and scene ownership agreed |
| H2–6 | A/B connect host and client, spawn, move/look; C builds hold station harness; D/E prepare readable test arena/HUD | **G1:** two standalone processes connect and move; at least one remote-PC LAN connection attempted |
| H6–12 | B/A integrate host gaze/effects; C connects modifier to work harness; D validates walls; E confirms four feedback states | **G2:** two-player primitive slice proves ally buff, enemy debuff, occlusion, and bounded effects |
| H12–18 | C/A integrate stations, timer, score/results/reset; B fixes movement/aim issues; D places station routes; E binds game HUD | **G3:** a full 1v1 round ends consistently on both machines and can reset |
| H18–24 | A/C/E complete lobby/ready/modes; B tests simultaneous gaze; D finishes mirrored play space | **G4:** four-client 2v2 completes; team/palette selection and readiness are authoritative |
| H24–30 | D/E add tiny cosmetics, sounds, settings, clear signs; A/B/C fix playtest issues | **G5:** intended first-version features integrated; protected known-good release candidate |
| H30–36 | Cross-PC playtests, limited tuning, at most one already viable stretch item | **G6:** both formats pass critical matrix; feature freeze at H36 |
| H36–44 | Everyone tests/reproduces/fixes; E tracks regressions; A produces candidates | **G7:** no release-blocking defects; repeated rematches and disconnects pass |
| H44–48 | Build, unzip-on-clean-PC verification, documentation, package preparation, submission by designated owner | **G8:** tested packaged build and reproducible handoff ready before deadline |

The critical path is networking -> player aim/movement -> gaze -> authoritative objectives -> results/reset -> four-player validation. Art, cosmetics, and extra menus cannot become dependencies of that path. At H36, no new packages, objective types, scene architecture, or networking transport changes. Reserve remaining time for defects, small readability fixes, and packaging only.

## 19. Acceptance criteria and multiplayer test strategy

### Definition of the requested MVP

A fresh standalone build allows two or four people to host/join on LAN, select valid teams/colors, ready, play an objective race with meaningful ally/enemy gaze, see consistent results, and return to the lobby for another round. Solid walls stop gaze, effects cannot reduce speed to zero, the host alone awards progress/results, and connection failures return users to a usable UI. A four-player build has been exercised on real separate PCs.

### Test layers

1. **Pure rule tests:** effect formula/clamps, same-tick result resolution, timeout comparisons, progress integration with changing modifiers, allowed team counts.
2. **Local integration:** host plus separate client process; debug visible sender/team/phase/target/LOS/multiplier data; regression after each subsystem merge.
3. **Separate-PC LAN:** test both host choices, wrong addresses, firewall permission, complete matches, all gaze relationships, reset, and disconnects.
4. **Four-player session:** ideally four PCs; if equipment limits this, at least two real PCs with four processes total, explicitly record that limitation and verify all four inputs independently.
5. **Network adversity:** use available transport tooling to add a modest latency/loss profile, document actual settings and whether the result is acceptable; LAN delivery remains the baseline.
6. **Release candidate:** test the extracted distributable on a machine that did not build it. Editor-only success never qualifies as release verification.

### Critical test matrix

| ID | Scenario | Expected result |
| --- | --- | --- |
| T01 | Host starts; correct/wrong IP and port; cancel/retry | Correct join succeeds; errors/timeouts recover without duplicate managers |
| T02 | Fifth player, wrong build, join mid-round | Rejected with usable explanation; no leaked slot/player object |
| T03 | Remote movement, diagonal input, pitch/yaw, local cameras | One controlled player per user; no diagonal boost or duplicate listeners |
| T04 | Ally, enemy, self/no target, just inside/outside range/cone | Correct sign; no self effect; predictable boundaries |
| T05 | Wall, corner, trigger, cosmetic, intervening player | Solid world/body occlusion blocks; triggers/cosmetics do not interfere |
| T06 | Acquire/release, switch target, mutual gaze, multiple observers | Lock timer resets; immediate host-side release; unique bounded contributions |
| T07 | One ally plus one/two enemies | Formula matches; both incoming types shown; no permanent modifier |
| T08 | Work while supported/disrupted; effect changes mid-task | Correct integrated progress and authoritative speed |
| T09 | Wrong team/sequence, out-of-range/occluded work, two workers | Invalid work rejected; one valid worker; progress retained; slot releases |
| T10 | Repeat completion request; finish both teams in same step | Score once; same-step final completion is a draw |
| T11 | Timeout, tie, final completion at deadline | Every client shows the same defined result |
| T12 | Team/palette/cosmetic/mode changes and readiness | Validated selection; all ready flags reset; only balanced full roster starts |
| T13 | Client leaves in lobby/countdown/game/results | Slot freed; active round aborts where specified; no phantom gaze/worker |
| T14 | Host closes/crashes; client retries later | Clients return to menu; fresh session succeeds |
| T15 | Three consecutive rounds, return to lobby, leave/rehost | No stale input/progress/stats/ready flags, duplicate callbacks, or missing objects |
| T16 | Delayed/lost snapshots, stale/reordered/wrong-round input | Old intent expires; no runaway movement/work or stale-round actions |
| T17 | 1v1 and 2v2 from distributed builds | Both formats complete on separate PCs; cosmetic/team identity remains clear |
| T18 | Optional Relay/carry/content if included | Specific feature tests pass without breaking direct LAN or core loop |

Record build/commit, machines/process count, host, scenario, reproduction steps, expected/actual result, and evidence in `Docs/TestLog.md`. Use P0 for crash/cannot-connect/wrong-authority/broken-gaze/wrong-result defects; P1 for serious usability issues; P2 for cosmetic polish. Fix P0 first and rerun affected tests plus one full round after a gameplay/networking change.

## 20. Risks and smallest safe fallbacks

| Risk / trigger | Smallest practical response | What must remain |
| --- | --- | --- |
| Networking gate missed by H6 | Pair A/B on minimal host/client prefab; remove presentation dependencies; cut all stretch work | Two/four-player host-client foundation |
| Ally/enemy gaze unreliable by H12 | Use one chest anchor, flat arena, fixed configs, debug rays; reduce visual complexity | Both effect signs, real LOS, host validation |
| Server movement feels slow | Check send/tick settings and presentation smoothing on LAN; keep movement simple | Server owns gameplay speed; do not silently switch authority |
| Team inexperienced with chosen package | Verify a small official example against pinned package; keep one API style | Working compatible stack; no last-minute framework migration |
| Several objective types unfinished | Ship only hold stations and reuse the same prefab | Complete objective race and gaze interaction |
| Object physics desynchronizes | Drop that stretch task; use logical carry state later | No required physics networking |
| Cosmetics or animations break builds | Disable optional visuals; default to None/static capsules | Team colors and identical gameplay bodies |
| Shader outlines too costly | Use body tint, A/B labels, reticle icons | Readable team and effect identity |
| Relay unavailable or consumes time | Return to tested LAN transport; document connection instructions | Functional direct multiplayer |
| Round pacing poor | Tune station duration, travel distance, cover, range, and coefficients | Core gaze rule and objectives |
| Cover causes confusing hits | Simplify collider shapes and layer masks; show debugging anchors | No buff/debuff through walls |
| Four-player instability near deadline | Fix hard cap/roster/reset and reduce content; document any unresolved limitation | Do not claim 2v2 completion without a successful test |
| Scene or prefab merge conflicts | Single integrator, isolated prefabs, return to known-good asset version | Preserve working gameplay references and .meta GUIDs |
| Late defect after freeze | Revert the offending optional feature or patch narrowly; rerun affected matrix | Last known-good playable build available |
| Hardware unavailable | Book test PCs at H0; use multi-process fallback and disclose remaining risk | At least a real cross-PC connection before release |

If the full MVP cannot be met, deliver the last known-good prototype with explicit missing requirements and reproduction notes. Do not quietly replace the game with an offline demo, omit ally gaze, or ship 1v1 while calling 2v2 complete.

## 21. Optional Internet multiplayer and other stretch work

Relay may be attempted only after G5 passes (with any P1 cuts documented) and a known-good LAN build is preserved; allow a short bounded spike, approximately two hours. For Unity 6 projects, Unity recommends accessing Relay through the unified Multiplayer Services SDK. Use anonymous authentication and host allocation/join-code flows. Allocate for **three joining connections** when supporting four total players. Relay changes transport routing; the player's computer still runs authoritative gameplay. [Unity Relay with NGO tutorial](https://docs.unity.com/en-us/mps-sdk/tutorials/relay-and-ngo), [Unity Relay server model](https://docs.unity.com/relay/relay-servers).

Separate direct versus Relay connection configuration inside NetworkGameManager. Initialize services only for Relay mode, catch service/authentication failures, display a usable error, and leave LAN available. No cloud Lobby service or matchmaking is required merely to run the game's own in-session lobby. Verify current service access, limits, and project configuration before depending on Internet play; do not assume free unlimited capacity.

Relay acceptance requires separate Internet connections, valid/invalid codes, timeout/cancel, host departure, four total players, and LAN regression. A code shown in the lobby alone does not prove Relay works. If these checks cannot finish before H36, exclude Relay from the release.

Other optional work is limited to a second small objective type or modest art/audio additions. Do not start multiple large stretch items because several developers appear temporarily free; use that time for real multiplayer playtests.

## 22. Build and submission definition

The implementation checklist contains the detailed release steps. The release candidate must satisfy:

- Correct Who's Watching? title, one intended Windows target, correct startup scene, clean build from the committed project.
- No missing scripts/assets, unintended debug team overrides, developer-only start bypass, or compilation errors.
- Working Host/Join and readable LAN IP/port instructions; OS firewall guidance limited to the needed application/private network access.
- Full executable plus associated data/runtime files packaged together; do not distribute just the executable.
- README with controls, player count, host requirements, how gaze works, rematch/leave behavior, known issues, and Relay instructions only if tested.
- Asset credits/license notes, tested version/build identifier, and short release notes.
- Package extracted and played on another PC, including both formats, team selection, a complete round, and repeated reset.
- A designated human submission owner, verified delivery requirements, an early upload buffer, and a preserved previous known-good archive.

No storefront publication or external submission is performed by this planning task. Future release work must use the team's actual delivery destination and rules.

## 23. Post-jam roadmap

Prioritize improvements according to playtest evidence: improve high-latency movement through prediction/reconciliation; improve aim/occlusion feel without relaxing authority; expand objective variety; improve accessible team palettes and onboarding; add controller/rebinding support; automate multiplayer regressions; measure balance across skill levels.

Only then consider Internet-first UX, recoverable disconnects, host migration, matchmaking/rankings, additional maps, and 3v3. Increasing to six players requires new team-cap settings, gaze-stack balance, spawn/UI changes, capacity and Relay allocation changes, and fresh performance/authority tests. Do not treat it as simply raising a player limit.

## 24. Documentation and completion policy

`Plan.md` describes intended scope and rules. `Todo.md` tracks implementation actions and gates. `progression.md` records the latest actual state, decisions, validation, blockers, and exact next actions. Update the handoff at small milestones; check tasks only when their stated evidence exists. Source links above support technology behavior; gameplay rules and time budgets are project recommendations.

At implementation start, record editor/packages and any agreed change to these defaults in `Docs/Decisions.md`. If a decision changes authority, balance rules, reset semantics, or required scope, update both documents so the team never follows contradictory instructions.
