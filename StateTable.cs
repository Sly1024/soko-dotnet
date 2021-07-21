using System.Runtime.InteropServices;

namespace soko 
{
    /**
     * Uses Zobrist hashes to store the states
     * https://en.wikipedia.org/wiki/Zobrist_hashing
     */
    public class StateTable<TValue>
    {
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct HashEntry<TEntryValue> {
            public ulong zHash;
            public TEntryValue value;
        }

        private HashEntry<TValue>[] states;
        private int count = 0;

        public int Count { get => count; }

        public StateTable(int minimumSize)
        {
            states = new HashEntry<TValue>[FindPrimeAbove(minimumSize)];
        }

        public bool TryAdd(ulong zHash, TValue value)
        {
            if (InternalAdd(zHash, value)) {
                ++count;
                CheckLoadFactor();
                return true;
            }
            return false;
        }

        public bool ContainsKey(ulong zHash)
        {
            return states[FindState(zHash)].zHash != 0;
        }

        public TValue this[ulong zHash] 
        {
            get => states[FindState(zHash)].value;
        }

        // linear probing
        private int FindState(ulong zHash)
        {
            int size = states.Length;
            int idx = (int)(zHash % (ulong)size);
            ref HashEntry<TValue> state = ref states[idx];

            while (state.zHash != 0 && state.zHash != zHash) {
                if (++idx == size) idx = 0;
                state = ref states[idx];
            }
            return idx;
        }


        private bool InternalAdd(ulong zHash, TValue value)
        {
            int idx = FindState(zHash);
            if (states[idx].zHash == zHash) return false;
            states[idx] = new HashEntry<TValue> { zHash = zHash, value = value };
            return true;
        }

        private void CheckLoadFactor()
        {
            int size = states.Length;
            // above 75% 
            if (count * 4 > size * 3) {
                var oldStates = states;
                states = new HashEntry<TValue>[FindPrimeAbove(size * 7 / 4)];   // 1.75x

                foreach (var item in oldStates) {
                    InternalAdd(item.zHash, item.value);
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