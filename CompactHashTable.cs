using System;
using System.Runtime.InteropServices;

namespace soko 
{
    public class CompactHashTable<TValue>
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct HashEntry<TEntryValue> {
            public ulong key;
            public TEntryValue value;
        }

        private HashEntry<TValue>[] entries;
        private int count = 0;
        private float loadFactor;
        private int sizeTimesLoadFactor;

        private readonly Object sync = new Object();

        public int Count { get => count; }

        public CompactHashTable(int minimumSize, float loadFactor = 0.75f)
        {
            this.loadFactor = loadFactor;
            entries = new HashEntry<TValue>[FindPrimeAbove(minimumSize)];
            sizeTimesLoadFactor = (int)(entries.Length * loadFactor);
        }

        public bool TryAdd(ulong key, TValue value)
        {
            if (InternalAdd(key, value)) {
                ++count;
                CheckLoadFactor();
                return true;
            }
            return false;
        }

        public bool ContainsKey(ulong key)
        {
            return entries[FindEntry(key)].key != 0;
        }

        public TValue this[ulong key] 
        {
            get => entries[FindEntry(key)].value;
        }

        // linear probing
        private int FindEntry(ulong key)
        {
            lock (sync) {
                int size = entries.Length;
                int idx = (int)(key % (ulong)size);
                ref HashEntry<TValue> item = ref entries[idx];

                while (item.key != 0 && item.key != key) {
                    if (++idx == size) idx = 0;
                    item = ref entries[idx];
                }
                return idx;
            }
        }


        private bool InternalAdd(ulong key, TValue value)
        {
            lock (sync) {
                int idx = FindEntry(key);
                if (entries[idx].key == key) return false;
                entries[idx] = new HashEntry<TValue> { key = key, value = value };
                return true;
            }
        }

        private void CheckLoadFactor()
        {
            if (count > sizeTimesLoadFactor) lock (sync) {
                var oldEntries = entries;
                entries = new HashEntry<TValue>[FindPrimeAbove(entries.Length * 7 / 4)];   // 1.75x
                sizeTimesLoadFactor = (int)(entries.Length * loadFactor);

                foreach (var item in oldEntries) {
                    InternalAdd(item.key, item.value);
                }
            }
        }

        /** util functions - move them somewhere else? **/

        // https://en.wikipedia.org/wiki/Primality_test
        bool IsPrime(int n)
        {
            if (n == 2 || n == 3) return true;

            if (n <= 1 || n % 2 == 0 || n % 3 == 0) return false;

            for (int i = 5; i * i <= n; i += 6) {
                if (n % i == 0 || n % (i + 2) == 0) return false;
            }

            return true;
        }

        int FindPrimeAbove(int n)
        {
            // for now, just check the odd numbers
            if ((n & 1) == 0) n++;
            while (!IsPrime(n)) n += 2;
            return n;
        }

    }
}