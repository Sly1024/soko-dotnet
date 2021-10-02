namespace soko
{
    // Left = 0,
    // Right = 1,
    // Up = 2,
    // Down = 3
    public struct Move
    {
        public static readonly string[] PushCodeForDirection = new [] { "L", "R", "U", "D"};

        // converts (int, int) tuple to Move
        public static implicit operator Move((int boxPos, int dir) a) => new Move { encoded = (ushort)((a.boxPos << 2) | a.dir) };
        // converts (int, int, bool) tuple to Move
        public static implicit operator Move((int boxPos, int dir, bool otherSideReachable) a) => 
            new Move { encoded = (ushort)((a.otherSideReachable ? 1 << 14 : 0) | (a.boxPos << 2) | a.dir) };

        public ushort encoded;

        public int Direction { get => encoded & 3; }    // Last 2 bits
        public int BoxPos { get => (encoded >> 2) & ((1<<12) - 1); }    // next 12 bits
        public bool IsBoxOtherSideReachable { get => ((encoded >> 14) & 1) == 1; } // 14. bit
        public bool IsLast { get => encoded >> 15 == 1; }   // first (15.) bit

        public int NewBoxPos { get => BoxPos + Level.DirOffset[Direction]; }

        public void SetLastBit()
        {
            encoded |= 1 << 15;
        }

        public override string ToString()
        {
            return $"[{BoxPos} {PushCodeForDirection[Direction]}]";
        }
    }
}