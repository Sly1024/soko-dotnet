using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace soko
{
    public class State
    {
        Level level;
        public int[] boxPositions;
        int playerPosition;

        const int WALL = int.MaxValue;
        const int BOX = WALL - 1;
        const int BLOCKED = BOX;
        const int MAX_REACHABLE = BLOCKED;

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
                table[i] = level.table[i].has(Cell.Wall) ? WALL : 0;
            }
            foreach (var box in boxPositions)
            {
                table[box] = BOX;
            }
        }

        // for debugging
        internal void PrintTable()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < table.Length; i++) {
                sb.Append(table[i] switch {
                    WALL => "#",
                    BOX => "$",
                    _ => table[i].ToString()
                });
                sb.Append(" ");
                if (i % level.width == level.width -1) sb.Append("\n");
            }
            Console.WriteLine(sb.ToString());
            Console.WriteLine($"PlayerPos: {playerPosition}");
            Console.WriteLine($"boxPositions: {string.Join(",", boxPositions)}");
        }

        public void CalculatePlayerReachableMap()
        {
            var width = level.width;

            // if currentReachable overflows
            if (++currentReachable >= MAX_REACHABLE) {
                FillTable();
                currentReachable = 1;
            }

            table[playerPosition] = currentReachable;

            Filler.Fill(table, width, playerPosition, 
                // update player position to be the lowest value
                pos => { if (pos < playerPosition) playerPosition = pos; return false; },
                (value, idx) => (value != currentReachable && value < BLOCKED) ? currentReachable : -1
            );
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

        public List<Move> GetPossibleMoves(bool isPull)
        {
            var moves = new List<Move>();
            for (var boxIdx = 0; boxIdx < boxPositions.Length; boxIdx++)
            {
                var boxPos = boxPositions[boxIdx];
                for (var dir = 0; dir < 4; dir++)
                {
                    var offset = GetOffset((Direction)dir, level.width);
                    if (table[boxPos - offset] == currentReachable && 
                        (isPull ? table[boxPos - 2*offset] < BLOCKED :
                            // dead cells only affect push moves
                            (table[boxPos + offset] < BLOCKED && !level.table[boxPos + offset].has(Cell.DeadCell))))
                    {
                        moves.Add(new Move { boxIndex = boxIdx, direction = (Direction)dir });
                    }
                }
            }
            return moves;
        }

        public int ApplyPushMove(Move move)
        {
            var boxIdx = move.boxIndex;
            var boxPos = boxPositions[boxIdx];
            var offset = GetOffset(move.direction, level.width);

            // update boxPositions
            SetBoxPosition(boxIdx, boxPos + offset);

            table[boxPos + offset] = BOX;
            table[boxPos] = currentReachable;
            playerPosition = boxPos;
            return boxIdx;
        }
        
        public int ApplyPullMove(Move move)
        {
            var boxIdx = move.boxIndex;
            var boxPos = boxPositions[boxIdx];
            var offset = GetOffset(move.direction, level.width);

            // update boxPositions
            var newBoxIdx = SetBoxPosition(boxIdx, boxPos - offset);

            table[boxPos - offset] = BOX;
            table[boxPos] = 0;  // TODO: reachable?? Doesn't matter if we don't reuse the reachable table
            playerPosition = boxPos - offset*2;
            return newBoxIdx;
        }

        private int SetBoxPosition(int boxIdx, int position)
        {
            int temp;
            while (boxIdx > 0 && (temp = boxPositions[boxIdx-1]) > position) {
                boxPositions[boxIdx--] = temp;
            }
            while (boxIdx < boxPositions.Length-1 && (temp = boxPositions[boxIdx+1]) < position) {
                boxPositions[boxIdx++] = temp;
            }
            boxPositions[boxIdx] = position;
            return boxIdx;
        }

        internal bool IsEndState()
        {
            return boxPositions.SequenceEqual(level.goalPositions);
        }

        internal bool IsStartState()
        {
            return IsPlayerReachable(level.playerPosition) && boxPositions.SequenceEqual(level.boxPositions);
        }

        private bool IsPlayerReachable(int position)
        {
            return table[position] == currentReachable;
        }

        internal int GetPlayerPositionFor(Move move) {
            return boxPositions[move.boxIndex] - GetOffset(move.direction, level.width);
        }

        internal string FindPlayerPath(int playerPos, Move move)
        {
            return FindPlayerPath(playerPos, GetPlayerPositionFor(move));
        }

        internal string FindPlayerPath(int playerPos, int targetPos)
        {
            var width = level.width;
            FillTable();
            table[targetPos] = 1;

            int distance = 0;

            Filler.Fill(table, width, targetPos, 
                pos => { distance = table[pos] + 1; return pos == playerPos; },
                (value, pos) => value == 0 ? distance : -1
            );

            var sb = new StringBuilder();
            while (playerPos != targetPos) {
                var dist = table[playerPos] - 1;
                if (table[playerPos + 1] == dist) { playerPos++; sb.Append('r'); }
                else if (table[playerPos - 1] == dist) { playerPos--; sb.Append('l'); }
                else if (table[playerPos + width] == dist) { playerPos += width; sb.Append('d'); }
                else if (table[playerPos - width] == dist) { playerPos -= width; sb.Append('u'); }
            }
            return sb.ToString();
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