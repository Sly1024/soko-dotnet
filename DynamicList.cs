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

        public void Add(T state) 
        {
            EnsureCapacity(idx);
            items[idx++] = state;
        }

        private void EnsureCapacity(int size)
        {
            if (items.Length <= size) {
                var newItems = new T[size+size];
                Array.Copy(items, newItems, size);
                items = newItems;
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