namespace soko 
{
    public class BoxPositions
    {
        public int[] list;
        int[] index;

        public BoxPositions(int tableSize, int[] initialPositions)
        {
            list = new int[initialPositions.Length];
            index = new int[tableSize];

            int freeIdx = 0;
            foreach (var boxPos in initialPositions) {
                list[freeIdx] = boxPos;
                index[boxPos] = freeIdx;
                freeIdx++;
            }
        }

        public void Move(int oldPos, int newPos) 
        {
            var freeIdx = index[oldPos];
            list[freeIdx] = newPos;
            index[newPos] = freeIdx;
        }
    }
}