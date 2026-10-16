# Project handoff

## Objective and current step — 2026-10-16
Create the Unity URP project in this repository, provide a multiplayer implementation runbook, and begin networking/tests while art is supplied later. **Initial LAN development foundation is implemented and tested on one PC.** Full game implementation remains pending. User approved Unity 6000.3.23f1 instead of requested 6000.3.24f1. Quota was recovered and checked above reserve during this continuation.

## Completed
- Repository-root Unity project: Assets/, Packages/, ProjectSettings/; editor/revision pin, generated package lock, visible meta files/text serialization, URP, Windows target and product name.
- Pinned NGO 2.13.1, Transport 2.7.4, URP 17.3.0, Input System 1.20.0. No cloud/service startup dependency.
- Assets/_WhosWatching/Scenes/Main.unity and Prefabs/Player/NetworkPlayer.prefab: registered network player, authoritative NetworkTransform/CharacterController, owner camera, fixed anchors, primitive visuals; floor/cover/walls and menu camera.
- Scripts/Multiplayer/NetworkGameManager.cs: direct host/join/cancel/leave, IPv4/port validation, protocol token, four-slot cap with pending reservation handling, callbacks and timeout. Session always represents a development lobby.
- Scripts/Player/NetworkPlayer.cs: owner-only input/camera, validated unreliable owner RPC, sequence/rate/finite checks, server movement at 30 Hz, 20 Hz input, 0.25-second freshness policy and out-of-bounds recovery. Player/player movement collision disabled.
- SessionRules and 13 EditMode tests. SmokeProbe is explicitly opt-in and development-only.
- Tools/Unity.ps1 and Tools/Smoke.ps1 provide repeatable tests/build/process checks. Multiplayer Setup.md, README.md, Docs/Decisions.md and Docs/TestLog.md describe setup, contracts, next milestones and evidence.
- Plan/Todo status synchronized conservatively. No full implementation gate claimed. No Git commit or push performed.

## Validation
- Initial import and scene-generation batch runs completed successfully.
- All 13 EditMode tests passed.
- Windows development build and final incremental rebuild succeeded.
- Two-process and four-process loopback tests passed on one Windows PC: population, authoritative movement replication and stale stop on every participant.
- Fifth client rejected at capacity; invalid protocol rejected in both topologies. No runtime exception/error markers in smoke logs.
- Unity generated asset metadata; file metadata check found no omissions. Library, Logs, Builds and UserSettings are ignored.
- Detailed limits and reproduction: Docs/TestLog.md. Baseline revision c85c8b4 plus current uncommitted changes. No committed known-good game revision yet.

## Partial work and decisions
- This is a movement test lobby, **not full 1v1/2v2 matches**. Teams/readiness, match phases/round IDs, gaze, work, scores/results and rematch are not implemented.
- Input uses temporary direct Input System device polling and IMGUI. Planned input action asset and Canvas UI remain to do.
- Leave/retry reuses a stopped NetworkManager instead of reloading Main. Full recovery acceptance is outstanding; see Docs/Decisions.md.
- Keep art under VisualRoot; preserve physics/network root and eye/chest anchors. Use primitive geometry until art arrives.
- ProjectSetup.Create refuses to overwrite existing Main; do not delete/regenerate scene/prefabs to hide integration issues.

## Exact next actions
1. Recheck quota and review Git status. Open repository root with 6000.3.23f1, open Main and play. Use README commands for repeatable builds/tests.
2. Manually test rendered scene, mouse capture/Escape, independent controls, collisions and audio listeners. Test a second PC over LAN with both host roles; record actual firewall/network results.
3. Add lifecycle tests for cancel/timeout/retry, Leave/rehost/rejoin and host crash. Verify slot cleanup and usable UI, then complete NET tasks based on evidence.
4. Introduce explicit input snapshots including round ID/input-enabled/interact/objective flags, Input Actions, team spawns and production phase gating. Keep server authority and stale-input behavior.
5. Implement Plan's gaze/LOS/lock and bounded effect aggregation, with pure tests and development work harness. Prove G2 before cosmetic/polish work.
6. Continue objectives, match lifecycle, real lobby/team/readiness, UI and art integration via Multiplayer Setup.md. Assign human scene/prefab owners and obtain clean-clone reproduction.
7. Update handoff and test evidence at each milestone. Commit only intended source/assets/settings/docs; no caches/builds/raw private logs.

## Pending operations and blockers
No background operations remain. No implementation blocker. Real separate-PC LAN and user-facing visual/input checks require suitable interactive testing/hardware. 24f1 compatibility is untested. Local build output exists under Builds/Windows and is intentionally ignored. The user has not requested a commit or push.
