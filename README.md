# Who's Watching? — Developer README

Developer tuning guide for the current Arena game, updated 2026-10-18.

**Unity:** 6000.3.23f1, Universal Render Pipeline.
**Main scene:** `Assets/_WhosWatching/Scenes/Arena.unity`.
**Build output:** `Builds/Windows/WhosWatching.exe` plus its adjacent data/runtime files.
**Current compatibility token:** `whos-watching/lan/9-arena-ui`.

This guide distinguishes **Inspector settings** from **code settings**. Not every mechanic has an Inspector slider. Numbers described as code settings must be changed in the indicated script, compiled and rebuilt.

## 1. Where to begin

1. Open **Arena.unity**.
2. Select **TeamLobby** in the Hierarchy for objective timing, crate references and spawn references on **Arena Gameplay**.
3. Select **Arena Audio** for all sound clips and volume settings.
4. Select `Assets/_WhosWatching/Settings/GazeConfig.asset` in the Project window for movement/work buffs, debuffs and visibility rules. The scene's **Networking → Gaze System → Config** reference selects the actual configuration used.
5. Open `Assets/_WhosWatching/Prefabs/Player/NetworkPlayer.prefab` for the physical player, camera anchors and character references.
6. Open the Devil or Ghost character prefab for appearance and visual movement settings.

Edit outside Play mode, save the scene/assets, then rebuild. Play-mode scene/component changes normally disappear when you stop. Changing a field initializer in a script does **not** replace an already serialized value on an existing scene, prefab or asset; edit that object in the Inspector.

The host owns movement, gaze evaluation, objectives and match results. Runtime network variables such as `Completed`, `Crates`, `Gaze`, `EndsAt`, `Winner`, `Working`, `DisplayName` and event counters are **state**, not balance settings. Do not use them as persistent Inspector configuration.

### Quick location map

All paths in this README are relative to the repository root. In tables, **Scripts/** means `Assets/_WhosWatching/Scripts/`.

| What you want to change | Where |
| --- | --- |
| Base walking speed, jump, sensitivity, punch force | `Scripts/Player/NetworkPlayer.cs` |
| Movement/work buffs and debuffs | `Assets/_WhosWatching/Settings/GazeConfig.asset` |
| Punch buff/debuff scaling | `Scripts/Gaze/GazeRules.cs` and `NetworkPlayer.PunchMultiplier` |
| Placement and sabotage time | Arena → **TeamLobby → Arena Gameplay** |
| Regulation/overtime duration and win rules | `Scripts/Multiplayer/ArenaGameplay.cs` |
| Spawns/deathbox | Arena → **TeamLobby → Spawn_A, Spawn_B, Deathbox** |
| Crate positions and checkpoint ownership | **TeamLobby → Arena Gameplay** arrays |
| Sound clips, music, volume, sound distance | Arena → **Arena Audio → Arena Audio (Script)** |
| Character look and movement animation | Character prefabs, animation assets, `CharacterPresentation.cs` |
| Lobby/UI colors, fonts and rounded panels | `Scripts/Multiplayer/GameUI.cs` |
| Lobby layout and buttons | `Scripts/Multiplayer/NetworkGameManager.cs` |
| Scoreboard, timer and results | `ArenaGameplay.OnGUI()` |
| Detailed HUD and nameplates | `NetworkPlayer.OnGUI()` / `DrawNameplate()` |
| Player name rules | `NetworkPlayer.CleanName()` / `SetDisplayNameRpc()` |
| Team colors | `Scripts/Multiplayer/TeamPalette.cs` |
| Player limits and lobby rules | `SessionRules.cs`, `TeamLobby.cs` |
| Resolution, rendering, physics, input project settings | **Edit → Project Settings**; see section 10 |

## 2. Movement, jumping, camera and input

These are code settings in `Scripts/Player/NetworkPlayer.cs`, unless marked Inspector.

| Setting | Current value / location | Effect |
| --- | --- | --- |
| Base movement speed | `FixedUpdate()`: `5 * Gaze.Value.Move` | 5 world units/second before gaze scaling. Change the `5`. Both characters share gameplay speed. |
| Jump height | `JumpHeight = 1.25f` | Desired ballistic height in world units. Velocity is derived using gravity. |
| Jump repeat guard | `JumpRpc()`: `0.25` seconds | Minimum interval; jumping also requires fresh input and a grounded controller. |
| Gravity | **Project Settings → Physics → Gravity** | Used every physics step and when calculating jump launch speed. Keep a negative Y gravity for this implementation. |
| Ground adhesion | `FixedUpdate()`: vertical `-2` | Keeps a grounded controller attached to the surface. |
| Mouse sensitivity | `Update()`: `delta.x/y * 0.12f` | Horizontal/vertical sensitivity; change both or tune separately. |
| Pitch limits | `Update()` and `SubmitRpc()`: `-80` to `80` | Update both local and server clamps together. |
| Field of view | GazeConfig → **Vertical Field Of View**, 60° | Server sends this to each player camera. Changing only the camera's Inspector FOV is overwritten. |
| Near clip plane | `Scripts/Gaze/GazeRules.cs`: `NearClip = 0.05f` | Applied to the player view at runtime. |
| Supported viewport aspect | `GazeRules.MinAspect = 0.25f`, `MaxAspect = 4` | Used by the camera and server validation. |
| Input send interval | `Update()`: `0.05` seconds | Approximately 20 input messages/second. |
| Input freshness | `FreshInput`: `0.25` seconds | Stale inputs stop active movement/work/gaze. |
| Input minimum receive interval | `SubmitRpc()`: `0.015` seconds | Server rate guard. |
| Position/orientation replication | **NetworkPlayer prefab → Network Transform** | Interpolation and replicated transform settings. Preserve server authority. |

### Physical size and terrain movement — Inspector

Open `Assets/_WhosWatching/Prefabs/Player/NetworkPlayer.prefab`, select its root **Character Controller**:

| Field | Current serialized value |
| --- | --- |
| Height | 2 |
| Radius | 0.4 |
| Center | (0, 0, 0) |
| Slope Limit | 45° |
| Step Offset | 0.3 |
| Skin Width | 0.08 |

`EyeAnchor`, `ChestAnchor`, `OwnerCamera`, and the `NetworkPlayer` **View / Visual Root / Devil Prefab / Ghost Prefab / Eye / Chest** references define camera position, aim origins and visuals. The root is the physical body center, not the feet.

If you resize the player, also review the **GazeBody** capsule and the fixed samples in `Scripts/Gaze/GazeVisibility.cs`. Those samples currently assume a 2 m tall, 0.8 m wide body. Increasing only the cosmetic mesh does not increase gaze visibility.

### Controls

Bindings are read directly from `Keyboard.current` / `Mouse.current` in `NetworkPlayer.Update()`:

| Action | Current input |
| --- | --- |
| Move | WASD |
| Look | Mouse delta |
| Jump | Space |
| Pick up own-team crate | F |
| Return carried crate | Q |
| Work / sabotage | Hold E |
| Punch / capture mouse | Left click |
| Release mouse / menu | Escape |
| Detailed / Essentials HUD | H |

Editing an Input Actions asset alone will not remap these controls. Change the script and update the control labels in the UI. `GameUI.PointerOverMenu()` protects menu clicks from recapturing the mouse; update its rectangle if you move/resize the pause menu.

## 3. Gaze buffs, debuffs and visibility

**Inspector:** `Assets/_WhosWatching/Settings/GazeConfig.asset`.

| Inspector field | Current value | Meaning |
| --- | --- | --- |
| Range | 15 | Maximum eye-to-visible-body-sample distance. |
| Vertical Field Of View | 60 | Shared vertical view angle; horizontal coverage follows aspect ratio. |
| Lock Seconds | 0.15 | Continuous visible time needed per observer/target pair. |
| Evaluations Per Second | 20 | Server gaze checks per second, clamped to 1–60. |
| Move Support | 0.35 | Adds 35% movement multiplier per allied observer. |
| Move Disruption | 0.25 | Subtracts 25% per enemy observer. |
| Work Support | 0.35 | Adds 35% work multiplier per allied observer. |
| Work Disruption | 0.25 | Subtracts 25% per enemy observer. |
| Minimum | 0.5 | Lowest final movement/work multiplier. |
| Maximum | 1.5 | Highest final movement/work multiplier. |
| Blockers | Default + GazeTarget (mask 513) | Layers included in visibility raycasts. |

The formula is:

```text
multiplier = clamp(1 + allies × support − enemies × disruption, Minimum, Maximum)
movement speed = base movement speed × movement multiplier
work progress per second = work multiplier / base action duration
```

Visibility is based on the camera viewport and unobstructed body samples, not just the crosshair. Every visible target can count; the data structure currently supports three outgoing targets per player. Walls and intervening gaze bodies can obstruct visibility.

**Code locations:** `GazeSystem.cs` handles registration, evaluation and applying config; `GazeVisibility.cs` handles body samples and obstruction; `GazeRules.cs` handles viewport math and multiplier calculation.

### Important: punch multipliers are separate

`NetworkPlayer.PunchMultiplier` currently calls `GazeRules.Multiplier(allies, enemies)` **without passing GazeConfig values**. Punches therefore use the defaults in `GazeRules.cs`: support **0.35**, disruption **0.25**, minimum **0.5**, maximum **1.5**.

Changing GazeConfig's movement/work values does not change punching. To tune punching independently, pass explicit values in `PunchMultiplier`, or change the default arguments of `GazeRules.Multiplier` after reviewing its other callers. There is currently no punch multiplier Inspector field.

## 4. Punches, knockback and respawn protection

**Code:** `Scripts/Player/NetworkPlayer.cs`.

| Setting | Current value / symbol |
| --- | --- |
| Punch cooldown | `PunchRpc()`: 0.55 seconds |
| Maximum reach | Initial `distance = 2.3f` |
| Forward aim requirement | Dot product at least `0.5f` (roughly a 60° half-angle cone) |
| Horizontal knockback speed | `9 * PunchMultiplier` |
| Vertical punch launch | `2.5f * PunchMultiplier`, subject to current vertical velocity |
| Knockback decay | `FixedUpdate()`: `18 * Time.fixedDeltaTime` |
| Respawn punch protection | `ServerRespawn()`: 1.25 seconds |
| Team immunity | `PunchRpc()` skips targets on the same team |
| Punch obstruction | World raycast excludes Player/GazeTarget layers 8 and 9 and ignores triggers |
| Hit outcome | Increments hit state, knocks target back, returns carried crate and resets work |

Punches do not currently have a health/damage system. They are displacement and disruption attacks. Changing sound or animation does not change hit detection.

## 5. Crates, placement, sabotage and checkpoints

**Inspector:** Arena → **TeamLobby → Arena Gameplay**.

| Field | Current value / use |
| --- | --- |
| Work Seconds | **3** seconds for placement at 1× workforce |
| Sabotage Seconds | **4** seconds for sabotage at 1× workforce |
| Death Height | **-8**; below this world Y, the server respawns the player |
| Spawn A / Spawn B | References to the team's spawn anchor transforms |
| Checkpoints | Five transform references: two per team, then middle objective |
| Teams | Matching ownership entries for Checkpoints: A, A, B, B, None |
| Final Checkpoint | **4**, a zero-based index into Checkpoints |
| Crate Homes | Six **world-space Vector3 positions**, not Transform references |
| Crate Teams | Matching entries: A, A, A, B, B, B |
| Crate Prefab | Existing `Prefabs/TeamReady/Crate_Small.prefab` visual |

Keep Work Seconds and Sabotage Seconds **greater than zero**. These fields have no automatic positive-value validator. Setting zero/negative values can break or trivialize progress.

### Sabotage is deliberately timed

Hold E while looking at a reachable **completed enemy checkpoint**. Neutral sabotage takes 4 seconds. With constant 1.5× workforce it takes about 2.67 seconds; at 0.5× it takes 8 seconds. Placement similarly takes 2–6 seconds with the current limits. If workforce changes during the action, the progress rate changes with it.

Releasing E, losing valid aim/range/line of sight, changing target, getting punched or respawning resets that player's incomplete work. Players do not pool their partial work into a shared progress bar. Work interruption and completion behavior live in `ArenaGameplay.FixedUpdate()` and `Release()`.

### Interaction settings — code

In `Scripts/Multiplayer/ArenaGameplay.cs`:

| Setting | Current location / value |
| --- | --- |
| Pickup/work reach | `Reach = 2.8f` |
| Interaction aim threshold | `CanReach()`: dot product at least 0.25 |
| Target sample offsets | Pickup: crate home + 0.35 Y; checkpoint: transform + 0.6 Y |
| Interaction obstruction | `CanReach()` raycasts; player layers excluded; target's own colliders allowed |
| One carried crate per player | `Carrying()` / `Pickup()` |
| Who can pick up | `Pickup()` requires matching CrateTeams entry |
| Carry position | `Update()`: forward 0.95, down 0.15 from player root |
| Loose crate bob/spin | `Update()`: sine amplitude 0.06; spin 25°/second |
| Visual crate size | `MakeVisuals()`: normalize largest bounds dimension to 0.7 |
| Checkpoint markers | `MakeVisuals()` / `Update()` control shape, color, height and pulse |

Crates are logical inventory items with visual replicas, not free Rigidbody props. Pickup removes a crate from its home visually and attaches its replica to the carrier. Placement consumes one; sabotage makes one available at the affected team's base. Q, a fall, a punch and a disconnect return carried supply. Decorative scene crates are disabled so they do not inflate the usable count.

For each team, available supply is its unfinished team checkpoints **plus one reserved for the final** before the match ends; carried crates are included. To alter checkpoint/crate counts, coordinate all four arrays, the final index, the supply rule, setup code and tests. Simply adding a decorative crate does not create a usable one.

### Final objective and match rules — code

`ArenaGameplay.cs`:

| Rule | Where to change |
| --- | --- |
| Regulation duration | `RoundSeconds = 300` |
| Maximum overtime duration | `OvertimeSeconds = 180` |
| Unlock final after all own checkpoints | `FinalUnlocked()` |
| Final placement wins immediately | Placement branch in `FixedUpdate()` |
| More team checkpoints at regulation expiry wins | `CompletedCount()` / `RegulationWinner()` / deadline branch |
| Equal regulation scores enter overtime | Deadline branch |
| Successful sabotage ends overtime | Sabotage branch in `FixedUpdate()` |
| Overtime expiry draws | Deadline branch |
| Reset round and refill all supply | `ResetRound()` / `SpawnEveryone()` |

Current overtime behavior: a successful enemy-checkpoint sabotage awards the saboteur's team the win; final placement also wins. Ordinary placement alone does not end overtime. Match state is server-owned. These rules and durations are **not** editable through the Inspector yet.

## 6. Bases, fall recovery and level editing

- Move/rotate **TeamLobby → Spawn_A / Spawn_B** in the scene. Rotation controls facing. `ArenaGameplay.SpawnPosition()` adds a ±0.7 unit offset along the anchor's right vector for teammates.
- Keep spawn anchors high enough to fit the full CharacterController above a solid surface. Current anchor Y is 1.2.
- Select **TeamLobby → Deathbox → Box Collider** to edit the trigger's position and size. The authored trigger is centered around Y -7, size 160 × 2 × 160.
- **Arena Gameplay → Death Height** provides a separate fallback even outside the trigger footprint. Coordinate it with the deathbox height.
- `ArenaDeathbox.OnTriggerEnter()` and `ArenaGameplay.Respawn()` route falls into `NetworkPlayer.ServerRespawn()`.
- Checkpoint references follow their scene transforms. Crate Homes are stored world coordinates and must be updated if you move the base or arena.
- Use `Assets/_WhosWatching/Prefabs/TeamReady/TeamA` and `TeamB` for team-owned environment pieces. Edit a shared base prefab only when all its variants should inherit the change.
- Keep colliders on walkable platforms/walls. Decorative meshes alone are not solid.

**Setup warning:** **Who's Watching → Install Arena gameplay** (`Editor/ArenaSetup.cs`) derives references/positions from expected object names and tint ownership, rewrites spawn/home configuration, disables decorative crates and saves the scene. It is a setup tool, not a harmless refresh button after manual layout tuning. Review/version your scene before rerunning it.

## 7. Sounds and music — drag-and-drop Inspector setup

Select **Arena Audio** in Arena's Hierarchy, then use **Arena Audio (Script)**. Import your clips into Assets, drag them into these slots, save Arena, and rebuild to ship them. All clips are optional; currently no sound files are bundled and empty slots remain silent.

| Slot | When it plays |
| --- | --- |
| Background Music | Looped 2D music |
| Devil Walking / Ghost Walking | Grounded movement steps, cadence scaled by movement speed |
| Devil Jump / Ghost Jump | Confirmed jump; falling alone does not trigger it |
| Landing | Transition from airborne to grounded, shared by both characters |
| Buff Effect | Local player gains an allied gaze effect |
| Debuff Effect | Local player gains an enemy gaze effect |
| Punch | Confirmed punch swing, including a miss |
| Punch Impact | Confirmed hit on a target |
| Checkpoint Complete | Successful placement |
| Checkpoint Sabotage | Completed sabotage, not the start of work |
| Crate Pickup / Crate Return | Successful pickup or explicit carried-crate release |
| Respawn | Player respawns during active gameplay |
| Round Start | Entering a round |
| Overtime Start | Entering overtime |
| Victory / Defeat / Draw | Local player's round outcome |

| Setting | Default | Use |
| --- | --- | --- |
| Music In Lobby | On | Whether music also plays outside rounds |
| Music Volume | 0.35 | Music output gain, 0–1 |
| Effects Volume | 0.8 | Effects master gain, 0–1 |
| Footstep Volume | 0.5 | Additional gain multiplied by Effects Volume |
| Footstep Interval | 0.42 seconds | Cadence at nominal movement speed; use single-step clips |
| Audible Distance | 24 | Spatial effect maximum distance; runtime minimum is 3 |
| Voice Limit | 16 | Bounded reusable simultaneous effects; supported range 4–32 |
| Music Mixer / Effects Mixer | Unassigned | Optional AudioMixerGroup output routing |

Music, announcements, local character effects and local gaze cues are 2D. Other character effects and objective interactions are spatial. `ArenaAudio.Play()` uses linear rolloff, minimum distance 2, Doppler 0, pitch 1. Change those code values for different spatial behavior. `Update()` defines triggers/cadence; `PlayEvent()` maps authoritative objective events to clips.

Clip import settings are on the **AudioClip asset Inspector**: trim/normalize your source recordings as needed; tune load type, compression, quality, mono conversion and preload there. Mono suits positional effects; music can stay stereo. Use **Window → Audio → Audio Mixer** if you want group attenuation/effects, then assign its groups above.

**Who's Watching → Install Arena audio slots** restores a missing component while preserving existing assignments. There is no in-game audio settings menu yet; volume sliders are developer Inspector controls.

## 8. Characters, animations, colors and materials

| Editable item | Location |
| --- | --- |
| Devil/Ghost visual prefabs | `Assets/_WhosWatching/Prefabs/Character/Devil.prefab` / `Ghost.prefab` |
| Which prefab each choice uses | NetworkPlayer prefab → **Devil Prefab / Ghost Prefab** |
| Motion Amount | Character prefab → **Character Presentation**, range 0–2; soft bob/sway/lean strength |
| Floating behavior | Same component → **Floating** |
| Animator/controller | Same component's **Animator**, and the character Animator component |
| Authored animation assets | `Assets/_WhosWatching/Animations/` |
| Procedural movement/landing | `CharacterPresentation.SampleMotion()` / `ApplyJumpShape()` |
| Punch, carrying, work, hit, respawn poses | `CharacterPresentation.ApplyActions()` |
| First-person hand appearance and motion | `Scripts/Player/ArenaHands.cs` |
| Character visual offset/scale | `NetworkPlayer.RefreshCharacter()` (local Y -1 and prefab scale/rotation) |
| Six named team palettes | `Scripts/Multiplayer/TeamPalette.cs`: `Names`, `Colors`, `Neutral` |
| Environment/character tint slots | **Team Tint** components: Team, renderer/material index, Strength, Base Color, Emissive |
| Prop materials/geometry | Shared TeamReady base prefabs and their referenced material/model assets |

The first-person hands and action poses are procedural. There are no separate authored punch/sabotage animation clips to swap. Changing bones named `Arm.L` / `Arm.R` requires updating the procedural arm lookup. Cosmetic scaling does not change the CharacterController.

Team A/B ownership and team colors are separate. Changing a palette color must not change logical Team IDs. `TeamTint` respects nested ownership; avoid retinting unrelated materials such as eyes/wood/metal by adding broad renderer bindings.

## 9. Lobby, names and HUD customization

### Name entry and nameplates

- Lobby entry: `NetworkGameManager.DrawCharacterChoice()`; text field limit is 16.
- Server validation: `NetworkPlayer.CleanName()` and `SetDisplayNameRpc()`; allowed characters are letters/digits, spaces, underscore and hyphen. Empty/invalid-only input becomes `Player`.
- Network storage: `FixedString64Bytes DisplayName`. If increasing length, review UTF-8 capacity, server limit, text-field limit and label widths together.
- Names may only be changed in the lobby. Duplicate names are allowed.
- Nameplate style/position: `NetworkPlayer.DrawNameplate()`. Current offset is +1.4 Y, max distance 28, panel size 190 × 29 virtual pixels. World geometry blocks visibility. Only other players' nameplates are drawn.
- Local saved preference: `WW.PlayerName` in PlayerPrefs.

### Detailed and Essentials HUD

Players can switch in the lobby/pause menu or press H. **Essentials keeps nameplates, aiming dot, team scores and timer**. It hides detailed multipliers, work prompts/progress and control help. Pause/results still appear. Saved preference: `WW.DetailedHUD` (1 detailed, 0 essentials).

| Visual setting | Code location |
| --- | --- |
| Shared palette | `GameUI.Ink`, `Cream`, `Gold`, `Mint` |
| Font sizes/styles, button/text-field appearance | `GameUI.Ensure()` |
| Rounded backgrounds, shadows | `GameUI.Fill()` / `Card()` |
| Resolution scaling | `GameUI.Scale`, `Width`, `Height`; reference size 1280 × 720 |
| Default HUD preference and persistence | `GameUI.Details` |
| Reticle | `GameUI.Reticle()` |
| Lobby and pause menu layout/text | `NetworkGameManager.OnGUI()` |
| Character choice UI | `NetworkGameManager.DrawCharacterChoice()` |
| Detailed lower HUD/work bar | `NetworkPlayer.OnGUI()` |
| Scores/timer/results layout/text | `ArenaGameplay.OnGUI()` |

These screens use **IMGUI**, drawn from code. There is no Canvas/prefab UI hierarchy to drag around in the scene. Layout edits mean changing Rects/GUILayout calls in those methods. If widening names or translating text, check wrapping at multiple window sizes.

### Lobby/session rules

`TeamLobby.cs` controls team switching, leader/color selection, readiness, start requirements and returning to lobby. Capacity is two per team in `HasTeamSpace()`; total capacity is four in `SessionRules.Capacity`. The start requires players on both teams and everyone ready. Switching teams clears readiness; changing character clears that player's readiness; color changes clear that team's readiness.

Increasing capacity is a coordinated code change: update team limits, spawn spacing, gaze target storage (currently three outgoing IDs), UI and tests. Do not just increase `Capacity`.

Transport port defaults to 7777 in `NetworkGameManager`; lobby address/port are editable fields. Connection timeout is 10 seconds; reservations are 10 seconds. The scene **Networking → Network Manager** configuration has Tick Rate 30 and Client Connection Buffer Timeout 10. Matching builds use `SessionRules.Protocol`; change the token when altering network schemas. Runtime transport configuration is applied by `StartSession()`, so Inspector transport values may be overwritten.

## 10. Unity project and build settings

| Setting | Where / caveat |
| --- | --- |
| Product name, version, icon, company, resolution/fullscreen defaults | **Edit → Project Settings → Player** (`ProjectSettings/ProjectSettings.asset`) |
| Quality levels, shadows, antialiasing, VSync | **Project Settings → Quality** and the assigned URP pipeline asset |
| Render pipeline assignment/render scale/HDR/shadow options | **Project Settings → Graphics / Quality** and URP assets under `Assets/Settings/` |
| Gravity and layer collision matrix | **Project Settings → Physics** |
| Layer names | **Project Settings → Tags and Layers**; Player uses 8, GazeTarget uses 9 |
| Fixed timestep | `NetworkGameManager.Awake()` assigns `Time.fixedDeltaTime = 1f / 30`; changing only Project Settings → Time is overridden |
| Run In Background | `NetworkGameManager.Awake()` forces true |
| Audio project defaults | **Project Settings → Audio**; per-game clips/gains are on Arena Audio |
| Package versions/modules | `Packages/manifest.json` and `packages-lock.json` |
| Included scene | **File → Build Profiles → Scene List** / `ProjectSettings/EditorBuildSettings.asset` |
| Scripted build target, output and Development flag | `Assets/_WhosWatching/Editor/ProjectSetup.cs`, `Build()` |

`ProjectSetup.Build()` explicitly builds Arena to `Builds/Windows/WhosWatching.exe` for Windows64 with Development enabled. Changing only the editor scene list will not change this method's explicit scene path. Remove/change `BuildOptions.Development` there for a release build. Test probes are compiled only for Editor/Development builds.

Do not rerun **Create initial LAN scene** to update the current game: that setup is for the old initial scene. Other generation/migration tools under `Assets/_WhosWatching/Editor/` can rewrite assets; inspect their paths and behavior before running them.

## 11. Useful tuning recipes

### Slower sabotage without changing movement

Select **TeamLobby → Arena Gameplay → Sabotage Seconds**, e.g. change 4 to 6. With existing work limits, sabotage then takes 4–12 seconds. Save Arena and rebuild. No need to change gaze or walking speed.

### Faster movement but unchanged checkpoint timers

Change the `5` movement coefficient in `NetworkPlayer.FixedUpdate()`. Leave GazeConfig Work Support/Disruption and Work/Sabotage Seconds unchanged. If animation/footstep cadence feels wrong at the new nominal speed, review the `/5` normalization in presentation/audio code.

### Stronger allied buffs, gentler enemy disruption

Edit GazeConfig Move Support / Work Support and Move Disruption / Work Disruption. Raise Maximum if larger bonuses should actually be reachable. Punch bonuses remain separate unless you change `PunchMultiplier`.

### Longer rounds

Change `RoundSeconds` and/or `OvertimeSeconds` in ArenaGameplay.cs. Timer rendering derives from the server deadline. Update explanatory lobby copy, README and timing tests that refer to five/three minutes.

### Add sound effects

Import clips, select Arena Audio, assign the corresponding slots, adjust Music/Effects/Footstep Volume, save scene and rebuild. Test with a second player to hear distance attenuation and local-only gaze feedback.

## 12. Validation and delivery

Close the project in the interactive Unity editor before batch commands. From the repository root, set `$unityEditor` to your installed Unity executable:

```powershell
./Tools/Unity.ps1 -UnityEditor $unityEditor -Action Test
./Tools/Unity.ps1 -UnityEditor $unityEditor -Action Build
./Tools/ArenaSmoke.ps1
```

Relevant tests live under `Assets/_WhosWatching/Tests/EditMode/`: Arena configuration/rules, audio setup, name sanitation, gaze, appearance, collisions, team variants and session validation. `ArenaProbe.cs` plus `Tools/ArenaSmoke.ps1` checks actual two-peer gameplay. Test logs/build logs live in ignored `Logs/`.

The older Jump/Appearance/Gaze probe tools remain available, but some use assumptions from the earlier scene; use the current Arena test as the primary submission flow. Do not infer a new mechanic works solely because an older probe passed.

After tuning: confirm positive work durations; test both teams and both characters; test one buff/debuff; place/sabotage a checkpoint; fall while carrying; verify final/timeout/overtime rules; check Detailed and Essentials modes and long names. Confirm sounds after assigning real clips—empty-slot tests cannot establish audible balance.

Distribute the **whole `Builds/Windows` folder**, not the executable alone. Rebuild after changing scene/prefab/audio assignments so the output includes them. Current validation and remaining manual checks are tracked in [progression.md](progression.md); player controls and quick audio setup are in [Docs/ArenaGameplay.md](Docs/ArenaGameplay.md).
