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

        private int maxHop = 31;
        private int[] hop;
        private HashEntry<TValue>[] entries;
        private int count = 0;
        private float loadFactor;

        public int Count { get => count; }

        public CompactHashTable(int minimumSize, float loadFactor = 0.75f): this(minimumSize) 
        {
            this.loadFactor = loadFactor;
        }
        public CompactHashTable(int minimumSize)
        {
            entries = new HashEntry<TValue>[FindPrimeAbove(minimumSize)];
            hop = new int[entries.Length];
        }

        public bool TryAdd(ulong zHash, TValue value)
        {
            var addCode = InternalAdd(zHash, value);
            if (addCode == 1) {
                GrowTable();
                addCode = InternalAdd(zHash, value);
            }
            if (addCode == 1) {
                throw new IndexOutOfRangeException($"CompactHash table is fully full");
            }
            if (addCode == 0) {
                ++count;
                return true;
            }
            return false;
        }

        public bool ContainsKey(ulong zHash)
        {
            return Lookup(zHash) >= 0;
        }

        public TValue this[ulong zHash] 
        {
            get => entries[Lookup(zHash)].value;
        }


        // linear probing
        private int Lookup(ulong zHash)
        {
            int size = entries.Length;
            int idx = (int)(zHash % (ulong)size);
            if (hop[idx] == 0) return -1;

            int maxIdx = (idx + maxHop) % size;
            // linear search
            ref HashEntry<TValue> state = ref entries[idx];
            while (idx != maxIdx && state.key != zHash) {
                if (++idx == size) idx = 0;
                state = ref entries[idx];
            }

            return idx == maxIdx ? -1 : idx;
        }

        // returns: 
        // -1 = already have key
        // 0 = OK, added
        // 1 = need to grow tables
        private int InternalAdd(ulong key, TValue value)
        {
            int size = entries.Length;
            int bucket = (int)(key % (ulong)size);

            if (hop[bucket] == (1 << maxHop) - 1) {
                // this bucket neighborhood is full
                return 1;
            }

            int idx = bucket;
            // linear search
            ref HashEntry<TValue> entry = ref entries[idx];
            while (entry.key != 0 && entry.key != key) {
                if (++idx == size) idx = 0;
                entry = ref entries[idx];
            }
            if (entry.key != 0) return -1;

            // now idx is the empty slot
            while ((idx - bucket + size) % size >= maxHop) {
                var foundEntry = false;
                for (var k = maxHop-1; !foundEntry && k > 0; --k) {
                    // find an item that hashes to [idx - k] 
                    var hopIdx = (idx - k + size) % size;
                    var hopBits = hop[hopIdx];
                    for (var b = 0; b < k; ++b) {
                        // hopbits are in reverse order, LSB is for the first index in the neighborhood
                        if ((hopBits & (1 << b)) != 0) {
                            // found an item we can move
                            var newIdx = (idx - k + b + size) % size;
                            entries[idx] = entries[newIdx];
                            idx = newIdx;

                            // move the bit in hopBits
                            hop[hopIdx] = hopBits & ~(1 << b) | (1 << k);
                            foundEntry = true;
                            break;
                        }
                    }
                }
                if (!foundEntry) {
                    // in case we moved an item, and didn't clear out the new "empty" slot
                    entries[idx].key = 0;
                    return 1;
                }
            }

            // finally we can store value at idx
            entries[idx] = new HashEntry<TValue> { key = key, value = value };

            // and add the hop bit
            hop[bucket] |= 1 << (idx - bucket);

            return 0;
        }

        private void GrowTable()
        {
            // Console.WriteLine($"Growing table, ratio: {count * 100 / states.Length} %");

            var oldEntries = entries;
            entries = new HashEntry<TValue>[FindPrimeAbove(oldEntries.Length * 7 / 4)];   // 1.75x
            hop = new int[entries.Length];

            foreach (var item in oldEntries) {
                if (item.key != 0) {
                    if (InternalAdd(item.key, item.value) == 1) {
                        throw new Exception("Ooops, need to grow the table again?");
                    }
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
            // https://en.wikipedia.org/wiki/Quadratic_probing - need to be a prime congruent to 3 mod 4
            while (n % 4 != 3 || !IsPrime(n)) n += 2;
            return n;
        }

    }
}