using System.Collections.Generic;

namespace soko 
{
    public class DeadlockPatterns
    {
        List<int> boxRemaining = new List<int>();
        List<int>[] patternsForBoxPos;

        public DeadlockPatterns(Level level)
        {
            patternsForBoxPos = new List<int>[level.table.Length];
            for (int i = 0; i < patternsForBoxPos.Length; i++)
            {
                if (level.table[i] != Cell.Wall) patternsForBoxPos[i] = new List<int>();
            }
        }

        public bool UpdateRemaining(int boxPos, int delta) 
        {
            bool foundZero = false;
            foreach (var patternIdx in patternsForBoxPos[boxPos])
                if ((boxRemaining[patternIdx] += delta) == 0) foundZero = true;

            return foundZero;
        }

        public void AddPattern(int[] boxPositions, int count) 
        {
            var numPatterns = boxRemaining.Count;
            boxRemaining.Add(0);
            for (int i = 0; i < count; i++) {
                patternsForBoxPos[boxPositions[i]].Add(numPatterns);
            }
        }
    }
}