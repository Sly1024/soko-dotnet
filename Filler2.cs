using System.Runtime.CompilerServices;

namespace soko {
    public class Filler2 {
        private DynamicList<int> list = new DynamicList<int>(100);

        public int Fill(int[] table, int width, int startPos, int reachable) {
            list.Clear();

            table[startPos] = reachable;
            list.Add(startPos);

            while (list.Count > 0) {
                var pos = list.Pop();
                if (pos < startPos) startPos = pos;
                
                checkPosition(pos + 1);
                checkPosition(pos - 1);
                checkPosition(pos + width);
                checkPosition(pos - width);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void checkPosition(int pos)
            {
                if (table[pos] < reachable) { table[pos] = reachable; list.Add(pos); }
            }
            return startPos;
        }
    }
}