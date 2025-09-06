using System;
using System.Collections.Generic;

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
        var width = level.width;

        int numBoxesAdded = 0;
        var pushes = push ? level.distances.Pushes : level.distances.Pulls;

        int minRoom = reachable.CalculateRooms();
        var rTable = reachable.table;

        distArr.Clear();
        for (int boxIdx = 0; boxIdx < boxPositions.Length; boxIdx++)
        {
            int boxPos = boxPositions[boxIdx];
            if (level.table[boxPos].has(push ? Cell.Goal : Cell.Box)) continue; // if box is on Goal, its distance = 0, skip

            var distances = pushes[boxPos];
            for (var goalIdx = 0; goalIdx < numBoxes; goalIdx++)
            {
                int goalPos = level.goalPositions[goalIdx];

                int goalRoom = rTable[goalPos];
                if (goalRoom < minRoom) continue; // goal is not reachable, probably a box is on it - possibly :]

                // check if box is in the same room as goal
                if (rTable[boxPos - 1] == goalRoom || rTable[boxPos + 1] == goalRoom
                 || rTable[boxPos - width] == goalRoom || rTable[boxPos + width] == goalRoom)
                {
                    distArr.Add((boxIdx, goalIdx, distances[goalIdx]));
                }
                else
                {
                    // punish by 2x if not reachable
                    distArr.Add((boxIdx, goalIdx, distances[goalIdx] * 2));
                }
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