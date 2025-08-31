using System.Threading;

namespace soko 
{
    public class CompactHashTable<TValue>
    {
        private const ulong LOCKED_STATE = 1;
        private const int BucketSizeBits = 3;
        private const int BucketSize = 1 << BucketSizeBits;

        private readonly ReaderWriterLockSimple rwLock = new ();
        private class Table
        {
            public readonly int bucketBits;
            public readonly ulong bucketMask;

            public readonly ulong[] keys;
            public readonly TValue[] values;

            public Table(int minimumSize)
            {
                var sizeBits = FindPowerOfTwoAbove(minimumSize);
                bucketBits = sizeBits - BucketSizeBits;
                bucketMask = (1UL << bucketBits) - 1;

                keys = new ulong[1 << sizeBits];
                values = new TValue[1 << sizeBits];
            }
        }

        private int count = 0;
        private readonly float loadFactor;
        private int sizeTimesLoadFactor;

        private Table table;

        public int Count => count;

        public CompactHashTable(int minimumSize, float loadFactor = 0.9f)
        {
            this.loadFactor = loadFactor;
            table = new Table(minimumSize);
            sizeTimesLoadFactor = (int)(table.keys.Length * loadFactor);
        }


        /// <returns>true if inserted, false if already present</returns>
        public bool TryAdd(ulong key, TValue value/*, out TValue existingValue*/)
        {
            var _table = Volatile.Read(ref table);      // do we need volatile?
            int idx = FindKeyOrEmpty(_table, key);
            if (_table.keys[idx] == key)
            {
                // existingValue = _table.values[idx];
                return false;
            }

            rwLock.AcquireReadLock();   // resize is blocked until we release the read lock

            // check if table changed (resized)
            var _table2 = Volatile.Read(ref table);
            if (_table != _table2)
            {
                _table = _table2;
                idx = FindKeyOrEmpty(_table, key);
                if (_table.keys[idx] == key)
                {
                    rwLock.ReleaseReadLock();
                    // existingValue = _table.values[idx];
                    return false;
                }
            }

            while (true)
            {
                if (Interlocked.CompareExchange(ref _table.keys[idx], LOCKED_STATE, 0) == 0)
                {
                    // Success! We own this slot now. Write the value FIRST.
                    _table.values[idx] = value;

                    // "Publish" the real key. This must be a volatile write
                    // to ensure the value write is visible before the key. This also unlocks.
                    Volatile.Write(ref _table.keys[idx], key);
                    Interlocked.Increment(ref count);
                    rwLock.ReleaseReadLock();

                    CheckLoadFactor();
                    // existingValue = default;
                    return true;
                }
                Thread.SpinWait(1);

                // at this point, another thread might have inserted a key, so just retry with FindKeyOrEmpty()
                // TODO[optimize]: we don't need to start from the first probe index, we could continue!
                idx = FindKeyOrEmpty(_table, key);
                if (_table.keys[idx] == key)
                {
                    rwLock.ReleaseReadLock();
                    // existingValue = _table.values[idx];
                    return false;
                }
            }
        }

        public bool ContainsKey(ulong key)
        {
            var _table = table;
            return _table.keys[FindKeyOrEmpty(_table, key)] != 0;
        }

        /// <summary>
        /// Only use with known keys, otherwise this may return invalid values.
        /// </summary>
        public TValue this[ulong key]
        {
            get
            {
                var _table = table;
                return _table.values[FindKeyOrEmpty(_table, key)];
            }
        }

        private void CheckLoadFactor()
        {
            if (count < sizeTimesLoadFactor) return;

            rwLock.AcquireWriteLock();
            // check if in the meantime another thread had done the resize
            if (count < sizeTimesLoadFactor)
            {
                rwLock.ReleaseWriteLock();
                return;
            }

            var table2 = new Table(table.keys.Length * 2);

            for (var idx = 0; idx < table.keys.Length; idx++)
            {
                var key = table.keys[idx];
                if (key != 0)
                {
                    var t2_idx = FindKeyOrEmpty(table2, key);
                    table2.keys[t2_idx] = key;
                    table2.values[t2_idx] = table.values[idx];
                }
            }
            sizeTimesLoadFactor = (int)(table2.keys.Length * loadFactor);
            Volatile.Write(ref table, table2);
            rwLock.ReleaseWriteLock();
        }

        /** static functions **/


        private static int FindPowerOfTwoAbove(int minSize)
        {
            int bits = 4;
            while ((1 << bits) < minSize) bits++;
            return bits;
        }


        private static int FindKeyOrEmpty(Table table, ulong key)
        {
            // Probe b1
            int b1bucketIdx = (int)(key & table.bucketMask);
            int idx = b1bucketIdx << BucketSizeBits;
            for (int i = 0; i < BucketSize; i++, idx++)
            {
                while (true)
                {
                    ulong foundKey = Volatile.Read(ref table.keys[idx]);

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
                    ulong foundKey = Volatile.Read(ref table.keys[idx]);

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
                ulong foundKey = Volatile.Read(ref table.keys[idx]);

                if (foundKey == 0 || foundKey == key) return idx;

                if (foundKey == LOCKED_STATE)
                {
                    // Someone is inserting here —> spin 
                    Thread.SpinWait(1);
                    continue;
                }

                if (++idx >= table.keys.Length) idx = 0;
            }
        }


    }
}