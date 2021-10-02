using System.Text;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace soko
{
    // using StateTable = Dictionary<ulong, HashState>;
    using StateTable = CompactHashTable<HashState>;
    using StateTableBack = CompactHashTable<HashState>;

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
        public int moveIdx;
    }
    public struct ToProcessBck
    {
        public ulong state;
        public int moveIdx;
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

        public MoveRanges moves = new MoveRanges(1000);

        public Queue<ToProcess> statesToProcess;
        public Queue<ToProcessBck> statesToProcessBck;

        DynamicList<HashState> sourceAncestors;
        DynamicList<HashState> targetAncestors;

        public Solver(Level level)
        {
            this.level = level;
        }

        public Task Solve()
        {
            sourceAncestors = new DynamicList<HashState>(100);
            targetAncestors = new DynamicList<HashState>(100);

            forwardVisitedStates = new StateTable(1<<17);
            backwardVisitedStates = new StateTableBack(1<<17);

            statesToProcess = new Queue<ToProcess>();
            statesToProcessBck = new Queue<ToProcessBck>();

            // prepare fwd states
            var state = new State(level, level.boxPositions, level.playerPosition);
            
            startStateZ = state.GetZHash();
            forwardVisitedStates.TryAdd(startStateZ, new HashState());

            fwdState = state;
            statesToProcess.Enqueue(new ToProcess{
                state = startStateZ,
                moveIdx = fwdState.GetPossiblePushMoves(moves, (0, 0)) // cameFrom.IsBoxOtherSideReachable == false
            });

            // prepare bck states
            endStateZs = new List<ulong>();
            bckStates = new List<State>();
            
            var endPlayerPositions = GenerateEndPlayerPositions();
            foreach (var endPlayerPos in endPlayerPositions)
            {
                var endState = new State(level, level.goalPositions, endPlayerPos);
                var endStateZ = endState.GetZHash();
                backwardVisitedStates.TryAdd(endStateZ, new HashState());
                statesToProcessBck.Enqueue(new ToProcessBck{
                    bckStateIdx = (byte)bckStates.Count,
                    state = endStateZ,
                    moveIdx = endState.GetPossiblePullMoves(moves, (0, 0)) // cameFrom.IsBoxOtherSideReachable == false
                });
                endStateZs.Add(endStateZ);
                bckStates.Add(endState);
            }


            return Task.Run(() => {
                while (commonState == 0) {
                    if (statesToProcess.Count > 0 && statesToProcess.Count < statesToProcessBck.Count) 
                        SolveForwardOneStep();
                    else 
                        SolveReverseOneStep();
                }
            });
        }



        private void SolveForwardOneStep()
        {
            if (statesToProcess.Count > 0) {
                var toProcess = statesToProcess.Dequeue();
                ulong stateZHash = toProcess.state;

                MoveStateInto(fwdState, stateZHash, forwardVisitedStates, false);

                // Console.WriteLine("Processing state...");
                // fullState.PrintTable();

                var mIdx = toProcess.moveIdx;
                Move move;
                do
                {
                    move = moves.items[mIdx++];
                    fwdState.ApplyPushMove(move);

                    if (!fwdState.isBoxDeadLocked(move.NewBoxPos)) {
                        var newZHash = fwdState.GetZHash();

                        if (forwardVisitedStates.TryAdd(newZHash, (stateZHash, move))) {
                            if (backwardVisitedStates.ContainsKey(newZHash)) {
                                commonState = newZHash;
                                return;
                            }
                            var moveIdx2 = fwdState.GetPossiblePushMoves(moves, move);
                            if (moveIdx2 >= 0) {
                                statesToProcess.Enqueue(new ToProcess { state = newZHash, moveIdx = moveIdx2 });
                            }
                        }
                    }

                    fwdState.ApplyPullMove(move);
                } while (!move.IsLast);
                moves.RemoveRange(toProcess.moveIdx, mIdx);
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
        private void MoveStateInto(State state, ulong targetZhash, StateTable visitedStates, bool backward)
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
                state.ApplyMove(sourceAncestors.items[i].move, !backward);
            }

            for (var i = targetAncestors.Count - 1; i >= 0; i--) {
                state.ApplyMove(targetAncestors.items[i].move, backward);
            }
        }

        private void SolveReverseOneStep()
        {
            if (statesToProcessBck.Count > 0) {
                var toProcess = statesToProcessBck.Dequeue();
                ulong stateZHash = toProcess.state;

                var bckState = bckStates[toProcess.bckStateIdx];

                MoveStateInto(bckState, stateZHash, backwardVisitedStates, true);

                // Console.WriteLine("Processing state...");
                // fullState.PrintTable();

                var mIdx = toProcess.moveIdx;
                Move move;
                do
                {
                    move = moves.items[mIdx++];
                    bckState.ApplyPullMove(move);

                    var newZHash = bckState.GetZHash();

                    if (backwardVisitedStates.TryAdd(newZHash, (stateZHash, move))) {
                        if (forwardVisitedStates.ContainsKey(newZHash)) {
                            commonState = newZHash;
                            return;
                        }
                        var moveIdx2 = bckState.GetPossiblePullMoves(moves, move);
                        if (moveIdx2 >= 0) {
                            statesToProcessBck.Enqueue(new ToProcessBck { state = newZHash, moveIdx = moveIdx2, bckStateIdx = toProcess.bckStateIdx });
                        }
                    }

                    bckState.ApplyPushMove(move);
                } while (!move.IsLast);
                moves.RemoveRange(toProcess.moveIdx, mIdx);
            }
        }

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

            while (state != startStateZ) {
                var fromState = forwardVisitedStates[state];
                forwardSteps.Add(fromState);
                state = fromState.zHash;
            }

            var backwardSteps = new List<HashState>();
            state = commonState;

            while (!endStateZs.Contains(state)) {
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

            Console.WriteLine(solution);
            int pushCount = forwardSteps.Count + backwardSteps.Count;
            Console.WriteLine($"{pushCount} pushes, {solution.Length - pushCount} moves");
        }

        private int WriteSolutionMoves(StringBuilder sb, List<HashState> steps) 
        {
            steps.Reverse();
            var playerPos = level.playerPosition;

            MoveStateInto(fwdState, startStateZ, forwardVisitedStates, false);

            foreach (var step in steps)
            {
                sb.Append(fwdState.FindPlayerPath(playerPos, step.move));
                sb.Append(Move.PushCodeForDirection[step.move.Direction]);
                fwdState.ApplyPushMove(step.move);
                playerPos = fwdState.playerPosition;
            }

            return playerPos;
        }

        private void WriteReversedSolutionMoves(StringBuilder sb, List<HashState> steps, int playerPos, State bckState) 
        {
            // this shouldn't be necessary, the state is already in the commonState
            MoveStateInto(bckState, commonState, backwardVisitedStates, true);

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