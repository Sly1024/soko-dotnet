using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace soko
{
    public static class Cell
    {
        public const int Empty = 0;
        public const int Wall = 1;
        public const int Goal = 2;
        public const int Box = 4;
        public const int Player = 8;
        public const int PushDeadCell = 16;
        public const int PullDeadCell = 32;

        public static bool has(this int cell, int value) {
            return (cell & value) != Cell.Empty;
        }
    }

    public class Level
    {
        public readonly int[] table;
        public int width;
        public int playerPosition = -1;
        public int[] boxPositions;
        public int[] goalPositions;

        public ulong[] boxZbits;
        public ulong[] playerZbits;

        public static int[] DirOffset;

        public HeuristicDistances distances;
        public BitArray pushDeadCells;
        public BitArray pullDeadCells;

        public Level(int[] table, int width)
        {
            this.table = table;
            this.width = width;

            // L, R, U, D
            DirOffset = [-1, 1, -width, width];

            PreProcessTable();
            Filler.FillPerimeter(table, width, Cell.Wall);

            distances = new HeuristicDistances(this);

            DetectDeadCells();
            CountDeadCells();
            GenerateZobristBitstrings();
        }

        private void CountDeadCells()
        {
            var dead = 0;
            var free = 0;
            for (var i = 0; i < table.Length; i++)
            {
                var cell = table[i];
                if (cell.has(Cell.PushDeadCell)) dead++; else if (!cell.has(Cell.Wall)) free++;
            }
            Console.WriteLine("Dead: " + dead + " Free: " + free);
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
            pushDeadCells = new BitArray(table.Length);
            pullDeadCells = new BitArray(table.Length);

            for (var i = 0; i < table.Length; i++) {
                if (!table[i].has(Cell.Wall | Cell.Goal) && distances.Pushes[i] == null) {
                    table[i] |= Cell.PushDeadCell;
                    pushDeadCells[i] = true;
                }
                if (!table[i].has(Cell.Wall | Cell.Box) && distances.Pulls[i] == null) {
                    table[i] |= Cell.PullDeadCell;
                    pullDeadCells[i] = true;
                }
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

        static readonly Dictionary<char, int> cell_map = new()
        {
            { ' ', Cell.Empty },
            { '#', Cell.Wall },
            { '@', Cell.Player },
            { '+', Cell.Player | Cell.Goal },
            { '$', Cell.Box },
            { '*', Cell.Box | Cell.Goal },
            { '.', Cell.Goal },
        };

        public static Level Parse(string text)
        {
            var lines = text.Split(["\r", "\n", "\r\n"], StringSplitOptions.RemoveEmptyEntries);
            var width = lines.Max(line => line.Length);
            var cells = new List<int>();

            foreach (var line in lines)
            {
                var column = 0;
                foreach (var ch in line)
                {
                    if (cell_map.TryGetValue(ch, out int cell)) {
                        cells.Add(cell);
                    } else {
                        throw new ArgumentException($"Invalid character {ch}.");
                    }
                    column++;
                }
                while (column++ < width) cells.Add(Cell.Empty);
            }
            return new Level([.. cells], width);
        }
    }
}