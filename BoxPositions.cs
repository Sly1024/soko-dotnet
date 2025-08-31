using System.Linq;

namespace soko 
{
    public class BoxPositions
    {
        readonly int numBoxes;

        // list and index are the inverse of each other: list[x] == y <-> index[y] == x
        public int[] list;
        int[] index;

        public BoxPositions(int tableSize, int[] initialPositions)
        {
            numBoxes = initialPositions.Length;
            list = new int[numBoxes];
            index = new int[tableSize];

            int freeIdx = 0;
            foreach (var boxPos in initialPositions)
            {
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

        public int[] ToArray()
        {
            return [.. list.Take(numBoxes)];
        }
    }
}