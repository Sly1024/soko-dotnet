using System.Text;
using System;

using System.Collections.Generic;

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

        private Filler2 filler = new Filler2();

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

            playerPosition = filler.Fill(reachableTable, width, playerPosition, currentReachable);

            reachableValid = true;
        }

        public int GetPossiblePushMoves(MoveRanges moves, Move cameFrom)
        {
            if (!reachableValid) CalculatePlayerReachableMap();

            int cameFromOffset = 0;
            int cameFromBoxPos = 0;

            // if IsBoxOtherSideReachable is false, we don't want to calculate these, just leave them as 0
            if (cameFrom.IsBoxOtherSideReachable) {
                cameFromOffset = Level.DirOffset[cameFrom.Direction];
                cameFromBoxPos = cameFrom.BoxPos + cameFromOffset;
            }

            moves.StartAddRange();

            foreach (var boxPos in boxPositions.list) {
                for (var dir = 0; dir < 4; dir++) {
                    var offset = Level.DirOffset[dir];
                    if (reachableTable[boxPos - offset] == currentReachable) {
                        // if IsBoxOtherSideReachable == false, cameFromBoxPos==0, so this will quickly fail
                        if (boxPos == cameFromBoxPos && offset == -cameFromOffset) {
                            // this is the move that basically undoes the move `cameFrom`, so we don't need to check it, the resulting state is 
                            // from where we got to the current state
                            continue;
                        }

                        if (reachableTable[boxPos + offset] < BLOCKED && !level.pushDeadCells[boxPos + offset]) {
                            bool otherSideReachable = reachableTable[boxPos + offset] == currentReachable;
                            moves.AddRangeItem((boxPos, dir, otherSideReachable));
                        }
                    }
                }
            }

            return moves.FinishAddRange();
        }
        
        public int GetPossiblePullMoves(MoveRanges moves, Move cameFrom)
        {
            if (!reachableValid) CalculatePlayerReachableMap();

            int cameFromOffset = 0;
            int cameFromBoxPos = 0;

            // if IsBoxOtherSideReachable is false, we don't want to calculate these, just leave them as 0
            if (cameFrom.IsBoxOtherSideReachable) {
                cameFromOffset = Level.DirOffset[cameFrom.Direction];
                cameFromBoxPos = cameFrom.BoxPos - cameFromOffset;
            }

            moves.StartAddRange();

            foreach (var boxPos in boxPositions.list)
            {
                for (var dir = 0; dir < 4; dir++)
                {
                    var offset = Level.DirOffset[dir];
                    if (reachableTable[boxPos - offset] == currentReachable) {
                        // if IsBoxOtherSideReachable == false, cameFromBoxPos==0, so this will quickly fail
                        if (boxPos == cameFromBoxPos && offset == -cameFromOffset) {
                            // this is the move that basically undoes the move `cameFrom`, so we don't need to check it, the resulting state is 
                            // from where we got to the current state
                            continue;
                        }
                        if (reachableTable[boxPos - 2*offset] < BLOCKED && !level.pullDeadCells[boxPos - offset]) {
                            bool otherSideReachable = reachableTable[boxPos + offset] == currentReachable;
                            moves.AddRangeItem((boxPos - offset, dir, otherSideReachable));
                        }
                    }
                }
            }

            return moves.FinishAddRange();
        }

        public void ApplyPushMove(Move move)
        {
            var boxPos = move.BoxPos;
            var newBoxPos = move.NewBoxPos;

            // update boxPositions
            boxPositions.Move(boxPos, newBoxPos);

            // update boxZhash
            boxZhash ^= level.boxZbits[boxPos] ^ level.boxZbits[newBoxPos];

            reachableTable[newBoxPos] = BOX;
            reachableTable[boxPos] = currentReachable;
            playerPosition = boxPos;

            reachableValid = false;
        }
        
        public void ApplyPullMove(Move move)
        {
            var offset = Level.DirOffset[move.Direction];
            var newBoxPos = move.BoxPos;
            var boxPos = newBoxPos + offset;

            // update boxPositions
            boxPositions.Move(boxPos, newBoxPos);

            // update boxZhash
            boxZhash ^= level.boxZbits[boxPos] ^ level.boxZbits[newBoxPos];

            reachableTable[newBoxPos] = BOX;
            reachableTable[boxPos] = 0;  // TODO: reachable?? Doesn't matter, we set reachableValid = false
            playerPosition = newBoxPos - offset;

            reachableValid = false;
        }

        public void ApplyMove(Move move, bool pull)
        {
            var offset = Level.DirOffset[move.Direction];
            var boxPos = move.BoxPos;
            var newBoxPos = boxPos;

            if (pull) boxPos += offset; else newBoxPos += offset;

            // update boxPositions
            boxPositions.Move(boxPos, newBoxPos);

            // update boxZhash
            boxZhash ^= level.boxZbits[boxPos] ^ level.boxZbits[newBoxPos];

            reachableTable[newBoxPos] = BOX;
            reachableTable[boxPos] = pull ? 0 : currentReachable;  // TODO: reachable?? Doesn't matter, we set reachableValid = false
            playerPosition = pull ? newBoxPos - offset : boxPos;

            reachableValid = false;
        }

        public bool isBoxDeadLocked(int boxPos)
        {
            boxPositions.ResetMarked();
            
            if (isBoxMovable(boxPos)) return false;

            var blockedBoxes = boxPositions.markedList;
            for (int i = boxPositions.markedCount-1; i >= 0; --i) {
                if (!level.table[blockedBoxes[i]].has(Cell.Goal)) return true;
            }

            return false;
        }

        private bool isBoxMovable(int boxPos) 
        {
            if (boxPositions.IsMarked(boxPos)) return false;

            // if it's free either horizontally or vertically, then it's movable
            int H1 = reachableTable[boxPos - 1];
            int H2 = reachableTable[boxPos + 1];
            if (H1 < BLOCKED && H2 < BLOCKED) return true;

            var w = level.width;
            int V1 = reachableTable[boxPos - w];
            int V2 = reachableTable[boxPos + w];
            if (V1 < BLOCKED && V2 < BLOCKED) return true;

            // temporarily mark the box so we avoid recursive loops
            boxPositions.MarkBox(boxPos);

            // at this point we know that both directions are blocked, need to check if the neighbour boxes are movable

            // Note: The middle "&" is NOT a short-circuiting operator.
            // We need that so that isBoxMovable() is called for the other side (H2/V2) even if the left-hand is false
            // to mark the boxes "blocked" and isBoxDeadLocked() will check if they are on a goal position
            // Imagine the following case: we push the lower box up into the corner (the only goal position)
            // ####    ####
            // #.$  => #*$
            // #$      #
            // Now (... || H1 < BLOCKED) fails because there's a WALL to the left of the box in the corner, but we still need to mark the 
            // box on the right as blocked so we can check, and if it's not on a goal position, this is a deadlock!
            if ((H1 == BOX && isBoxMovable(boxPos - 1) || H1 < BLOCKED) & (H2 == BOX && isBoxMovable(boxPos + 1) || H2 < BLOCKED) ||
                (V1 == BOX && isBoxMovable(boxPos - w) || V1 < BLOCKED) & (V2 == BOX && isBoxMovable(boxPos + w) || V2 < BLOCKED))
            {
                boxPositions.UnmarkBox(boxPos);
                return true;
            }

            return false;
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
            return move.BoxPos - Level.DirOffset[move.Direction];
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
        DynamicList<(int box, int goal, int dist)> distArr1 = new DynamicList<(int box, int goal, int dist)>(100);
        DynamicList<(int box, int goal, int dist)> distArr2 = new DynamicList<(int box, int goal, int dist)>(100);

        public int GetHeuristicPushDistance() {
            return level.distances.GetHeuristicDistance(boxPositions.list, true, distArr1);
        }

        public int GetHeuristicPullDistance() {
            return level.distances.GetHeuristicDistance(boxPositions.list, false, distArr2);
        }
    }
}