using System;
using System.Text;

namespace soko
{
    public class PlayerReachable
    {
        const int width = 6;
        const int WALL = int.MaxValue;
        // const int BOX = int.MaxValue-1;

        static int[] fillPositions;
        // 8 bits: X5 X4 X3, C5 C4 C3, O6 O2
        // O - opened to player reachable
        // C - closed from player reachable
        // X - closed from other X regions
        public static byte O2 = 1 << 0;
        public static byte O6 = 1 << 1;
        public static byte C3 = 1 << 2;
        public static byte C4 = 1 << 3;
        public static byte C5 = 1 << 4;
        public static byte X3 = 1 << 5;
        public static byte X4 = 1 << 6;
        public static byte X5 = 1 << 7;
        public static byte[] decisionBits;

        public static void GenerateMoveTable()
        {
            decisionBits = new byte[1 << 9];

            int P1, P2, P3, P4, P5, P6;
            fillPositions = new [] {
                P1 = 2*width + 1,
                P2 = width + 2,
                P3 = width + 3,
                P4 = 2*width + 4,
                P5 = 3*width + 3,
                P6 = 3*width + 2
            };

            // 00 ######   # = wall to prevent overflow
            // 06 #1234#   @ = player
            // 12 #@$_9#   $ = box
            // 18 #5678#   _ = empty cell
            // 24 ######   1..9 = cell to place wall/empty
            // index of the first fillable cell is 7
            // bits in position mask: (MSB) 123456789 (LSB)
            int[] before = new int[width * 5];
            int[] after = new int[width * 5];
            
            foreach (var cell in Filler.GetPerimeterCells(before.Length, width)) {
                before[cell] = after[cell] = WALL;
            }

            int noActionCount = 0;

            for (var wallBits = 0; wallBits < 1 << 9; wallBits++)
            {
                // fill the table
                FillTableFromBits(before, width, wallBits, afterPush: false);
                FillReachable(before, width);
                FillTableFromBits(after, width, wallBits, afterPush: true);
                FillReachable(after, width);
                
                // PrintTable(before, width);
                // PrintTable(after, width);

                byte decision = 0; 

                if (after[P2] != WALL && before[P1] != before[P2] && after[P1] == after[P2]) {
                    // P2 was opened
                    decision |= O2;
                }
                if (after[P6] != WALL && before[P1] != before[P6] && after[P1] == after[P6]) {
                    // P6 was opened
                    decision |= O6;
                }

                bool FillWithNew(int P, int val) {
                    return after[P] != WALL && before[P] != val && after[P] == val;
                }

                if (FillWithNew(P3, 3)) {
                    decision |= C3;
                }

                if (FillWithNew(P4, 4)) {
                    decision |= C4;
                }

                if (FillWithNew(P5, 5)) {
                    decision |= C5;
                }

                // bool OpenToP1(int P) {
                //     return after[P1] == after[P];
                // }
                // bool ClosedFromP1(int P) {
                //     return before[P1] == before[P] && after[P1] != after[P];
                // }

                // // Mutually exclusive cases when P3,P4,P5 one is different than the other two 
                // bool NeedToFillOne(int P, int PO1, int PO2) { // P is different, PO1/2 are the others
                //     return after[P] != WALL && 
                //         !OpenToP1(P) &&    // !(opened up to P1)
                //         // before the push they are all equal (reachable via the box's new position)
                //         (after[P] != after[PO1] && after[PO1] == after[PO2] && after[PO1] != WALL); // closed from PO1/2
                // }

                // // ClosedFromP1 means it's not wall
                // if (ClosedFromP1(P3)) decision |= C3;
                // if (ClosedFromP1(P4)) decision |= C4;
                // if (ClosedFromP1(P5)) decision |= C5;

                // // fill P3 if ..
                // if (NeedToFillOne(P3, P4, P5) /* && ((decision & C3) == 0) */) {
                //     decision |= X3;
                // } else
                // // fill P4 if ..
                // if (NeedToFillOne(P4, P3, P5) /* && ((decision & C4) == 0) */) {
                //     decision |= X4;
                // } else
                // // fill P5 if ..
                // if (NeedToFillOne(P5, P3, P4) /* && ((decision & C5) == 0) */) {
                //     decision |= X5;
                // } else 
                // // when all three are equal, the only case when we need to fill them is when they got closed off from P1,
                // // but that is included in the condition in NeedToFillOne()
                // // so the last case is when all three are different (at most 1 is a wall, if 2 of them are wall -> no need to fill)
                // if (after[P3] != after[P4] && after[P4] != after[P5] && after[P5] != after[P3]) {
                //      // if any of those are non-wall, let's fill it
                //      if (after[P3] != WALL /* && ((decision & C3) == 0) */ && !OpenToP1(P3)) decision |= X3;
                //      if (after[P4] != WALL /* && ((decision & C4) == 0) */ && !OpenToP1(P4)) decision |= X4;
                //      if (after[P5] != WALL /* && ((decision & C5) == 0) */ && !OpenToP1(P5)) decision |= X5;
                // }

                decisionBits[wallBits] = decision;
                if (decision == 0) noActionCount++;

                // if ((decision & C3) != 0 && (decision & X3) != 0) {
                //     System.Console.WriteLine("x");
                // }
            }

            Console.WriteLine($" No action count {noActionCount}");
        }

        private static void FillReachable(int[] table, int width)
        {
            for (var i = 0; i < fillPositions.Length; i++) {
                var pos = fillPositions[i];
                if (table[pos] == 0) {
                    table[pos] = i+1;
                    Filler.Fill(table, width, pos, (_) => false, (value, pos) => value == 0 ? i+1 : -1);
                }
            }
        }

        private static void FillTableFromBits(int[] table, int width, int bits, bool afterPush)
        {
            // first row
            for (var p = 0; p < 4; p++) table[width + 1 + p] = (bits & (1 << 8 - p)) == 0 ? 0 : WALL;
            // last row
            for (var p = 0; p < 4; p++) table[3*width + 1 + p] = (bits & (1 << 4 - p)) == 0 ? 0 : WALL;
            // middle row
            table[2*width + 1] = 0;
            table[2*width + 2] = afterPush ? 0 : WALL;
            table[2*width + 3] = afterPush ? WALL : 0;
            table[2*width + 4] = (bits & 1) == 0 ? 0 : WALL;
        }

                // for debugging
        internal static void PrintTable(int[] table, int width)
        {
            var sb = new StringBuilder("\n");
            for (var i = 0; i < table.Length; i++) {
                sb.Append(table[i] switch {
                    WALL => "#",
                    //BOX => "$",
                    _ => table[i].ToString()
                });
                sb.Append(" ");
                if (i % width == width -1) sb.Append("\n");
            }
            Console.WriteLine(sb.ToString());
        }
    }
}
