using System;
namespace soko
{
    public class HashStateList 
    {
        public HashState[] items;
        public int idx = 0;

        public HashStateList(int capacity)
        {
            items = new HashState[capacity];
        }

        public void Add(HashState state) 
        {
            EnsureCapacity(idx);
            items[idx++] = state;
        }

        private void EnsureCapacity(int size)
        {
            if (items.Length <= size) {
                var newItems = new HashState[size+size];
                Array.Copy(items, newItems, size);
                items = newItems;
            }
        }

        public int FindZhash(ulong zHash) 
        {
            for (var i = 0; i < idx; i++) if (items[i].zHash == zHash) return i;
            return -1;
        }
    }
}