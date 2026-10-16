using UnityEngine;

namespace WhosWatching
{
    [CreateAssetMenu(menuName = "Who's Watching/Gaze configuration")]
    public sealed class GazeConfig : ScriptableObject
    {
        [Header("Screen visibility")]
        [Tooltip("Maximum distance from the observer eye to a visible body sample, in metres.")]
        public float Range = 15;
        [Tooltip("Vertical camera field of view. The host sends this to every player camera; horizontal coverage follows the viewport aspect ratio.")]
        [Range(30, 100)] public float VerticalFieldOfView = 60;
        [Tooltip("Continuous visibility time required independently for each observer-target pair.")]
        public float LockSeconds = 0.15f;
        public float EvaluationsPerSecond = 20;
        [Header("Per-observer contributions (0.35 = 35%)")]
        [Tooltip("Added movement multiplier per visible allied observer.")]
        public float MoveSupport = 0.35f;
        [Tooltip("Subtracted movement multiplier per visible enemy observer.")]
        public float MoveDisruption = 0.25f;
        [Tooltip("Added work multiplier per visible allied observer.")]
        public float WorkSupport = 0.35f;
        [Tooltip("Subtracted work multiplier per visible enemy observer.")]
        public float WorkDisruption = 0.25f;
        [Header("Stacking limits")]
        [Tooltip("Final minimum speed/work multiplier AFTER summing all positive and negative contributions. 0.5 means half speed.")]
        public float Minimum = 0.5f;
        [Tooltip("Final maximum speed/work multiplier AFTER summing all contributions. Increase this to allow stronger combined buffs (e.g. 2.05 for three +35% observers).")]
        public float Maximum = 1.5f;
        [Header("Obstruction")]
        [Tooltip("World/default and GazeTarget layers. Movement controllers use the separate Player layer.")]
        public LayerMask Blockers = (1 << 0) | (1 << 9);
        void OnValidate()
        {
            Range = Mathf.Max(0.1f, Range); VerticalFieldOfView = Mathf.Clamp(VerticalFieldOfView, 30, 100);
            LockSeconds = Mathf.Max(0, LockSeconds); EvaluationsPerSecond = Mathf.Clamp(EvaluationsPerSecond, 1, 60);
            Minimum = Mathf.Max(0.01f, Minimum); Maximum = Mathf.Max(Minimum, Maximum);
            MoveSupport = Mathf.Max(0, MoveSupport); MoveDisruption = Mathf.Max(0, MoveDisruption);
            WorkSupport = Mathf.Max(0, WorkSupport); WorkDisruption = Mathf.Max(0, WorkDisruption);
        }
    }
}
