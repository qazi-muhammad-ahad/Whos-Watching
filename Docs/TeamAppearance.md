# Characters and team appearance

## Team variants and motion follow-up

For map building, use `Prefabs/TeamReady/TeamA/*_TeamA.prefab` and `Prefabs/TeamReady/TeamB/*_TeamB.prefab`. Each folder has 27 linked Prefab Variants: all 26 imported environment props plus the checkpoint marker. Edit shared geometry in the common base under `Prefabs/TeamReady`; both variants inherit it. Nest A and B variants inside a larger wall/map prefab to retain independent accent ownership. Editor previews show default team colors outside play and selected colors during play. This fixes the earlier workflow where most base wrappers defaulted to Team A.

The variant generator does not replace or move existing Main objects. For existing placements, set Team Tint > Team individually; for new placements, drag the named variant. **Who's Watching > Generate Team A and B environment variants** regenerates these assets without opening Main. The full appearance migration now preserves existing Main team bindings.

Lobby Team A/B buttons allow moves to a team with space (maximum two players per team). Server validation rejects full teams, invalid IDs and requests during play. A successful change clears everyone's readiness and recalculates leaders. If both teams are full, slots must open before a player can move; this does not implement a two-player swap negotiation.

CharacterPresentation adds visual-only bob, lateral sway, lean, roll and subtle squash/stretch. Speed ramps smoothly on starts/stops; Ghost has a gentle idle hover, and added motion fades during buff dances. Tune **Motion Amount** on either character prefab (0 disables the extra layer; 1 is default). The imported axis rotation is respected; the gameplay root, colliders and camera stay steady. Ghost retains its uniformly dark body/arms and Devil retains lighter accents.

Current protocol: `whos-watching/lan/6-jump`; rebuild every peer together. The extended AppearanceSmoke covers fourteen stages, including team-switch success, full-team/invalid rejection, ready clearing and leadership changes.

## Jump and physical environment

Press **Space** while the mouse is captured to jump. The server accepts the owner's request only during play with fresh input and a grounded controller. Jump height is 1.25 m; there is no double jump or gaze-based jump-height bonus. Gravity, ceiling hits and landing are server-authoritative. Replicated Airborne state drives each character's raised-arm/tucked-leg Jump pose; a visual stretch and brief landing squash soften the transition. Ghost jumps with the same physical rules as Devil.

Environment bases now contain physical colliders, inherited by both team variants. Crates use one BoxCollider; ramps, steps, walls, corner pieces, platforms and structural detail meshes use static non-convex MeshColliders to preserve slopes and openings. Cloth, emblems, accent badges and tiny decorations are excluded. The existing Main placements also have colliders. These are static map props: adding Rigidbody-driven movable crates or doors requires separate physics setup.

**Who's Watching > Install jump animations and environment collision** maintains the Jump clips/controllers, base colliders and Main scene colliders. Existing colliders are retained. Full appearance regeneration also retains jump/collision support. `Tools/JumpSmoke.ps1` checks lobby rejection, ordinary jumps, airborne repeat rejection, landing, ceiling limits and remote jump animation in two real network processes.

## Play flow

Choose Devil or Ghost before hosting/joining, or change the selection in the lobby. Teams automatically balance on connection. The connected player with the lowest client ID on each team is its leader, indicated by a star; leadership passes to the next remaining teammate after a disconnect.

Each leader chooses one of six named colors. The opponent's chosen color is disabled, and the server rejects duplicate or out-of-range choices. Character/ready/color RPCs require ownership of the requesting player. Only the host can start or return to the lobby. Gameplay input and gaze are gated until everyone is ready and both teams have at least one player. A successful character change clears that player's ready state; a changed team color clears the whole team's ready state. Joining during play is rejected.

This is the pre-game appearance flow for the existing gaze arena. It does not add objective rounds, scoring, a countdown, selected 1v1/2v2 modes, or checkpoint capture logic. The existing explicit headless movement/gaze probes bypass the lobby; the appearance acceptance probe exercises it.

## Colors and ownership

- `Scripts/Multiplayer/TeamPalette.cs`: six preset main colors and display names. Change the palette here and rebuild every peer together.
- `Scripts/Multiplayer/TeamLobby.cs`: server-owned replicated palette indices and play gate. Team IDs A/B remain the gameplay identity.
- `Scripts/Player/TeamTint.cs`: per-renderer, per-material-slot property blocks. Shared materials are never mutated.
- **Ghost:** the entire body and both arms use the main, darker team color. Eyes and face retain their neutral materials.
- **Devil:** body uses the main, darker team color; hands/arms, feet and blush use a lighter tint (45% white). Eyes, horns and facial details stay neutral.
- Props recolor existing painted trim, crate strips, team fabric and station accents. Concrete/neutral face materials stay intact. An all-concrete corner wall receives a separate accent badge. West/East boundary walls have team stripes.

Use `Prefabs/TeamReady/*.prefab` for new props: 26 wrappers for the imported environment FBXs plus a checkpoint marker prefab. Select a placed prefab and set **Team Tint > Team** to A, B or None. None uses neutral gray. `Slots` specify exactly which renderer and material index should receive color. Strength 1 uses the main team color; lower strength mixes with Base Color. Existing Main props also have explicit bindings as scene overrides.

Initial Main ownership preserves explicit Blue/Red asset identity as A/B. Other imported props use the side of their existing placement (X <= 0 is A; X > 0 is B). This assignment is saved in the scene, not recomputed at runtime. The current Main layout is the user's imported-asset showcase; arrange gameplay lanes and change ownership in the Inspector as the arena design develops. The checkpoint prefab is a visual marker, ready to connect to future checkpoint logic.

Changing a team's replicated palette index updates all bound props and remote players, including existing state when a client joins the lobby. To change an object's ownership during later objective gameplay, replicate that gameplay ownership and set its TeamTint.Team on each peer; TeamTint is a presentation component, not an ownership network authority.

## Animation and player integration

`Prefabs/Character/Devil.prefab` uses copied clips from `Blue_Female_Puff.fbx`; `Ghost.prefab` uses `Blue_Male_Wisp_Cap.fbx`. Copies live under `Animations/`, with a controller for each character. Generic rig paths are preserved. The source FBXs and their embedded clips are unchanged.

Devil has a held idle pose and walk loop. Ghost floats at idle and uses its float-wave loop while moving. Both play the source buff dance when allied support first becomes active. The server replicates actual horizontal speed to drive remote locomotion. Root motion is disabled; copied curves omit authoring root transforms so network movement remains authoritative. Ghost's rig/mesh authoring offset is normalized together.

The common NetworkPlayer prefab instantiates only the chosen visual under VisualRoot. The owner uses the existing first-person camera and hides its own visual. Character choice never alters controller dimensions, gaze body, eye/chest anchors or network identity. New art remains collider-free.

## Maintenance and verification

The migration has already been applied. **Who's Watching > Install character and team appearance** is an explicit maintenance command for regenerating clips/controllers, bindings and wrappers after art changes. It preserves existing Main team bindings, layout and source FBXs. `ProjectSetup.Create` remains initial-creation only.

- EditMode: palette/character validation, matching animation paths and bone motion, root stability/scale, Ghost arm/body equality, lighter Devil accents, shared-material isolation, and all wrapper bindings.
- `Tools/AppearanceSmoke.ps1`: four real local processes check selections, invalid/duplicate/unauthorized changes, ready/start gating, locked play choices, lobby return, character swaps, leader disconnect/succession and actual renderer property blocks.
- `AppearancePreview.Render`: renders actual character and prop assets to the ignored `Logs/AppearancePreview.png` for visual inspection.

Protocol is `whos-watching/lan/6-jump`; rebuild all peers together.
