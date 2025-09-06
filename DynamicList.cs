using System;
namespace soko
{
    public class DynamicList<T>(int capacity)
    {
        public T[] items = new T[capacity];
        public int idx = 0;

        public void Add(T item)
        {
            if (items.Length < idx + 1) EnsureCapacity(idx + 1);
            items[idx++] = item;
        }

        public T Pop()
        {
            return items[--idx];
        }

        public void EnsureCapacity(int size)
        {
            var newItems = new T[Math.Max(items.Length * 3 / 2, size)];
            Array.Copy(items, newItems, idx);
            items = newItems;
        }

        public void Clear()
        {
            idx = 0;
        }

        public void Truncate(int size)
        {
            idx = size;
        }

        public int Count { get => idx; }

    }
    public static class DynmicListExtensions
    {
        public static int FindZhash(this DynamicList<HashState> list, ulong zHash)
        {
            for (var i = 0; i < list.idx; i++) if (list.items[i].zHash == zHash) return i;
            return -1;
        }
    }
}