namespace soko
{
    public enum Direction
    {
        Left = 0,
        Right = 1,
        Up = 2,
        Down = 3
    }

    public struct Move
    {
        public int boxIndex;
        public Direction direction;
    }
}