using System;

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

        public override string ToString()
        {
            return $"[{boxIndex} {GetPushCodeForDirection(direction)}]";
        }

        public string PushCode {
            get => GetPushCodeForDirection(direction);
        }

        public static string GetPushCodeForDirection(Direction direction) {
            return direction switch
            {
                Direction.Left => "L",
                Direction.Right => "R",
                Direction.Up => "U",
                Direction.Down => "D",
                _ => throw new ArgumentException($"Invalid direction {direction}")
            };
        }
    }
}