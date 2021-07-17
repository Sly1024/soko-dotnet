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

        public int collisions = 0;

        public StateTable(int minimumSize)
        {
            states = new HashState[FindPrimeAbove(minimumSize)];
        }

        /** returns a HashState with zHash = 0 if not found */
        public HashState GetState(ulong zHash)
        {
            return states[FindState(zHash)];
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
        
        // https://en.wikipedia.org/wiki/Quadratic_probing - with alternating signs
        private int FindState(ulong zHash)
        {
            int size = states.Length;
            int startIdx = (int)(zHash % (ulong)size);
            int quadInc = 1;
            int quadOffset = 0;
            int idx = startIdx;
            ref HashState state = ref states[startIdx];
            while (state.zHash != 0 && state.zHash != zHash) {
                quadOffset += quadInc;
                quadInc += 2;
                idx = (startIdx + quadOffset) % size;
                // ++collisions;
                state = ref states[idx];
                
                if (!(state.zHash != 0 && state.zHash != zHash)) return idx;

                quadOffset += quadInc;
                quadInc += 2;
                idx = (startIdx - quadOffset) % size;
                if (idx < 0) idx += size;
                // ++collisions;
                state = ref states[idx];
            }
            return idx;
        }

        private bool InternalAdd(HashState state)
        {
            int idx = FindState(state.zHash);
            if (states[idx].zHash == state.zHash) return false;
            states[idx] = state;
            return true;
        }

        private void CheckSaturation()
        {
            int size = states.Length;
            // above 75% 
            if (count * 4 > size * 3) {
                collisions = 0;
                var oldStates = states;
                states = new HashState[FindPrimeAbove(size * 7 / 4)];   // 1.75x

                for (var i = 0; i < size; i++) {
                    if (oldStates[i].zHash != 0) {
                        InternalAdd(oldStates[i]);
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