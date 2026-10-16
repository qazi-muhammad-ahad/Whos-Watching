# Implementation decisions

- 2026-10-16: User approved Unity 6000.3.23f1 already installed, in place of requested 6000.3.24f1. The later patch has not been tested. Keep the team on one version.
- Repository root is the Unity project. URP template settings retained; Main is the development startup scene.
- NGO 2.13.1 / Transport 2.7.4 / URP 17.3.0 / Input System 1.20.0 are pinned and resolved. Test Framework 1.4.2 and uGUI 2.0.0 remain pinned. Package lock records transitive dependencies.
- First deliverable is a development LAN harness: four slots, protocol validation, host/client transport, server movement and stale-input expiration. Full game phases, teams, gaze and objectives remain pending.
- Temporary IMGUI and direct Input System device polling keep this harness small. Replace with the planned action asset and UI presenters before the game UI milestone.
- Movement in the harness lobby is intentional for connectivity tests. Production phase gating and round IDs must replace this before match implementation. The admission adapter currently always reports lobby; pure rejection policy already accepts a phase gate.
- Leave/retry reuses the same stopped NetworkManager and clears reservations; it does not reload Main. Lifecycle acceptance needs further tests before claiming NET-10. Never create a second manager during retry.
- Only the server moves CharacterController; owner input uses owner-permission unreliable RPCs with sender, finite-value, range, rate and sequence validation. No prediction. Camera rotation is local.
- Artists replace VisualRoot children, preserving network root, colliders and anchors. No art dependency blocks network validation.
- No Relay, services accounts, host migration, combat or scope expansion in this baseline.

## Gaze milestone decisions — 2026-10-16

- User confirmed the previous multiplayer baseline worked. Protocol advanced to `whos-watching/lan/2-gaze` because RPC/state changed; rebuild all peers together.
- GazeConfig.asset controls range/cone/acquisition/evaluation rate and separate movement/work coefficients. GazeSystem runs on the host and aggregates a full evaluation before writing GazeState.
- RaycastAll selects the nearest non-source hit. Default/world and Player layers participate; triggers are ignored; the first hit must belong to the candidate. Cosmetic visuals must remain collider-free or outside the blocker mask.
- Teams auto-balance on connection. Host-only development buttons permit unbalanced ally testing. Production team selection/readiness remains pending.
- Input carries enabled/held-work flags; menu/focus loss suppresses intent and stale input expires. Movement consumes GazeState.Move; the five-second work harness consumes GazeState.Work.
- Gaze runs in the development arena; production phase and round gating are still required with match lifecycle implementation.
- GazeProbe only activates explicitly in Editor/development builds with -wwGaze. Test teleports and temporary obstacles never run in ordinary play.

## Screen-wide gaze revision — 2026-10-16

User chose ALL visible players simultaneously and requested stacking amounts/limits editable in Unity UI. GazeConfig Inspector provides labeled contributions and Minimum/Maximum, with old defaults retained. Sum positive and negative contributions before applying bounds. Camera vertical FOV comes from the host; bounded aspect comes from owner input and matches local camera projection (0.25–4). Protocol changed to 3-screen-gaze.

Fixed body samples approximate screen visibility; both viewport and LOS must pass for the same sample. Dedicated GazeBody collider on layer 9 / GazeTarget matches the standard body; movement uses the separate CharacterController on Player layer. All GazeTarget physical collisions are disabled, while explicit gaze raycasts still hit it. This fixes controller-query shrinkage at the visible head. Preserve that collider when replacing art.

One observer can support/disrupt multiple targets; independent acquisition timers and per-target incoming counts prevent duplicated sample contributions. The fixed three outgoing ID slots match the four-player cap; expanding player count requires a protocol/state update. A client's claimed aspect is bounded, not proof of its physical monitor; this remains the trusted-host LAN prototype.
