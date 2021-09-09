using System.Text;
using System;

namespace soko
{
    public class State
    {
        Level level;
        public int playerPosition;
        BoxPositions boxPositions;
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
            boxPositions = new BoxPositions(level.table.Length, initialBoxPositions);

            boxZhash = level.GetZHashForBoxes(initialBoxPositions);
            playerPosition = initialPlayerPosition;
            FillTable();
        }

        private void FillTable()
        {
            reachableTable = new int[level.table.Length];
            for (var i = 0; i < reachableTable.Length; i++) {
                reachableTable[i] = level.table[i].has(Cell.Wall) ? WALL : 0;
            }
            foreach (var box in boxPositions.list) {
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
        }

        public void CalculatePlayerReachableMap()
        {
            var width = level.width;

            // if currentReachable overflows
            if (++currentReachable >= MAX_REACHABLE) {
                ClearReachableTable();
                currentReachable = 1;
            }

            playerPosition = Filler.Fill2(reachableTable, width, playerPosition, currentReachable);

            reachableValid = true;
        }

        public int GetPossiblePushMoves(MoveRanges moves, Move cameFrom)
        {
            if (!reachableValid) CalculatePlayerReachableMap();

            int cameFromOffset = 0;
            int cameFromBoxPos = 0;

            // if IsBoxOtherSideReachable is false, we don't want to calculate these, just leave them as 0
            if (cameFrom.IsBoxOtherSideReachable) {
                cameFromOffset = level.dirOffset[cameFrom.Direction];
                cameFromBoxPos = cameFrom.BoxPos + cameFromOffset;
            }

            moves.StartAddRange();

            foreach (var boxPos in boxPositions.list) {
                for (var dir = 0; dir < 4; dir++) {
                    var offset = level.dirOffset[dir];
                    if (reachableTable[boxPos - offset] == currentReachable) {
                        // if IsBoxOtherSideReachable == false, cameFromBoxPos==0, so this will quickly fail
                        if (boxPos == cameFromBoxPos && offset == -cameFromOffset) {
                            // this is the move that basically undoes the move `cameFrom`, so we don't need to check it, the resulting state is 
                            // from where we got to the current state
                            continue;
                        }

                        // dead cells only affect push moves
                        if (reachableTable[boxPos + offset] < BLOCKED && !level.table[boxPos + offset].has(Cell.DeadCell)) {
                            bool otherSideReachable = reachableTable[boxPos + offset] == currentReachable;
                            moves.AddRangeItem((boxPos, dir, otherSideReachable));
                        }
                    }
                }
            }

            return moves.FinishAddRange();
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

        public int ApplyPushMove(Move move)
        {
            var offset = level.dirOffset[move.Direction];
            var boxPos = move.BoxPos;
            int newBoxPos = boxPos + offset;

            // update boxPositions
            boxPositions.Move(boxPos, newBoxPos);

            // update boxZhash
            boxZhash ^= level.boxZbits[boxPos] ^ level.boxZbits[newBoxPos];

            var newBoxPosReachable = reachableTable[newBoxPos];
            reachableTable[newBoxPos] = BOX;
            reachableTable[boxPos] = currentReachable;
            playerPosition = boxPos;

            if (reachableValid) {
                var pDirOff = level.width + 1 - Math.Abs(offset); // perpendicular direction offset

                //********************************************
                // @ = player,  [x] = box, move it to the right
                // +---+---+---+---+    +---+---+---+---+
                // |   |C1 |C2 |C3 |    |   |C1 |C2 |C3 |
                // +---+---+---+---+    +---+---+---+---+
                // | @ |[x]|   |C7 | -> |   | @ |[x]|C7 |
                // +---+---+---+---+    +---+---+---+---+
                // |   |C4 |C5 |C6 |    |   |C4 |C5 |C6 |
                // +---+---+---+---+    +---+---+---+---+
                // right direction: offset
                // down direction: pDirOff
                //
                // Cases when we need to do a fill:
                //  - C1 opened = C1 was not reachable (but now it is)
                //  - C4 opened = C4 was not reachable (but now it is)
                //  - C2 closed = C2 was reachable AND C1 blocked AND oneof C34567 blocked
                //  - C5 closed = C5 was reachable AND C4 blocked AND oneof C12367 blocked
                //  - C7 closed = C7 was reachable AND oneof C123 blocked AND oneof C456 blocked
                //********************************************

                if (
                    // C1 opened
                    (reachableTable[boxPos - pDirOff] != currentReachable) ||
                    // C4 opened
                    (reachableTable[boxPos + pDirOff] != currentReachable) ||
                    // C2 closed
                    ((reachableTable[boxPos - pDirOff + offset] == currentReachable) && 
                        (reachableTable[boxPos - pDirOff] >= BLOCKED) && 
                        (reachableTable[boxPos - pDirOff + 2*offset] >= BLOCKED || reachableTable[boxPos + pDirOff] >= BLOCKED || reachableTable[boxPos + pDirOff + offset] >= BLOCKED || reachableTable[boxPos + pDirOff + 2*offset] >= BLOCKED || reachableTable[boxPos + 2*offset] >= BLOCKED)
                    ) ||
                    // C5 closed
                    ((reachableTable[boxPos + pDirOff + offset] == currentReachable) && 
                        (reachableTable[boxPos + pDirOff] >= BLOCKED) && 
                        (reachableTable[boxPos - pDirOff] >= BLOCKED || reachableTable[boxPos - pDirOff + offset] >= BLOCKED || reachableTable[boxPos - pDirOff + 2*offset] >= BLOCKED || reachableTable[boxPos + pDirOff + 2*offset] >= BLOCKED || reachableTable[boxPos + 2*offset] >= BLOCKED)
                    ) ||
                    // C7 closed
                    ((reachableTable[boxPos + 2*offset] == currentReachable) && 
                        (reachableTable[boxPos - pDirOff] >= BLOCKED || reachableTable[boxPos - pDirOff + offset] >= BLOCKED || reachableTable[boxPos - pDirOff + 2*offset] >= BLOCKED) &&
                        (reachableTable[boxPos + pDirOff] >= BLOCKED || reachableTable[boxPos + pDirOff + offset] >= BLOCKED || reachableTable[boxPos + pDirOff + 2*offset] >= BLOCKED)
                    )
                )
                {
                    reachableValid = false;
                } else {
                    // no need to fill
                    return newBoxPosReachable;
                }
            }
            return 0;
        }
        
        public void ApplyPullMove(Move move, int oldBoxPosReachable = 0)
        {
            var offset = level.dirOffset[move.Direction];
            var newBoxPos = move.BoxPos;
            int boxPos = newBoxPos + offset;

            // update boxPositions
            boxPositions.Move(boxPos, newBoxPos);

            // update boxZhash
            boxZhash ^= level.boxZbits[boxPos] ^ level.boxZbits[newBoxPos];

            reachableTable[newBoxPos] = BOX;
            reachableTable[boxPos] = oldBoxPosReachable;  // TODO: reachable?? Doesn't matter, if we set reachableValid = false
            playerPosition = boxPos - offset*2;

            if (oldBoxPosReachable == 0) reachableValid = false;
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