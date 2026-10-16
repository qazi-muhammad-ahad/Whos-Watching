# Who's Watching?

Unity 6.3 URP multiplayer prototype. The current deliverable is a **development LAN movement harness**, with primitive art. Gaze, team readiness, objectives and full matches are the next milestones.

## Open

Install **Unity 6000.3.23f1** with Windows build support. Add this repository root in Unity Hub, allow package import, and open `Assets/_WhosWatching/Scenes/Main.unity`. Press Play. The project pins NGO 2.13.1, Transport 2.7.4, URP 17.3.0 and Input System 1.20.0. Commit the package lock and all asset metadata with changes.

## Try multiplayer

- Run Host LAN in one instance and Join LAN in another. Use `127.0.0.1` on the same PC or the host's local IPv4 on the same LAN. UDP port defaults to 7777; host binds all interfaces.
- Click the arena to the right of the menu to capture the mouse. WASD moves; mouse looks; Escape releases the mouse; Leave ends the session.
- Maximum four players including host. Movement is enabled in this test lobby. There are no playable teams, gaze effects or objective rounds yet.
- A separate-PC test may require allowing the executable on the intended private network. Direct Internet connectivity is not promised.

## Build and test

From PowerShell at the repository root, set `$unityEditor` to the installed `Unity.exe`, then run:

```powershell
./Tools/Unity.ps1 -UnityEditor $unityEditor -Action Test
./Tools/Unity.ps1 -UnityEditor $unityEditor -Action Build
./Tools/Smoke.ps1 -Players 2
./Tools/Smoke.ps1 -Players 4
```

Close the project in Unity before batch commands. The Windows development executable is `Builds/Windows/WhosWatching.exe`; distribute its whole folder, not only the executable. Builds and local logs are ignored by Git. Smoke tests are headless, same-PC process tests; they do not certify graphics or separate-PC LAN.

Read [Multiplayer Setup.md](Multiplayer%20Setup.md) for the implementation roadmap, [Docs/TestLog.md](Docs/TestLog.md) for measured results, and [progression.md](progression.md) for exact next steps. [Plan.md](Plan.md) and [Todo.md](Todo.md) describe the full intended game, not the current implemented scope.
