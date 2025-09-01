using System.Text;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading;
using System.Diagnostics;

namespace soko
{
    // using StateTable = Dictionary<ulong, HashState>;
    using StateTable = CompactHashTable<HashState>;
    using StateTableBack = CompactHashTable<HashState>;

    using StatesToProcess = ConcurrentBucketedPriorityQueue<ToProcess>;
    using StatesToProcessBck = ConcurrentBucketedPriorityQueue<ToProcessBck>;
    // using StatesToProcess = PriorityQueue<ToProcess, int>;
    // using StatesToProcessBck = PriorityQueue<ToProcessBck, int>;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HashState {
        public ulong zHash;
        public Move move;
        // converts a (ulong, Move) tuple to HashState
        public static implicit operator HashState((ulong z, Move m) tuple) => new HashState { zHash = tuple.z, move = tuple.m };
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ToProcess
    {
        public ulong state;
        // public int moveIdx;
        public int distance;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ToProcessBck
    {
        public ulong state;
        // public int moveIdx;
        public int distance;
        public byte bckStateIdx;
    }

    class SolutionIndicator
    {
        public ulong commonState = 0;
    }

    public partial class Solver(Level level)
    {

        private SolutionIndicator solutionIndicator;
        private ulong startStateZ;
        private List<ulong> endStateZs;

        private State fwdState;
        private List<State> bckStates;

        public StateTable forwardVisitedStates;
        public StateTableBack backwardVisitedStates;


        public StatesToProcess statesToProcess;
        public StatesToProcessBck statesToProcessBck;

        public static readonly int NumSolverThreadsPerSide = 5;  //(Environment.ProcessorCount-2) / 2;

        public Task Solve()
        {
            solutionIndicator = new SolutionIndicator();

            forwardVisitedStates = new StateTable(8 << 20);    // 16M slots
            backwardVisitedStates = new StateTableBack(8 << 20);

            statesToProcess = new StatesToProcess(NumSolverThreadsPerSide);
            statesToProcessBck = new StatesToProcessBck(NumSolverThreadsPerSide);

            var fwdSolvers = new ForwardSolverThread[NumSolverThreadsPerSide];
            var bckSolvers = new BackwardSolverThread[NumSolverThreadsPerSide];

            // prepare fwd states
            var state = new State(level, level.boxPositions, level.playerPosition);

            startStateZ = state.GetZHash();
            forwardVisitedStates.TryAdd(startStateZ, new HashState());  // parent of the root is "null" (zeros)

            fwdState = state;
            int pushDistance = fwdState.GetHeuristicPushDistance();

            if (pushDistance >= HeuristicDistances.Unreachable)
            {
                throw new InvalidOperationException($"Could not find a suitable box-goal pairing from start position, pushDistance={pushDistance}");
            }

            statesToProcess.Enqueue(new ToProcess
            {
                state = startStateZ,
                // moveIdx = fwdState.InsertPossiblePushMovesInto(movesFwd, (0, 0)), // cameFrom.IsBoxOtherSideReachable == false
                distance = 0
            }, pushDistance);


            for (int i = 0; i < NumSolverThreadsPerSide; i++)
                fwdSolvers[i] = new ForwardSolverThread(new State(fwdState), statesToProcess, forwardVisitedStates, backwardVisitedStates, solutionIndicator);

            // prepare bck states
            endStateZs = [];
            bckStates = [];

            var endPlayerPositions = GenerateEndPlayerPositions();
            foreach (var endPlayerPos in endPlayerPositions)
            {
                var endState = new State(level, level.goalPositions, endPlayerPos);
                var endStateZ = endState.GetZHash();

                backwardVisitedStates.TryAdd(endStateZ, new HashState());
                // forwardVisitedStates.TryAdd(endStateZ, (0, new Move { BackwardStateBit = true }), out var _);

                int pullDistance = endState.GetHeuristicPullDistance();

                if (pullDistance >= HeuristicDistances.Unreachable)
                {
                    throw new InvalidOperationException($"Could not find a suitable box-goal pairing from end position, pullDistance={pullDistance}");
                }

                statesToProcessBck.Enqueue(new ToProcessBck
                {
                    bckStateIdx = (byte)bckStates.Count,
                    state = endStateZ,
                    // moveIdx = endState.GetPossiblePullMoves(movesBck, (0, 0)), // cameFrom.IsBoxOtherSideReachable == false
                    distance = 0
                }, pullDistance);
                endStateZs.Add(endStateZ);
                bckStates.Add(endState);
            }

            for (int i = 0; i < NumSolverThreadsPerSide; i++)
                bckSolvers[i] = new BackwardSolverThread([.. bckStates.Select(s => new State(s))], statesToProcessBck, forwardVisitedStates, backwardVisitedStates, solutionIndicator);

            return Task.WhenAny([
                .. fwdSolvers.Select(solver => Task.Run(() => { while (solutionIndicator.commonState == 0) solver.SolveForwardOneStep(); })),
                .. bckSolvers.Select(solver => Task.Run(() => { while (solutionIndicator.commonState == 0) solver.SolveReverseOneStep(); })),
            ]);
        }



        // general case: source=6, target=2
        //     5
        //    / \
        //   3   6(s)
        //  /
        // 2(t)
        // sourceAncestors: [6, 5], targetAncestors: [2, 3, 5]
        // we exclude the common state (5) from both
        // 
        private static Move? MoveStateInto(State state, ulong targetZhash, StateTable visitedStates, bool backward, DynamicList<HashState> sourceAncestors, DynamicList<HashState> targetAncestors)
        {
            var sourceZHash = state.GetZHash();
            if (sourceZHash == targetZhash) return null;

            sourceAncestors.Clear();
            targetAncestors.Clear();

            var sourceState = visitedStates[sourceZHash];
            sourceAncestors.Add((sourceZHash, sourceState.move));
            var targetState = visitedStates[targetZhash];
            targetAncestors.Add((targetZhash, targetState.move));

            while (true)
            {
                var sourcePrevZ = sourceState.zHash;
                if (sourcePrevZ != 0)
                {
                    var srcInTarget = targetAncestors.FindZhash(sourcePrevZ);
                    if (srcInTarget >= 0)
                    {
                        // ignore items in targetAncestors after sourceState
                        targetAncestors.Truncate(srcInTarget);
                        break;
                    }
                    sourceState = visitedStates[sourcePrevZ];
                    sourceAncestors.Add((sourcePrevZ, sourceState.move));
                }

                var targetPrevZ = targetState.zHash;
                if (targetPrevZ != 0)
                {
                    var targetInSrc = sourceAncestors.FindZhash(targetPrevZ);
                    if (targetInSrc >= 0)
                    {
                        // ignore items in sourceAncestors after targetState
                        sourceAncestors.Truncate(targetInSrc);
                        break;
                    }
                    targetState = visitedStates[targetPrevZ];
                    targetAncestors.Add((targetPrevZ, targetState.move));
                }
            }

            // walk up the tree from the source to the common ancestor node
            for (var i = 0; i < sourceAncestors.Count; i++)
            {
                state.ApplyMove(sourceAncestors.items[i].move, !backward);
            }

            for (var i = targetAncestors.Count - 1; i >= 0; i--)
            {
                state.ApplyMove(targetAncestors.items[i].move, backward);
            }

            // last move
            return targetAncestors.Count > 0 ? targetAncestors.items[0].move : null;
        }



        /** returns minimum indexes for each player-reachable area */
        private int[] GenerateEndPlayerPositions()
        {
            var table = new int[level.table.Length];
            for (var i = 0; i < table.Length; i++)
            {
                // at the end state, all boxes are on goal positions
                table[i] = level.table[i].has(Cell.Wall | Cell.Goal) ? 1 : 0;
            }

            var playerPositions = new List<int>();

            for (var i = 0; i < table.Length; i++)
            {
                if (table[i] == 0)
                {
                    playerPositions.Add(i);
                    Filler.FillBoundsCheck(table, level.width, i, value => value == 0, 1);
                }
            }

            return [..playerPositions];
        }

        public void PrintSolution()
        {
            if (solutionIndicator.commonState == 0) return;

            var forwardSteps = new List<HashState>();
            var state = solutionIndicator.commonState;

            while (state != startStateZ)
            {
                var fromState = forwardVisitedStates[state];
                forwardSteps.Add(fromState);
                state = fromState.zHash;
            }

            var backwardSteps = new List<HashState>();
            state = solutionIndicator.commonState;

            while (!endStateZs.Contains(state))
            {
                var fromState = /*forwardVisitedStates[state]; */ backwardVisitedStates[state];
                backwardSteps.Add(fromState);
                state = fromState.zHash;
            }

            var endIdx = endStateZs.IndexOf(state);
            var bckState = bckStates[endIdx];

            var sb = new StringBuilder();
            var playerPos = WriteSolutionMoves(sb, forwardSteps);
            WriteReversedSolutionMoves(sb, backwardSteps, playerPos, bckState);
            string solution = sb.ToString();


            int pushCount = forwardSteps.Count + backwardSteps.Count;
            Console.WriteLine($"{pushCount} (F/B:{forwardSteps.Count}/{backwardSteps.Count}) pushes, {solution.Length - pushCount} moves, " +
                $"deadlockrate: {fwdState.reachable._pulldeadlockCnt}/{fwdState.reachable._pullmoveCnt}" +
                $":{bckState.reachable._pulldeadlockCnt}/{bckState.reachable._pullmoveCnt}"
            );

            Console.WriteLine(solution);

        }

        private int WriteSolutionMoves(StringBuilder sb, List<HashState> steps)
        {
            steps.Reverse();
            var playerPos = level.playerPosition;

            MoveStateInto(fwdState, startStateZ, forwardVisitedStates, false, new (100), new (100));

            foreach (var step in steps)
            {
                sb.Append(fwdState.FindPlayerPath(playerPos, step.move));
                sb.Append(Move.PushCodeForDirection[step.move.Direction]);
                fwdState.ApplyPushMove(step.move);
                playerPos = fwdState.reachable.playerPosition;
            }

            return playerPos;
        }

        private void WriteReversedSolutionMoves(StringBuilder sb, List<HashState> steps, int playerPos, State bckState)
        {
            // this shouldn't be necessary, the state is already in the commonState
            MoveStateInto(bckState, solutionIndicator.commonState, backwardVisitedStates /*forwardVisitedStates*/, true, new (100), new (100));

            foreach (var step in steps)
            {
                sb.Append(bckState.FindPlayerPath(playerPos, step.move));
                sb.Append(Move.PushCodeForDirection[step.move.Direction]);
                bckState.ApplyPushMove(step.move);
                playerPos = step.move.BoxPos;
            }
        }


        /* */


        class ForwardSolverThread(State fwdState, StatesToProcess statesToProcess, StateTable forwardVisitedStates, StateTableBack backwardVisitedStates, SolutionIndicator solution)
        {

            readonly DynamicList<HashState> sourceAncestors = new(100);
            readonly DynamicList<HashState> targetAncestors = new(100);

            public Move MoveStateInto2(State state, ulong targetZHash)
            {
                return MoveStateInto(state, targetZHash, forwardVisitedStates, false, sourceAncestors, targetAncestors) ?? (0, 0);
            }
            public void SolveForwardOneStep()
            {
                if (statesToProcess.Count > 0)
                {
                    var toProcess = statesToProcess.Dequeue();
                    var pushes = toProcess.distance + 1;
                    ulong stateZHash = toProcess.state;

                    var lastMove = MoveStateInto2(fwdState, stateZHash);

                    // Console.WriteLine("Processing state...");
                    // fullState.PrintTable();

                    foreach (Move move in fwdState.GetPossiblePushMoves(lastMove).ToArray())
                    {
                        if (fwdState.ApplyPushMove(move))
                        {
                            var newZHash = fwdState.GetZHash();

                            if (forwardVisitedStates.TryAdd(newZHash, (stateZHash, move)))
                            {
                                if (backwardVisitedStates.ContainsKey(newZHash))
                                {
                                    Interlocked.CompareExchange(ref solution.commonState, newZHash, 0);
                                    return;
                                }

                                int pushDistance = fwdState.GetHeuristicPushDistance();
                                if (pushDistance < HeuristicDistances.Unreachable)
                                {
                                    statesToProcess.Enqueue(
                                        new ToProcess { state = newZHash, distance = pushes },
                                        pushDistance + pushes
                                    );
                                }
                            }
                            // revert the move
                            fwdState.ApplyMove(move, pull: true);
                        }
                    }
                }
            }
        }

        class BackwardSolverThread(State[] bckStates, StatesToProcessBck statesToProcessBck, StateTable forwardVisitedStates, StateTableBack backwardVisitedStates, SolutionIndicator solution)
        {
            readonly DynamicList<HashState> sourceAncestors = new(100);
            readonly DynamicList<HashState> targetAncestors = new(100);

            public Move MoveStateInto2(State state, ulong targetZHash)
            {
                return MoveStateInto(state, targetZHash, backwardVisitedStates, true, sourceAncestors, targetAncestors) ?? (0, 0);
            }
            public void SolveReverseOneStep()
            {
                if (statesToProcessBck.Count > 0)
                {
                    var toProcess = statesToProcessBck.Dequeue();
                    var pulls = toProcess.distance + 1;
                    ulong stateZHash = toProcess.state;

                    var bckState = bckStates[toProcess.bckStateIdx];
                    var lastMove = MoveStateInto2(bckState, stateZHash);

                    foreach (Move m in bckState.GetPossiblePullMoves(lastMove).ToArray())
                    {
                        var move = m;
                        if (bckState.ApplyPullMove(move))
                        {
                            var newZHash = bckState.GetZHash();

                            move.BackwardStateBit = true;
                            if (backwardVisitedStates.TryAdd(newZHash, (stateZHash, move)))
                            {
                                if (forwardVisitedStates.ContainsKey(newZHash))
                                {
                                    Interlocked.CompareExchange(ref solution.commonState, newZHash, 0);
                                    return;
                                }
                                int pullDistance = bckState.GetHeuristicPullDistance();
                                if (pullDistance < HeuristicDistances.Unreachable)
                                {
                                    statesToProcessBck.Enqueue(
                                        new ToProcessBck { state = newZHash, bckStateIdx = toProcess.bckStateIdx, distance = pulls },
                                        pullDistance + pulls
                                    );
                                }
                            }
                            bckState.ApplyMove(move, pull: false);
                        }
                    }
                }
            }
        }

    }       /// Solver

}