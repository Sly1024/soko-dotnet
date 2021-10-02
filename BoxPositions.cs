using System;
namespace soko 
{
    public class BoxPositions
    {
        int numBoxes;

        // list and index are the inverse of each other: list[x] == y <-> index[y] == x
        public int[] list;
        int[] index;

        // list of box positions that are marked
        public int[] markedList;
        public int markedCount = 0;

        // for each box position (as index) this map stores a value that:
        // - indicates that the box is marked if value >= currentMarkedMin
        // - and (value - currentMarkedMin) gives the index of the boxPosition in markedList
        //   so that markedList[markedMap[boxPos] - currentMarkedMin] == boxPos
        int[] markedMap;
        int currentMarkedMin = 0;

        public BoxPositions(int tableSize, int[] initialPositions)
        {
            numBoxes = initialPositions.Length;
            list = new int[numBoxes];
            index = new int[tableSize];
            markedList = new int[numBoxes];
            markedMap = new int[tableSize];

            int freeIdx = 0;
            foreach (var boxPos in initialPositions) {
                list[freeIdx] = boxPos;
                index[boxPos] = freeIdx;
                freeIdx++;
            }
        }

        public void Move(int oldPos, int newPos) 
        {
            var idx = index[oldPos];
            list[idx] = newPos;
            index[newPos] = idx;
        }

        public void ResetMarked()
        {
            markedCount = 0;

            if ((currentMarkedMin += numBoxes) >= int.MaxValue - numBoxes) {
                Array.Fill(markedMap, 0);
                currentMarkedMin = 1;
            }
        }

        public void MarkBox(int pos)
        {
            markedMap[pos] = currentMarkedMin + markedCount;
            markedList[markedCount++] = pos;
        }

        public void UnmarkBox(int pos)
        {
            var idx = markedMap[pos] - currentMarkedMin;
            markedMap[pos] = 0;

            var lastItem = markedList[--markedCount];
            markedMap[lastItem] = currentMarkedMin + idx;
            markedList[idx] = lastItem;
        }

        public bool IsMarked(int pos)
        {
            return markedMap[pos] >= currentMarkedMin;
        }
    }
}