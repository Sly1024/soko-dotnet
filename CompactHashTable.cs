using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace soko 
{
    public class CompactHashTable<TValue>
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct HashEntry<TEntryValue>
        {
            public ulong key;
            public TEntryValue value;
        }

        private const ulong LOCKED_STATE = 1;
        private const int BucketSizeBits = 2;
        private const int BucketSize = 1 << BucketSizeBits;

        private class Table
        {
            public readonly int sizeBits;
            public readonly int bucketBits;
            public readonly ulong bucketMask;
            public readonly HashEntry<TValue>[] entries;

            public Table(int minimumSize)
            {
                sizeBits = FindPowerOfTwoAbove(minimumSize);
                bucketBits = sizeBits - BucketSizeBits;
                bucketMask = (1UL << bucketBits) - 1;

                entries = new HashEntry<TValue>[1 << sizeBits];
            }
        }

        private int count = 0;
        private float loadFactor;
        private int sizeTimesLoadFactor;

        private Table table;

        public int Count { get => count; }

        public CompactHashTable(int minimumSize, float loadFactor = 0.75f)
        {
            this.loadFactor = loadFactor;
            table = new Table(minimumSize);
            sizeTimesLoadFactor = (int)(table.entries.Length * loadFactor);
        }

        /// <returns>true if inserted, false if already present</returns>
        public bool TryAdd(ulong key, TValue value)
        {
            if (InternalAdd(key, value))
            {
                Interlocked.Increment(ref count);
                CheckLoadFactor();
                return true;
            }
            return false;
        }

        public TValue this[ulong key] => table.entries[FindKeyOrEmpty(table, key)].value;

        /// <returns>true if inserted, false if already present</returns>
        private bool InternalAdd(ulong key, TValue value)
        {
            while (true)
            {
                var _table = Volatile.Read(ref table);
                int idx = FindKeyOrEmpty(_table, key);
                if (_table.entries[idx].key == key) return false;

                // attempt to insert

                // first need to lock
                if (Interlocked.CompareExchange(ref _resizing, 1, 0) != 0)
                {
                    // lock failed, wait and retry 
                    Thread.SpinWait(16);
                    continue;
                }

                if (Interlocked.CompareExchange(ref table.entries[idx].key, LOCKED_STATE, 0) == 0)
                    {
                        // Success! We own this slot now.
                        // Write the value FIRST.
                        table.entries[idx].value = value;

                        // "Publish" the real key. This must be a volatile write
                        // to ensure the value write is visible before the key. This also unlocks.
                        Volatile.Write(ref table.entries[idx].key, key);
                        return true;
                    }
                // at this point, another thread might have inserted a key, so just retry with FindKeyOrEmpty (it will spin if the slot is locked)
            }
        }

        private void LockedInsert(Table table, ulong key, TValue value)
        {
            int idx = FindKeyOrEmpty(table, key);
            table.entries[idx].value = value;
            table.entries[idx].key = key;
        }


        private int _resizing = 0;
        private void CheckLoadFactor()
        {
            if (count < sizeTimesLoadFactor) return;

            if (Interlocked.CompareExchange(ref _resizing, 1, 0) != 0) return; // another thread is doing it



            var table2 = new Table(table.entries.Length * 2);

            for (var idx = 0; idx < table.entries.Length; idx++)
            {
                ref var item = ref table.entries[idx];
                if (item.key != 0) LockedInsert(table2, item.key, item.value);
            }
        }

        /** util functions - move them somewhere else? **/


        static int FindPowerOfTwoAbove(int minSize)
        {
            int bits = 4;
            while ((1 << bits) < minSize) bits++;
            return bits;
        }


        private int FindKeyOrEmpty(Table table, ulong key)
        {
            // Probe b1
            int b1bucketIdx = (int)(key & table.bucketMask);
            int idx = b1bucketIdx << BucketSizeBits;
            for (int i = 0; i < BucketSize; i++, idx++)
            {
                while (true)
                {
                    ulong foundKey = Volatile.Read(ref table.entries[idx].key);

                    if (foundKey == 0 || foundKey == key) return idx;

                    if (foundKey == LOCKED_STATE)
                    {
                        // Someone is inserting here —> spin 
                        Thread.SpinWait(1);
                        continue;
                    }
                    break;
                }
            }
            // Probe b2
            int b2bucketIdx = (int)((key >> table.bucketBits) & table.bucketMask);
            idx = b2bucketIdx << BucketSizeBits;
            for (int i = 0; i < BucketSize; i++, idx++)
            {
                while (true)
                {
                    ulong foundKey = Volatile.Read(ref table.entries[idx].key);

                    if (foundKey == 0 || foundKey == key) return idx;

                    if (foundKey == LOCKED_STATE)
                    {
                        // Someone is inserting here —> spin 
                        Thread.SpinWait(1);
                        continue;
                    }
                    break;
                }
            }
            // Linear Probe b3
            idx = (b1bucketIdx ^ b2bucketIdx) << BucketSizeBits;

            while (true)
            {
                ulong foundKey = Volatile.Read(ref table.entries[idx].key);

                if (foundKey == 0 || foundKey == key) return idx;

                if (foundKey == LOCKED_STATE)
                {
                    // Someone is inserting here —> spin 
                    Thread.SpinWait(1);
                    continue;
                }

                if (++idx >= table.entries.Length) idx = 0;
            }
        }




    }
}