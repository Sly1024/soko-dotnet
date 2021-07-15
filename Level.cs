using System;
using System.Collections.Generic;
using System.Linq;

namespace soko
{
    public enum Cell
    {
        Empty = 0,
        Wall = 1,
        Goal = 2,
        Box = 4,
        Player = 8,
        DeadCell = 16,
    }

    public static class CellExtensions 
    {
        public static bool has(this Cell cell, Cell value) {
            return (cell & value) != Cell.Empty;
        }
    }

    public class Level
    {
        public Cell[] table;
        public int width;
        public int playerPosition = -1;
        public int[] boxPositions;
        public int[] goalPositions;

        public ulong[] boxZbits;
        public ulong[] playerZbits;

        public int[] dirOffset;

        public Level(Cell[] table, int width)
        {
            this.table = table;
            this.width = width;

            dirOffset = new [] { -1, 1, -width, width };

            PreProcessTable();
            FillPerimeterWithWall();
            DetectDeadCells();
            GenerateZobristBitstrings();
        }

        private void PreProcessTable()
        {
            var boxes = new List<int>();
            var goals = new List<int>();
            
            for (var i = 0; i < table.Length; i++)
            {
                var cell = table[i];
                if (cell.has(Cell.Box)) {
                    if (cell.has(Cell.Wall)) throw new ArgumentException($"Box cannot be on Wall (pos: {i}).");
                    boxes.Add(i);
                }
                if (cell.has(Cell.Goal)) {
                    if (cell.has(Cell.Wall)) throw new ArgumentException($"Goal cannot be on Wall (pos: {i}).");
                    goals.Add(i);
                }
                if (cell.has(Cell.Player)) {
                    if (playerPosition != -1) throw new ArgumentException($"More than one player in the level ({playerPosition}, {i}).");
                    if (cell.has(Cell.Wall | Cell.Box)) throw new ArgumentException("Player cannot be on wall or box.");
                    playerPosition = i;
                }
            }

            if (playerPosition == -1) throw new ArgumentException($"Player not found.");
            if (boxes.Count != goals.Count) throw new ArgumentException($"Number of Boxes ({boxes.Count}) and Goals ({goals.Count}) dont match.");

            boxPositions = boxes.ToArray();
            goalPositions = goals.ToArray();
        }

        private void DetectDeadCells()
        {
            var cornersInRow = new Dictionary<int, List<int>>();
            var cornersInColumn = new Dictionary<int, List<int>>();

            // corner detection
            for (var i = 0; i < table.Length; i++) {
                if (table[i] != Cell.Wall && table[i] != Cell.Goal && IsCornerCell(i)) {
                    table[i] |= Cell.DeadCell;
                    var row = i / width;
                    var col = i % width;
                    cornersInRow.TryAdd(row, new List<int>());
                    cornersInRow[row].Add(i);
                    cornersInColumn.TryAdd(col, new List<int>());
                    cornersInColumn[col].Add(i);
                }
            }

            foreach (var (row, corners) in cornersInRow) {
                var pos1 = corners[0];
                for (var i = 1; i < corners.Count; i++) {
                    var pos2 = corners[i];
                    ProcessAlongWall(pos1, pos2, 1, width);
                    pos1 = pos2;
                }
            }

            foreach (var (col, corners) in cornersInColumn) {
                var pos1 = corners[0];
                for (var i = 1; i < corners.Count; i++) {
                    var pos2 = corners[i];
                    ProcessAlongWall(pos1, pos2, width, 1);
                    pos1 = pos2;
                }
            }
        }

        private void ProcessAlongWall(int pos1, int pos2, int dir, int lateralDir)
        {
            var unsafeLine = true;
            for (var i = pos1 + dir; i < pos2; i += dir) {
                if (table[i].has(Cell.Wall | Cell.Goal) || (table[i + lateralDir] != Cell.Wall && table[i - lateralDir] != Cell.Wall)) {
                    unsafeLine = false;
                    break;
                }
            }
            if (unsafeLine) {
                for (var i = pos1 + dir; i < pos2; i += dir) table[i] |= Cell.DeadCell;
            }
        }

        private bool IsCornerCell(int pos)
        {
            return (table[pos-1] | table[pos+1]).has(Cell.Wall) 
                && (table[pos-width] | table[pos+width]).has(Cell.Wall);
        }

        private void FillWithWall(int position) 
        {
            if (table[position] != Cell.Wall) {
                Filler.FillBoundsCheck(table, width, position, 
                    (value) => value != Cell.Wall,
                    Cell.Wall
                );
            }
        }

        private void FillPerimeterWithWall()
        {
            for (var i = 0; i < width; i++) {
                FillWithWall(i);
                FillWithWall(table.Length - 1 - i);
            }

            for (var i = table.Length / width - 2; i > 0; i--) {
                FillWithWall(i * width);
                FillWithWall(i * width + width - 1);
            }
        }

        private void GenerateZobristBitstrings()
        {
            var length = table.Length;
            boxZbits = new ulong[length];
            playerZbits = new ulong[length];
            var rand = new Random(1024);

            for (var i = 0; i < length; i++) if (table[i] != Cell.Wall) {
                boxZbits[i] = Get64BitZobristString(rand);
                playerZbits[i] = Get64BitZobristString(rand);
            }
        }

        private ulong Get64BitZobristString(Random rand)
        {
            // Unfortunately rand.Next() returns an int32 but with only 31 random bits, so using two of these would
            // get me 62 bits, and I would need to get 2 more random bits anyway, I decided to go with 4x16 bits.
            var a = (ulong)rand.Next(1<<16);
            var b = (ulong)rand.Next(1<<16);
            var c = (ulong)rand.Next(1<<16);
            var d = (ulong)rand.Next(1<<16);
            return (a << 48) | (b << 32) | (c << 16) | d;
        }

        public ulong GetZHashForBoxes(int[] boxPositions)
        {
            ulong hash = 0;
            foreach (var box in boxPositions) hash ^= boxZbits[box];
            return hash;
        }

        public static Level Parse(string text) 
        {
            var lines = text.Split(new []{ "\r", "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var width = lines.Max(line => line.Length);
            var cells = new List<Cell>();

            foreach (var line in lines)
            {
                var column = 0;
                foreach (var ch in line)
                {
                    switch (ch)
                    {   
                        case ' ': cells.Add(Cell.Empty);
                            break;
                        case '#': cells.Add(Cell.Wall);
                            break;
                        case '@': cells.Add(Cell.Player);
                            break;
                        case '+': cells.Add(Cell.Player | Cell.Goal);
                            break;
                        case '$': cells.Add(Cell.Box);
                            break;
                        case '*': cells.Add(Cell.Box | Cell.Goal);
                            break;
                        case '.': cells.Add(Cell.Goal);
                            break;
                        default: throw new ArgumentException($"Invalid character {ch}.");
                    }
                    column++;
                }
                while (column++ < width) cells.Add(Cell.Empty);
            }
            return new Level(cells.ToArray(), width);
        }
    }
}