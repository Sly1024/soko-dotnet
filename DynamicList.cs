using System;
namespace soko
{
    public class DynamicList<T>
    {
        public T[] items;
        public int idx = 0;

        public DynamicList(int capacity)
        {
            items = new T[capacity];
        }

        public void Add(T item) 
        {
            EnsureCapacity(idx + 1);
            items[idx++] = item;
        }

        public T Pop() 
        {
            return items[--idx];
        }

        public void EnsureCapacity(int size)
        {
            if (items.Length < size) {
                var newItems = new T[Math.Max(items.Length*3/2, size)];
                Array.Copy(items, newItems, idx);
                items = newItems;
            }
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

        public T this[int idx] {
            get {
                EnsureCapacity(idx+1);
                return items[idx];
            }
            set {
                EnsureCapacity(idx+1);
                items[idx] = value;
            }
        }
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