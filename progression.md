# Project handoff — 2026-10-16

## Current objective and status
Screen-wide multiplayer gaze is implemented and validated in the development arena. Every visible player can receive gaze simultaneously; all incoming ally and enemy contributions are summed before configurable limits. Unity remains 6000.3.23f1. Working tree changes are uncommitted on baseline bb534cf; nothing pushed. This is not the full objective-round game.

## Completed
- Scripts under Assets/_WhosWatching/Scripts/Gaze/: rectangular camera-frustum checks, validated owner viewport aspect, host-controlled FOV, 15 m default range, independent observer-target acquisition (0.15 s), and occlusion checks at fixed body samples.
- Dedicated GazeBody capsule in Prefabs/Player/NetworkPlayer.prefab on GazeTarget layer 9; no physical collisions. Avoids CharacterController ray-query shrinkage. World and other gaze bodies block visibility; cosmetics do not expand gameplay visibility.
- Multi-target replicated state/HUD, additive positive/negative movement and work effects, independent target release, stale/menu suppression and disconnect cleanup. Existing server movement/work authority retained.
- Unity Inspector configuration: Assets > _WhosWatching > Settings > GazeConfig. Edit Per-observer contributions and Stacking limits > Minimum/Maximum. Defaults +35% ally, -25% enemy, 0.50–1.50 final multiplier. Maximum 2.05 allows three full +35% contributions. Save outside Play mode and rebuild peers.
- Protocol whos-watching/lan/3-screen-gaze; all peers must rebuild together. Main Networking/GazeSystem references the config asset. ProjectSetup.InstallGaze migrates existing prefab/scene; Create is for initial creation only.
- README.md, Multiplayer Setup.md, Plan.md, Todo.md and Docs/Decisions.md describe current rules and tuning. Tools/ScreenGazeSmoke.ps1 and ScreenGazeProbe.cs add 18 reproducible network scenarios.

## Validation actually run
- Initial four-peer screen suite failed partial-head visibility (17/18 stages); investigation reproduced CharacterController query mismatch. Dedicated GazeBody collider corrected it.
- After correction: 30/30 EditMode tests passed, including viewport edges/aspect, body visibility/occlusion, independent acquisition and configurable additive bounds.
- Final Windows development build succeeded.
- Final Tools/ScreenGazeSmoke.ps1 -Players 4: all 18 stages passed on all four peers (262143/262143), including off-center/all-target effects, mixed stacking, partial visibility, walls, intervening body, invalid aspect, range, stale/menu input and disconnect cleanup.
- Final Tools/GazeSmoke.ps1 -Players 2: all ten stages passed on both peers (1023), work=True and movement=3. Actual movement and server work checked.
- Asset file metadata present. Build/log output is ignored locally in Builds/Windows and Logs. Detailed evidence/limitations: Docs/TestLog.md.

## Limits and next actions
1. Recheck account quota before further work; preserve the user's 10% reserve and existing changes.
2. Open Main and manually inspect the new HUD and screen-edge behavior, then test matching builds on separate PCs. Current automated runs are headless loopback processes on one PC with synthetic input. User confirmation applied to the prior gaze build, not this revision.
3. Visibility uses fixed standard-body samples, not rendered pixels. Very thin visible slivers may miss; art must stay under visual children and preserve the controller/network root and GazeBody.
4. Continue lifecycle acceptance: cancel/timeout/retry, Leave/rehost/rejoin, host loss. Production input actions, readiness/lobby, round IDs/phase gates, objectives/results/rematch remain planned work. E work is a test harness.
5. Exact work completion timings, injected network impairment, a clean clone on another machine and Unity 6000.3.24f1 upgrade remain untested. No full production gate signoff claimed.
6. Commit intended source/assets/settings/docs only when requested; do not commit Library, Builds, Logs or UserSettings.

## Pending operations
None. No known blocking compile/runtime failure after the fixes. Current requested screen-gaze/stacking implementation is complete; manual visual and real-LAN acceptance remain outstanding.
