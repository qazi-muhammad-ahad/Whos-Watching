# Test log

## Baseline — 2026-10-16

Revision: working tree based on `c85c8b4`; implementation is uncommitted. Unity 6000.3.23f1 (09d2ecc7fb28), Windows x64 development build, Mono backend. Packages: NGO 2.13.1, Transport 2.7.4, URP 17.3.0, Input 1.20.0, Test Framework 1.4.2. Full resolved versions: Packages/packages-lock.json.

| Check | Procedure / expected | Actual |
| --- | --- | --- |
| Package import | Resolve manifest, compile template | PASS; Unity exited successfully and generated lock/version metadata |
| Scene generation | Compile project scripts; generate saved Main, prefab/list, URP material, floor/cover/walls | PASS; setup exited 0; Main enabled in build settings |
| EditMode policies | Run SessionRulesTests | PASS: 13 tests, 0 failed, 0 skipped |
| Windows build | ProjectSetup.Build, then incremental rebuild after final RPC/test adjustments | PASS; executable and associated runtime/data generated |
| Two-player loopback | Host and client, UDP 7777, independent owner test input | PASS; both report two network objects, replicated movement >1 metre, stable position after input stream ends |
| Bad protocol, two-player | Additional client sends deliberately invalid bytes | PASS; no player spawned, incompatible-build reason received |
| Four-player loopback | Host plus three separate client processes | PASS; all four observe four network objects and pass movement/stale-stop assertions |
| Fifth join | Additional valid client after four admitted | PASS; rejected with Session full, no local player spawned |
| Bad protocol, four-player | Additional malformed-token client | PASS; incompatible-build reason received |
| Runtime errors | Scan smoke logs for exceptions / error markers | None found |
| Repository hygiene | Asset .meta presence; cache/build/log ignore paths | PASS; no missing file metadata; Library/Logs/Builds/UserSettings excluded |

Process smoke tests run headless on **one PC**, not four physical machines. Input is synthesized through the owner RPC path; it does not test keyboard/cursor usability. Movement test sends forward for one second, stops packets entirely, then verifies settled z position remains within 0.05 m and away from boundary walls. This verifies stale stopping behavior, not an exact 250 ms timing measurement. Each process exits with failure code if assertions fail. Wrong-token/fifth-client probes require a matching rejection reason, not merely failure to connect.

Local evidence (ignored): Logs/Import.log, Setup.log, EditMode.xml, EditMode.log, Build.log, Smoke2-*.log, Smoke4-*.log. Share this sanitized summary rather than raw logs containing machine paths. The local build is Builds/Windows/WhosWatching.exe with its associated folders; it is not committed or distributed. Rebuild from source using Tools/Unity.ps1. The executable launcher alone does not identify all game data; use the source revision plus complete build folder for future release identification.

## Not yet verified

- Separate-PC LAN, firewall permissions, hardware/input responsiveness, rendered URP visual quality.
- Cancel/timeout/Leave/rehost/rejoin and host-loss recovery through interactive UI. Controls exist but full lifecycle acceptance is pending.
- Exactly timed stale expiration under injected loss/latency/reordering; synthetic owner spoofing and excessive-rate integration attacks.
- Readiness/teams/round IDs, gaze effects, objectives, results, rematch and production phase gating: not implemented.
- 6000.3.24f1 upgrade; clean clone by another developer; release archive acceptance.
- No G0/G1 or later full gate claimed: team agreement and real LAN evidence remain outstanding.

## Reproduction

Read README.md and run Tools/Unity.ps1 -Action Test, then -Action Build with the pinned Unity executable. Run Tools/Smoke.ps1 -Players 2 and -Players 4 sequentially so hosts do not contend for port 7777. Close other game hosts first. Normal runs never activate the smoke probe; its flags only exist in Editor/development builds.

Future entry fields: date, source revision/build, versions, topology/host, test ID, steps, expected result, actual result, pass/fail, defects, evidence path and untested limitations.

## Gaze milestone — 2026-10-16

Working tree based on bb534cf, same Unity/package pins as baseline; protocol 2-gaze. User reported the prior multiplayer baseline worked (hardware/topology not specified).

- PASS: scene upgrade and compilation, 24/24 EditMode tests (13 session + 11 gaze). Gaze cases cover additive bounds, angle/range edges, lock acquisition/release/switch, zero target ID, deterministic tie-breaks.
- PASS: final Windows development build, no C# errors/warnings.
- PASS: Tools/GazeSmoke.ps1 -Players 2 and -Players 4. All six participating process runs reported stages=1023, work=True, movement=3, and successful exit.
- Ten replicated stages: neutral, ally support, enemy disruption, solid wall, turning away, input disabled/menu, expired observer input, mixed teams, supported movement, disrupted movement. Four-peer mixed case is one ally plus two enemies (0.85x); all-ally/all-enemy cases exercise 1.50x/0.50x clamps.
- Measured motion: after acquisition, target moves for about 0.5 seconds. Each peer asserts distance against 5 m/s times duration times modifier, tolerance 0.65 m for scheduling/interpolation. This proves real movement changes, not precise network performance timing.
- Work test verifies server progress during support exceeds the expected lower bound. Exact five-second completion timings and production station validation remain untested.
- Host verifies effects return to neutral after all observer clients disconnect. No runtime exception/error markers in final gaze logs.
- Evidence (ignored local files): Logs/Test.log, EditMode.xml, Build.log, Gaze2-0/1.log, Gaze4-0/1/2/3.log. Headless, same-PC processes with synthetic input through owner RPCs.
- Still pending: manual HUD/cursor/readability playtest for the new gaze build; real-LAN gaze testing; mutual gaze and thin-corner/intervening-body/cosmetic integration scenarios; full production lifecycle, rounds and objectives. The raycaster supports intervening-body blocking, but that specific scenario was not automated in this run.

## Screen-wide gaze revision — 2026-10-16

Working tree based on bb534cf; Unity 6000.3.23f1, protocol 3-screen-gaze. This entry supersedes historical single-cone behavior and the earlier unimplemented-gaze notes.

- Initial four-peer screen run failed one of 18 stages: head above cover. Investigation reproduced CharacterController ray-query shrinkage. Added a dedicated GazeBody capsule on GazeTarget layer 9, excluded from physical collisions, and updated physics tests to use both the actual controller and separate gaze collider.
- PASS after correction: 30/30 EditMode tests, zero failed. Covers rectangular projection, aspect validation, behind-camera/range/edge cases, partial body and obstruction, cosmetic filtering, independent pair acquisition/removal, multi-target state membership/counts and additive configurable bounds.
- PASS: final Windows development build.
- PASS: Tools/ScreenGazeSmoke.ps1 -Players 4, all four peers report stages=262143/262143 (18 stages). Covers off-center support/disruption, mixed incoming effects, one observer affecting all three targets, partial screen edge, fully offscreen, portrait aspect, independent target occlusion, menu/stale input, looking behind, head above low cover, full cover, intervening body, invalid aspect and out-of-range rejection. Host disconnect cleanup reports True.
- PASS: Tools/GazeSmoke.ps1 -Players 2 on the final build; both peers report stages=1023, work=True, movement=3. Confirms replicated effects still drive actual movement and server work. The four-player version of this older suite was not rerun for protocol 3.
- No exception/error markers in these final process runs. Asset files have corresponding metadata.
- Local ignored evidence: Logs/EditMode.xml, Test.log, Build.log, ScreenGaze4-0/1/2/3.log, Gaze2-0/1.log. The failed initial screen logs were overwritten by the final rerun; its failure and remedy are recorded above.

These are headless separate processes on one PC, with synthetic input through owner RPCs. Manual camera/HUD rendering and separate-PC LAN acceptance for this revision remain pending. Fixed body samples approximate visibility rather than testing every rendered pixel; extremely thin slivers can miss. Exact work completion timing, network impairment, production rounds/objectives and full lifecycle acceptance remain outside this validation. The user reported the prior gaze worked; no user acceptance of this new build is claimed.
