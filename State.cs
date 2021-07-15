using System.Text;
using System;
using System.Collections.Generic;

namespace soko
{
    public class State
    {
        Level level;
        int playerPosition;
        HashSet<int> boxPositions;
        ulong boxZhash;

        const int WALL = int.MaxValue;
        const int BOX = WALL - 1;
        const int BLOCKED = BOX;
        const int MAX_REACHABLE = BLOCKED;

        int[] reachableTable;
        int currentReachable = 0;
        bool reachableValid = false;

        public State(Level level, int[] initialBoxPositions, int initialPlayerPosition)
        {
            this.level = level;
            boxPositions = new HashSet<int>(initialBoxPositions);
            boxZhash = level.GetZHashForBoxes(initialBoxPositions);
            playerPosition = initialPlayerPosition;
            FillTable(initialBoxPositions);
        }

        private void FillTable(int[] boxPositions)
        {
            reachableTable = new int[level.table.Length];
            for (var i = 0; i < reachableTable.Length; i++) {
                reachableTable[i] = level.table[i].has(Cell.Wall) ? WALL : 0;
            }
            foreach (var box in boxPositions) {
                reachableTable[box] = BOX;
            }
            reachableValid = false;
        }

        // for debugging
        internal void PrintTable()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < reachableTable.Length; i++) {
                sb.Append(reachableTable[i] switch {
                    WALL => "#",
                    BOX => "$",
                    _ => " "
                });
                sb.Append(" ");
                if (i % level.width == level.width -1) sb.Append("\n");
            }
            Console.WriteLine(sb.ToString());
            Console.WriteLine($"PlayerPos: {playerPosition}");
        }

        private void ClearReachableTable() 
        {
            for (var i = 0; i < reachableTable.Length; i++) {
                var value = reachableTable[i];
                if (value != 0 && value < BLOCKED) reachableTable[i] = 0;
            }
        }

        public void CalculatePlayerReachableMap()
        {
            var width = level.width;

            // if currentReachable overflows
            if (++currentReachable >= MAX_REACHABLE) {
                ClearReachableTable();
                currentReachable = 1;
            }

            reachableTable[playerPosition] = currentReachable;

            Filler.Fill(reachableTable, width, playerPosition, 
                // update player position to be the lowest value
                pos => { if (pos < playerPosition) playerPosition = pos; return false; },
                (value, idx) => (value != currentReachable && value < BLOCKED) ? currentReachable : -1
            );

            reachableValid = true;
        }

        public List<Move> GetPossibleMoves(bool isPull)
        {
            if (!reachableValid) CalculatePlayerReachableMap();

            var moves = new List<Move>();
            foreach (var boxPos in boxPositions)
            {
                for (var dir = 0; dir < 4; dir++)
                {
                    var offset = level.dirOffset[dir];
                    if (reachableTable[boxPos - offset] == currentReachable && 
                        (isPull ? reachableTable[boxPos - 2*offset] < BLOCKED :
                            // dead cells only affect push moves
                            (reachableTable[boxPos + offset] < BLOCKED && !level.table[boxPos + offset].has(Cell.DeadCell))))
                    {
                        moves.Add((boxPos, dir));
                    }
                }
            }
            return moves;
        }

        public void ApplyPushMove(Move move)
        {
            var offset = level.dirOffset[move.Direction];
            var boxPos = move.BoxPos;
            int newBoxPos = boxPos + offset;

            // update boxPositions
            boxPositions.Remove(boxPos);
            boxPositions.Add(newBoxPos);

            // update boxZhash
            boxZhash ^= level.boxZbits[boxPos] ^ level.boxZbits[newBoxPos];

            reachableTable[newBoxPos] = BOX;
            reachableTable[boxPos] = currentReachable;
            playerPosition = boxPos;

            reachableValid = false;
        }
        
        public void ApplyPullMove(Move move)
        {
            var offset = level.dirOffset[move.Direction];
            var newBoxPos = move.BoxPos;
            int boxPos = newBoxPos + offset;

            // update boxPositions
            boxPositions.Remove(boxPos);
            boxPositions.Add(newBoxPos);

            // update boxZhash
            boxZhash ^= level.boxZbits[boxPos] ^ level.boxZbits[newBoxPos];

            reachableTable[newBoxPos] = BOX;
            reachableTable[boxPos] = 0;  // TODO: reachable?? Doesn't matter, we set reachableValid = false
            playerPosition = boxPos - offset*2;

            reachableValid = false;
        }

        public ulong GetZHash()
        {
            if (!reachableValid) CalculatePlayerReachableMap();
            return boxZhash ^ level.playerZbits[playerPosition];
        }

        // private bool IsPlayerReachable(int position)
        // {
        //     return reachableTable[position] == currentReachable;
        // }

        // internal int GetPlayerPositionFor(Move move) {
        //     return boxPositions[move.boxIdx] - GetOffset(move.direction, level.width);
        // }

        // internal string FindPlayerPath(int playerPos, Move move)
        // {
        //     return FindPlayerPath(playerPos, GetPlayerPositionFor(move));
        // }

        // internal string FindPlayerPath(int playerPos, int targetPos)
        // {
        //     var width = level.width;
        //     FillTable();
        //     reachableTable[targetPos] = 1;

        //     int distance = 0;

        //     Filler.Fill(reachableTable, width, targetPos, 
        //         pos => { distance = reachableTable[pos] + 1; return pos == playerPos; },
        //         (value, pos) => value == 0 ? distance : -1
        //     );

        //     var sb = new StringBuilder();
        //     while (playerPos != targetPos) {
        //         var dist = reachableTable[playerPos] - 1;
        //         if (reachableTable[playerPos + 1] == dist) { playerPos++; sb.Append('r'); }
        //         else if (reachableTable[playerPos - 1] == dist) { playerPos--; sb.Append('l'); }
        //         else if (reachableTable[playerPos + width] == dist) { playerPos += width; sb.Append('d'); }
        //         else if (reachableTable[playerPos - width] == dist) { playerPos -= width; sb.Append('u'); }
        //     }
        //     return sb.ToString();
        // }

    }
}