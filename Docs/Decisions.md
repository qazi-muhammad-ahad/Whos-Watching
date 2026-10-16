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
