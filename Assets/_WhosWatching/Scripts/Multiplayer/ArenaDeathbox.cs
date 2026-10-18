using UnityEngine;
namespace WhosWatching {
    public sealed class ArenaDeathbox : MonoBehaviour {
        void OnTriggerEnter(Collider other) {
            var player = other.GetComponentInParent<NetworkPlayer>();
            if (player && player.IsSpawned && player.IsServer && ArenaGameplay.Instance) ArenaGameplay.Instance.Respawn(player);
        }
    }
}
