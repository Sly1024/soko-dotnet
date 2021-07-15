using System;

namespace soko
{
    // Left = 0,
    // Right = 1,
    // Up = 2,
    // Down = 3
    public struct Move
    {
        public static readonly string[] PushCodeForDirection = new [] { "L", "R", "U", "D"};
        public static implicit operator Move((int boxPos, int dir) a) => new Move { encoded = (ushort)((a.boxPos << 2) | a.dir) };

        public ushort encoded;

        public int BoxPos { get => encoded >> 2; }
        public int Direction { get => encoded & 3; }

        public override string ToString()
        {
            return $"[{BoxPos} {PushCodeForDirection[Direction]}]";
        }
    }
}