using System.Text;
using System.Collections.Generic;
using soko.Collections;

namespace soko;

public class State
{
    readonly Level level;
    readonly BoxPositions boxPositions;
    readonly int initialPlayerPosition;
    ulong boxZhash;

    public PlayerReachable reachable;
    public PlayerReachable prevReachable;
    private HeuristicDistanceComputer hdComputer;

    public State(Level level, int[] initialBoxPositions, int initialPlayerPosition)
    {
        this.level = level;
        boxPositions = new(level.table.Length, initialBoxPositions);
        this.initialPlayerPosition = initialPlayerPosition;
        boxZhash = level.GetZHashForBoxes(initialBoxPositions);
        reachable = new(level, initialBoxPositions, initialPlayerPosition);
        prevReachable = new(level, initialBoxPositions, initialPlayerPosition);
        hdComputer = new HeuristicDistanceComputer(level);
    }

    // copy ctor
    public State(State other): this(other.level, other.boxPositions.ToArray(), other.initialPlayerPosition)
    {
    }

    // public int InsertPossiblePushMovesInto(MoveRanges moves, Move cameFrom)
    // {
    //     moves.StartAddRange();

    //     foreach (Move m in GetPossiblePushMoves(cameFrom)) moves.AddRangeItem(m);

    //     return moves.FinishAddRange();
    // }

    public IEnumerable<Move> GetPossiblePushMoves(Move cameFrom)
    {
        reachable.CalculateReachableMap();

        int cameFromOffset = 0;
        int cameFromBoxPos = 0;

        // Why do we need IsBoxOtherSideReachable??
        //
        // We want to avoid examining the move that undoes the last move - so moving a box back to where it was.
        // Imagine this setup, we just moved the box to the right, we don't want to move it back to the left, because that state is already examined.
        //
        // ######      ######      ######
        // #@$  #  =>  # @$ #  =>  # $@ # (same state, the player is on the other side, but it's the "same side" because it's reachable by the player)
        // #    #      #    #      #    #
        // ######      ######      ######
        //
        // However, this move might actually make sense, if the player opens up a room and then pushes the box back, but now the player is in the other room.
        // #######      #######      #######
        // # @$  #  =>  #  @$ #  =>  #  $@ # (the box is in the same position, but the player is on the other side => different state)
        // # #   #      # #   #      # #   #
        // #######      #######      #######
        //
        // This case only happens if the "other side of the box is unreachable" in the initial state.


        // if IsBoxOtherSideReachable is false, we don't want to calculate these, just leave them as 0
        if (cameFrom.IsBoxOtherSideReachable)
        {
            cameFromOffset = Level.DirOffset[cameFrom.Direction];
            cameFromBoxPos = cameFrom.BoxPos + cameFromOffset;
        }


        foreach (var boxPos in boxPositions.list)
        {
            for (var dir = 0; dir < 4; dir++)
            {
                var offset = Level.DirOffset[dir];
                if (reachable[boxPos - offset])
                {
                    // if IsBoxOtherSideReachable == false, cameFromBoxPos==0, so this will quickly fail
                    if (boxPos == cameFromBoxPos && offset == -cameFromOffset)
                    {
                        // this is the move that basically undoes the move `cameFrom`, so we don't need to check it, the resulting state is 
                        // from where we got to the current state
                        continue;
                    }

                    if (!reachable.Blocked(boxPos + offset) && !level.pushDeadCells[boxPos + offset])
                    {
                        yield return (boxPos, dir, otherSideReachable: reachable[boxPos + offset]);
                    }
                }
            }
        }

    }
    
    // public int InsertPossiblePullMovesInto(MoveRanges moves, Move cameFrom)
    // {
    //     moves.StartAddRange();

    //     foreach (Move m in GetPossiblePullMoves(cameFrom)) moves.AddRangeItem(m);

    //     return moves.FinishAddRange();
    // }
    public IEnumerable<Move> GetPossiblePullMoves(Move cameFrom)
    {
        reachable.CalculateReachableMap();

        int cameFromOffset = 0;
        int cameFromBoxPos = 0;

        // if IsBoxOtherSideReachable is false, we don't want to calculate these, just leave them as 0
        if (cameFrom.IsBoxOtherSideReachable)
        {
            cameFromOffset = Level.DirOffset[cameFrom.Direction];
            cameFromBoxPos = cameFrom.BoxPos;
        }

        foreach (var boxPos in boxPositions.list)
        {
            for (var dir = 0; dir < 4; dir++)
            {
                var offset = Level.DirOffset[dir];
                if (reachable[boxPos - offset])
                {
                    // if IsBoxOtherSideReachable == false, cameFromBoxPos==0, so this will quickly fail
                    if (boxPos == cameFromBoxPos && offset == -cameFromOffset)
                    {
                        // this is the move that basically undoes the move `cameFrom`, so we don't need to check it, the resulting state is 
                        // from where we got to the current state
                        continue;
                    }
                    if (!reachable.Blocked(boxPos - 2 * offset) && !level.pullDeadCells[boxPos - offset])
                    {
                        yield return (boxPos - offset, dir, otherSideReachable: reachable[boxPos + offset]);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reverts the step if failed (deadlock)
    /// </summary>
    /// <returns>false if it's a deadlock</returns>
    public bool ApplyPushMove(Move move)
    {
        var boxPos = move.BoxPos;
        var newBoxPos = move.NewBoxPos;

        if (reachable.ApplyPushMoveAndCheckDeadlock(boxPos, newBoxPos))
        {
            return false;
        }

        // update boxPositions
        boxPositions.Move(boxPos, newBoxPos);

        // update boxZhash
        boxZhash ^= level.boxZbits[boxPos] ^ level.boxZbits[newBoxPos];

        return true;
    }
    
    public bool ApplyPullMove(Move move)
    {
        var offset = Level.DirOffset[move.Direction];
        var newBoxPos = move.BoxPos;
        var boxPos = newBoxPos + offset;

        // reachable.ApplyPullMove(boxPos, newBoxPos, offset);
        if (reachable.ApplyPullMoveAndCheckDeadlock(boxPos, newBoxPos, offset)) {
            return false;
        }

        // update boxPositions
        boxPositions.Move(boxPos, newBoxPos);

        // update boxZhash
        boxZhash ^= level.boxZbits[boxPos] ^ level.boxZbits[newBoxPos];

        return true;
    }

    public void ApplyMove(Move move, bool pull)
    {
        var offset = Level.DirOffset[move.Direction];
        var boxPos = move.BoxPos;
        var newBoxPos = boxPos;

        if (pull) boxPos += offset; else newBoxPos += offset;

        // update boxPositions
        boxPositions.Move(boxPos, newBoxPos);

        // update boxZhash
        boxZhash ^= level.boxZbits[boxPos] ^ level.boxZbits[newBoxPos];

        if (pull) reachable.ApplyPullMove(boxPos, newBoxPos, offset); 
        else reachable.ApplyPushMove(boxPos, newBoxPos, offset);
    }

    public ulong GetZHash()
    {
        reachable.CalculateReachableMap();
        ulong z = boxZhash ^ level.playerZbits[reachable.playerPosition];
        // make sure we don't generate 0 (empty) or 1 (LOCKED_STATE)
        return z < CompactHashTable<HashState>.MIN_SAFE_KEY ? z + CompactHashTable<HashState>.MIN_SAFE_KEY : z;
    }


    internal int GetPlayerPositionFor(Move move) {
        return move.BoxPos - Level.DirOffset[move.Direction];
    }

    internal string FindPlayerPath(int playerPos, Move move)
    {
        return FindPlayerPath(playerPos, GetPlayerPositionFor(move));
    }

    internal string FindPlayerPath(int playerPos, int targetPos)
    {
        var width = level.width;
        reachable.ClearTable();
        reachable.table[targetPos] = 1;

        int distance = 0;

        Filler.Fill(reachable.table, width, targetPos, 
            pos => { distance = reachable.table[pos] + 1; return pos == playerPos; },
            (value, pos) => value == 0 ? distance : -1
        );

        var sb = new StringBuilder();
        while (playerPos != targetPos) {
            var dist = reachable.table[playerPos] - 1;
            if (reachable.table[playerPos + 1] == dist) { playerPos++; sb.Append('r'); }
            else if (reachable.table[playerPos - 1] == dist) { playerPos--; sb.Append('l'); }
            else if (reachable.table[playerPos + width] == dist) { playerPos += width; sb.Append('d'); }
            else if (reachable.table[playerPos - width] == dist) { playerPos -= width; sb.Append('u'); }
        }
        return sb.ToString();
    }

    public int GetHeuristicPushDistance()
    {
        return hdComputer.GetHeuristicDistance(boxPositions.list, reachable, true);
    }

    public int GetHeuristicPullDistance() {
        return hdComputer.GetHeuristicDistance(boxPositions.list, reachable, false);
    }

    public void CopyPrevReachable() {
        reachable.CalculateReachableMap();
        prevReachable.CopyFrom(reachable);
    }
}
