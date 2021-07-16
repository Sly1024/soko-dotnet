using System;
using System.Runtime.InteropServices;

namespace soko 
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct HashState {
        public ulong zHash;
        public ulong prevState;
        public Move move;
    }

    /**
     * Uses Zobrist hashes to store the states
     * https://en.wikipedia.org/wiki/Zobrist_hashing
     */
    public class StateTable
    {
        public HashState[] states;
        private int count = 0;

        public int Count { get => count; }

        public StateTable(int minimumSize)
        {
            states = new HashState[FindPrimeAbove(minimumSize)];
        }

        public bool TryAdd(HashState state)
        {
            if (InternalAdd(state)) {
                ++count;
                CheckSaturation();
                return true;
            }
            return false;
        }

        private bool InternalAdd(HashState state)
        {
            int size = states.Length;
            ulong stateZhash = state.zHash;
            int idx = (int)(stateZhash % (ulong)size);
            while (states[idx].zHash != 0) {
                if (states[idx].zHash == stateZhash) return false;
                if (++idx == size) idx = 0;
            }
            states[idx] = state;
            return true;
        }

        private void CheckSaturation()
        {
            int size = states.Length;
            // above 75% 
            if (count * 4 > size * 3) {
                var oldStates = states;
                states = new HashState[FindPrimeAbove(size * 3 / 2)];

                for (var i = 0; i < size; i++) {
                    if (oldStates[i].zHash != 0) {
                        InternalAdd(oldStates[i]);
                    }
                }
            }
        }

        /** returns a HashState with zHash = 0 if not found */
        public HashState GetState(ulong zHash)
        {
            int size = states.Length;
            int idx = (int)(zHash % (ulong)size);
            ulong sZhash;
            while ((sZhash = states[idx].zHash) != zHash && sZhash != 0) {
                if (++idx == size) idx = 0;
            }
            return states[idx];
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