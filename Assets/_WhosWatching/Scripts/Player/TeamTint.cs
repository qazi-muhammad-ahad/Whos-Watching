using System;
using UnityEngine;

namespace WhosWatching
{
    // Explicit material-slot bindings keep eyes, wood, stone, metal and signs intact.
    [ExecuteAlways]
    public sealed class TeamTint : MonoBehaviour
    {
        [Serializable] public struct Slot
        {
            public Renderer Renderer;
            public int MaterialIndex;
            [Range(0,1)] public float Strength;
            public Color BaseColor;
            public bool Emissive;
        }
        public TeamId Team = TeamId.None;
        public Slot[] Slots = Array.Empty<Slot>();
        NetworkPlayer player;
        MaterialPropertyBlock block;
        Color previous;
        bool applied;
        void OnEnable() { player = GetComponentInParent<NetworkPlayer>(); applied = false; Apply(); }
        void LateUpdate() => ApplyCached();
        void OnValidate() { applied = false; }
        public void Apply() { applied = false; ApplyCached(); }
        void ApplyCached()
        {
            var team = player ? player.Team.Value : Team;
            var lobby = TeamLobby.Instance;
            Color color = lobby && lobby.IsSpawned ? lobby.Color(team) : team == TeamId.None ? TeamPalette.Neutral : TeamPalette.Color((byte)(team == TeamId.B ? 1 : 0));
            if (applied && previous == color) return;
            previous = color; applied = true;
            block ??= new MaterialPropertyBlock();
            foreach (var slot in Slots)
            {
                // Nested variants own their renderers inside larger map prefabs.
                if (!slot.Renderer || slot.Renderer.GetComponentInParent<TeamTint>() != this) continue;
                slot.Renderer.GetPropertyBlock(block,slot.MaterialIndex);
                var tint = UnityEngine.Color.Lerp(slot.BaseColor, color, slot.Strength);
                block.SetColor("_BaseColor", tint); block.SetColor("_Color", tint);
                if (slot.Emissive) block.SetColor("_EmissionColor",tint * 0.5f);
                slot.Renderer.SetPropertyBlock(block,slot.MaterialIndex); block.Clear();
            }
        }
    }
}
