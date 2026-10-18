using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace WhosWatching.Tests
{
    public sealed class ArenaAudioTests
    {
        [Test] public void ArenaHasOneEditableAudioComponentWithAllRequestedSlots()
        {
            EditorSceneManager.OpenScene("Assets/_WhosWatching/Scenes/Arena.unity");
            var audio = Object.FindObjectsByType<ArenaAudio>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(audio.Length, Is.EqualTo(1));
            foreach (string name in new[] { "BackgroundMusic", "DevilWalking", "GhostWalking", "DevilJump", "GhostJump", "BuffEffect", "DebuffEffect", "Punch", "CheckpointComplete", "CheckpointSabotage" })
                Assert.That(typeof(ArenaAudio).GetField(name)?.FieldType, Is.EqualTo(typeof(AudioClip)), name);
        }
        [Test] public void UnassignedClipsAreSafeAndDoNotAllocateVoices()
        {
            var go = new GameObject("Audio test");
            try {
                var audio = go.AddComponent<ArenaAudio>();
                Assert.DoesNotThrow(() => {
                    audio.Play(null);
                    foreach (ArenaSound sound in System.Enum.GetValues(typeof(ArenaSound))) audio.PlayEvent(sound, Vector3.zero);
                });
                Assert.That(go.GetComponentsInChildren<AudioSource>().Count(s => !s.loop), Is.EqualTo(0));
            } finally { Object.DestroyImmediate(go); }
        }
    }
}
