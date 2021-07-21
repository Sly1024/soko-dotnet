using System.Text;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace soko
{
    // using StateTable = System.Collections.Generic.Dictionary<ulong, soko.HashState>;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct HashState {
        public ulong zHash;
        public Move move;
        // converts a (ulong, Move) tuple to HashState
        public static implicit operator HashState((ulong z, Move m) tuple) => new HashState { zHash = tuple.z, move = tuple.m };
    }
    public class Solver
    {
        private Level level;
        private /* volatile */ ulong commonState;
        private ulong startState;
        private List<ulong> endStates;

        private State fullState;

        public StateTable<HashState> forwardVisitedStates;
        public StateTable<HashState> backwardVisitedStates;

        public MoveRanges moves = new MoveRanges(1000);

        public Solver(Level level)
        {
            this.level = level;
        }

        public Task Solve()
        {
            sourceAncestors = new DynamicList<HashState>(100);
            targetAncestors = new DynamicList<HashState>(100);

            forwardVisitedStates = new StateTable<HashState>(1<<17);
            backwardVisitedStates = new StateTable<HashState>(1<<17);

            var state = new State(level, level.boxPositions, level.playerPosition);
            
            startState = state.GetZHash();
            forwardVisitedStates.TryAdd(startState, new HashState());

            fullState = state;

            endStates = new List<ulong>();
            
            var endPlayerPositions = GenerateEndPlayerPositions();
            foreach (var endPlayerPos in endPlayerPositions)
            {
                var endState = new State(level, level.goalPositions, endPlayerPos);
                endStates.Add(endState.GetZHash());
                backwardVisitedStates.TryAdd(endState.GetZHash(), new HashState());
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
            statesToProcess.Enqueue(new ToProcess{ state = startState, moveIdx = fullState.GetPossiblePushMoves(moves) });

            while (statesToProcess.Count > 0) {
                var toProcess = statesToProcess.Dequeue();
                ulong stateZHash = toProcess.state;

                MoveStateInto(fullState, stateZHash, forwardVisitedStates);

                // Console.WriteLine("Processing state...");
                // fullState.PrintTable();

                //fullState.GetPossibleMoves(possibleMoves, false);
                // foreach (var move in moves.GetRangeAt(toProcess.moveIdx))
                var mIdx = toProcess.moveIdx;
                Move move;
                do
                {
                    move = moves.items[mIdx++];
                    fullState.ApplyPushMove(move);

                    // if (commonState != null) return;

                    var newZHash = fullState.GetZHash();

                    if (forwardVisitedStates.TryAdd(newZHash, (stateZHash, move))) {
                        if (backwardVisitedStates.ContainsKey(newZHash)) {
                            commonState = newZHash;
                            return;
                        }
                        var moveIdx2 = fullState.GetPossiblePushMoves(moves);
                        if (moveIdx2 >= 0) {
                            statesToProcess.Enqueue(new ToProcess { state = newZHash, moveIdx = moveIdx2 });
                        }
                    }

                    fullState.ApplyPullMove(move);
                } while (!move.IsLast);
                moves.RemoveRange(toProcess.moveIdx, mIdx);
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
        private void MoveStateInto(State state, ulong targetZhash, StateTable<HashState> visitedStates)
        {
            var sourceZHash = state.GetZHash();
            if (sourceZHash == targetZhash) return;

            sourceAncestors.Clear();
            targetAncestors.Clear();

            var sourceState = visitedStates[sourceZHash];
            sourceAncestors.Add((sourceZHash, sourceState.move));
            var targetState = visitedStates[targetZhash];
            targetAncestors.Add((targetZhash, targetState.move));

            while (true) {
                var sourcePrevZ = sourceState.zHash; 
                if (sourcePrevZ != 0) {
                    var srcInTarget = targetAncestors.FindZhash(sourcePrevZ);
                    if (srcInTarget >= 0) {
                        // ignore items in targetAncestors after sourceState
                        targetAncestors.Truncate(srcInTarget);
                        break;
                    }
                    sourceState = visitedStates[sourcePrevZ];
                    sourceAncestors.Add((sourcePrevZ, sourceState.move));
                }

                var targetPrevZ = targetState.zHash;
                if (targetPrevZ != 0) {
                    var targetInSrc = sourceAncestors.FindZhash(targetPrevZ);
                    if (targetInSrc >= 0) {
                        // ignore items in sourceAncestors after targetState
                        sourceAncestors.Truncate(targetInSrc);
                        break;
                    }
                    targetState = visitedStates[targetPrevZ];
                    targetAncestors.Add((targetPrevZ, targetState.move));
                }
            }
            
            // walk up the tree from the source to the common ancestor node
            for (var i = 0; i < sourceAncestors.Count; i++) {
                state.ApplyPullMove(sourceAncestors.items[i].move);
            }

            for (var i = targetAncestors.Count - 1; i >= 0; i--) {
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
            Console.WriteLine($"{moves.idx}/{moves.items.Length} moves generated");

            var forwardSteps = new List<HashState>();
            var state = commonState;

            while (state != startState) {
                var fromState = forwardVisitedStates[state];
                forwardSteps.Add(fromState);
                state = fromState.zHash;
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