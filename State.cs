using System.Text;
using System;

namespace soko
{
    public class State
    {
        Level level;
        BoxPositions boxPositions;
        DeadlockPatterns deadlocks;
        ulong boxZhash;

        public PlayerReachable reachable, prevReachable;

        public State(Level level, int[] initialBoxPositions, int initialPlayerPosition, DeadlockPatterns deadlocks)
        {
            this.level = level;
            this.deadlocks = deadlocks;
            boxPositions = new BoxPositions(level.table.Length, initialBoxPositions);

            boxZhash = level.GetZHashForBoxes(initialBoxPositions);
            reachable = new PlayerReachable(level, initialBoxPositions, initialPlayerPosition);
            prevReachable = new PlayerReachable(level, initialBoxPositions, initialPlayerPosition);
        }

        public int GetPossiblePushMoves(MoveRanges moves, Move cameFrom)
        {
            reachable.CalculateMap();

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
                    if (reachable[boxPos - offset]) {
                        // if IsBoxOtherSideReachable == false, cameFromBoxPos==0, so this will quickly fail
                        if (boxPos == cameFromBoxPos && offset == -cameFromOffset) {
                            // this is the move that basically undoes the move `cameFrom`, so we don't need to check it, the resulting state is 
                            // from where we got to the current state
                            continue;
                        }

                        if (!reachable.Blocked(boxPos + offset) && !level.pushDeadCells[boxPos + offset]) {
                            moves.AddRangeItem((boxPos, dir, otherSideReachable: reachable[boxPos + offset]));
                        }
                    }
                }
            }

            return moves.FinishAddRange();
        }
        
        public int GetPossiblePullMoves(MoveRanges moves, Move cameFrom)
        {
            reachable.CalculateMap();

            int cameFromOffset = 0;
            int cameFromBoxPos = 0;

            // if IsBoxOtherSideReachable is false, we don't want to calculate these, just leave them as 0
            if (cameFrom.IsBoxOtherSideReachable) {
                cameFromOffset = Level.DirOffset[cameFrom.Direction];
                cameFromBoxPos = cameFrom.BoxPos;
            }

            moves.StartAddRange();

            foreach (var boxPos in boxPositions.list)
            {
                for (var dir = 0; dir < 4; dir++)
                {
                    var offset = Level.DirOffset[dir];
                    if (reachable[boxPos - offset]) {
                        // if IsBoxOtherSideReachable == false, cameFromBoxPos==0, so this will quickly fail
                        if (boxPos == cameFromBoxPos && offset == -cameFromOffset) {
                            // this is the move that basically undoes the move `cameFrom`, so we don't need to check it, the resulting state is 
                            // from where we got to the current state
                            continue;
                        }
                        if (!reachable.Blocked(boxPos - 2*offset) && !level.pullDeadCells[boxPos - offset]) {
                            moves.AddRangeItem((boxPos - offset, dir, otherSideReachable: reachable[boxPos + offset]));
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

            reachable.ApplyPushMove(boxPos, newBoxPos);

            // update deadlocks
            deadlocks.UpdateRemaining(newBoxPos, -1);
            deadlocks.UpdateRemaining(boxPos, 1);
        }
        
        public void ApplyPullMove(Move move)
        {
            var offset = Level.DirOffset[move.Direction];
            var newBoxPos = move.BoxPos;
            var boxPos = newBoxPos + offset;

            reachable.ApplyPullMove(boxPos, newBoxPos, offset);

            // update boxPositions
            boxPositions.Move(boxPos, newBoxPos);

            // update boxZhash
            boxZhash ^= level.boxZbits[boxPos] ^ level.boxZbits[newBoxPos];

            // update deadlocks
            deadlocks.UpdateRemaining(newBoxPos, -1);
            deadlocks.UpdateRemaining(boxPos, 1);
        }

        public bool ApplyPullMoveAndCheckDeadlock(Move move)
        {
            var offset = Level.DirOffset[move.Direction];
            var newBoxPos = move.BoxPos;
            var boxPos = newBoxPos + offset;

            var restoreValues = reachable.ApplyPullMove(boxPos, newBoxPos, offset);

            var isDeadlock = deadlocks.UpdateRemaining(newBoxPos, -1);
            // update/check for deadlock
            if (!isDeadlock && reachable.isBoxPullDeadLocked(newBoxPos)) {
                reachable.StoreDeadlock(deadlocks);
                isDeadlock = true;
            }

            if (isDeadlock) {
                // found a deadlock pattern, we undo all operations we did this far
                deadlocks.UpdateRemaining(newBoxPos, 1);
                reachable.UnApplyPullMove(boxPos, newBoxPos, restoreValues);
                return false;
            }

            deadlocks.UpdateRemaining(boxPos, 1);

            // update boxPositions
            boxPositions.Move(boxPos, newBoxPos);

            // update boxZhash
            boxZhash ^= level.boxZbits[boxPos] ^ level.boxZbits[newBoxPos];

            return true;
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

            if (pull) reachable.ApplyPullMove(boxPos, newBoxPos, offset); 
            else reachable.ApplyPushMove(boxPos, newBoxPos);

            // update deadlocks
            deadlocks.UpdateRemaining(newBoxPos, -1);
            deadlocks.UpdateRemaining(boxPos, 1);
        }

        public ulong GetZHash()
        {
            reachable.CalculateMap();
            return boxZhash ^ level.playerZbits[reachable.playerPosition];
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
            reachable.ClearTable();
            reachable.table[targetPos] = 1;

            int distance = 0;

            Filler.Fill(reachable.table, width, targetPos, 
                pos => { distance = reachable.table[pos] + 1; return pos == playerPos; },
                (value, pos) => value == 0 ? distance : -1
            );

            var sb = new StringBuilder();
            while (playerPos != targetPos) {
                var dist = reachable.table[playerPos] - 1;
                if (reachable.table[playerPos + 1] == dist) { playerPos++; sb.Append('r'); }
                else if (reachable.table[playerPos - 1] == dist) { playerPos--; sb.Append('l'); }
                else if (reachable.table[playerPos + width] == dist) { playerPos += width; sb.Append('d'); }
                else if (reachable.table[playerPos - width] == dist) { playerPos -= width; sb.Append('u'); }
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

        public void CopyPrevReachable() {
            reachable.CalculateMap();
            prevReachable.CopyFrom(reachable);
        }
    }
}