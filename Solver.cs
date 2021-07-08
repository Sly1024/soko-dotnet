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
        private State startState, endState;

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

            startState = new State(level, level.boxPositions, level.playerPosition);
            startState.CalculatePlayerReachableMap();
            statesToProcess.Enqueue(startState);

            while (statesToProcess.Count > 0) {
                var state = statesToProcess.Dequeue();

                var moves = state.GetPossibleMoves();
                foreach (var move in moves)
                {
                    var newState = new State(state);
                    newState.ApplyMove(move);
                    newState.CalculatePlayerReachableMap();

                    if (!visitedStates.ContainsKey(newState)) {
                        visitedStates.Add(newState, new CameFrom { state = state, move = move });
                        statesToProcess.Enqueue(newState);
                    }

                    if (newState.IsEndState()) {
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
        
        public void PrintSolution()
        {
            Console.WriteLine($"Found solution, checked {visitedStates.Count} states.");
            var steps = new List<CameFrom>();
            var state = endState;

            while (state != startState) {
                var from = visitedStates[state];
                steps.Add(from);
                state = from.state;
            }

            steps.Reverse();

            var playerPos = level.playerPosition;
            var sb = new StringBuilder();

            foreach (var step in steps)
            {
                sb.Append(step.state.FindPlayerPath(playerPos, step.move));
                sb.Append(GetPushCodeForDirection(step.move.direction));
                playerPos = step.state.boxPositions[step.move.boxIndex];
            }

            Console.WriteLine(sb.ToString());
            Console.WriteLine($"{steps.Count} pushes, {sb.Length - steps.Count} moves");
        }

        private string GetPushCodeForDirection(Direction direction) {
            return direction switch
            {
                Direction.Left => "L",
                Direction.Right => "R",
                Direction.Up => "U",
                Direction.Down => "D",
                _ => throw new ArgumentException($"Invalid direction {direction}")
            };
        }

    }
}