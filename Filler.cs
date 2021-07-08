using System;
using System.Collections.Generic;

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

        public static void FillBoundsCheck<T>(T[] table, int width, int startPos, Func<T, bool> isEmpty, T fillWith) {
            var queue = new Queue<int>();
            queue.Enqueue(startPos);

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
    }
}