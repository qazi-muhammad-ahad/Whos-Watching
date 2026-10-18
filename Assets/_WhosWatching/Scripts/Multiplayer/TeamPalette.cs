using UnityEngine;

namespace WhosWatching
{
    public enum CharacterId : byte { Devil, Ghost }

    public static class TeamPalette
    {
        public const int Count = 6;
        public static readonly string[] Names = { "Azure", "Coral", "Gold", "Mint", "Violet", "Rose" };
        static readonly Color[] Colors = {
            new(0.08f, 0.58f, 1f), new(1f, 0.28f, 0.12f), new(1f, 0.76f, 0.06f),
            new(0.12f, 0.85f, 0.47f), new(0.63f, 0.32f, 1f), new(1f, 0.27f, 0.65f)
        };
        public static bool Valid(byte index) => index < Count;
        public static bool Valid(CharacterId character) => character == CharacterId.Devil || character == CharacterId.Ghost;
        public static bool CanChoose(byte index, byte opponent) => Valid(index) && index != opponent;
        public static Color Color(byte index) => Colors[Valid(index) ? index : 0];
        public static Color Neutral => new(0.48f, 0.53f, 0.58f);
    }
}
