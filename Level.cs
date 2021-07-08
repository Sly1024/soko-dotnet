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

        public Level(Cell[] table, int width)
        {
            this.table = table;
            this.width = width;
            PreProcessTable();
            FillPerimeterWithWall();
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