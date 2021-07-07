using System.Collections.Generic;

namespace soko
{
    public class Solver
    {
        private Level level;

        public Solver(Level level)
        {
            this.level = level;
        }

        public void solve()
        {
            var visitedStates = new HashSet<State>();
            var statesToProcess = new Queue<State>();

            var startState = new State(level, level.boxPositions, level.playerPosition);
            statesToProcess.Enqueue(startState);

            while (statesToProcess.Count > 0) {
                var state = statesToProcess.Dequeue();
                state.CalculatePlayerReachableMap();

                var moves = state.GetPossibleMoves();
                foreach (var move in moves)
                {
                    var newState = new State(state);
                    newState.ApplyMove(move);
                    newState.CalculatePlayerReachableMap();

                    if (newState.IsEndState()) {
                        System.Console.WriteLine($"Found solution, checked {visitedStates.Count} states.");
                        return;
                    }

                    if (!visitedStates.Contains(newState)) {
                        visitedStates.Add(newState);
                        statesToProcess.Enqueue(newState);
                    }
                }
            }

            System.Console.WriteLine("No solution found.");
        }
    }
}