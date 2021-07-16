using System.Text;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace soko
{
    public class Solver
    {
        private Level level;
        private /* volatile */ ulong commonState;
        private ulong startState;
        private List<ulong> endStates;

        private State fullState;

        public StateTable forwardVisitedStates;
        public StateTable backwardVisitedStates;

        public DynamicList<Move> moves = new DynamicList<Move>(1000);

        public Solver(Level level)
        {
            this.level = level;
        }

        public Task Solve()
        {
            sourceAncestors = new DynamicList<HashState>(100);
            targetAncestors = new DynamicList<HashState>(100);

            forwardVisitedStates = new StateTable(100000);
            backwardVisitedStates = new StateTable(100000);

            var state = new State(level, level.boxPositions, level.playerPosition);
            
            startState = state.GetZHash();
            forwardVisitedStates.TryAdd(new HashState { zHash = startState });

            fullState = state;

            endStates = new List<ulong>();
            
            var endPlayerPositions = GenerateEndPlayerPositions();
            foreach (var endPlayerPos in endPlayerPositions)
            {
                var endState = new State(level, level.goalPositions, endPlayerPos);
                endStates.Add(endState.GetZHash());
                backwardVisitedStates.TryAdd(new HashState { zHash = endState.GetZHash() });
            }

            return Task.WhenAny(new [] {
                Task.Run(SolveForward),
                // Task.Run(SolveReverse)
            })/* .ContinueWith((_) => {
                sourceAncestors = null;
                targetAncestors = null;
            }) */;
        }

        public struct ToProcess 
        {
            public ulong state;
            public int moveIdx;
        }

        public Queue<ToProcess> statesToProcess;

        private void SolveForward()
        {
            statesToProcess = new Queue<ToProcess>();
            statesToProcess.Enqueue(new ToProcess{ state = startState, moveIdx = fullState.GetPossibleMoves(moves, false) });

            while (statesToProcess.Count > 0) {
                var toProcess = statesToProcess.Dequeue();
                ulong stateZHash = toProcess.state;
                var moveIdx = toProcess.moveIdx;

                MoveStateInto(fullState, stateZHash, forwardVisitedStates);

                // Console.WriteLine("Processing state...");
                // fullState.PrintTable();

                //fullState.GetPossibleMoves(possibleMoves, false);
                // foreach (var move in moves) {
                while (true) {
                    var move = moves.items[moveIdx++];

                    fullState.ApplyPushMove(move);

                    // if (commonState != null) return;

                    var newZHash = fullState.GetZHash();

                    if (forwardVisitedStates.TryAdd(new HashState { zHash = newZHash, prevState = stateZHash, move = move })) {
                        if (backwardVisitedStates.GetState(newZHash).zHash != 0) {
                            commonState = newZHash;
                            return;
                        }
                        var moveIdx2 = fullState.GetPossibleMoves(moves);
                        if (moveIdx2 >= 0) {
                            statesToProcess.Enqueue(new ToProcess { state = newZHash, moveIdx = moveIdx2 });
                        }
                    }

                    fullState.ApplyPullMove(move);

                    if (move.IsLast) break;
                }
            }
        }

        DynamicList<HashState> sourceAncestors;
        DynamicList<HashState> targetAncestors;

        // general case: source=6, target=2
        //     5
        //    / \
        //   3   6(s)
        //  /
        // 2(t)
        // sourceAncestors: [6, 5], targetAncestors: [2, 3, 5]
        // we exclude the common state (5) from both
        // 
        private void MoveStateInto(State state, ulong targetZhash, StateTable visitedStates)
        {
            var sourceZHash = state.GetZHash();
            if (sourceZHash == targetZhash) return;

            sourceAncestors.idx = 0;
            targetAncestors.idx = 0;

            var sourceState = visitedStates.GetState(sourceZHash);
            sourceAncestors.Add(sourceState);
            var targetState = visitedStates.GetState(targetZhash);
            targetAncestors.Add(targetState);

            while (true) {
                sourceState = visitedStates.GetState(sourceState.prevState);
                if (sourceState.zHash != 0) {
                    var srcInTarget = targetAncestors.FindZhash(sourceState.zHash);
                    if (srcInTarget >= 0) {
                        // ignore items in targetAncestors after sourceState
                        targetAncestors.idx = srcInTarget;
                        break;
                    }
                    sourceAncestors.Add(sourceState);
                }

                targetState = visitedStates.GetState(targetState.prevState);
                if (targetState.zHash != 0) {
                    var targetInSrc = sourceAncestors.FindZhash(targetState.zHash);
                    if (targetInSrc >= 0) {
                        // ignore items in sourceAncestors after targetState
                        sourceAncestors.idx = targetInSrc;
                        break;
                    }
                    targetAncestors.Add(targetState);
                }
            }
            
            // walk up the tree from the source to the common ancestor node
            for (var i = 0; i < sourceAncestors.idx; i++) {
                state.ApplyPullMove(sourceAncestors.items[i].move);
            }

            for (var i = targetAncestors.idx - 1; i >= 0; i--) {
                state.ApplyPushMove(targetAncestors.items[i].move);
            }
        }

        

        // private void SolveReverse()
        // {
        //     var statesToProcess = new Queue<State>(endStates);

        //     while (statesToProcess.Count > 0) {
        //         var state = statesToProcess.Dequeue();

        //         var moves = state.GetPossibleMoves(true);
        //         foreach (var move in moves)
        //         {
        //             var newState = new State(state);
        //             var boxIdx = newState.ApplyPullMove(move);
        //             newState.CalculatePlayerReachableMap();

        //             if (commonState != null) return;

        //             if (backwardVisitedStates.TryAdd(newState, new CameFrom { state = state, 
        //                     move = new Move { boxIndex = boxIdx, direction = move.direction }
        //                 })) 
        //             {
        //                 if (forwardVisitedStates.ContainsKey(newState)) {
        //                     commonState = newState;
        //                     return;
        //                 }
        //                 statesToProcess.Enqueue(newState);
        //             }
        //         }
        //     }
        // }

        /** returns minimum indexes for each player-reachable area */
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
            Console.WriteLine($"{moves.items.Length} moves generated");
            var forwardSteps = new List<HashState>();
            var state = commonState;

            while (state != startState) {
                var fromState = forwardVisitedStates.GetState(state);
                forwardSteps.Add(fromState);
                state = fromState.prevState;
            }

            // var backwardSteps = new List<CameFrom>();
            // state = commonState;

            // while (!endStates.Contains(state)) {
            //     var from = backwardVisitedStates[state];
            //     backwardSteps.Add(from);
            //     state = from.state;
            // }

            var sb = new StringBuilder();
            var playerPos = WriteSolutionMoves(sb, forwardSteps);
            // WriteReversedSolutionMoves(sb, backwardSteps, playerPos);
            string solution = sb.ToString();

            Console.WriteLine(solution);
            int pushCount = forwardSteps.Count/*  + backwardSteps.Count */;
            Console.WriteLine($"{pushCount} pushes, {solution.Length - pushCount} moves");
        }

        private int WriteSolutionMoves(StringBuilder sb, List<HashState> steps) 
        {
            steps.Reverse();
            var playerPos = level.playerPosition;

            MoveStateInto(fullState, startState, forwardVisitedStates);

            foreach (var step in steps)
            {
                sb.Append(fullState.FindPlayerPath(playerPos, step.move));
                sb.Append(Move.PushCodeForDirection[step.move.Direction]);
                fullState.ApplyPushMove(step.move);
                playerPos = fullState.playerPosition;
            }

            return playerPos;
        }

        // private void WriteReversedSolutionMoves(StringBuilder sb, List<CameFrom> steps, int playerPos) 
        // {
        //     var state = commonState;

        //     foreach (var step in steps)
        //     {
        //         sb.Append(state.FindPlayerPath(playerPos, step.move));
        //         sb.Append(step.move.PushCode);
        //         playerPos = state.boxPositions[step.move.boxIndex];
        //         state = step.state;
        //     }
        // }

    }
}