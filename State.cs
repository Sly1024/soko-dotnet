using System;
using System.Collections.Generic;
using System.Linq;

namespace soko
{
    public class State
    {
        Level level;
        int[] boxPositions;
        int playerPosition;

        const int WALL = 255;
        const int BOX = 254;

        int[] table;
        int currentReachable = 0;

        public State(Level level, int[] initialBoxPositions, int initialPlayerPosition)
        {
            this.level = level;
            boxPositions = (int[])initialBoxPositions.Clone();
            playerPosition = initialPlayerPosition;
            FillTable();
        }

        public State(State state)
        {
            level = state.level;
            boxPositions = (int[])state.boxPositions.Clone();
            playerPosition = state.playerPosition;
            currentReachable = state.currentReachable;
            table = (int[])state.table.Clone();
        }

        private void FillTable()
        {
            table = new int[level.table.Length];
            for (var i = 0; i < table.Length; i++)
            {
                table[i] = (level.table[i] == Cell.Wall) ? WALL : 0;
            }
            foreach (var box in boxPositions)
            {
                table[box] = BOX;
            }
        }

        public void CalculatePlayerReachableMap()
        {
            var width = level.width;
            // TODO: what if currentReachable overflows?
            table[playerPosition] = ++currentReachable;

            var queue = new Queue<int>();
            queue.Enqueue(playerPosition);

            while (queue.Count > 0) {
                var pos = queue.Dequeue();
                // update player position to be the lowest value
                if (pos < playerPosition) playerPosition = pos;
                if (table[pos + 1] != currentReachable && table[pos + 1] < BOX) { table[pos + 1] = currentReachable; queue.Enqueue(pos + 1); }
                if (table[pos - 1] != currentReachable && table[pos - 1] < BOX) { table[pos - 1] = currentReachable; queue.Enqueue(pos - 1); }
                if (table[pos + width] != currentReachable && table[pos + width] < BOX) { table[pos + width] = currentReachable; queue.Enqueue(pos + width); }
                if (table[pos - width] != currentReachable && table[pos - width] < BOX) { table[pos - width] = currentReachable; queue.Enqueue(pos - width); }
            }
        }

        private int GetOffset(Direction direction, int width) {
            return direction switch {
                Direction.Left => -1,
                Direction.Right => 1,
                Direction.Up => -width,
                Direction.Down => width,
                _ => throw new ArgumentException($"Invalid direction {direction}")
            };
        }

        public List<Move> GetPossibleMoves()
        {
            var moves = new List<Move>();
            for (var boxIdx = 0; boxIdx < boxPositions.Length; boxIdx++)
            {
                var boxPos = boxPositions[boxIdx];
                for (var dir = 0; dir < 4; dir++)
                {
                    var offset = GetOffset((Direction)dir, level.width);
                    if (table[boxPos - offset] == currentReachable && table[boxPos + offset] < BOX) {
                        moves.Add(new Move { boxIndex = boxIdx, direction = (Direction)dir });
                    }
                }
            }
            return moves;
        }

        public void ApplyMove(Move move)
        {
            var boxIdx = move.boxIndex;
            var boxPos = boxPositions[boxIdx];
            var offset = GetOffset(move.direction, level.width);

            // update boxPositions
            boxPositions[boxIdx] = boxPos + offset;
            FixBoxOrder(boxIdx);

            table[boxPos + offset] = BOX;
            table[boxPos] = currentReachable;
            playerPosition = boxPos;
        }

        private void FixBoxOrder(int boxIdx)
        {
            int temp;
            while (boxIdx > 0 && (temp = boxPositions[boxIdx-1]) > boxPositions[boxIdx]) {
                boxPositions[boxIdx-1] = boxPositions[boxIdx];
                boxPositions[boxIdx--] = temp;
            }
            while (boxIdx < boxPositions.Length-1 && (temp = boxPositions[boxIdx+1]) < boxPositions[boxIdx]) {
                boxPositions[boxIdx+1] = boxPositions[boxIdx];
                boxPositions[boxIdx++] = temp;
            }
        }

        internal bool IsEndState()
        {
            return boxPositions.SequenceEqual(level.goalPositions);
        }

        public override bool Equals(object obj)
        {
            var state = (State)obj;
            return playerPosition == state.playerPosition && boxPositions.SequenceEqual(state.boxPositions);
        }

        public override int GetHashCode()
        {
            var hash = playerPosition;

            foreach (var box in boxPositions)
            {
                hash = hash * 37 + box;
            }

            return hash;
        }
    }
}