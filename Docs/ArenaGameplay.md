# Arena submission controls and rules

Open `Assets/_WhosWatching/Scenes/Arena.unity`. Windows builds use this scene.
All players must use the same `lan/9-arena-ui` build. Host LAN, join the host's IPv4,
choose teams/characters, ready everyone, then the host starts the game.

- WASD moves; mouse looks after clicking the arena; Space jumps; Esc releases the mouse.
- F picks up a nearby crate belonging to your team. Q returns your carried crate to its base.
- Hold E while looking at your unfinished checkpoint to place a carried crate (3 seconds at neutral workforce).
- Hold E at an enemy completed checkpoint to sabotage it (4 seconds at neutral workforce).
- Left click punches an enemy in front of you, knocking them back and returning their carried crate. Cooldown: 0.55 seconds. Gaze support strengthens punches; disruption weakens them.
- Gaze workforce speeds up/slows down placement and sabotage. Releasing E, leaving range, losing sight, changing target, or getting hit resets unfinished work.
- Falling into the deathbox returns you to your base and returns your crate. Respawn grants 1.25 seconds of punch protection.

Each team starts with three usable crates: two for its team checkpoints and one for the shared gold middle objective. Carried crates count toward supply. A completed checkpoint consumes one crate; sabotage restores one at the affected team's base. Disconnects return carried crates. The existing decorative crates are disabled.

Finish both team checkpoints to unlock the middle checkpoint for your team. Delivering there wins immediately. At five minutes, the team with more completed checkpoints wins. A tie starts three minutes of overtime. In overtime, completing the middle objective or sabotaging an enemy completed checkpoint wins immediately. Ordinary placement alone does not end overtime. If overtime expires, the match is a draw. The host can return everyone to the lobby for a fresh round.

Visuals include existing walk/jump/landing animations plus procedural punch, work, carrying, hit and respawn poses, owner hands, floating supply crates and checkpoint status pulses. No new authored animation clips are required.

Validation commands: `Tools/Unity.ps1 -UnityEditor <editor-path> -Action Test`, then `-Action Build`, then `Tools/ArenaSmoke.ps1`. Smoke tests use hidden local host/client instances. Separate-computer LAN and subjective animation acceptance still need a human playtest.


## Assigning sounds in Unity
1. Open `Assets/_WhosWatching/Scenes/Arena.unity`.
2. Select **Arena Audio** at the root of the Hierarchy.
3. In its **Arena Audio (Script)** Inspector component, drag imported WAV/OGG/MP3 AudioClips into the named fields. All fields are optional; empty slots are silent.
4. Save the scene and rebuild the Windows player to include your clips. Assign clips in edit mode; Play-mode Inspector changes are temporary.

| Inspector group | Clip slots |
| --- | --- |
| Music | Background Music (automatically looped; Music In Lobby toggle) |
| Character movement | Devil Walking, Ghost Walking, Devil Jump, Ghost Jump, Landing |
| Gaze effects | Buff Effect, Debuff Effect |
| Combat/objectives | Punch (swing), Punch Impact (confirmed hit), Checkpoint Complete, Checkpoint Sabotage, Crate Pickup, Crate Return, Respawn |
| Round announcements | Round Start, Overtime Start, Victory, Defeat, Draw |

Use short single-step clips for walking, not full walking loops: cadence follows actual grounded movement speed. Ghost Walking can be a soft movement/float pulse. Jump clips play on a server-confirmed jump, not merely falling off a ledge. Buff/debuff cues play when your own player gains the effect, not every frame. You can use the same clip in both character jump slots.

Background music and announcements are 2D. Your character sounds are 2D; other players and objective interactions use distance-attenuated 3D audio. Mono clips work well for spatial effects. Music Volume, Effects Volume, Footstep Volume, Footstep Interval and Audible Distance are adjustable. Optional Music Mixer and Effects Mixer slots accept AudioMixerGroups; no mixer asset is required. The reusable effect voice pool is bounded by Voice Limit.

No audio files were supplied with the project; the saved slots intentionally start empty. Assigning clips is the only required audio setup. The installer menu **Who's Watching > Install Arena audio slots** can restore a missing component and preserves existing assignments.

## Cartoon UI, Essentials mode and player names
The lobby and gameplay HUD share rounded navy cards, cream text, colorful team badges and bold action buttons. During play, Esc releases the mouse and exposes a compact menu; click outside that menu to resume.

Enter **Your Name** in the lobby (up to 16 letters, digits, spaces, underscores or hyphens). It is remembered on this computer and replicated to peers. Other players see it above your character when you are visible and nearby; walls block nameplates. Empty names become Player. Names can be edited in the lobby, not during a round. Team colors distinguish nameplates; duplicate names are allowed.

Choose **HUD: DETAILED / ESSENTIALS** in the lobby or pause menu, or press **H** during play. The preference is saved locally. Essentials keeps player nameplates, the aiming dot, team scores and the timer; it hides multipliers, work-progress details and control prompts. It does not change rules or mute audio. Pause menus and the final result card remain available in either mode.
