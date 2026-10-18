# Project handoff — 2026-10-17

## Current objective
Finish team-specific environment variants, cartoon movement, lobby team switching, Space jumping/animations and environment collision. Implementation and automated validation are complete. No commits/pushes made. Unity 6000.3.23f1; network protocol `whos-watching/lan/6-jump` requires matching peer builds.

## Completed
- Earlier character selection and six leader-selectable team presets remain. Ghost body and arms share dark main color; Devil uses dark body/light hands, feet and blush. Original source FBXs preserved.
- 54 linked variants under `Assets/_WhosWatching/Prefabs/TeamReady/TeamA` and `TeamB`, derived from 27 shared bases. Nested TeamTint ownership prevents a parent map tint overwriting child team accents; editor preview supported.
- Smoothed visual-only bob, sway, lean and squash in `Scripts/Player/CharacterPresentation.cs`; MotionAmount tunes strength without moving gameplay root/camera.
- Lobby A/B buttons, capacity two, server validation, readiness reset and leader succession. A free slot is required; reciprocal full-team swaps are not implemented.
- `NetworkPlayer.cs` adds server-authoritative grounded Space jump, repeat rejection, ceiling response and replicated Airborne. Both character controllers have Jump poses; presentation adds landing squash.
- Crate BoxColliders and static structural MeshColliders on all 27 bases, inherited by all 54 variants. Decorative pieces excluded. Existing Main imported props received matching colliders without repositioning.
- Editor maintenance: `TeamVariantSetup.cs`, `JumpAndCollisionSetup.cs`, updated `AppearanceSetup.cs` preserve nested bindings and regenerate variants/jumps/collision. Paths above are relative to `Assets/_WhosWatching` unless explicit.
- Docs/TeamAppearance.md explains authoring/use; README, Todo and Docs/Decisions updated.

## Validation actually run
- 47/47 EditMode tests passed (appearance, variant source links/nested isolation, motion bounds, collision presence, surface raycasts, jump clips, previous session/gaze coverage).
- Final Windows build succeeded; ignored executable `Builds/Windows/WhosWatching.exe`. Latest rebuild also succeeded after test-fixture change (`Logs/Build.log`).
- Real Unity motion and jump previews rendered and inspected (`Logs/MotionPreview-*.png`, `Logs/JumpPreview.png`).
- Final collider-enabled four-peer AppearanceSmoke passed 14 stages on three survivors (16383); departing leader passed seven (127).
- Two-peer JumpSmoke passed seven stages on each (127): lobby gate, actual jump/landing, no airborne repeat, ceiling, remote animator. Peaks 1.333/1.287 m; ceiling peaks 0.620/0.500 m.
- First collider-enabled ScreenGazeSmoke failed stages 1–3 because newly collidable imported props obstructed the old synthetic sightline. ScreenGazeProbe now creates its explicit test pad at X=100; normal arena collision unchanged. Rebuild and rerun passed all 18 stages on all four peers (262143/262143).
- Source/docs/scripts whitespace check passed. Unity YAML has generated trailing spaces. Asset metadata checked; generated machine-specific Resources files removed.

## Pending operation and next actions
- No pending operations. Requested implementation and automated checks are complete. Open Main for manual playtesting when ready.
- Recheck account quota before further implementation/tests; preserve the last 10% for handoff. No further feature work is needed for requested scope.
- Manual full UI/gameplay and separate-PC LAN acceptance remain untested. Automated peers use headless loopback processes. Prior two-peer GazeSmoke passed before this follow-up, not rerun on final collider build.
- Preserve user Mobile_RPAsset edit and saved Main layout. User saved/closed Unity before migrations. Checkpoint remains visual-only; objective rounds/scoring and host migration are outside scope. Props are static, not Rigidbody-driven crates.
- Keep Library, Builds, Logs and machine-generated files out of commits. Commit only if requested.

