using System.Text;
using System;
using System.Collections.Generic;

namespace soko
{
    public class PriorityQueue<TElement>
    {
        int lowestPriority = int.MaxValue;
        int count = 0;

        DynamicList<Queue<TElement>> entries = new DynamicList<Queue<TElement>>(16);

        public void Enqueue(TElement element, int priority) {
            (entries[priority] ??= new Queue<TElement>(4)).Enqueue(element);
            if (priority < lowestPriority) lowestPriority = priority;
            count++;
            // System.Console.WriteLine($"Eq (count = {count}, prio = {priority}, lowest = {lowestPriority})");
        }

        public TElement Dequeue() 
        {
            if (count == 0) return default(TElement);
            count--;

            do {
                var list = entries[lowestPriority];
                if (list == null || list.Count == 0) ++lowestPriority; else return list.Dequeue();
            } while (lowestPriority < entries.Count);

            throw new InvalidOperationException("This should never happen!");
        }

        public int Count => count;

        internal string GetTop3Count()
        {
            var top3 = new List<(int idx, int cnt)>();
            for (int i = 0; i < entries.items.Length; i++) {
                var entry = entries.items[i];
                if (entry?.Count > 0) {
                    top3.Add((i, entry.Count));
                    if (top3.Count == 3) break;
                }
            }
            string result = "";
            foreach (var (idx, cnt) in top3)
            {
                result += $"[{idx}]:{cnt} ";
            }

            return result;
        }
    }
}