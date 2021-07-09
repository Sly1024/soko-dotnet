using System.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace soko
{
    struct CameFrom 
    {
        public State state;
        public Move move;
    }
    public class Solver
    {
        private Level level;
        private Dictionary<State, CameFrom> visitedStates;
        private State startState;
        private List<State> endStates = new List<State>();

        public Solver(Level level)
        {
            this.level = level;
        }

        public bool Solve()
        {
            var watch = new Stopwatch();

            visitedStates = new Dictionary<State, CameFrom>();
            var statesToProcess = new Queue<State>();

            watch.Start();

            // Console.WriteLine("Identified End positions");

            var endPlayerPositions = GenerateEndPlayerPositions();
            foreach (var endPlayerPos in endPlayerPositions)
            {
                var state = new State(level, level.goalPositions, endPlayerPos);
                endStates.Add(state);
                state.CalculatePlayerReachableMap();
                statesToProcess.Enqueue(state);
                visitedStates.Add(state, new CameFrom());

                // state.PrintTable();
            }

            while (statesToProcess.Count > 0) {
                var state = statesToProcess.Dequeue();

                // Console.WriteLine("Processing state:");
                // state.PrintTable();

                var moves = state.GetPossibleMoves();
                foreach (var move in moves)
                {
                    // Console.WriteLine($"Trying move {move}");

                    var newState = new State(state);
                    var boxIdx = newState.ApplyPullMove(move);
                    newState.CalculatePlayerReachableMap();
                    
                    // newState.PrintTable();

                    if (!visitedStates.ContainsKey(newState)) {
                        visitedStates.Add(newState, new CameFrom { state = state, 
                            move = new Move { boxIndex = boxIdx, direction = move.direction } 
                        });
                        statesToProcess.Enqueue(newState);
                    }

                    if (newState.IsStartState()) {
                        startState = newState;
                        watch.Stop();
                        Console.WriteLine("Time: " + watch.Elapsed);
                        return true;
                    }
                }
            }

            Console.WriteLine("No solution found.");
            return false;
        }

        private int[] GenerateEndPlayerPositions()
        {
            var table = new int[level.table.Length];
            for (var i = 0; i < table.Length; i++)
            {
                table[i] = (level.table[i].has(Cell.Wall | Cell.Goal)) ? 1 : 0;
            }
            
            var playerPositions = new List<int>();

            for (var i = 0; i < table.Length; i++)
            {
                if (table[i] == 0) {
                    playerPositions.Add(i);
                    Filler.FillBoundsCheck(table, level.width, i, value => value == 0, 1);
                }
            }

            return playerPositions.ToArray();
        }

        public void PrintSolution()
        {
            Console.WriteLine($"Found solution, checked {visitedStates.Count} states.");
            var steps = new List<CameFrom>();
            var state = startState;

            while (!endStates.Contains(state)) {
                var from = visitedStates[state];
                steps.Add(from);
                state = from.state;
            }

            // steps.Reverse();

            var playerPos = level.playerPosition;
            var sb = new StringBuilder();

            state = startState;

            foreach (var step in steps)
            {
                sb.Append(state.FindPlayerPath(playerPos, step.move));
                sb.Append(step.move.PushCode);
                playerPos = state.boxPositions[step.move.boxIndex];
                state = step.state;
            }

            Console.WriteLine(sb.ToString());
            Console.WriteLine($"{steps.Count} pushes, {sb.Length - steps.Count} moves");
        }



    }
}