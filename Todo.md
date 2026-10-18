# Who's Watching? — Detailed Implementation Checklist

**Purpose:** explain how to build the game, in dependency order, with concrete tasks, owners, artifacts, and verification. [Plan.md](Plan.md) defines the intended behavior and scope. [progression.md](progression.md) contains actual progress and the next handoff.

**Current status:** a Unity URP LAN development harness exists. Checked tasks have implementation evidence; uncompleted game and team-validation tasks remain open. See Docs/TestLog.md and progression.md.

**Latest follow-up:** linked Team A/B variants for all accented environment assets, editor tint previews and nested ownership isolation, visual bob/sway/squash, capacity-checked lobby team switching, Space jumping/airborne poses, and environment colliders are implemented. These do not complete the remaining objective-round, mode-selection or production-UI tasks.

**2026-10-17 appearance scope:** Devil/Ghost selection, first-connected team leaders, six individually selected presets, ready/start gating, animations and per-slot arena accents are implemented; see `Docs/TeamAppearance.md`. The user's six-color leader selection supersedes LOB-02's earlier host-selected color-pair design. Broader LOB tasks remain unchecked where they also require mode selection, team switching, full round lifecycle, or acceptance not covered by this change.

## How to use this checklist

- Work by phase and gate, not by whichever feature looks most interesting. The first deliverable is a multiplayer gaze slice with primitive visuals.
- Each checkbox has a stable ID. Check it only after its implementation and applicable verification are complete. Record commits/builds and test results in `Docs/TestLog.md`; record cuts and decisions in `Docs/Decisions.md`.
- **P0** means required MVP. **P1** means intended first-version work that can be cut if documented. **P2** means optional polish. **P3** means gated stretch. A skipped item remains unchecked with a short `CUT: reason` note.
- Owners: **A** networking/integration; **B** player/gaze; **C** objectives/match; **D** arena/identity; **E** UI/audio/QA. Ownership does not prevent pair work or review.
- Every phase ends with a milestone checkpoint: preserve a known-good commit/build, update `progression.md`, check account quota when an agent is doing the work, and stop feature work if the user's reserve rule applies.
- Estimates are elapsed-hour targets for parallel human work. Breaks, installs, hardware constraints, and debugging can shift them. Cut optional work before moving the H36 feature freeze.

## Dependency map and delivery gates

```text
00 Setup/contracts
  -> 01 Host/client
  -> 02 Player movement/look
  -> 03 Gaze + 04 Effect aggregation           [G2 vertical slice]
  -> 05 Objectives + 06 Match lifecycle       [G3 full 1v1]
  -> 07 Lobby + 08 Arena + 09 Core UI         [G4 full 2v2]
  -> 10 Cosmetics/settings/audio             [G5 intended first version]
  -> 11 Hardening + 13 Optional stretch      [G6 feature freeze]
  -> 12 Acceptance/regression                [G7 release candidate]
  -> 14 Packaging/submission handoff          [G8 delivery]
```

Sections are dependency groups, not a ban on parallel preparation. D can build the graybox after contracts; E can build local UI against mock data; C can test hold-progress math while A/B connect players. Mock data must be removed from release bindings. Section 12 testing begins with the earliest playable builds and continues throughout.

| Gate | Target | Required evidence |
| --- | --- | --- |
| G0 | H2 | Common project opens; versions/contracts/owners agreed |
| G1 | H6 | Host + client move/look; separate-PC LAN attempted |
| G2 | H12 | Ally buff, enemy debuff, LOS and caps work in a networked slice |
| G3 | H18 | Full 1v1 objective race ends and resets consistently |
| G4 | H24 | Full four-player 2v2 plus lobby/team/color/readiness works |
| G5 | H30 | Small intended presentation/cosmetic features integrated or explicitly cut |
| G6 | H36 | Core matrix passes; feature freeze starts |
| G7 | H44 | Tested candidate without release blockers |
| G8 | H48 | Extracted package tested; instructions and delivery ready |

## 00 — Establish the project and shared contracts

**Window:** H0–2. **Owners:** A leads; all contribute. **Priority:** P0. **Dependencies:** none. **Output:** reproducible empty Unity project, working agreements, and small shared types.

### Project and toolchain

- [x] **SET-01** Confirm the public title is `Who's Watching?`; use `WhosWatching` for filesystem-safe identifiers and C# namespaces. Set Windows desktop as the initial build target and keyboard/mouse as required input.
- [ ] **SET-02** Inventory editor versions and available test PCs. Select one supported Unity 6 LTS patch that everyone can use; verify NGO/UTP compatibility in Package Manager and the matching official documentation before committing to it.
- [x] **SET-03** Create the Unity project at the repository root, preserving the existing Markdown files and `.gitignore`. Use the agreed 3D pipeline, preferably URP if nobody has an existing preference. Do not change render pipelines later in the jam.
- [ ] **SET-04** Install compatible Netcode for GameObjects, Unity Transport, Input System, and the UI/text dependencies actually needed. Import TextMeshPro essentials if the chosen template requires them. Do not add Relay/cloud packages yet.
- [ ] **SET-05** Enable visible meta files and text asset serialization. Confirm `.gitignore` excludes Library, Temp, Logs, local build output, and machine-specific settings. Commit Assets with matching .meta files, Packages, and ProjectSettings.
- [ ] **SET-06** Create the folder layout from Plan section 15 inside `Assets/_WhosWatching`. Use the Unity Editor for asset/folder operations where needed to preserve .meta files and GUIDs.
- [x] **SET-07** Create `Main.unity`, add it as the initial enabled build scene/profile entry, and save a minimal camera/light/floor setup. Record that this is the release's single persistent session scene.
- [ ] **SET-08** Set product name to `Who's Watching?` and a simple initial build version. Confirm a blank Windows development build launches before implementing gameplay.
- [ ] **SET-09** Ask another developer to open the committed project using the pinned editor and build it. Resolve missing packages/references now, not at the final packaging gate.

### Agreements that unblock parallel work

- [x] **SET-10** Create `Docs/Decisions.md` with editor/package versions, render pipeline, initial platform, 1v1/2v2 cap, authority model, one-scene approach, input bindings, and agreed default balance values.
- [ ] **SET-11** Assign A–E to named teammates and appoint A as Main scene integrator. Assign player-root ownership to A/B by agreement; D delivers arena/visual prefabs; E owns UI prefab assets; C owns station prefabs.
- [ ] **SET-12** Agree branch/review rules: small commits, integrations every two to four hours, no simultaneous edits to Main, no upgrading packages without team agreement, and no replacing .meta files to hide merge conflicts.
- [ ] **SET-13** Define `TeamId` with None/A/B, `MatchPhase` with Lobby/Countdown/Playing/Results, and small structs for input, gaze, effects, objectives, and results. Document field meaning, validity flags, and server/client writers.
- [ ] **SET-14** Define `InputSnapshot`: sequence, round ID, move vector, yaw/pitch, held interact, target-present flag, target ID, and input-enabled flag. Define sequence wrap handling or a comparison policy valid for the session lifetime.
- [ ] **SET-15** Define `GazeSnapshot` and `EffectSnapshot`: confirmed-target presence/ID, ally/enemy incoming counts, movement multiplier, work multiplier. Keep acquisition preview separate from confirmed effect state.
- [ ] **SET-16** Define objective/result data: team, sequence, normalized progress, complete flag, worker-present/ID, round ID; result type, winner if any, final team scores, and abort reason. Avoid free-form network strings for fixed enums.
- [ ] **SET-17** Create `PlayerConfig`, `GazeConfig`, `EffectConfig`, and `MatchConfig` assets with defaults from Plan. Add validation for positive durations/rates, valid angles, positive minimum multiplier, and maximum >= minimum.
- [ ] **SET-18** Create `Docs/TestLog.md` with build, commit, host, PCs/processes, test ID, steps, expected result, actual result, pass/fail, and defect link fields. Schedule real-PC test sessions by H6 and H24.
- [ ] **SET-19** Let B/C work against explicit temporary test inputs and E against a mock view model. Label each harness `Development only`, isolate it from the release scene, and track its removal/binding task.
- [ ] **SET-20 — G0** Verify everyone opens the project, the empty build launches, contracts are available, and scene/prefab ownership is agreed. Commit the baseline and update `progression.md` with actual results.

## 01 — Prove host/client connectivity

**Window:** H2–4, continuing to G1 at H6. **Owner:** A; E supplies temporary controls. **Priority:** P0. **Dependencies:** SET-04, SET-07, shared identity contracts. **Output:** working direct host/join and four-player admission boundary.

- [x] **NET-01** Add exactly one NetworkManager and UnityTransport to the Main networking root. Select client-server topology, assign player prefab, configure registered prefabs, and use the installed package's supported APIs.
- [x] **NET-02** Create the primitive player root with one NetworkObject and server-authoritative NetworkTransform. Add a capsule renderer and CharacterController; avoid nested NetworkObjects for visual children.
- [ ] **NET-03** Implement `NetworkGameManager` methods for StartHostDirect, StartClientDirect, Cancel/Leave, and shutdown completion. Put address/port validation and transport setup in this component, outside UI code.
- [x] **NET-04** Add temporary Host/Join/address/port/Leave controls. Choose a documented default port, such as 7777. Separate the host's listen/bind address from the address shown for a remote client to enter.
- [x] **NET-05** Make hosting configure a reachable LAN bind address and joining use the entered host address. Use loopback only for same-machine tests; explain the difference in debug help text.
- [ ] **NET-06** Register connection/disconnection/shutdown handlers once and unregister them on teardown. Track a four-player registry by actual NGO client/network identity; never index a fixed array directly with a client ID.
- [ ] **NET-07** Implement connection approval with a build/protocol token, payload length check, current-phase check, hard total cap of four including host, and reservation of pending approved slots. Release reservations on failure/timeout.
- [ ] **NET-08** Configure server-side player spawning and one player object per connection. Give each player temporary debug identity/team state. Verify NGO owns spawn/despawn; clients do not independently instantiate gameplay copies.
- [ ] **NET-09** Add connection-in-progress UI, an approximately 10-second timeout, cancel, and readable failure messages. Disable duplicate connect attempts; restore controls after any failure.
- [ ] **NET-10** Implement clean Leave: suppress local input, initiate shutdown, wait for completion, then reload Main locally. Confirm the old network root cannot survive to create a duplicate.
- [ ] **NET-11** Run host and client as separate processes; verify two distinct player objects appear on both. Disconnect the client, rejoin in Lobby, and verify the player count and registry recover.
- [ ] **NET-12** Attempt a real LAN connection from a second PC. Check correct address, matching build, same-network reachability, and application firewall permission; log the exact outcome and fix failures before proceeding far into content.
- [x] **NET-13** Attempt a fifth connection using additional processes and an incompatible build token in a test harness. Verify approval rejects each appropriately and does not reserve a slot forever.
- [ ] **NET-14** Commit the working connection baseline and record reproduction instructions. Keep cloud authentication and Relay absent from the P0 startup path.

## 02 — Implement server-controlled movement and looking

**Window:** H2–6. **Owners:** B with A. **Priority:** P0. **Dependencies:** NET-02, shared input contract. **Output:** two network players move and look correctly; only the owner controls its camera/input.

- [ ] **PLY-01** Add Input System actions for Move, Look, Interact, and Menu. Bind WASD/mouse/E/Escape and choose sensitivity/pitch limits in the Inspector. Do not add jump or sprint.
- [ ] **PLY-02** Build a player hierarchy with authoritative root, body visuals, fixed eye/chest anchors, and a separate local camera rig. Keep all gameplay targets on fixed standard-body anchors.
- [ ] **PLY-03** On network spawn, enable input, Camera, and AudioListener only for the local owner. Disable remote cameras/listeners. Initialize current identity/visual state immediately and subscribe to later changes.
- [ ] **PLY-04** Implement owner input sampling and snapshots at about 20 Hz. Send yaw/pitch rather than a camera position; include current round ID, monotonically ordered sequence, and held flags.
- [ ] **PLY-05** On the host, verify message sender owns that player, then reject non-finite values, bad ranges, old sequences, wrong-round snapshots, and excessive rates. Clamp movement magnitude and pitch; normalize yaw.
- [x] **PLY-06** Store the latest accepted input and server receipt time per player. After 0.25 seconds without fresh input, set movement/interact/gaze intent inactive. Ensure idle players still send heartbeats so valid gaze is not expired accidentally.
- [x] **PLY-07** Implement server-only CharacterController movement at the agreed simulation rate. Apply base 5 m/s speed, normalized movement relative to accepted yaw, gravity, and collision. Never run the same simulation on both host client and host server paths.
- [x] **PLY-08** Set up physics layers so players collide with solid world geometry but do not push/block one another. Keep target colliders eligible for gaze queries despite the movement collision matrix.
- [x] **PLY-09** Replicate root position/yaw through server-authoritative NetworkTransform. Confirm its installed-version authority setting in the Inspector or supported override, rather than relying on an unrelated tutorial's component.
- [x] **PLY-10** Rotate the owner camera immediately from local input and use light presentation smoothing for replicated position if needed. Keep authoritative eye position separate; avoid double interpolation or moving the physics root locally.
- [ ] **PLY-11** Replicate accepted aim pitch only if used by remote head/body feedback. Disable unused transform axes such as scale. Ensure mouse pitch never tilts the collision capsule.
- [ ] **PLY-12** Implement phase input gating. Lobby/countdown/results disable gameplay; a local menu releases input while allowing the network heartbeat and session to continue. Closing the menu requires current input, not a stale held E.
- [ ] **PLY-13** Add team spawn lookup and server teleport/reset. Temporarily handle CharacterController teleport constraints correctly; clear cached velocity/input and presentation interpolation where the package requires it.
- [ ] **PLY-14** Add a simple out-of-bounds threshold that returns players to their team spawn and clears work/gaze. Build a flat arena without required jumping.
- [ ] **PLY-15** Test host and client movement independently, diagonal speed, 360-degree yaw, pitch clamp, wall collision, single AudioListener, and menu input release. Repeat with roles exchanged between PCs.
- [ ] **PLY-16 — G1** Produce a build with Host -> Join -> two spawns -> movement/look on both. Record remote-PC connectivity outcome, movement limitations, and the known-good commit in `progression.md`.

## 03 — Implement host-validated gaze detection

**Window:** H6–10. **Owner:** B; A reviews authority; D supplies blockers. **Priority:** P0. **Dependencies:** G1, accepted aim, team identity, eye/chest anchors. **Output:** all-visible-player screen gaze with independent acquisition and real occlusion.

- [x] **GAZ-01** Add a temporary host-only debug team selector so two players can be allies or enemies without a complete lobby. Compile/gate it for development only and add a release removal check.
- [x] **GAZ-02** Add `GazeDetector` and a registry of valid spawned players. Read server position, accepted aim, phase, input freshness, and team IDs; never use the host's local Camera for every observer.
- [x] **GAZ-03** Schedule gaze evaluation at 20 Hz using server time. Keep acquisition timers independent of render FPS; process all observers before applying effects.
- [x] **GAZ-04** Reject self/invalid/out-of-range samples and test rectangular camera bounds using server FOV and validated owner aspect. Keep owner rendering and server projection consistent; expose FOV/range through GazeConfig.
- [x] **GAZ-05** Construct LOS rays from server eye to on-screen fixed standard-body samples; the same sample must pass screen and LOS checks. Include solid world/player body layers, ignore triggers and source-owned colliders, and require the nearest remaining hit to belong to that candidate.
- [x] **GAZ-06** Handle multiple collider hits explicitly: sort/select by distance; map child collider to owning player; never assume query result ordering. If a non-alloc buffer fills, retry safely or treat the view as blocked.
- [x] **GAZ-07** Exclude hats, particles, UI markers, and objective interaction triggers from gaze blockers. Keep actual station/environment solid geometry in the world blocker layer. Verify the gaze ray does not accidentally collide with its observer.
- [x] **GAZ-08** Confirm all visible players simultaneously, with the same visibility rules for allies/enemies. Count each observer-target pair once even when several body samples qualify.
- [x] **GAZ-09** Track acquisition independently per observer-target pair. Confirm after 0.15 continuous seconds; clear only affected pairs on invalid visibility/stale input/despawn. Phase/reset cleanup remains required for production match state.
- [x] **GAZ-10** On invalidity, clear the confirmed outgoing target at the current evaluation. Do not leave the previous effect active while acquiring a new target or while a visual icon fades.
- [x] **GAZ-11** Publish all confirmed target IDs and outgoing support/disruption counts only when changed. Keep local candidate previews visually distinguishable from confirmed effects; do not let previews modify stats.
- [ ] **GAZ-12** Add development gizmos for eye position, aim direction, frustum/range, body samples, LOS hit, candidate, lock progress, and confirmed target. Rate-limit debug text/logs.
- [ ] **GAZ-13** Test angle/range boundaries, turning away, rapid target switching, a solid wall, a thin corner, a trigger, an intervening player, and an oversized cosmetic test mesh. Verify all outcomes match Plan section 8.
- [ ] **GAZ-14** Test mutually looking players and two observers selecting the same target. Verify directionality: the looked-at player is affected, not automatically the observer.
- [ ] **GAZ-15** Despawn/disconnect/reset a target and observer during acquisition and after confirmation. Remove stale references without exceptions or remaining relationships.

## 04 — Apply buffs/debuffs and prove the vertical slice

**Window:** H8–12. **Owner:** B; C consumes work modifier; E adds debug feedback. **Priority:** P0. **Dependencies:** confirmed gaze relationships. **Output:** gaze changes actual authoritative movement/work speed with bounded stacking.

- [x] **EFF-01** Implement a small pure calculation function taking allied/enemy observer counts and config. Default both multipliers to `clamp(1 + 0.35*A - 0.25*E, 0.50, 1.50)` with separate move/work coefficients available.
- [x] **EFF-02** Implement `GazeEffectController` to recompute incoming counts from the complete current relationship set. Count distinct observers once, reject self links, and never modify last frame's speed multiplicatively.
- [x] **EFF-03** Wire PlayerMotor to `BaseMoveSpeed * MoveMultiplier`. Remove any separate speed override in input/UI code. Document the intentional next-movement-step application after a gaze evaluation.
- [x] **EFF-04** Expose authoritative WorkMultiplier through a simple read-only player API for C. Create a temporary five-second hold bar harness to demonstrate speed effects before full objectives are ready.
- [ ] **EFF-05** Replicate effect counts and effective multipliers from host only. Initialize owner/remote views on spawn and unsubscribe on despawn. Reset to 1.0 with zero counts outside Playing.
- [x] **EFF-06** Implement the minimum four feedback states: outgoing support, outgoing disruption, incoming support, incoming disruption. Show both incoming types together when mixed, plus the net multiplier.
- [ ] **EFF-07** Add rule tests for no observers, one ally, one enemy, two enemies, one ally plus one enemy, one ally plus two enemies, duplicate observer prevention, and min/max clamps under synthetic counts.
- [ ] **EFF-08** Measure the five-second harness at 1.00x, 1.35x, 0.75x, and 0.50x. Accept timing within simulation/network display tolerance; target theoretical durations are 5.00, 3.70, 6.67, and 10.00 seconds.
- [ ] **EFF-09** Verify modifier removal after wall occlusion, turning away, disconnect, despawn, and reset. Confirm wall blocking removes gameplay effect at the next host evaluation even if remote UI arrives later.
- [ ] **EFF-10 — G2** Run two connected players first as allies, then as enemies; prove real movement and work changes, wall blocking, and both sender/receiver feedback. Record a build and test evidence. If this fails, stop content work and pair A/B on the slice.

## 05 — Build the reusable objective and station race

**Window:** H12–16; C may prototype math earlier. **Owner:** C; D places stations. **Priority:** P0. **Dependencies:** network identity, host phase, WorkMultiplier, G2 for integration. **Output:** six placed stations using one tested objective prefab.

- [ ] **OBJ-01** Create ObjectiveConfig with positive base duration, interaction range, title, and prompt. Use 12 seconds and 2 m initially; validate config values in the Editor.
- [ ] **OBJ-02** Implement ObjectiveBase's server-owned team, sequence, round, progress, completion, and optional worker identity. Add the small interaction/reset contract described in Plan, without a generic quest framework.
- [ ] **OBJ-03** Implement HoldObjective's progress step as `dt * worker.WorkMultiplier / BaseDuration`. Clamp [0,1]; keep accumulated progress on release; never reset merely because the modifier changes.
- [ ] **OBJ-04** Add initial interaction acquisition using aim at the station anchor, valid range, clear world LOS, owning team, active sequence, Playing phase, fresh held input, and an available worker slot.
- [ ] **OBJ-05** Let the client request an objective ID from local selection; resolve and validate it on the host. Reject unknown/despawned/wrong-team/future/completed objective IDs and stale round IDs.
- [ ] **OBJ-06** After successful acquisition, continue validating range, LOS, held state, team, phase, and freshness each step; continuous facing is not required. Keep automatic gaze active while a player looks away from the station to support/disrupt.
- [ ] **OBJ-07** Enforce one active worker per station. Resolve simultaneous acquisitions deterministically; show Busy to another teammate. Let a held attempt retry after the slot becomes free.
- [ ] **OBJ-08** Release the worker on input release, stale input, leaving range, obstruction, disconnect, phase change, reset, or completion. Prevent one player from holding two stations by tracking its active objective.
- [ ] **OBJ-09** Implement ObjectiveManager's station registration and per-team ordered sequence. Validate exactly three unique sequence entries for each team; surface missing/duplicate references in development.
- [ ] **OBJ-10** Make completed stations advance the corresponding team's index and score once per round. Use completion guards; repeated callbacks/RPC attempts cannot add another point.
- [ ] **OBJ-11** Publish progress snapshots around 10 Hz, plus immediate worker/completion changes. Keep full-precision server progress independent from display throttling; smooth only client visuals.
- [ ] **OBJ-12** Build a station prefab with a visible panel, fixed interaction anchor, progress display attachment, team/sequence label, solid body if desired, and a separate trigger layer if used for hints.
- [ ] **OBJ-13** Place three mirrored stations per team. Each station permits work only by its owner team; opponent players may approach for gaze but cannot steal/reset progress.
- [ ] **OBJ-14** Add tests for partial work retention, effect change during work, exactly-once completion, zero/invalid durations, rejected ineligible workers, and one active station per player.
- [ ] **OBJ-15** In two builds, verify identical progress/completion, release at range/LOS boundaries, support/debuff timing, teammate worker contention, and duplicate completion protection.
- [ ] **OBJ-16** Remove the temporary work harness from Main once the real station is integrated; retain a development-only test scene only if it helps reproduce defects.

## 06 — Implement the complete round lifecycle

**Window:** H14–18. **Owner:** C; A handles session callbacks. **Priority:** P0. **Dependencies:** objective completion/score contracts, player/effect resets. **Output:** a full 1v1 round, results, and repeatable reset.

- [ ] **MAT-01** Create server-owned MatchManager state for phase, round ID, countdown deadline, match deadline, result, and final team score. Clients render these values and cannot advance phases directly.
- [ ] **MAT-02** Implement the server start precondition against initialized players and readiness. A temporary development start path may be used before the lobby exists, but it must call the same reset/start method and be removed from release.
- [ ] **MAT-03** Implement reset in an explicit order: suppress gameplay; increment round ID; clear accepted input/work/gaze/statistics; reset objective sequences/progress; place players; set initial effects; publish countdown state/deadline.
- [ ] **MAT-04** Start a three-second countdown using server time. Keep players still and objectives inactive. Transition once to Playing and set the 180-second match deadline.
- [ ] **MAT-05** Establish one server simulation ordering: consume input -> move using previous modifier -> due gaze evaluation and aggregation -> advance all objectives -> resolve result. Avoid independent scripts racing to award a winner.
- [ ] **MAT-06** Resolve final-station completion after both teams' work is processed. One finishing team wins; two in the same step draw. Guard the result against subsequent changes.
- [ ] **MAT-07** At timeout, compare completed stations, then current station normalized progress with 0.001 tie tolerance. Limit the final work step to time remaining; final completion during that interval has precedence over timeout ranking.
- [ ] **MAT-08** Transition to Results once, freeze movement/work/gaze, store scores, and clear active interactions/effects. Preserve final progress and result for the results view until reset.
- [ ] **MAT-09** Implement host Return to Lobby/Rematch as one safe reset route preserving roster and valid selections while clearing ready states and transient input. Require another normal ready/start cycle.
- [ ] **MAT-10** Handle non-host disconnect during Countdown/Playing as Aborted with reason and no winner. Free its registry/worker/gaze entries. In Lobby, just free the slot; in Results, preserve the already recorded result.
- [ ] **MAT-11** Handle host departure or lost connection locally by stopping input, shutting down, and returning to the menu with a message. Do not attempt host migration or restoration of a live round.
- [ ] **MAT-12** Reject joins during Countdown/Playing/Results. Permit fresh joins again in Lobby. Check a rejoining player does not inherit stale ready/input state from an earlier identity.
- [ ] **MAT-13** Add pure rule tests for single winner, same-step draw, timeout score/progress comparisons, tolerance ties, final-step duration clamp, and a result that cannot be overwritten.
- [ ] **MAT-14** Run a short debug-duration round for each ending path, then restore the intended config. Verify host/client agreement and inspect reset of every authoritative field.
- [ ] **MAT-15 — G3** Play a complete 1v1 round on two PCs/builds, return to Lobby, and start again. Record results and update `progression.md`; keep this known-good build before adding lobby polish.

## 07 — Complete teams, color selection, readiness, and lobby

**Window:** H18–22. **Owners:** A/E; D provides materials. **Priority:** P0. **Dependencies:** server player identity, lifecycle, stable selection IDs. **Output:** authoritative lobby supporting exactly 1v1 or 2v2.

- [ ] **LOB-01** Implement TeamManager with stable Team A/B IDs, mode-specific capacity, connected roster, palette ID, and spawn lookup. Do not compare renderer colors to decide whether players are allies.
- [ ] **LOB-02** Create three curated distinct color-pair entries and A/B symbols. Give the host a pair selector; show the assigned colors and labels to every player. Avoid arbitrary color picking or color-dependent rules.
- [ ] **LOB-03** Implement local team-change requests through sender-owned identity. Accept only in Lobby and only if the requested slot is free. Auto-assign new players to an available slot without exceeding selected-mode capacity. A third/fourth joiner in a 1v1 lobby waits unassigned and blocks Start until the host selects 2v2 or extra players leave; do not spawn a live spectator role.
- [ ] **LOB-04** Implement mode selection by host. Switching to 1v1 is rejected while more than two are connected; switching modes with a valid population rebalances slots deterministically and clears readiness. A 2v2 lobby may wait with one to three players.
- [ ] **LOB-05** Store ready status server-side. Clear all readiness on any successful team, mode, palette, or cosmetic change; clear readiness on disconnect or roster changes as well.
- [ ] **LOB-06** Implement the start predicate: host request, Lobby phase, exact selected player count, balanced full teams, every player initialized, and everyone ready. Validate again on the server even if the Start button is disabled locally.
- [ ] **LOB-07** Mark a client initialized only after its player and session state have spawned and its UI can bind. Send an owner-authenticated acknowledgement if needed; do not count a pending connection as ready.
- [ ] **LOB-08** Bind lobby rows to current player identity/team/readiness/cosmetic state. Create/remove rows on spawn/despawn and populate initial values immediately. Do not assume callbacks alone will paint already-existing values.
- [ ] **LOB-09** Apply team body tint through a renderer property block or the selected pipeline's tested equivalent. Preserve separate cosmetic materials and add A/B labels or shapes to nameplates and station signs.
- [ ] **LOB-10** Route start, team, cosmetic, ready, and palette actions through validated server requests with bounded frequency. Use RPC sender/ownership metadata rather than trusting submitted client IDs.
- [ ] **LOB-11** Show why Start is unavailable: waiting for players, unbalanced teams, initialization pending, or not everyone ready. Disable controls during Countdown/Playing/Results to match server rules.
- [ ] **LOB-12** Remove debug team-selection and unrestricted-start controls from the release path. Verify the actual lobby is the only normal route into a match.
- [ ] **LOB-13** Test full-team selection rejection, simultaneous team requests, palette propagation, ready clearing, invalid cosmetic IDs, host-only permissions, and mode changes with two/three/four players.
- [ ] **LOB-14** Run a complete four-player lobby -> 2v2 -> results -> lobby session. Verify each person controls only their own player and all four see matching scores/effects.

## 08 — Build a readable, fair arena

**Window:** graybox H0–6, integration H18–24, polish H24–30. **Owner:** D; B/C review sightlines/objectives. **Priority:** P0 graybox/readability, P2 decoration. **Dependencies:** spawn/station prefab contracts; no art dependency for G2.

- [ ] **LVL-01** Build a standalone arena prefab from cubes/planes with a flat walkable surface, a central contested sightline, two team routes, and simple cover. Keep it small enough for 15 m gaze to matter.
- [ ] **LVL-02** Mirror team spawn and station layouts. Place two non-overlapping spawn points per team with comparable routes to their first station. Avoid spawning already inside a wall or station collider.
- [ ] **LVL-03** Place A1/A2/A3 and B1/B2/B3 stations in readable sequence. Design each worker area so an ally has a useful support position and opponents have at least one contestable line of sight.
- [ ] **LVL-04** Include cover that lets a player break incoming gaze, plus routes around cover so a team cannot permanently deny all useful sightlines. Avoid a single mandatory exposed choke or an unreachable safe work pocket.
- [ ] **LVL-05** Separate visual meshes from simple solid collision shapes where necessary. Audit layer masks for world blockers, player targets, cosmetic meshes, interaction hints, and non-blocking effects.
- [ ] **LVL-06** Keep station anchors visible and reachable at 2 m. Check a player's own station cannot block every ray from its eye to the interaction anchor. Ensure gaze chest anchors stay above low floor geometry.
- [ ] **LVL-07** Add team signs, A/B symbols, station numbers, and current/locked/completed visual states. Make essential labels readable at gameplay distance and distinguish them beyond color alone.
- [ ] **LVL-08** Add bounds/fall reset region and verify there are no playable routes requiring jumping. Remove decorative collision catches and camera-clipping obstacles.
- [ ] **LVL-09** Measure travel times for both teams, support positions, and disruption approaches. Record asymmetries and adjust placements before replacing primitives with art.
- [ ] **LVL-10** Run 1v1 and 2v2 playtests with a player unfamiliar with the level. Check they can locate their station and understand where to look for support/disruption without verbal coaching.
- [ ] **LVL-11** Deliver the arena prefab to A for Main integration. Verify all prefab references survive merging and neither scene scripts nor station sequence IDs are duplicated.
- [ ] **LVL-12 — P2** Only after G4, replace or decorate primitive visuals while retaining tested collider shapes and readable sightlines. Re-run occlusion/interaction tests after each meaningful geometry change.

## 09 — Bind core UI and clear gaze feedback

**Window:** local mock work H0–12; network binding H12–24. **Owner:** E; A/B/C supply events. **Priority:** P0. **Dependencies:** stable state contracts; final completion requires G3 and lobby state.

- [ ] **UI-01** Create one Canvas with Main Menu, Connecting, Lobby, HUD, local menu, and Results panels. Choose a reference resolution and scaling policy; test common 16:9 and smaller laptop windows.
- [ ] **UI-02** Add the Who's Watching? title, Host/Join/Settings/Quit, address/port fields, connection error text, and Cancel. Validate input locally for readable messages while retaining host-side validation.
- [ ] **UI-03** Bind panels to session/phase changes. Ensure only the correct interactive panel is active, cursor lock follows local menu state, and closing a panel does not destroy networking objects.
- [ ] **UI-04** Finish roster rows and team/mode/palette/cosmetic/ready controls. Show host-only controls distinctly and waiting/start-disabled reasons from the lobby predicate.
- [ ] **UI-05** Build HUD elements for A/B identity, current station number/name, both completion counts, authoritative timer, crosshair, interaction prompt, and progress bar. Initialize from current state before subscribing to changes.
- [ ] **UI-06** Show Acquire/Supporting teammate/Disrupting enemy with distinct icon/text cues. Use local candidate preview only for acquisition; use confirmed host target for active support/disruption.
- [ ] **UI-07** Show incoming ally/enemy counts and net speed/work multiplier. Verify mixed incoming effects can display together and a net slowdown does not hide allied help.
- [ ] **UI-08** Add clear station states: Wrong team, Complete previous station, Hold E, Busy, Out of range, and Completed. Avoid flashing between prompts from several nearby targets; follow the selected/active target consistently.
- [ ] **UI-09** Derive the timer from replicated server deadline and synchronized time; clamp display at zero. Never award local results when the display reaches zero.
- [ ] **UI-10** Build Results with winner/draw/aborted reason, final team scores, host Return to Lobby/Rematch, and local Leave. Non-host players see Waiting for host.
- [ ] **UI-11** Keep visual fade effects separate from authoritative gameplay. When an effect ends, labels/state update correctly even if a decorative highlight fades briefly.
- [ ] **UI-12** Remove mock/demo state providers from Main and verify every runtime progress/score/effect view binds to actual replicated state. Test opening a panel after state already changed.
- [ ] **UI-13** Test cursor/input behavior while opening/closing menus, disconnecting, ending a round, and returning to lobby. Ensure menu focus does not leave movement or E stuck active.
- [ ] **UI-14** Have someone identify all four gaze feedback cases without explanation. Adjust text/icons/contrast if they cannot tell who is affected or whether acquisition is confirmed.
- [ ] **UI-15 — G4** Run four players through lobby selections, ready/start, complete 2v2, results, and another lobby. Log all P0 defects; preserve the first integrated four-player build and update the handoff.

## 10 — Add small cosmetics, settings, sound, and statistics

**Window:** H24–30. **Owners:** D/E; A/C validate data. **Priority:** P1 unless marked P2. **Dependencies:** G4 and no unresolved core blockers. **Output:** intended first-version presentation features, each safely removable.

### Cosmetic catalog and selection

- [ ] **COS-01** Create a stable catalog with None and two small hats, each assigned an immutable ID for this build. Add a visual prefab and optional UI preview reference; do not introduce inventory/account services.
- [ ] **COS-02** Create the player's head socket and CosmeticManager. On initial network state and later CosmeticId changes, remove the old visual and attach the selected local prefab at fixed scale/offset.
- [ ] **COS-03** Remove/disable colliders, NetworkObjects, and gameplay scripts from hats. Confirm no cosmetic changes eye/chest anchors, CharacterController dimensions, movement, work, or gaze range.
- [ ] **COS-04** Bind a lobby selector to owner requests; validate catalog membership and Lobby phase on host. Invalid/missing IDs fall back to None and cannot block player spawning.
- [ ] **COS-05** Save the local preference and apply it only through a fresh validated lobby request. Reset ready states after an accepted change. Do not trust saved local data as authoritative selection.
- [ ] **COS-06** Compare every cosmetic from front/back/side at gameplay distance. Team tint and A/B labels must remain readable. Verify identical target/collision bounds and no gaze-ray interference.

### Local settings and readable feedback

- [ ] **POL-01** Add mouse sensitivity and master volume controls with sensible bounds and persistent local preferences. Apply defaults when missing/invalid; do not replicate these settings.
- [ ] **POL-02** Add short support-start, disruption-start, incoming-state-change, and station-complete cues. Trigger from confirmed state transitions, avoid double-play on host, and rate-limit repeated acquisition sounds.
- [ ] **POL-03** Add a compact controls/help panel explaining teammate gaze speeds them up, enemy gaze slows them down, cover breaks gaze, and E advances the team's active station.
- [ ] **POL-04** If desired, collect host-side active work/support/disruption seconds and completed-station counts per player. Count once per interval while Playing; reset on round start and publish final statistics at Results.
- [ ] **POL-05** Display small final statistics only if their collection is reliable; otherwise retain final team scores and document the cut. Do not substitute locally estimated competitive values.
- [ ] **POL-06 — P2** Add minimal particles or body pose/head-look animation derived from accepted movement/effect state. Keep them local; avoid networked particle objects or controller complexity.
- [ ] **POL-07** Re-run team readability, LOS, input, and rematch checks after cosmetic/audio/UI additions. Remove any feature that introduces a core regression and cannot be fixed within the phase.
- [ ] **POL-08 — G5** Record whether cosmetics, settings, cues, and statistics shipped or were explicitly cut. Build a known-good candidate; update `progression.md` and preserve it before stretch work.

## 11 — Harden multiplayer behavior and tune the game

**Window:** H24–36, with fixes through H44. **Owners:** A/B/C; E logs evidence. **Priority:** P0. **Dependencies:** full integrated loop. **Output:** stable resets, sensible pacing, bounded traffic, and observable failures.

### Authority, cleanup, and lifecycle audit

- [ ] **HARD-01** Inspect all NetworkVariables and rule-changing RPCs. Confirm only the server writes team/ready/effects/objective/phase/score/result state; identity-sensitive requests check actual sender ownership.
- [ ] **HARD-02** Inspect every client-provided value: aim, movement, objective ID, cosmetic ID, team, ready, palette, mode, and round. Reject non-finite or invalid data before applying state or dereferencing objects.
- [ ] **HARD-03** Verify three input defenses together: old sequence rejection, wrong-round rejection, and 0.25-second freshness expiry. Test release packet loss while a subsequent held=false snapshot still stops work.
- [ ] **HARD-04** Check reset clears movement velocity, held interaction, worker reservations, outgoing targets, lock timers, incoming counts, multipliers, station progress/completion, statistics, ready flags, and stale result state at the appropriate phase.
- [ ] **HARD-05** Audit spawn/despawn/UI event subscriptions. Subscribe once, initialize current values, unsubscribe on destruction, and avoid accumulating callbacks after rematch or leave/rehost.
- [ ] **HARD-06** Force client closure in each connected phase, then host closure. Verify exactly the documented abort/menu behavior, no phantom worker/gaze, and no live host migration attempts.
- [ ] **HARD-07** Run three consecutive rematches and a leave/rehost/join cycle. Count network objects and callbacks before/after; investigate growth or missing in-scene objects.
- [ ] **HARD-08** Check the same code path on host and remote client: no doubled movement, timer advancement, objective tick, cue playback, or scoring on the host.

### Performance and network behavior

- [ ] **PERF-01** Add a development overlay showing build ID, local role, players, round/phase, accepted input age, gaze target/counts, modifiers, station worker/progress, and available network timing metrics. Hide it in normal release UI.
- [ ] **PERF-02** Profile four active players with gaze and work. Record CPU/frame time, raycast counts, allocations, and network traffic on the weakest test PC; target stable 60 FPS without inventing unmeasured guarantees.
- [ ] **PERF-03** Verify approximately 20 Hz input/gaze, 30 Hz simulation/transform baseline, and 10 Hz objective display updates. Measure actual behavior in the selected package; avoid sending identical reliable state every render frame.
- [ ] **PERF-04** Remove per-frame scene searches, repeated material creation, unnecessary garbage-heavy LINQ in hot paths, and spam logs if observed in profiling. Do not introduce jobs/ECS/spatial partitioning for four players without evidence.
- [ ] **PERF-05** Confirm particles, local UI, camera effects, and decorative animation are not individually synchronized. Disable unused transform axes and avoid continuous string payloads.
- [ ] **PERF-06** Test a documented modest latency/loss scenario using available transport tools, including reordered input and stale expiry. Separate the measured result from expected LAN quality; investigate stuck state before tuning smoothing.
- [ ] **PERF-07** Check a long host frame does not produce unbounded catch-up work, duplicate results, or more progress than elapsed match time permits. Keep scheduling policy explicit and deterministic enough to debug.

### Gameplay tuning and deadline decisions

- [ ] **BAL-01** Play 1v1 with both players attempting work, then with one prioritizing disruption. Check 0.50 minimum speed preserves progress and that sightline repositioning matters.
- [ ] **BAL-02** Play 2v2 with worker/support, two independent workers attempting the same station, and aggressive enemy disruption. Confirm one-worker restriction and the actual value of ally gaze.
- [ ] **BAL-03** Observe mixed gaze cases and mutual gaze. Tune coefficients/lock/range or level cover only after noting the concrete issue; preserve ally-positive/enemy-negative behavior and solid-wall rejection.
- [ ] **BAL-04** Measure round duration, route fairness, and time spent idle. Adjust station duration/travel/cover for a short enjoyable race; record final Inspector values and ensure every build uses the same config assets.
- [ ] **BAL-05** Lock selected round configuration on the host at start. Ensure clients cannot change authoritative rules through local Inspector/preferences; verify build/protocol checks catch incompatible distributed builds.
- [ ] **BAL-06 — G6** At H36, freeze feature scope and package versions. Remove unfinished stretch entry points, list remaining defects by severity, build the candidate, and update `progression.md` with precise unfinished work.

## 12 — Execute the acceptance and regression matrix

**Window:** starts at G1, main pass H30–44. **Owner:** E coordinates; everyone tests/fixes. **Priority:** P0 except optional-feature rows. **Dependencies:** relevant feature implemented. **Output:** evidence, defect list, and a release decision.

For each item below, log the build and actual test environment. A checked item means the expected outcome was observed, not merely that the code appears to handle it. Use the matching T-number in Plan section 19. Do not mark a whole matrix passed when only host/editor behavior was exercised.

### Connection, population, and player control

- [ ] **QA-01 / T01** Host a fresh standalone build and join from a second PC with the correct address/port. Repeat with wrong address, wrong port, cancel, and retry; confirm recoverable UI and no duplicate manager.
- [ ] **QA-02 / T02** Fill four slots, attempt a fifth, test a mismatched build/protocol token, and attempt joining in Countdown/Playing/Results. Confirm clear rejection and later successful join in Lobby.
- [ ] **QA-03 / T03** Test each player's independent movement/look, local camera/listener, diagonal speed, pitch clamp, collision, menu release, and out-of-bounds reset. Exchange which PC hosts.
- [ ] **QA-04 / T12** Exercise 1v1/2v2 selection, full team rejection, three-player waiting lobby, readiness reset on every accepted selection/roster change, and non-host start attempts.

### Gaze and modifiers

- [ ] **QA-05 / T04** In the debug harness or 2v2 session, verify ally target speeds that target up; enemy target slows that target; self cannot be targeted; no-target returns to 1.0. Test both host observer and client observer.
- [ ] **QA-06 / T04** Move across range/cone boundaries and hold acquisition just below/above 0.15 seconds. Confirm angle is a half-angle, distance is measured from agreed anchors, and switching target restarts acquisition.
- [ ] **QA-07 / T05** Put a wall/corner between observer and target; repeat with trigger, cosmetic, and another player's body. Confirm only intended solids/players block and partial hit-buffer handling cannot produce through-wall effects.
- [ ] **QA-08 / T06** Test mutual allies, mutual enemies, two enemies looking at one player, and observer switching. Confirm unique counts, correct recipient, and no order-dependent lingering contribution.
- [ ] **QA-09 / T07** Test one ally + one enemy and one ally + two enemies; expect 1.10 and 0.85 default multipliers. Verify two enemies alone produce 0.50 and no state ever reaches zero speed.
- [ ] **QA-10 / T06–T07** Look away, open a local menu, disconnect, and reset while gazing. Verify authoritative removal, correct icon transitions, and no stale lock or multiplier after reconnect/lobby.

### Objective and round correctness

- [ ] **QA-11 / T08** Work at neutral, buffed, and debuffed rates; then change gaze midway through a task. Compare observed host progress against integrated time, not a restarted countdown.
- [ ] **QA-12 / T09** Try enemy-owned, locked, completed, missing, distant, and wall-occluded stations. Try two workers and a worker leaving range. Confirm one worker, valid retry, retained partial progress, and released reservations.
- [ ] **QA-13 / T09** Acquire a station, keep E held, and look toward a player while staying in range/LOS. Confirm work continues and gaze applies, matching the explicit interaction rule.
- [ ] **QA-14 / T10** Trigger repeated completion requests and simultaneous final station completions through a deterministic test harness. Verify one score increment per station and a same-step draw, independent of processing order.
- [ ] **QA-15 / T11** Exercise timeout with unequal completed counts, equal counts/unequal partial progress, tolerance tie, and final completion on the deadline step. Verify identical results on all clients.
- [ ] **QA-16 / T15** Finish three rounds in a row through normal lobby readiness. Verify scores, workers, effects, timers, input, statistics, ready flags, and result presentation start clean each time.

### Failure handling, visuals, and build parity

- [ ] **QA-17 / T13** Disconnect a client in Lobby, Countdown, Playing, and Results. Verify slot release, abort where specified, no lingering effects/work, and no rewritten result after a completed match.
- [ ] **QA-18 / T14** Close/terminate the host; verify all clients return to a usable menu. Host a fresh session and reconnect from those clients without restarting the application if the cleanup flow supports it.
- [ ] **QA-19 / T16** Inject delay/loss/reordering and old-round input. Confirm stale intent expires, old snapshots cannot reactivate work, and recovery does not multiply movement or score.
- [ ] **QA-20 / T17** Play full 1v1 and 2v2 using the same standalone release-candidate build. Prefer four PCs; if using two PCs/four processes, record that limitation explicitly and exercise every process independently.
- [ ] **QA-21 / T17** Check every supported palette/cosmetic with readable labels and all four gaze-feedback states. Verify no cosmetic changes collision, aim anchors, gaze blocking, speed, or task duration.
- [ ] **QA-22 / T17** Verify main menu, lobby, HUD, settings, results, cursor lock, Quit, and Leave at common window sizes. Ensure controls/help match shipped bindings and no UI prompt implies an unimplemented feature.
- [ ] **QA-23 / T18 — if included** Run the dedicated tests for Relay or any second objective type. An omitted stretch feature is marked CUT, not tested/passed.
- [ ] **QA-24** Review the defect list: reproduce each P0, fix or revert its cause, rerun its specific scenario, and run one full round after gameplay/network fixes. Do not reopen broad unrelated work after successful validation.
- [ ] **QA-25 — G7** Confirm no P0 release blockers, record remaining known issues and untested hardware/network conditions, and name the exact candidate build/commit. Update `progression.md` with actual pass/fail evidence.

## 13 — Optional work, explicitly gated

**Window:** only after G5; integrate and test before H36. **Owners:** assigned below. **Priority:** P3. **Dependencies:** P1 work completed or explicitly cut, no unresolved P0 blocker, and a preserved LAN build. **Output:** at most one substantial stretch feature. Skip this entire section when the critical path is late.

### Relay Internet connections — A with E testing

- [ ] **REL-01** Decide whether Internet play materially helps the submission and whether service/project access already exists. Record a bounded spike of approximately two hours; do not begin merely because Relay is mentioned in the concept.
- [ ] **REL-02** Verify the chosen Unity/editor compatibility, then add the unified Multiplayer Services SDK required for the current Relay workflow. Follow official documentation linked in Plan section 21; do not mix legacy standalone package samples into the project.
- [ ] **REL-03** Link the project to the intended Unity services project and verify service access/configuration. Keep credentials, private dashboard data, and account identifiers out of source-controlled notes.
- [ ] **REL-04** Add a connection-mode selection that keeps Direct LAN working. Initialize services and anonymous authentication only for Relay mode; show progress, cancel/failure recovery, and a return to the normal menu.
- [ ] **REL-05** Implement host allocation for three joining connections, obtain a join code, configure UnityTransport, and start the NGO host. Preserve four total players including host in admission logic.
- [ ] **REL-06** Implement client code entry, join allocation, transport configuration, and NGO client start. Normalize/validate input and handle invalid/expired codes without leaving the UI stuck.
- [ ] **REL-07** Reuse the existing game lobby, player spawn, gameplay authority, ready flow, and results. Do not add cloud Lobby/matchmaking unless an already proven requirement emerges outside this jam scope.
- [ ] **REL-08** Test over separate Internet connections with four total players, an invalid code, timeout/cancel, host loss, and a complete round/rematch. Then re-test direct LAN from a fresh application start.
- [ ] **REL-09** If the spike fails or acceptance cannot finish before freeze, revert/disable the Relay feature and dependency paths as appropriate, retain the tested LAN build, and mark this section CUT with the actual reason.

### One extra objective type — C with D/E

- [ ] **EXT-01** Choose exactly one type only if the existing objective contract can support it without changing core lifecycle or scoring. Prefer a repair visual variant or simple switch before a carry/capture system.
- [ ] **EXT-02** Define complete rules before coding: eligibility, start/stop/completion, team ownership, progress persistence, gaze benefit, reset, and simultaneous interactions. Record how it fits the same station race.
- [ ] **EXT-03** Implement through ObjectiveBase/ObjectiveManager with host-owned state. For a logical carry task, store carrier/item/destination IDs on the host and render a local attachment; avoid free networked physics.
- [ ] **EXT-04** Add only the new prefab/prompt/presentation needed; preserve existing hold stations as a fallback. Verify worker/carry state clears on disconnect/reset and score is awarded exactly once.
- [ ] **EXT-05** Run the new type on host/client and in a four-player round, including mixed gaze and invalid requests. If tests do not pass before H36, remove it from the release scene and record the cut.

### Low-cost polish — D/E

- [ ] **OPT-01 — P2** Add extra hat variations or environmental detail only after the base catalog and readability tests pass. Confirm the added assets do not require new gameplay code or packages.
- [ ] **OPT-02 — P2** Add subtle animation/particles/transitions only when they clarify existing state. Re-test frame rate, team visibility, and the absence of extra network traffic.
- [ ] **OPT-03** At freeze, hide every unfinished optional control, remove placeholder instructions, and verify the build behaves correctly with all optional features disabled.

## 14 — Build, package, and prepare delivery

**Window:** H44–48. **Owners:** A builds; E verifies; one named human handles submission. **Priority:** P0. **Dependencies:** G7. **Output:** a tested distributable, instructions, release notes, and accurate handoff.

### Lock and build the candidate

- [ ] **SHIP-01** Agree the submission owner, actual destination, required format/size, deadline, and upload buffer. Preserve the previous known-good archive before rebuilding. Do not spend the buffer on a new feature.
- [ ] **SHIP-02** Review Git status and commit intended release changes. Confirm ProjectVersion, manifest, lockfile, scenes, prefabs, configs, and .meta files are included; machine caches, credentials, and local logs are excluded.
- [ ] **SHIP-03** Verify product/window title `Who's Watching?`, target Windows architecture, intended app icon if ready, version/build ID, and Main as the correct initial scene. Exclude development test scenes from the release scene list.
- [ ] **SHIP-04** Disable development-only team/start overrides, fake providers, debug HUD by default, and noisy logs. Confirm disabling debug code does not remove required input/session handling.
- [ ] **SHIP-05** Check no missing scripts/references, compilation errors, invalid layer masks, duplicate managers/listeners, or unassigned required station/spawn/catalog references. Keep runtime-friendly error messages for connection failures.
- [ ] **SHIP-06** Build the release candidate from the committed project. Record the exact editor/package versions, commit, build settings, and build result. Keep a development build separately for diagnosis if needed.

### Instructions, licensing, and packaging

- [ ] **SHIP-07** Update README with title, game summary, Windows requirement, two/four players including host, tested network mode, and basic controls. Explain ally gaze buffs, enemy gaze debuffs, cover, and station progression.
- [ ] **SHIP-08** Document direct Host/Join steps: same LAN, host's reachable local address, agreed port, matching builds, and permission for the application on the intended private network. State that arbitrary Internet direct-IP connectivity is not guaranteed.
- [ ] **SHIP-09** Document Lobby -> ready -> start -> objectives -> results -> rematch/leave, including host-only controls and what happens when a player or host disconnects. Include Relay code instructions only if REL-08 passed.
- [ ] **SHIP-10** Add asset credits/license notes and known issues/untested conditions. Distinguish missing optional features from known defects in required functionality; disclose any remaining 2v2 or separate-PC test limitation.
- [ ] **SHIP-11** Add concise release notes with version, tested modes, last validation date, and reproduction instructions for important known issues. Ensure file names and help text use the confirmed game title consistently.
- [ ] **SHIP-12** Package the executable with its complete associated data/runtime files in one archive. Include README/credits as needed. Do not distribute only the executable or the entire Unity cache/project by mistake.

### Verify the artifact actually being delivered

- [ ] **SHIP-13** Copy and extract the archive on another PC to a clean directory. Launch from the extracted package, not from the Editor or an old build directory; confirm title/menu/resources appear correctly.
- [ ] **SHIP-14** From the extracted package, run a full 1v1 and four-player 2v2 session across PCs, test ally/enemy gaze and a solid wall, finish a round, rematch, and exercise Leave. Record archive/build identity with evidence.
- [ ] **SHIP-15** Re-run invalid connection and host-loss recovery on that package. Check logs for runtime exceptions; ensure an error does not leave controls disabled permanently.
- [ ] **SHIP-16** If the artifact differs after a fix, rebuild/repackage and repeat the affected checks plus a full round. Do not assume a test on the prior archive validates the replacement.
- [ ] **SHIP-17** Have the designated owner deliver/upload according to the actual destination rules and verify the published/downloadable artifact where applicable. Record the final archive/version and delivery result; keep a local backup of the known-good build.
- [ ] **SHIP-18 — G8** Update `progression.md` with delivered scope, completed and partial tasks, validation run, omitted checks, known issues, release identity, and exact next actions. Leave unimplemented items unchecked and close only the achieved gates.

## 15 — Deadline fallback actions

Use these when a gate slips. They are conditional actions, not additional features. Do not check them unless actually needed and performed. Capture each decision in `Docs/Decisions.md` and the handoff.

| Trigger | Immediate action | Keep working toward |
| --- | --- | --- |
| G1 missed | A/B pair on host/player/transport; D/E continue isolated graybox/UI; cut stretch work | Two connected moving players |
| G2 missed | Freeze cosmetics/art, use standard body samples/flat arena/debug rays, inspect authority and layers | Both gaze effects plus solid-wall rejection |
| G3 missed | Keep one hold objective prefab and one mode; remove extra task ideas | Full round, consistent result, reset |
| G4 missed | Reduce art/UI complexity and fix population/teams/simultaneous gaze | Actual 2v2, not a mislabeled 1v1 build |
| Cosmetics broken | Disable selector, default None for everyone | Readable team body colors and equal gameplay bodies |
| Relay broken | Exclude Internet mode and restore tested direct transport | LAN host/client |
| Physics/carry broken | Remove extra type, retain hold stations | One polished objective race |
| H36 reached | Stop features/package changes; triage fixes and preserve candidate | Stable tested delivery |
| Quota reserve reached during agent work | Stop feature investigation/builds/tests and write the concise handoff | Accurate recoverable project state |

- [ ] **CUT-01 — conditional** Record the failing gate, evidence, selected smallest fallback, affected task IDs, owner, and validation required after the cut. Do not silently remove fixed gameplay pillars.
- [ ] **CUT-02 — conditional** Remove disabled-feature UI/help text and restore a coherent release flow. Run the core connection/gaze/objective/results smoke test after a cut that changes runtime paths.
- [ ] **CUT-03 — conditional** If required MVP behavior remains incomplete, label the build a partial prototype and list exactly what fails. Preserve the last working build and reproducible next steps.

## 16 — Post-jam backlog; no work inside the initial deadline

Keep these unchecked during the 48-hour scope. Reprioritize from measured gameplay and player feedback, not architectural ambition.

- [ ] **POST-01** Gather playtest notes on aiming, support/disruption value, pacing, accessibility, and network feel; rank problems by frequency and severity.
- [ ] **POST-02** Prototype movement prediction/reconciliation and improved camera/body presentation under realistic Internet latency while preserving server validation and gaze effect enforcement.
- [ ] **POST-03** Evaluate gaze acquisition and anchor changes with controlled tests. Add additional visible target anchors only if every accepted target remains subject to real LOS checks.
- [ ] **POST-04** Add controller/rebinding support, accessible palettes, UI scaling, and a short interactive onboarding mode based on actual player confusion.
- [ ] **POST-05** Expand objective types one at a time with explicit authority, interruption, reset, and gaze-benefit rules. Add automated regression coverage to each type.
- [ ] **POST-06** Expand maps and character art while maintaining consistent collision/gaze bounds and team identity. Keep cosmetics visual-only.
- [ ] **POST-07** Improve Internet connection UX, service error handling, and automated multi-process/network-stress testing. Consider reconnect and host migration as separate designed features.
- [ ] **POST-08** Reassess 3v3 only after reviewing six-player capacity, spawn slots, UI, gaze-stack balance, Relay connections, objective pacing, and performance. Run fresh six-player authority and network tests before claiming support.
- [ ] **POST-09** Consider public matchmaking/rankings/progression only after reliable repeated matches and a justified product need. Do not retroactively treat these as missing jam requirements.

## 17 — Requirement-to-task coverage index

| Requested area | Main implementation tasks | Verification |
| --- | --- | --- |
| Unity stack, architecture, files/scenes | SET-01–20, NET-01–08, PLY-01–03 | SET-20, SHIP-02–06 |
| Host-client, maximum four, LAN | NET-01–14 | QA-01–02, QA-20 |
| Local/server ownership and synchronization | PLY-04–11, HARD-01–08, PERF-01–07 | QA-03, QA-19 |
| Gaze detection and LOS | GAZ-01–15 | QA-05–08, QA-10 |
| Buff/debuff stacking, bounds, task speed | EFF-01–10, OBJ-03 | QA-09–11 |
| Reusable objectives | OBJ-01–16, optional EXT-01–05 | QA-11–14 |
| Team colors, 1v1/2v2, ready/lobby | LOB-01–14 | QA-04, QA-20–21 |
| Full match/timer/results/rematch | MAT-01–15 | QA-14–18 |
| Cosmetics without advantage | COS-01–06 | QA-21 |
| Menus/HUD/feedback/settings/audio | UI-01–15, POL-01–08 | QA-10, QA-22 |
| Level and meaningful sightlines | LVL-01–12, BAL-01–04 | QA-07, QA-13, QA-20 |
| Five-person coordination and 48-hour gates | SET-10–20, each phase's owner/dependencies/gate | G0–G8 evidence |
| Risks and fallbacks | HARD-01–08, CUT-01–03 | QA-17–19, SHIP-10 |
| Optional Relay/join codes | REL-01–09 | REL-08, QA-23 |
| Build-to-build and separate-PC testing | NET-11–13, QA-01–25 | SHIP-13–16 |
| Final packaging/submission/handoff | SHIP-01–18 | G8 evidence |
| Post-jam expansion | POST-01–09 | Future planning and feature-specific tests |

## 18 — Milestone handoff template

After each gate or smaller meaningful milestone, replace the current snapshot in `progression.md` with concise actual status. Keep private account details and machine paths out of shared project files.

```text
Objective and current task:
Completed (task IDs and relative paths):
Partial work and remaining behavior:
Decisions and intentional cuts:
Validation run (build/commit, environment, result):
Checks not run and why:
Pending operations / blockers:
Exact next actions (ordered):
Known-good build/commit:
```

When account-usage telemetry is available, check it at session start, after small milestones, and before costly builds/investigations. If any applicable active window has 10% or less remaining, stop starting implementation and spend the remaining capacity on this handoff. Unknown telemetry is recorded as unknown, never invented. This rule does not make untested features complete.
