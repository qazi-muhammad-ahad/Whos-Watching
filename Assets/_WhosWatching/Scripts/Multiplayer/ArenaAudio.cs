using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace WhosWatching
{
    public enum ArenaSound : byte { CheckpointComplete, CheckpointSabotage, CratePickup, CrateReturn }

    [DisallowMultipleComponent]
    public sealed class ArenaAudio : MonoBehaviour
    {
        public static ArenaAudio Instance { get; private set; }
        [Header("Music (looped, 2D)")]
        public AudioClip BackgroundMusic;
        public bool MusicInLobby = true;
        [Range(0, 1)] public float MusicVolume = 0.35f;
        public AudioMixerGroup MusicMixer;

        [Header("Character movement (short one-shot clips)")]
        public AudioClip DevilWalking, GhostWalking;
        public AudioClip DevilJump, GhostJump;
        [Tooltip("Optional landing sound shared by both characters.")]
        public AudioClip Landing;
        [Range(0.15f, 1)] public float FootstepInterval = 0.42f;
        [Range(0, 1)] public float FootstepVolume = 0.5f;

        [Header("Gaze effects (local player only, on effect gained)")]
        public AudioClip BuffEffect, DebuffEffect;
        [Header("Combat and objectives")]
        public AudioClip Punch, PunchImpact;
        public AudioClip CheckpointComplete, CheckpointSabotage;
        public AudioClip CratePickup, CrateReturn, Respawn;
        [Header("Round announcements (2D)")]
        public AudioClip RoundStart, OvertimeStart, Victory, Defeat, Draw;
        [Header("Effects output")]
        [Range(0, 1)] public float EffectsVolume = 0.8f;
        public AudioMixerGroup EffectsMixer;
        [Min(1)] public float AudibleDistance = 24;
        [Range(4, 32)] public int VoiceLimit = 16;

        sealed class PlayerState
        {
            public uint Jump, Punch, Hit, Respawn;
            public bool Airborne, Buff, Debuff;
            public float StepAt;
        }
        readonly Dictionary<NetworkPlayer, PlayerState> players = new();
        readonly List<NetworkPlayer> stale = new();
        readonly List<AudioSource> voices = new();
        readonly List<float> gains = new();
        AudioSource music;
        Transform audioRoot;
        ArenaGameplay observedArena;
        bool playing, finished, overtime;
        int nextVoice;

        void Awake()
        {
            if (Instance && Instance != this) { enabled = false; return; }
            Instance = this;
            var root = new GameObject("Audio playback"); root.transform.SetParent(transform, false); audioRoot = root.transform;
            music = root.AddComponent<AudioSource>(); music.playOnAwake = false; music.loop = true; music.spatialBlend = 0;
        }
        void OnDisable()
        {
            if (music) music.Stop();
            foreach (var voice in voices) if (voice) voice.Stop();
            players.Clear(); playing = finished = overtime = false; observedArena = null;
        }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (!music) return;
            var arena = ArenaGameplay.Instance;
            var lobby = TeamLobby.Instance;
            bool inRound = arena && arena.IsSpawned && lobby && lobby.IsSpawned && lobby.Playing.Value;
            bool ended = inRound && arena.Finished.Value;
            bool extraTime = inRound && arena.Overtime.Value;
            UpdateMusic(MusicInLobby || inRound);
            for (int i = 0; i < voices.Count; i++)
                if (voices[i]) { voices[i].volume = EffectsVolume * gains[i]; voices[i].outputAudioMixerGroup = EffectsMixer; }
            if (observedArena != arena) { observedArena = arena; playing = finished = overtime = false; players.Clear(); }
            if (inRound && !playing) Play(RoundStart);
            if (inRound && extraTime && !overtime) Play(OvertimeStart);
            if (ended && !finished)
            {
                var local = LocalPlayer();
                Play(arena.Winner.Value == TeamId.None ? Draw : local && local.Team.Value == arena.Winner.Value ? Victory : Defeat);
            }
            if (!inRound && playing) { foreach (var voice in voices) if (voice) voice.Stop(); }
            playing = inRound; finished = ended; overtime = extraTime;
            var session = NetworkGameManager.Instance;
            if (!session || !session.Network.IsConnectedClient) { players.Clear(); return; }
            stale.Clear();
            foreach (var pair in players) if (!pair.Key || !pair.Key.IsSpawned) stale.Add(pair.Key);
            foreach (var p in stale) players.Remove(p);
            foreach (var p in session.Gaze.Players)
            {
                if (!p || !p.IsSpawned) continue;
                if (!players.TryGetValue(p, out var state))
                {
                    state = new PlayerState(); Snapshot(p, state); players.Add(p, state); continue;
                }
                bool respawned = state.Respawn != p.RespawnSequence.Value;
                bool jumped = state.Jump != p.JumpSequence.Value;
                if (inRound && !ended)
                {
                    if (respawned) { PlayAtPlayer(Respawn, p); state.StepAt = Time.time + 0.5f; }
                    else
                    {
                        if (jumped) PlayAtPlayer(p.Character.Value == CharacterId.Devil ? DevilJump : GhostJump, p);
                        if (state.Airborne && !p.Airborne.Value) PlayAtPlayer(Landing, p, 0.7f);
                        if (p.AnimationSpeed.Value > 0.35f && !p.Airborne.Value && !jumped && Time.time >= state.StepAt)
                        {
                            PlayAtPlayer(p.Character.Value == CharacterId.Devil ? DevilWalking : GhostWalking, p, FootstepVolume);
                            state.StepAt = Time.time + FootstepInterval / Mathf.Clamp(p.AnimationSpeed.Value / 5, 0.65f, 1.6f);
                        }
                    }
                    if (state.Punch != p.PunchSequence.Value) PlayAtPlayer(Punch, p);
                    if (state.Hit != p.HitSequence.Value) PlayAtPlayer(PunchImpact, p);
                    if (p.IsOwner)
                    {
                        if (!state.Buff && p.Gaze.Value.Allies > 0) Play(BuffEffect);
                        if (!state.Debuff && p.Gaze.Value.Enemies > 0) Play(DebuffEffect);
                    }
                }
                Snapshot(p, state);
            }
        }
        static void Snapshot(NetworkPlayer p, PlayerState state)
        {
            state.Jump = p.JumpSequence.Value; state.Punch = p.PunchSequence.Value;
            state.Hit = p.HitSequence.Value; state.Respawn = p.RespawnSequence.Value;
            state.Airborne = p.Airborne.Value; state.Buff = p.Gaze.Value.Allies > 0; state.Debuff = p.Gaze.Value.Enemies > 0;
        }
        static NetworkPlayer LocalPlayer()
        {
            var session = NetworkGameManager.Instance;
            return session && session.Network.LocalClient?.PlayerObject ? session.Network.LocalClient.PlayerObject.GetComponent<NetworkPlayer>() : null;
        }
        void UpdateMusic(bool wanted)
        {
            music.outputAudioMixerGroup = MusicMixer; music.volume = MusicVolume;
            if (music.clip != BackgroundMusic) { music.Stop(); music.clip = BackgroundMusic; }
            if (!wanted || !BackgroundMusic) { if (music.isPlaying) music.Stop(); }
            else if (!music.isPlaying) music.Play();
        }
        void PlayAtPlayer(AudioClip clip, NetworkPlayer p, float gain = 1) => Play(clip, p.transform.position, !p.IsOwner, gain);
        public void PlayEvent(ArenaSound sound, Vector3 position)
        {
            var clip = sound switch {
                ArenaSound.CheckpointComplete => CheckpointComplete,
                ArenaSound.CheckpointSabotage => CheckpointSabotage,
                ArenaSound.CratePickup => CratePickup,
                ArenaSound.CrateReturn => CrateReturn,
                _ => null
            };
            Play(clip, position, true);
        }
        // Bounded reusable voices avoid allocations and leaked AudioSources during long rounds.
        public void Play(AudioClip clip, Vector3 position = default, bool spatial = false, float gain = 1)
        {
            if (!clip || !isActiveAndEnabled || !audioRoot || EffectsVolume <= 0) return;
            int index = voices.FindIndex(v => !v.isPlaying);
            if (index < 0 && voices.Count < Mathf.Clamp(VoiceLimit, 4, 32))
            {
                var go = new GameObject("Effect voice " + voices.Count); go.transform.SetParent(audioRoot, false);
                var source = go.AddComponent<AudioSource>(); source.playOnAwake = false;
                index = voices.Count; voices.Add(source); gains.Add(1);
            }
            if (index < 0) { index = nextVoice++ % voices.Count; voices[index].Stop(); }
            var voice = voices[index]; gains[index] = Mathf.Clamp01(gain);
            voice.transform.position = position; voice.outputAudioMixerGroup = EffectsMixer;
            voice.spatialBlend = spatial ? 1 : 0; voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 2; voice.maxDistance = Mathf.Max(3, AudibleDistance); voice.dopplerLevel = 0;
            voice.loop = false; voice.pitch = 1; voice.volume = EffectsVolume * gains[index]; voice.clip = clip; voice.Play();
        }
    }
}
