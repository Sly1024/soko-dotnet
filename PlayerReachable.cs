using System;
using System.Text;

namespace soko
{
    public class PlayerReachable
    {
        const int WALL = int.MaxValue;
        const int BOX = int.MaxValue-1;

        public void GenerateMoveTable()
        {
            const int width = 6;

            // ######   # = wall to prevent overflow
            // #1234#   @ = player
            // #@$_9#   $ = box
            // #5678#   _ = empty cell
            // ######   1..9 = cell to place wall/empty
            // index of the first fillable cell is 7
            // bits in position mask: (MSB) 123456789 (LSB)
            int[] table = new int[width * 5];
            
            foreach (var cell in Filler.GetPerimeterCells(table.Length, width)) {
                table[cell] = WALL;
            }

            for (var bits = 0; bits < 1<<9; bits++)
            {
                // fill the table
                FillTableFromBits(table, width, bits);
                FillReachable(table, width);
                PrintTable(table, width);
            }
        }

        private void FillReachable(int[] table, int width)
        {
            var  areaNum = 1;

            for (var i = width+1; i < table.Length-width-1; i++)
            {
                if (table[i] == 0) {
                    table[i] = areaNum;
                    Filler.Fill(table, width, i, (pos) => false, (value, pos) => value == 0 ? areaNum : -1);
                    areaNum++;
                }
            }
        }

        private static void FillTableFromBits(int[] table, int width, int bits)
        {
            // first row
            for (var p = 0; p < 4; p++) table[width + 1 + p] = (bits & (1 << 8 - p)) == 0 ? 0 : BOX;
            // last row
            for (var p = 0; p < 4; p++) table[3*width + 1 + p] = (bits & (1 << 4 - p)) == 0 ? 0 : BOX;
            // middle row
            table[2*width + 1] = 0;
            table[2*width + 2] = BOX;
            table[2*width + 3] = 0;
            table[2*width + 4] = (bits & 1) == 0 ? 0 : BOX;
        }

                // for debugging
        internal void PrintTable(int[] table, int width)
        {
            var sb = new StringBuilder("\n");
            for (var i = 0; i < table.Length; i++) {
                sb.Append(table[i] switch {
                    WALL => "#",
                    BOX => "$",
                    _ => table[i].ToString()
                });
                sb.Append(" ");
                if (i % width == width -1) sb.Append("\n");
            }
            Console.WriteLine(sb.ToString());
        }
    }
}
