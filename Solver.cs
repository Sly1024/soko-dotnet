using System.Text;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;

namespace soko
{
    // using StateTable = Dictionary<ulong, HashState>;
    using StateTable = CompactHashTable<HashState>;
    using StateTableBack = CompactHashTable<HashState>;

    // using StatesToProcess = PriorityQueue<ToProcess>;
    // using StatesToProcessBck = PriorityQueue<ToProcessBck>;
    using StatesToProcess = PriorityQueue<ToProcess, int>;
    using StatesToProcessBck = PriorityQueue<ToProcessBck, int>;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HashState {
        public ulong zHash;
        public Move move;
        // converts a (ulong, Move) tuple to HashState
        public static implicit operator HashState((ulong z, Move m) tuple) => new HashState { zHash = tuple.z, move = tuple.m };
    }

    public struct ToProcess 
    {
        public ulong state;
        // public int moveIdx;
    }
    public struct ToProcessBck
    {
        public ulong state;
        // public int moveIdx;
        public byte bckStateIdx;
    }

    public class Solver
    {
        private Level level;
        private /* volatile */ ulong commonState = 0;
        private ulong startStateZ;
        private List<ulong> endStateZs;

        private State fwdState;
        private List<State> bckStates;

        public StateTable forwardVisitedStates;
        public StateTableBack backwardVisitedStates;

        // public MoveRanges movesFwd = new(1000);
        // public MoveRanges movesBck = new(1000);

        public StatesToProcess statesToProcess;
        public StatesToProcessBck statesToProcessBck;

        DynamicList<HashState> sourceAncestors1;
        DynamicList<HashState> targetAncestors1;
        
        DynamicList<HashState> sourceAncestors2;
        DynamicList<HashState> targetAncestors2;

        public Solver(Level level)
        {
            this.level = level;
        }

        public Task Solve()
        {
            sourceAncestors1 = new DynamicList<HashState>(100);
            targetAncestors1 = new DynamicList<HashState>(100);

            sourceAncestors2 = new DynamicList<HashState>(100);
            targetAncestors2 = new DynamicList<HashState>(100);

            forwardVisitedStates = new StateTable(1<<20);
            backwardVisitedStates = new StateTableBack(1<<20);

            statesToProcess = new StatesToProcess();
            statesToProcessBck = new StatesToProcessBck();

            // prepare fwd states
            var state = new State(level, level.boxPositions, level.playerPosition);
            
            startStateZ = state.GetZHash();
            forwardVisitedStates.TryAdd(startStateZ, new HashState());  // parent of the root is "null" (zeros)

            fwdState = state;
            int pushDistance = fwdState.GetHeuristicPushDistance();

            if (pushDistance >= HeuristicDistances.Unreachable) {
                throw new InvalidOperationException($"Could not find a suitable box-goal pairing from start position, pushDistance={pushDistance}");
            }

            statesToProcess.Enqueue(new ToProcess
            {
                state = startStateZ,
                // moveIdx = fwdState.InsertPossiblePushMovesInto(movesFwd, (0, 0)), // cameFrom.IsBoxOtherSideReachable == false
            }, pushDistance);

            // prepare bck states
            endStateZs = [];
            bckStates = [];
            
            var endPlayerPositions = GenerateEndPlayerPositions();
            foreach (var endPlayerPos in endPlayerPositions)
            {
                var endState = new State(level, level.goalPositions, endPlayerPos);
                var endStateZ = endState.GetZHash();
                backwardVisitedStates.TryAdd(endStateZ, new HashState());
                int pullDistance = endState.GetHeuristicPullDistance();

                if (pullDistance >= HeuristicDistances.Unreachable) {
                    throw new InvalidOperationException($"Could not find a suitable box-goal pairing from end position, pullDistance={pullDistance}");
                }

                statesToProcessBck.Enqueue(new ToProcessBck
                {
                    bckStateIdx = (byte)bckStates.Count,
                    state = endStateZ,
                    // moveIdx = endState.GetPossiblePullMoves(movesBck, (0, 0)), // cameFrom.IsBoxOtherSideReachable == false
                }, pullDistance);
                endStateZs.Add(endStateZ);
                bckStates.Add(endState);
            }


            // return Task.Run(() => {
            //     ToProcess fwdElem;
            //     ToProcessBck bckElem;
            //     int fwdPrio, bckPrio;
            //     while (commonState == 0 && statesToProcess.TryPeek(out fwdElem, out fwdPrio) && statesToProcessBck.TryPeek(out bckElem, out bckPrio)) {
            //         if (fwdPrio < bckPrio) 
            //             SolveForwardOneStep();
            //         else 
            //             SolveReverseOneStep();
            //     }
                
            // });

            return Task.WhenAny([
                // !!! HeuristicDistance re-uses private BitArrays, won't work with 2 threads!!!
                Task.Run(() => { while (commonState == 0) SolveForwardOneStep(); }),
                Task.Run(() => { while (commonState == 0) SolveReverseOneStep(); }),
            ]);
        }



        private void SolveForwardOneStep()
        {
            if (statesToProcess.Count > 0) {
                // var pushes = toProcess.pushes + 1;
                ulong stateZHash = statesToProcess.Dequeue().state;

                var lastMove = MoveStateInto(fwdState, stateZHash, forwardVisitedStates, false, sourceAncestors1, targetAncestors1) ?? (0, 0);

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
                                commonState = newZHash;
                                return;
                            }

                            int pushDistance = fwdState.GetHeuristicPushDistance();
                            if (pushDistance < HeuristicDistances.Unreachable)
                            {
                                statesToProcess.Enqueue(
                                    new ToProcess { state = newZHash },
                                    pushDistance
                                );
                            }
                        }
                        // revert the move
                        fwdState.ApplyMove(move, pull: true);
                    }
                }
            }
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

        private void SolveReverseOneStep()
        {
            if (statesToProcessBck.Count > 0) {
                var toProcess = statesToProcessBck.Dequeue();
                // var pushes = toProcess.pushes + 1;
                ulong stateZHash = toProcess.state;

                var bckState = bckStates[toProcess.bckStateIdx];

                var lastMove = MoveStateInto(bckState, stateZHash, backwardVisitedStates, true, sourceAncestors2, targetAncestors2) ?? (0, 0);

                foreach (Move move in bckState.GetPossiblePullMoves(lastMove).ToArray())
                {
                    // move = movesBck.items[mIdx++];
                    if (bckState.ApplyPullMove(move))
                    {
                        var newZHash = bckState.GetZHash();

                        if (backwardVisitedStates.TryAdd(newZHash, (stateZHash, move)))
                        {
                            if (forwardVisitedStates.ContainsKey(newZHash))
                            {
                                commonState = newZHash;
                                return;
                            }

                            int pullDistance = bckState.GetHeuristicPullDistance();
                            if (pullDistance < HeuristicDistances.Unreachable)
                            {
                                statesToProcessBck.Enqueue(
                                    new ToProcessBck { state = newZHash, bckStateIdx = toProcess.bckStateIdx },
                                    pullDistance
                                );
                            }
                        }
                        bckState.ApplyMove(move, pull: false);
                    }
                }
            }
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
                if (table[i] == 0) {
                    playerPositions.Add(i);
                    Filler.FillBoundsCheck(table, level.width, i, value => value == 0, 1);
                }
            }

            return [.. playerPositions];
        }

        public void PrintSolution()
        {
            if (commonState == 0) return;

            var forwardSteps = new List<HashState>();
            var state = commonState;

            while (state != startStateZ)
            {
                var fromState = forwardVisitedStates[state];
                forwardSteps.Add(fromState);
                state = fromState.zHash;
            }

            var backwardSteps = new List<HashState>();
            state = commonState;

            while (!endStateZs.Contains(state))
            {
                var fromState = backwardVisitedStates[state];
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
            Console.WriteLine($"{pushCount} ({forwardSteps.Count}/{backwardSteps.Count}) pushes, {solution.Length - pushCount} moves, " +
                $"deadlockrate: {fwdState.reachable._pulldeadlockCnt}/{fwdState.reachable._pullmoveCnt}" +
                $":{bckState.reachable._pulldeadlockCnt}/{bckState.reachable._pullmoveCnt}"
                );

            Console.WriteLine(solution);

        }

        private int WriteSolutionMoves(StringBuilder sb, List<HashState> steps) 
        {
            steps.Reverse();
            var playerPos = level.playerPosition;

            MoveStateInto(fwdState, startStateZ, forwardVisitedStates, false, sourceAncestors1, targetAncestors1);

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
            MoveStateInto(bckState, commonState, backwardVisitedStates, true, sourceAncestors2, targetAncestors2);

            foreach (var step in steps)
            {
                sb.Append(bckState.FindPlayerPath(playerPos, step.move));
                sb.Append(Move.PushCodeForDirection[step.move.Direction]);
                bckState.ApplyPushMove(step.move);
                playerPos = step.move.BoxPos;
            }
        }

    }
}