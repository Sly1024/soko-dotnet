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
        private State endState;
        private List<State> startStates = new List<State>();
        private bool reversed;

        public Solver(Level level)
        {
            this.level = level;
        }

        public bool Solve(bool reversed)
        {
            this.reversed = reversed;
            var watch = new Stopwatch();

            visitedStates = new Dictionary<State, CameFrom>();
            var statesToProcess = new Queue<State>();

            watch.Start();

            // Console.WriteLine("Identified End positions");

            if (reversed) {
                var endPlayerPositions = GenerateEndPlayerPositions();
                foreach (var endPlayerPos in endPlayerPositions)
                {
                    var state = new State(level, level.goalPositions, endPlayerPos);
                    startStates.Add(state);
                    state.CalculatePlayerReachableMap();
                    statesToProcess.Enqueue(state);
                    visitedStates.Add(state, new CameFrom());
                }
            } else {
                var state = new State(level, level.boxPositions, level.playerPosition);
                startStates.Add(state);
                state.CalculatePlayerReachableMap();
                statesToProcess.Enqueue(state);
                visitedStates.Add(state, new CameFrom());
            }

            while (statesToProcess.Count > 0) {
                var state = statesToProcess.Dequeue();

                // Console.WriteLine("Processing state:");
                // state.PrintTable();

                var moves = state.GetPossibleMoves(reversed);
                foreach (var move in moves)
                {
                    // Console.WriteLine($"Trying move {move}");

                    var newState = new State(state);
                    var boxIdx = reversed ? newState.ApplyPullMove(move) : newState.ApplyPushMove(move);
                    newState.CalculatePlayerReachableMap();
                    
                    // newState.PrintTable();

                    if (!visitedStates.ContainsKey(newState)) {
                        visitedStates.Add(newState, new CameFrom { state = state, 
                            move = new Move { boxIndex = boxIdx, direction = move.direction } 
                        });
                        statesToProcess.Enqueue(newState);
                    }

                    if (reversed ? newState.IsStartState() : newState.IsEndState()) {
                        endState = newState;
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
            var state = endState;

            while (!startStates.Contains(state)) {
                var from = visitedStates[state];
                steps.Add(from);
                state = from.state;
            }

            var solution = reversed ? GetReversedSolutionMoves(steps) : GetSolutionMoves(steps);
            Console.WriteLine(solution);
            Console.WriteLine($"{steps.Count} pushes, {solution.Length - steps.Count} moves");
        }

        private string GetSolutionMoves(List<CameFrom> steps) 
        {
            steps.Reverse();

            var playerPos = level.playerPosition;
            var sb = new StringBuilder();

            foreach (var step in steps)
            {
                sb.Append(step.state.FindPlayerPath(playerPos, step.move));
                sb.Append(step.move.PushCode);
                playerPos = step.state.boxPositions[step.move.boxIndex];
            }

            return sb.ToString();
        }

        private string GetReversedSolutionMoves(List<CameFrom> steps) 
        {
            var playerPos = level.playerPosition;
            var sb = new StringBuilder();

            var state = endState;

            foreach (var step in steps)
            {
                sb.Append(state.FindPlayerPath(playerPos, step.move));
                sb.Append(step.move.PushCode);
                playerPos = state.boxPositions[step.move.boxIndex];
                state = step.state;
            }

            return sb.ToString();
        }

    }
}