using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace soko
{
    public class Filler
    {
        public static void Fill(int[] table, int width, int startPos, Func<int, bool> isDone, Func<int, int, int> fillWith) {
            var queue = new Queue<int>();
            queue.Enqueue(startPos);

            while (queue.Count > 0) {
                var pos = queue.Dequeue();
                if (isDone(pos)) return;
                
                checkPosition(pos + 1);
                checkPosition(pos - 1);
                checkPosition(pos + width);
                checkPosition(pos - width);
            }

            void checkPosition(int pos)
            {
                var value = fillWith(table[pos], pos);
                if (value != -1) { table[pos] = value; queue.Enqueue(pos); }
            }
        }

        public static int Fill2(int[] table, int width, int startPos, int reachable) {
            var list = new Stack<int>();
            table[startPos] = reachable;
            list.Push(startPos);

            while (list.Count > 0) {
                var pos = list.Pop();
                if (pos < startPos) startPos = pos;
                
                checkPosition(pos + 1);
                checkPosition(pos - 1);
                checkPosition(pos + width);
                checkPosition(pos - width);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void checkPosition(int pos)
            {
                if (table[pos] < reachable) { table[pos] = reachable; list.Push(pos); }
            }
            return startPos;
        }

        public static int Fill3(int[] table, int width, int startPos, int reachable) {

            table[startPos] = reachable;
            var left = startPos-1;
            while (table[left] < reachable) table[left--] = reachable;
            ++left; 
            var right = startPos+1;
            while (table[right] < reachable) table[right++] = reachable;
            --right;

            if (left < startPos) startPos = left;

            var list = new Stack<(int, int, int)>();
            list.Push((left-width, right-width, -width));
            list.Push((left+width, right+width, +width));

            while (list.Count > 0) {
                var (x1, x2, dir) = list.Pop();
           
                if (x1 == x2 && table[x1] >= reachable) continue;

                var sx = x1;
                while (table[sx] < reachable) table[sx--] = reachable;
                ++sx;

                var x = x1 + 1;
                while (x <= x2 || x == x2+1 && x1 == x2) {
                    while (table[x] < reachable) table[x++] = reachable;
                    --x;
                    if (sx <= x) {
                        if (sx < startPos) startPos = sx;
                        list.Push((sx+dir, x+dir, dir));

                        if (sx < x1-1) list.Push((sx-dir, x1-2-dir, -dir));
                        if (x2+1 < x) list.Push((x1+2-dir, x-dir, -dir));
                    }
                    if (x >= x2-1) break;
                    sx = x + 1;
                    while (sx <= x2 && table[sx] >= reachable) ++sx;
                    x = sx;
                }
            }

            return startPos;
        }

        public static void FillBoundsCheck<T>(T[] table, int width, int startPos, Func<T, bool> isEmpty, T fillWith) {
            var queue = new Queue<int>();
            queue.Enqueue(startPos);
            table[startPos] = fillWith;
            
            while (queue.Count > 0) {
                var pos = queue.Dequeue();
                
                if (pos % width < width-1) checkPosition(pos + 1);
                if (pos % width > 0) checkPosition(pos - 1);
                if (pos + width < table.Length) checkPosition(pos + width);
                if (pos - width >= 0) checkPosition(pos - width);
            }

            void checkPosition(int pos)
            {
                if (isEmpty(table[pos])) { table[pos] = fillWith; queue.Enqueue(pos); }
            }
        }

        public static void FillWith<T>(T[] table, int width, int position, T value) where T : IEquatable<T>
        {
            if (!table[position].Equals(value)) {
                FillBoundsCheck(table, width, position, (cell) => !cell.Equals(value), value);
            }
        }

        public static IEnumerable<int> GetPerimeterCells(int tableSize, int width)
        {
            for (var i = 0; i < width; i++) {
                yield return i;
                yield return tableSize - 1 - i;
            }

            for (var i = tableSize / width - 2; i > 0; i--) {
                yield return i * width;
                yield return i * width + width - 1;
            }
        }

        public static void FillPerimeter<T>(T[] table, int width, T value) where T : IEquatable<T>
        {
            foreach (var cell in GetPerimeterCells(table.Length, width)) {
                FillWith(table, width, cell, value);
            }
        }
    }
}