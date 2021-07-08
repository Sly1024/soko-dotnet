# soko-dotnet
Sokoban solver in dotnet

## Phase 1

In the first iteration I just wanted to create the simpest solver that can solve a level. I'll improve it later.

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

It stops when the end position is reached (boxPositions == goalPositions). To be able to track back the necessary steps for
the solution, we keep a `CameFrom` record for each new visited state we store in `visitedStates`. It contains the state
from which the new state was reached and also the move (box, direction).
