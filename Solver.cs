using System.Text;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

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
        private volatile State commonState;
        private State startState;
        private List<State> endStates;
        private ConcurrentDictionary<State, CameFrom> forwardVisitedStates;
        private ConcurrentDictionary<State, CameFrom> backwardVisitedStates;

        public Solver(Level level)
        {
            this.level = level;
        }

        public Task Solve() 
        {
            forwardVisitedStates = new ConcurrentDictionary<State, CameFrom>();
            backwardVisitedStates = new ConcurrentDictionary<State, CameFrom>();

            startState = new State(level, level.boxPositions, level.playerPosition);
            startState.CalculatePlayerReachableMap();
            forwardVisitedStates.TryAdd(startState, new CameFrom());

            endStates = new List<State>();
            
            var endPlayerPositions = GenerateEndPlayerPositions();
            foreach (var endPlayerPos in endPlayerPositions)
            {
                var state = new State(level, level.goalPositions, endPlayerPos);
                endStates.Add(state);
                state.CalculatePlayerReachableMap();
                backwardVisitedStates.TryAdd(state, new CameFrom());
            }

            return Task.WhenAny(new [] {
                Task.Run(SolveForward),
                Task.Run(SolveReverse)
            });
        }

        private void SolveForward()
        {
            var statesToProcess = new Queue<State>();
            statesToProcess.Enqueue(startState);

            while (statesToProcess.Count > 0) {
                var state = statesToProcess.Dequeue();

                var moves = state.GetPossibleMoves(false);
                foreach (var move in moves)
                {
                    var newState = new State(state);
                    newState.ApplyPushMove(move);
                    newState.CalculatePlayerReachableMap();

                    if (commonState != null) return;

                    if (forwardVisitedStates.TryAdd(newState, new CameFrom { state = state, move = move })) {
                        if (backwardVisitedStates.ContainsKey(newState)) {
                            commonState = newState;
                            return;
                        }
                        statesToProcess.Enqueue(newState);
                    }
                }
            }
        }

        private void SolveReverse()
        {
            var statesToProcess = new Queue<State>(endStates);
           
            while (statesToProcess.Count > 0) {
                var state = statesToProcess.Dequeue();

                var moves = state.GetPossibleMoves(true);
                foreach (var move in moves)
                {
                    var newState = new State(state);
                    var boxIdx = newState.ApplyPullMove(move);
                    newState.CalculatePlayerReachableMap();
                    
                    if (commonState != null) return;

                    if (backwardVisitedStates.TryAdd(newState, new CameFrom { state = state, 
                            move = new Move { boxIndex = boxIdx, direction = move.direction }
                        })) 
                    {
                        if (forwardVisitedStates.ContainsKey(newState)) {
                            commonState = newState;
                            return;
                        }
                        statesToProcess.Enqueue(newState);
                    }
                }
            }
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
            Console.WriteLine($"Forward states {forwardVisitedStates.Count}");
            Console.WriteLine($"Backwards states {backwardVisitedStates.Count}");

            var forwardSteps = new List<CameFrom>();
            var state = commonState;

            while (state != startState) {
                var from = forwardVisitedStates[state];
                forwardSteps.Add(from);
                state = from.state;
            }

            var backwardSteps = new List<CameFrom>();
            state = commonState;

            while (!endStates.Contains(state)) {
                var from = backwardVisitedStates[state];
                backwardSteps.Add(from);
                state = from.state;
            }

            var sb = new StringBuilder();
            var playerPos = WriteSolutionMoves(sb, forwardSteps);
            WriteReversedSolutionMoves(sb, backwardSteps, playerPos);
            string solution = sb.ToString();

            Console.WriteLine(solution);
            int pushCount = forwardSteps.Count + backwardSteps.Count;
            Console.WriteLine($"{pushCount} pushes, {solution.Length - pushCount} moves");
        }

        private int WriteSolutionMoves(StringBuilder sb, List<CameFrom> steps) 
        {
            steps.Reverse();
            var playerPos = level.playerPosition;

            foreach (var step in steps)
            {
                sb.Append(step.state.FindPlayerPath(playerPos, step.move));
                sb.Append(step.move.PushCode);
                playerPos = step.state.boxPositions[step.move.boxIndex];
            }

            return playerPos;
        }

        private void WriteReversedSolutionMoves(StringBuilder sb, List<CameFrom> steps, int playerPos) 
        {
            var state = commonState;

            foreach (var step in steps)
            {
                sb.Append(state.FindPlayerPath(playerPos, step.move));
                sb.Append(step.move.PushCode);
                playerPos = state.boxPositions[step.move.boxIndex];
                state = step.state;
            }
        }

    }
}