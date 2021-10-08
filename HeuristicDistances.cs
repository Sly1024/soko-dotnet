using System.Collections;
using System;
using System.Collections.Generic;

namespace soko
{
    public class HeuristicDistances
    {
        private Level level;

        // [currentBoxPosition][goalIndex] = distance
        public int[][] Pushes;

        // [currentBoxPosition][initialBoxPositionIndex] = distance
        public int[][] Pulls;

        public int numBoxes;

        private DynamicList<(int box, int goal, int dist)> distArr;
        private BitArray boxUsed;
        private BitArray goalUsed;
        private BoxGoalDistComparer bgdComparer = new BoxGoalDistComparer();

        public HeuristicDistances(Level level)
        {
            this.level = level;
            numBoxes = level.boxPositions.Length;

            Pushes = new int[level.table.Length][];
            Pulls = new int[level.table.Length][];

            for (var i = 0; i < numBoxes; i++) {
                TryPullMoves(level.goalPositions[i], i);
            }

            for (var i = 0; i < numBoxes; i++) {
                TryPushMoves(level.boxPositions[i], i);
            }

            distArr = new DynamicList<(int box, int goal, int dist)>(numBoxes * numBoxes);
            boxUsed = new BitArray(numBoxes);
            goalUsed = new BitArray(numBoxes);
        }

        private void TryPullMoves(int pos, int goalIndex)
        {
            var stack = new Stack<int>();
            
            SetDistance(Pushes, pos, goalIndex, 0);
            stack.Push(pos);

            while (stack.Count > 0) {
                pos = stack.Pop();
                var dist = GetDistance(Pushes, pos, goalIndex) + 1;

                for (var dir = 0; dir < 4; dir++) {
                    var offset = Level.DirOffset[dir];
                    var newPos = pos + offset;
                    if (!level.table[newPos].has(Cell.Wall) && !level.table[newPos + offset].has(Cell.Wall)) {
                        if (dist < GetDistance(Pushes, newPos, goalIndex)) {
                            SetDistance(Pushes, newPos, goalIndex, dist);
                            stack.Push(newPos);
                        }
                    }
                }
            }
        }

        private void TryPushMoves(int pos, int boxIndex)
        {
            var stack = new Stack<int>();
            
            SetDistance(Pulls, pos, boxIndex, 0);
            stack.Push(pos);

            while (stack.Count > 0) {
                pos = stack.Pop();
                var dist = GetDistance(Pulls, pos, boxIndex) + 1;

                for (var dir = 0; dir < 4; dir++) {
                    var offset = Level.DirOffset[dir];
                    var newPos = pos + offset;
                    if (!level.table[newPos].has(Cell.Wall) && !level.table[pos - offset].has(Cell.Wall)) {
                        if (dist < GetDistance(Pulls, newPos, boxIndex)) {
                            SetDistance(Pulls, newPos, boxIndex, dist);
                            stack.Push(newPos);
                        }
                    }
                }
            }
        }

        private int GetDistance(int[][] distances, int pos, int index)
        {
            return distances[pos] == null ? int.MaxValue : distances[pos][index];
        }

        private void SetDistance(int[][] distances, int pos, int index, int distance)
        {
            (distances[pos] ??= GenerateDistanceArray())[index] = distance;
        }

        private int[] GenerateDistanceArray()
        {
            var arr = new int[numBoxes];
            for (var i = 0; i < arr.Length; i++) arr[i] = int.MaxValue;
            return arr;
        }
        
        public int GetHeuristicDistance(int[] boxPositions, bool push, DynamicList<(int box, int goal, int dist)> distArr) 
        {
            var boxUsed = new BitArray(numBoxes);
            var goalUsed = new BitArray(numBoxes);
            distArr.Clear();
            int numBoxesAdded = 0;
            var pushes = push ? level.distances.Pushes : level.distances.Pulls;

            for (int boxIdx = 0; boxIdx < boxPositions.Length; boxIdx++)
            {
                int boxPos = boxPositions[boxIdx];
                if (level.table[boxPos].has(push ? Cell.Goal : Cell.Box)) continue;

                var distances = pushes[boxPos];
                for (var goalIdx = 0; goalIdx < distances.Length; goalIdx++)
                {
                    distArr.Add((boxIdx, goalIdx, distances[goalIdx]));
                }
                numBoxesAdded++;
            }
            Array.Sort(distArr.items, 0, distArr.idx, bgdComparer);

            boxUsed.SetAll(false);
            goalUsed.SetAll(false);

            int sumDistance = 0;

            for (var i = 0; i < distArr.idx; i++)
            {
                var entry = distArr.items[i];
                if (!boxUsed[entry.box] && !goalUsed[entry.goal]) {
                    boxUsed[entry.box] = true;
                    goalUsed[entry.goal] = true;
                    sumDistance += entry.dist;
                    if (--numBoxesAdded == 0) break;
                }
            }

            return sumDistance;
        }

        private class BoxGoalDistComparer : IComparer<(int box, int goal, int dist)>
        {
            public int Compare((int box, int goal, int dist) x, (int box, int goal, int dist) y)
            {
                return x.dist - y.dist;
            }
        }
    }
}