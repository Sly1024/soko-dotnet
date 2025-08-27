using System;

namespace soko
{
    public class MarkedList
    {
        readonly int maxCount;

        // list of box positions that are marked
        public int[] list;
        public int count = 0;

        // for each box position (as index) this map stores a value that:
        // - indicates that the box is marked if value >= currentMarkedMin
        // - and (value - currentMarkedMin) gives the index of the boxPosition in markedList
        //   so that list[index[boxPos] - currentMarkedMin] == boxPos
        int[] index;
        int currentMarkedMin = 1;

        public MarkedList(int tableSize, int maxItemCount)
        {
            maxCount = maxItemCount;
            list = new int[maxCount];
            index = new int[tableSize];
        }

        public void ResetMarked()
        {
            count = 0;

            if ((currentMarkedMin += maxCount) >= int.MaxValue - maxCount) {
                Array.Fill(index, 0);
                currentMarkedMin = 1;
            }
        }

        public void MarkPos(int pos)
        {
            index[pos] = currentMarkedMin + count;
            list[count++] = pos;
        }

        public void UnmarkPos(int pos)
        {
            var idx = index[pos] - currentMarkedMin;
            index[pos] = 0;

            var lastItem = list[--count];
            index[lastItem] = currentMarkedMin + idx;
            list[idx] = lastItem;
        }

        public bool IsMarked(int pos)
        {
            return index[pos] >= currentMarkedMin;
        }
    }
}