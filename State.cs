using System.Text;
using System;
using System.Collections.Generic;

namespace soko
{
    public class State
    {
        Level level;
        public int playerPosition;
        HashSet<int> boxPositions;
        ulong boxZhash;

        const int WALL = int.MaxValue;
        const int BOX = WALL - 1;
        const int BLOCKED = BOX;
        const int MAX_REACHABLE = BLOCKED;

        int[] reachableTable;
        int currentReachable = 0;
        int maxReachable = 0;
        bool reachableValid = false;

        public State(Level level, int[] initialBoxPositions, int initialPlayerPosition)
        {
            this.level = level;
            boxPositions = new HashSet<int>(initialBoxPositions.Length*3);
            foreach (var box in initialBoxPositions) boxPositions.Add(box);

            boxZhash = level.GetZHashForBoxes(initialBoxPositions);
            playerPosition = initialPlayerPosition;
            FillTable();
            // CalculatePlayerReachableMap();
            UpdatePlayerReachableMap();
        }

        private void FillTable()
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
            var sb = new StringBuilder("\n");
            for (var i = 0; i < reachableTable.Length; i++) {
                sb.Append(reachableTable[i] switch {
                    WALL => "#",
                    BOX => "$",
                    _ => reachableTable[i] == currentReachable ? "." : " "
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
                if (reachableTable[i] < BLOCKED) reachableTable[i] = 0;
            }
            reachableValid = false;
        }

        // public void CalculatePlayerReachableMap()
        // {
        //     ClearReachableTable();
        //     maxReachable = 0;

        //     var width = level.width;

        //     for (var i = 0; i < reachableTable.Length; i++) {
        //         if (reachableTable[i] == 0) {
        //             var minPos = Filler.Fill2(reachableTable, width, i, ++maxReachable);
        //             if (reachableTable[playerPosition] == maxReachable) {
        //                 playerPosition = minPos;
        //                 currentReachable = maxReachable;
        //             }
        //         }
        //     }
        //     reachableValid = true;
        // }
        
        public void UpdatePlayerReachableMap()
        {
            CheckMaxReachable();
            playerPosition = Filler.Fill2(reachableTable, level.width, playerPosition, currentReachable = ++maxReachable);
            reachableValid = true;
        }

        private void CheckMaxReachable()
        {
            // if currentReachable might overflow
            if (maxReachable >= MAX_REACHABLE - 5) {
                ClearReachableTable();
            }
        }

        public int GetPossiblePushMoves(MoveRanges moves)
        {
            if (!reachableValid) UpdatePlayerReachableMap();

            //var moves = new List<Move>();
            moves.StartAddRange();

            foreach (var boxPos in boxPositions)
            {
                for (var dir = 0; dir < 4; dir++)
                {
                    var offset = level.dirOffset[dir];
                    if (reachableTable[boxPos - offset] == currentReachable && 
                            // dead cells only affect push moves
                            (reachableTable[boxPos + offset] < BLOCKED && !level.table[boxPos + offset].has(Cell.DeadCell)))
                    {
                        moves.AddRangeItem((boxPos, dir));
                    }
                }
            }

            return moves.FinishAddRange();
            // return moves;
        }
        
        // public int GetPossiblePullMoves(MoveRanges moves)
        // {
        //     if (!reachableValid) CalculatePlayerReachableMap();

        //     //var moves = new List<Move>();
        //     moves.StartAddRange();

        //     foreach (var boxPos in boxPositions)
        //     {
        //         for (var dir = 0; dir < 4; dir++)
        //         {
        //             var offset = level.dirOffset[dir];
        //             if (reachableTable[boxPos - offset] == currentReachable && 
        //                 reachableTable[boxPos - 2*offset] < BLOCKED)
        //             {
        //                 moves.AddRangeItem((boxPos, dir));
        //             }
        //         }
        //     }

        //     return moves.FinishAddRange();
        //     // return moves;
        // }

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
            if (boxPos < playerPosition) playerPosition = boxPos;

            //CheckMaxReachable();

            if (reachableValid && FixReachableFwd(move)) reachableValid = false;
        }

        private bool FixReachableFwd(Move move)
        {
            var dirOff = level.dirOffset[move.Direction];
            var pDirOff = level.width + 1 - Math.Abs(dirOff);

            var boxPos = move.BoxPos;
            int wallBits = 0;

            int pos = boxPos - dirOff - pDirOff;
            for (var i = 0; i < 4; ++i, pos += dirOff) {
                wallBits <<= 1;
                wallBits |= (reachableTable[pos] >= BLOCKED) ? 1 : 0;
            }

            pos = boxPos - dirOff + pDirOff;
            for (var i = 0; i < 4; ++i, pos += dirOff) {
                wallBits <<= 1;
                wallBits |= (reachableTable[pos] >= BLOCKED) ? 1 : 0;
            }

            wallBits <<= 1;
            wallBits |= (reachableTable[boxPos + 2*dirOff] >= BLOCKED ? 1 : 0);

            var decision = PlayerReachable.decisionBits[wallBits];

            return decision != 0;

            /*
            reachableTable[boxPos + dirOff] = BOX;
            reachableTable[boxPos] = currentReachable;
            if (boxPos < playerPosition) playerPosition = boxPos;

            if (decision == 0) return;

            var P1 = currentReachable; // reachableTable[boxPos - dirOff]; - obviously
            var P2 = reachableTable[boxPos - pDirOff];
            var P6 = reachableTable[boxPos + pDirOff];
            var P3 = reachableTable[boxPos - pDirOff + dirOff];
            var P4 = reachableTable[boxPos + 2*dirOff];
            var P5 = reachableTable[boxPos + pDirOff + dirOff];

            var P1overfill = false;

            if ((decision & PlayerReachable.C3) != 0) {
                if (P3 == P1 && !P1overfill) {
                    // over-fill P1
                    var minPos = Filler.FillOverWrite(reachableTable, level.width, boxPos - dirOff, P1, currentReachable = ++maxReachable);
                    if (minPos != int.MaxValue) playerPosition = minPos;
                    P1overfill = true;
                }
                Filler.FillOverWrite(reachableTable, level.width, boxPos - pDirOff + dirOff, P3, ++maxReachable);
            }

            if ((decision & PlayerReachable.C4) != 0) {
                if (P3 == P1 && !P1overfill) {
                    // over-fill P1
                    var minPos = Filler.FillOverWrite(reachableTable, level.width, boxPos - dirOff, P1, currentReachable = ++maxReachable);
                    if (minPos != int.MaxValue) playerPosition = minPos;
                    P1overfill = true;
                }
                Filler.FillOverWrite(reachableTable, level.width, boxPos + 2*dirOff, P4, ++maxReachable);
            }

            if ((decision & PlayerReachable.C5) != 0) {
                if (P3 == P1 && !P1overfill) {
                    // over-fill P1
                    var minPos = Filler.FillOverWrite(reachableTable, level.width, boxPos - dirOff, P1, currentReachable = ++maxReachable);
                    if (minPos != int.MaxValue) playerPosition = minPos;
                    P1overfill = true;
                }
                Filler.FillOverWrite(reachableTable, level.width, boxPos + pDirOff + dirOff, P5, ++maxReachable);
            }

            if ((decision & PlayerReachable.O2) != 0 && P1 != P2) {
                var minPos = Filler.FillOverWrite(reachableTable, level.width, boxPos - pDirOff, P2, currentReachable);
                if (minPos < playerPosition) playerPosition = minPos;
            }

            if ((decision & PlayerReachable.O6) != 0 && P1 != P6) {
                var minPos = Filler.FillOverWrite(reachableTable, level.width, boxPos + pDirOff, P6, currentReachable);
                if (minPos < playerPosition) playerPosition = minPos;
            }
            */
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

            var isBoxPosReachable = reachableTable[boxPos - 1] == currentReachable || reachableTable[boxPos + 1] == currentReachable ||
                reachableTable[boxPos - level.width] == currentReachable || reachableTable[boxPos + level.width] == currentReachable;

            reachableTable[boxPos] = isBoxPosReachable ? currentReachable : ++maxReachable;  // TODO: reachable?? Doesn't matter, if we set reachableValid = false
            
            // if (playerPosition == newBoxPos) { // can only happen if move = Left or Up
            //     while (reachableTable[++playerPosition] != currentReachable) {}
            // }

            var newPlayerPos = boxPos - offset*2;
            // if (isBoxPosReachable && boxPos < playerPosition) playerPosition = boxPos;
            /* if (newPlayerPos < playerPosition)  */playerPosition = newPlayerPos;
            
            CheckMaxReachable();
            /* if (reachableValid && FixReachableBck(move))  */reachableValid = false;
        }

        private bool FixReachableBck(Move move)
        {
            var dirOff = level.dirOffset[move.Direction];
            var pDirOff = level.width + 1 - Math.Abs(dirOff);

            var boxPos = move.BoxPos;
            int wallBits = 0;

            int pos = boxPos - dirOff - pDirOff;
            for (var i = 0; i < 4; ++i, pos += dirOff) {
                wallBits <<= 1;
                wallBits |= (reachableTable[pos] >= BLOCKED) ? 1 : 0;
            }

            pos = boxPos - dirOff + pDirOff;
            for (var i = 0; i < 4; ++i, pos += dirOff) {
                wallBits <<= 1;
                wallBits |= (reachableTable[pos] >= BLOCKED) ? 1 : 0;
            }

            wallBits <<= 1;
            wallBits |= (reachableTable[boxPos + 2*dirOff] >= BLOCKED ? 1 : 0);

            var decision = PlayerReachable.decisionBits[wallBits];
            return decision != 0;


            /*
            var P1 = currentReachable; // reachableTable[boxPos - dirOff]; - obviously
            var P2 = reachableTable[boxPos - pDirOff];
            var P6 = reachableTable[boxPos + pDirOff];
            var P3 = reachableTable[boxPos - pDirOff + dirOff];
            var P4 = reachableTable[boxPos + 2*dirOff];
            var P5 = reachableTable[boxPos + pDirOff + dirOff];


            var isNewOpeningReachable = P3 == currentReachable || P4 == currentReachable || P5 == currentReachable;
            var minFillNeighbours = P3 != WALL ? P3 : P4 != WALL ? P4 : P5 != WALL ? P5 : ++maxReachable;
            reachableTable[boxPos + dirOff] = isNewOpeningReachable ? currentReachable : minFillNeighbours;
            reachableTable[boxPos] = BOX;

            if (playerPosition == boxPos) { // can only happen if move = Left or Up
                while (reachableTable[++playerPosition] != currentReachable) {}
            }

            if (decision == 0) return;


            if ((decision & PlayerReachable.C3) != 0) {
                var minPos = Filler.FillOverWrite(reachableTable, level.width, boxPos - pDirOff + dirOff, P3, minFillNeighbours);
                if (minFillNeighbours == currentReachable && minPos < playerPosition) playerPosition = minPos;
            }

            if ((decision & PlayerReachable.C4) != 0) {
                var minPos = Filler.FillOverWrite(reachableTable, level.width, boxPos + 2*dirOff, P4, minFillNeighbours);
                if (minFillNeighbours == currentReachable && minPos < playerPosition) playerPosition = minPos;
            }

            if ((decision & PlayerReachable.C5) != 0) {
                var minPos = Filler.FillOverWrite(reachableTable, level.width, boxPos + pDirOff + dirOff, P5, minFillNeighbours);
                if (minFillNeighbours == currentReachable && minPos < playerPosition) playerPosition = minPos;
            }
            var P1overfill = false;

            if ((decision & PlayerReachable.O2) != 0) {
                if (!P1overfill) {
                    // over-fill P1
                    var minPos = Filler.FillOverWrite(reachableTable, level.width, boxPos - dirOff, P1, currentReachable = ++maxReachable);
                    if (minPos != int.MaxValue) playerPosition = minPos;
                    P1overfill = true;
                } else {
                    Filler.FillOverWrite(reachableTable, level.width, boxPos - pDirOff, P2, ++maxReachable);
                }
            }

            if ((decision & PlayerReachable.O6) != 0) {
                if (!P1overfill) {
                    // over-fill P1
                    var minPos = Filler.FillOverWrite(reachableTable, level.width, boxPos - dirOff, P1, currentReachable = ++maxReachable);
                    if (minPos != int.MaxValue) playerPosition = minPos;
                    P1overfill = true;
                } else {
                    Filler.FillOverWrite(reachableTable, level.width, boxPos + pDirOff, P6, ++maxReachable);
                }
            }
            */
        }

        public ulong GetZHash()
        {
            if (!reachableValid) UpdatePlayerReachableMap();
            return boxZhash ^ level.playerZbits[playerPosition];
        }

        // private bool IsPlayerReachable(int position)
        // {
        //     return reachableTable[position] == currentReachable;
        // }

        internal int GetPlayerPositionFor(Move move) {
            return move.BoxPos - level.dirOffset[move.Direction];
        }

        internal string FindPlayerPath(int playerPos, Move move)
        {
            return FindPlayerPath(playerPos, GetPlayerPositionFor(move));
        }

        internal string FindPlayerPath(int playerPos, int targetPos)
        {
            var width = level.width;
            ClearReachableTable();
            reachableTable[targetPos] = 1;

            int distance = 0;

            Filler.Fill(reachableTable, width, targetPos, 
                pos => { distance = reachableTable[pos] + 1; return pos == playerPos; },
                (value, pos) => value == 0 ? distance : -1
            );

            var sb = new StringBuilder();
            while (playerPos != targetPos) {
                var dist = reachableTable[playerPos] - 1;
                if (reachableTable[playerPos + 1] == dist) { playerPos++; sb.Append('r'); }
                else if (reachableTable[playerPos - 1] == dist) { playerPos--; sb.Append('l'); }
                else if (reachableTable[playerPos + width] == dist) { playerPos += width; sb.Append('d'); }
                else if (reachableTable[playerPos - width] == dist) { playerPos -= width; sb.Append('u'); }
            }
            return sb.ToString();
        }

    }
}