using System;
using System.Collections.Generic;
using soko.Collections;

namespace soko;

public class HeuristicDistanceComputer
{
    private readonly int numBoxes;
    public DynamicList<(int box, int goal, int dist)> distArr;

    public FastBitArray boxUsed;
    public FastBitArray goalUsed;

    private class BoxGoalDistComparer : IComparer<(int box, int goal, int dist)>
    {
        public int Compare((int box, int goal, int dist) x, (int box, int goal, int dist) y)
        {
            return x.dist - y.dist;
        }
    }
    private static readonly BoxGoalDistComparer bgdComparer = new BoxGoalDistComparer();
    private readonly Level level;

    public HeuristicDistanceComputer(Level level)
    {
        this.level = level;
        numBoxes = level.boxPositions.Length;
        distArr = new DynamicList<(int box, int goal, int dist)>(numBoxes * numBoxes);

        boxUsed = new FastBitArray(numBoxes);
        goalUsed = new FastBitArray(numBoxes);
    }

    public int GetHeuristicDistance(int[] boxPositions, PlayerReachable reachable, bool push)
    {
        var w = level.width;

        int numBoxesAdded = 0;
        var distances = push ? level.distances.Pushes : level.distances.Pulls;

        // int minRoom = reachable.CalculateRooms();
        var rTable = reachable.table;

        distArr.Clear();
        for (int boxIdx = 0; boxIdx < boxPositions.Length; boxIdx++)
        {
            int boxPos = boxPositions[boxIdx];
            if (level.table[boxPos].has(push ? Cell.Goal : Cell.Box)) continue; // if box is on Goal, its distance = 0, skip

            for (var goalIdx = 0; goalIdx < numBoxes; goalIdx++)
            {
                int goalPos = level.goalPositions[goalIdx];

                int goalRoom = rTable[goalPos];
                if (goalRoom == PlayerReachable.BOX) continue;   // goal has a box on it, skip

                // check the "optimal" path from the box to goal and if there's a box on it, just add 1
                var currentPos = boxPos;
                var currentDistance = distances[currentPos][goalIdx];
                if (currentDistance == HeuristicDistances.Unreachable) continue;    // unreachable goal, skip

                int distanceWithPenalty = currentDistance;

                while (currentDistance > 0)
                {
                    var nextPos = currentPos;
                    if (distances[currentPos - 1] != null && distances[currentPos - 1][goalIdx] < currentDistance) currentDistance = distances[nextPos = currentPos - 1][goalIdx];
                    if (distances[currentPos + 1] != null && distances[currentPos + 1][goalIdx] < currentDistance) currentDistance = distances[nextPos = currentPos + 1][goalIdx];
                    if (distances[currentPos - w] != null && distances[currentPos - w][goalIdx] < currentDistance) currentDistance = distances[nextPos = currentPos - w][goalIdx];
                    if (distances[currentPos + w] != null && distances[currentPos + w][goalIdx] < currentDistance) currentDistance = distances[nextPos = currentPos + w][goalIdx];

                    if (currentPos == nextPos) throw new ArgumentException("HeuristicDistanceComputer error - no path found to goal");
                    if (rTable[nextPos] == PlayerReachable.BOX) distanceWithPenalty++;
                    currentPos = nextPos;
                }

                distArr.Add((boxIdx, goalIdx, distanceWithPenalty));
            }
            numBoxesAdded++;
        }
        Array.Sort(distArr.items, 0, distArr.idx, bgdComparer);

        boxUsed.Clear();
        goalUsed.Clear();

        int sumDistance = 0;

        for (var i = 0; i < distArr.idx; i++)
        {
            var (box, goal, dist) = distArr.items[i];
            if (!boxUsed[box] && !goalUsed[goal])
            {
                boxUsed[box] = true;
                goalUsed[goal] = true;
                sumDistance += dist;
                if (--numBoxesAdded == 0) break;
            }
        }

        return sumDistance;
    }


    /** basic greedy algo **/

    // public int GetHeuristicDistance(int[] boxPositions, bool push)
    // {
    //     distArr.Clear();
    //     int numBoxesAdded = 0;
    //     var pushes = push ? level.distances.Pushes : level.distances.Pulls;

    //     for (int boxIdx = 0; boxIdx < boxPositions.Length; boxIdx++)
    //     {
    //         int boxPos = boxPositions[boxIdx];
    //         if (level.table[boxPos].has(push ? Cell.Goal : Cell.Box)) continue;

    //         var distances = pushes[boxPos];
    //         for (var goalIdx = 0; goalIdx < numBoxes; goalIdx++)
    //         {
    //             distArr.Add((boxIdx, goalIdx, distances[goalIdx]));
    //         }
    //         numBoxesAdded++;
    //     }
    //     Array.Sort(distArr.items, 0, distArr.idx, bgdComparer);

    //     boxUsed.Clear();
    //     goalUsed.Clear();

    //     int sumDistance = 0;

    //     for (var i = 0; i < distArr.idx; i++)
    //     {
    //         var (box, goal, dist) = distArr.items[i];
    //         if (!boxUsed[box] && !goalUsed[goal])
    //         {
    //             boxUsed[box] = true;
    //             goalUsed[goal] = true;
    //             sumDistance += dist;
    //             if (--numBoxesAdded == 0) break;
    //         }
    //     }

    //     return sumDistance;
    // }
}