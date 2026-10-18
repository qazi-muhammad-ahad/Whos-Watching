using System.Net;
using System.Text;
using UnityEngine;

namespace WhosWatching
{
    public enum TeamId : byte { None, A, B }
    public enum MatchPhase : byte { Lobby, Countdown, Playing, Results }

    public static class SessionRules
    {
        public const int Capacity = 4;
        public const string Protocol = "whos-watching/lan/9-arena-ui";
        public static byte[] Payload => Encoding.UTF8.GetBytes(Protocol);
        public static string Reject(byte[] payload, int occupied, bool lobby)
        {
            byte[] expected = Payload;
            if (payload == null || payload.Length != expected.Length) return "Incompatible build.";
            for (int i = 0; i < expected.Length; i++)
                if (payload[i] != expected[i]) return "Incompatible build.";
            if (!lobby) return "Match in progress. Retry in lobby.";
            return occupied >= Capacity ? "Session full (four players including host)." : null;
        }
        public static bool Endpoint(string host, string port, out ushort number)
        {
            number = 0;
            return IPAddress.TryParse(host, out var ip) &&
                ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.Broadcast) &&
                ushort.TryParse(port, out number) && number > 0;
        }
        public static bool Newer(uint next, uint previous) => next != previous && unchecked((int)(next - previous)) > 0;
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool ValidInput(Vector2 move, float yaw, float pitch) =>
            Finite(move.x) && Finite(move.y) && Finite(yaw) && Finite(pitch) &&
            Mathf.Abs(move.x) <= 2 && Mathf.Abs(move.y) <= 2 && Mathf.Abs(yaw) <= 36000 && Mathf.Abs(pitch) <= 90;
    }
}

