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
