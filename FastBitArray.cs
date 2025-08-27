using System;
namespace soko 
{
    public class FastBitArray(int length)
    {
        readonly int[] table = new int[length];
        int currentValue = 1;

        public bool this[int idx] {
            get => table[idx] == currentValue;
            set => table[idx] = value ? currentValue : 0;
        }

        public void Clear() {
            if (++currentValue >= int.MaxValue) {
                Array.Fill(table, 0);
                currentValue = 1;
            }
        }
    }
}