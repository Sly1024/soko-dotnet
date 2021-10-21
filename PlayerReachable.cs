
using System;
using System.Text;

namespace soko
{
    public class PlayerReachable
    {
        public const int WALL = int.MaxValue;
        public const int BOX = WALL - 1;
        public const int BLOCKED = BOX;
        public const int MAX_REACHABLE = BLOCKED;

        readonly int width;

        public readonly int[] table;
        int currentReachable;
        bool valid = false;
        public bool isValid => valid;

        public int playerPosition;

        readonly MarkedList markedPositions;

        Filler2 filler = new Filler2();
        Level level;

        public PlayerReachable(Level level, int[] boxPositions, int playerPos) {
            this.level = level;
            width = level.width;
            playerPosition = playerPos;

            table = new int[level.table.Length];
            currentReachable = 1;
            FillTable(level, boxPositions);

            markedPositions = new MarkedList(table.Length, boxPositions.Length);
        }

        private void FillTable(Level level, int[] boxPositions) {
            for (var i = 0; i < table.Length; i++) {
                table[i] = level.table[i].has(Cell.Wall) ? WALL : 0;
            }
            foreach (var box in boxPositions) {
                table[box] = BOX;
            }
        }

        public void ClearTable() {
            for (var i = 0; i < table.Length; i++) {
                if (table[i] < BLOCKED) table[i] = 0;
            }
        }

        public void CalculateMap() {
            if (valid) return;

            // if currentReachable overflows
            if (++currentReachable >= MAX_REACHABLE) {
                ClearTable();
                currentReachable = 1;
            }

            playerPosition = filler.Fill(table, width, playerPosition, currentReachable);

            valid = true;
        }

        public bool this[int idx] => table[idx] == currentReachable;

        public bool Blocked(int idx) => table[idx] >= BLOCKED;

        public void Invalidate() => valid = false;

        public void ApplyPushMove(int boxPos, int newBoxPos) {
            table[newBoxPos] = BOX;
            table[boxPos] = currentReachable;
            playerPosition = boxPos;
            valid = false;
        }

        public void ApplyPullMove(int boxPos, int newBoxPos, int offset) {
            table[newBoxPos] = BOX;
            table[boxPos] = 0;  // TODO: reachable?? Doesn't matter, we set reachableValid = false
            playerPosition = newBoxPos - offset;
            valid = false;
        }


        // for debugging
        internal void PrintTable()
        {
            var sb = new StringBuilder("\n");
            for (var i = 0; i < table.Length; i++) {
                sb.Append(table[i] switch {
                    WALL => "#",
                    BOX => "$",
                    _ => this[i] ? "." : " "
                });
                sb.Append(" ");
                if (i % width == width -1) sb.Append("\n");
            }
            Console.WriteLine(sb.ToString());
            Console.WriteLine($"PlayerPos: {playerPosition}");
        }

        public bool isBoxPushDeadLocked(int boxPos)
        {
            markedPositions.ResetMarked();
            
            if (isBoxPushable(boxPos)) return false;

            // we have a list of non-movable boxes, need to check if they're all on goal positions
            var blockedBoxes = markedPositions.list;
            for (int i = markedPositions.count-1; i >= 0; --i) {
                if (!level.table[blockedBoxes[i]].has(Cell.Goal)) return true;
            }

            return false;
        }

        private bool isBoxPushable(int boxPos) 
        {
            if (markedPositions.IsMarked(boxPos)) return false;

            // if it's free either horizontally or vertically, then it's movable
            int H1 = table[boxPos - 1];
            int H2 = table[boxPos + 1];
            if (H1 < BLOCKED && H2 < BLOCKED) return true;

            var w = width;
            int V1 = table[boxPos - w];
            int V2 = table[boxPos + w];
            if (V1 < BLOCKED && V2 < BLOCKED) return true;

            // temporarily mark the box so we avoid recursive loops
            markedPositions.MarkPos(boxPos);

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
            if ((H1 == BOX ? isBoxPushable(boxPos - 1) : H1 != WALL) & (H2 == BOX ? isBoxPushable(boxPos + 1) : H2 != WALL) ||
                (V1 == BOX ? isBoxPushable(boxPos - w) : V1 != WALL) & (V2 == BOX ? isBoxPushable(boxPos + w) : V2 != WALL))
            {
                markedPositions.UnmarkPos(boxPos);
                return true;
            }

            return false;
        }

        public bool isBoxPullDeadLocked(int boxPos)
        {
            markedPositions.ResetMarked();
            
            if (isBoxPullable(boxPos)) return false;

            // we have a list of non-movable boxes, need to check if they're all on goal positions
            var blockedBoxes = markedPositions.list;
            for (int i = markedPositions.count-1; i >= 0; --i) {
                if (!level.table[blockedBoxes[i]].has(Cell.Box)) return true;
            }

            return false;
        }

        private bool isBoxPullable(int boxPos) 
        {
            if (markedPositions.IsMarked(boxPos)) return false;

            // if the next 2 cells are free in either of the 4 directions, then it's movable
            int L1 = table[boxPos - 1];
            if (L1 < BLOCKED && table[boxPos - 2] < BLOCKED) return true;

            int R1 = table[boxPos + 1];
            if (R1 < BLOCKED && table[boxPos + 2] < BLOCKED) return true;

            var w = width;
            int U1 = table[boxPos - w];
            if (U1 < BLOCKED && table[boxPos - w - w] < BLOCKED) return true;

            int D1 = table[boxPos + w];
            if (D1 < BLOCKED && table[boxPos + w + w] < BLOCKED) return true;

            // temporarily mark the box so we avoid recursive loops
            markedPositions.MarkPos(boxPos);

            // at this point we know that all 4 directions are blocked, need to check if the neighbour boxes are movable

            // Note: The middle "&" is NOT a short-circuiting operator.
            // We need that so that isBoxPullabe() is called for the other box even if the left-hand is false
            // to mark the boxes "blocked" and isBoxDeadLocked() will check if they are on a goal position

            int L2, R2, U2, D2;

            if (// left
                (L1 == BOX ? isBoxPullable(boxPos - 1) : L1 != WALL) &
                (L1 != WALL && ((L2 = table[boxPos - 2]) == BOX ? isBoxPullable(boxPos - 2) : L2 != WALL)) ||
                // right
                (R1 == BOX ? isBoxPullable(boxPos + 1) : R1 != WALL) &
                (R1 != WALL && ((R2 = table[boxPos + 2]) == BOX ? isBoxPullable(boxPos + 2) : R2 != WALL)) ||
                // up
                (U1 == BOX ? isBoxPullable(boxPos - w) : U1 != WALL) &
                (U1 != WALL && ((U2 = table[boxPos - w - w]) == BOX ? isBoxPullable(boxPos - w - w) : U2 != WALL)) ||
                // down
                (D1 == BOX ? isBoxPullable(boxPos + w) : D1 != WALL) &
                (D1 != WALL && ((D2 = table[boxPos + w + w]) == BOX ? isBoxPullable(boxPos + w + w) : D2 != WALL))
            ) {
                markedPositions.UnmarkPos(boxPos);
                return true;
            }

            return false;
        }
    }
}
