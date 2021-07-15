# soko-dotnet
Sokoban solver in dotnet

Build:
```
dotnet build -c Release
```

Run:
```
.\bin\Release\net5.0\soko.exe .\levels\orig_1.sok
```

## Phase 1 - Simple Brute-force

In the first iteration I just wanted to create the simplest solver that can solve a level. I'll improve it later.

It is able to do it by reading in a `.sok` file in text format (passed as argument).
The `Level` class is responsible for parsing the text, building a table with Cells and extracting the box positions, 
goal positions and player position. Each "position" on the board is identified by a linear index starting from 0.


The `State` class is the key component - for now. It stores:
- boxPositions - an int array storing the box positions *always* in ascending order
- playerPosition - int, the smallest position the player can reach (calculated by `CalculatePlayerReachableMap`)
- table - int array, storing walls, boxes and player-reachable cells, also acts as a path finder for `FindPlayerPath`

The `Solver` uses a BFS (breadth first search) algorithm that stores the reached, but not processed states in a queue.
Takes a non-processed state, generates all possible "moves" and checks if any of those new states have been visited or not.
For efficiency I don't count each *player move* as a step, instead I consider a *box push* a valid step. 
This reduces the number of states I need to check. 

It stops when the end state is reached (boxPositions == goalPositions). To be able to track back the necessary steps for
the solution, we keep a `CameFrom` record for each new visited state we store in `visitedStates`. It contains the state
from which the new state was reached and also the move (box, direction).

## Phase 2 - Reverse solving

In this phase I tried reverse solving the levels. It means that the BFS search starts at the end state and goes backwards
until it finds the starting state.

The tricky part is that while there's only one start state, there can be multiple end states. That is because in the 
solved state there can be multiple isolated rooms, and it doesn't matter which room the player is in.
In my implementation the sate contains (roughly) which room the player is in, so I need to start from all possible end states
where the boxes are at the goal positions, and the player is in one of the rooms.

Each "move" is a **box pull**. It is a bit harder to imagine, but essentially it's the inverse of a box push. The conditions
for when a box can be pulled in a certain direction is different from the push conditions, but very similar.

Surprisingly, the reverse solve (without any deadlock detection) performs much better, becaue it avoids a lot of states where
the boxes would be in a deadlock position. Imagine pushing a box into a corner. You can never pull a box into a corner.
There are still room for improvement, but I'm hoping that the combined forward + reverse solve will perform even better.

## Phase 3 - Forward + Backward solving

I run both the forward and backward solving (on two different threads), and whenever one of them reaches a state that has
already been visited by the other thread then we have a solution. 

In theory this should be more efficient than both of the one-direction solves, but I need to use a ConcurrentDictionary to
facilitate accessing both `visitedStates` collection from the other thread.

An interesting side effect is that the solution is non-deterministic. Whichever thread finds the "common" state will exit 
from its main loop and set the `commonState` variable, which is tested in each loop cycle in the other thread, so it can
also exit as soon as a solution is found. The solution depends on the timing of the threads. 

However, since this (and all previous) iterations are based on a BFS in push moves, all solutions are push-optimal.
E.g. the number of pushes are always the same, but the number of moves may wary depending on thread timing.

Note: There's a bug, the demo0.sok (simplest level) has a one move solution and if the *wrong* thread finishes first, 
the program runs into an error trying to index a dictionary with a null key. I'm not going to fix this, this is not the 
final solution anyway.

## Phase 4 - Dead cell marking

A "dead cell" is a cell on the board that results in a deadlock (cannot solve the level) when a box is pushed into it.
The simplest example is a corner that is not a goal position. If a box is pushed into a corner, there is no way to move it
any further. Other dead cells are empty cells along a wall in case the run does not contain goal positions and 
both ends are dead cell corners.

Example: dead cells are marked with an 'x'.

```
########       # - wall
#xxxxxx#       @ - player
#x @ $ #       $ - box
#x    .#       . - goal
########       x - dead cell
```

These cells can be detected and marked once the level is loaded and we can prevent pushing a box into them by filtering out 
these in the `GetPossibleMoves` function. This only applies to *push moves*. A pull move can never move a box into a dead cell.

Note: Even though dead cell detection only filters out moves/states from a forward solve, it still affects the dual
forward + backward solve because the two threads reach the common state sooner with less visited states on both sides.
