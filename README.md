# Who's Watching?

Unity 6.3 URP multiplayer gaze prototype with animated Devil/Ghost characters, a pre-game appearance lobby, leader-selected team colors, and runtime arena accents. Objective rounds and the full production match lifecycle remain future milestones.

**Team variants:** build maps with `Assets/_WhosWatching/Prefabs/TeamReady/TeamA` and `TeamB`. Every accented prop has two linked variants sharing one base prefab. Use the lobby Team A/B buttons to switch to a team with space (two players maximum); switching clears everyone's readiness. Character motion now includes smoothly eased bob, sway and squash/stretch, adjustable with **Motion Amount** on the character prefab.

## Open and play

Install Unity **6000.3.23f1** with Windows build support, add this repository root in Unity Hub, and open `Assets/_WhosWatching/Scenes/Main.unity`. Allow package import before Play.

1. Host LAN in one instance and Join LAN in another. Use `127.0.0.1` on one PC, or the host local IPv4 on the same LAN. UDP port is 7777; maximum four players including host.
2. Choose **Devil** or **Ghost** before connecting or in the lobby. Each team's first connected player is its leader and chooses Azure, Coral, Gold, Mint, Violet, or Rose. The opposing team's color is unavailable. Everyone clicks **Ready up**, then the host clicks **Start game**. Both teams need a player; choices lock during play. The host can return everyone to the lobby.
3. Click the arena to the right of the menu to capture mouse. WASD moves, mouse aims, Escape releases and suspends outgoing gameplay intent. Leave ends your session.
4. Keep players anywhere in your camera view for 0.15 seconds: **all visible players count simultaneously**, within the configured 15 m range. You do not have to aim at their chest. Walls and intervening bodies block gaze; a visible head or body side can qualify.
5. Each target receives the sum of all incoming gaze: `1 + 0.35 × allied observers - 0.25 × enemy observers`, then the configured limits apply (defaults 0.50x–1.50x). One ally plus two enemies gives 0.85x; two allies plus one enemy gives 1.45x. The HUD shows every confirmed target, outgoing support/disruption, incoming counts and resulting movement/work multipliers. Team IDs stay A/B regardless of their selected colors.
6. Hold E for the five-second work test. Gaze changes its actual progress rate; this is a development harness, not the final station objectives.

Press **Space** with the mouse captured to jump. Both characters have an airborne pose and soft landing animation. Environment prefabs now include colliders inherited by their Team A/B variants.

All peers must use this new build (protocol `6-jump`). Older builds are rejected. Direct Internet NAT traversal is not included. Separate-PC LAN may require allowing the application on the intended private network.

## Build and test

Close this project in Unity before batch commands. Set `$unityEditor` to the installed Unity.exe, then run from the repository root:

```powershell
./Tools/Unity.ps1 -UnityEditor $unityEditor -Action Test
./Tools/Unity.ps1 -UnityEditor $unityEditor -Action Build
./Tools/Smoke.ps1 -Players 2
./Tools/Smoke.ps1 -Players 4
./Tools/GazeSmoke.ps1 -Players 2
./Tools/GazeSmoke.ps1 -Players 4
./Tools/ScreenGazeSmoke.ps1 -Players 2
./Tools/ScreenGazeSmoke.ps1 -Players 4
./Tools/AppearanceSmoke.ps1
./Tools/JumpSmoke.ps1
```

Run process suites sequentially; each host uses port 7777. The Windows development player is `Builds/Windows/WhosWatching.exe`; distribute the whole associated folder. Logs/builds are ignored by Git. Headless process tests do not certify graphics or separate-PC LAN.

Tune gaze in `Assets/_WhosWatching/Settings/GazeConfig.asset`. Artists replace visual children while preserving network roots, collision and anchors. See [Multiplayer Setup.md](Multiplayer%20Setup.md), [Docs/TestLog.md](Docs/TestLog.md), [progression.md](progression.md), [Plan.md](Plan.md) and [Todo.md](Todo.md).

See [Team appearance](Docs/TeamAppearance.md) for prefab ownership, palette tuning, animation sources, and material-slot bindings. Use the prefabs in `Assets/_WhosWatching/Prefabs/TeamReady` for new arena pieces.

## Change gaze and stacking values in Unity

In the **Project** window, open **Assets > _WhosWatching > Settings** and select **GazeConfig**. In the **Inspector**:

- **Stacking limits > Minimum / Maximum**: final movement/work multiplier limits after all positive and negative contributions are summed. Default 0.50 / 1.50. Set Maximum to 2.05 or higher to allow three full +35% ally bonuses; lower Minimum if you want stronger combined slowdowns.
- **Per-observer contributions > Move Support / Move Disruption / Work Support / Work Disruption**: each observer contribution (0.35 means 35%; disruption is a positive amount to subtract).
- **Screen visibility > Vertical Field Of View / Range / Lock Seconds**: camera coverage, maximum distance and acquisition delay. The host owns FOV; each client supplies its bounded viewport aspect ratio, so window resizing changes horizontal coverage.

For persistent settings, stop Play mode, edit and save this asset, then rebuild for standalone testing. The same asset is referenced by **Main > Networking > Gaze System > Config**. Host settings are authoritative. Do not change movement colliders to tune visibility: the separate **GazeBody** collider on the **GazeTarget** layer handles gaze and has no movement collisions.

Visibility uses samples on a standard body, not rendered pixels. Extremely thin visible slivers between samples may not qualify; hats never enlarge the gameplay visibility shape. The fixed-body approach keeps later imported art from changing gameplay.
