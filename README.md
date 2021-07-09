# soko-dotnet
Sokoban solver in dotnet

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
