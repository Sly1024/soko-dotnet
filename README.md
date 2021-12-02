# soko-dotnet
Sokoban solver in dotnet

Build:
```
dotnet build -c Release
```

Run:
```
.\bin\Release\net6.0\soko.exe .\levels\orig_1.sok
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

EDIT: I found another (better?) way to detect dead cells. A dead cell is a cell from which you cannot reach any of the goal 
positions with push moves. We can simulate pulling a box (on an empty board) from each goal position into all possible cells.
The cells that we cannot reach this way are the push-dead cells. The same idea works for detecting pull-dead cells, 
just try pushing boxes from each initial box position.

## Phase 5 - Zobrist hashes and some

This is quite a big change since the last phase. I introduced Zobrist hashing for states, MoveRanges for more efficient storage 
of move lists, improved the fill algorithm, and some logic to traverse the search tree without storing the states.

### Zobrist Hashing
You can read about this on [the Wikipedia page](https://en.wikipedia.org/wiki/Zobrist_hashing). In the `Level` class we
pre-generate a 64bit random number (bitstring) for each position on the table that is empty - can be occupied by a box or the 
player. We generate another table for the player. Basically the Zobrist hash for a state is the XOR of the values where the
boxes are and where the normalized player position is. 

Theoretically it's a hash function, so it is possible that two different states have the same Z-hash value, but we're going to
ignore this for now. Essentially we can just use the Z-hash value to identify the state uniquely. 

### CompactHashTable
I can't just use an ordinary HashTable or Dictionary, right? That would be no fun. And wasting memory. A Dictionary, in addition
to the key and the value, it stores other pieces of information. The reference implementation of Dictionary&lt;TKey, TValue&gt; has 
extra [hashCode and next filed in its Entry](https://referencesource.microsoft.com/#mscorlib/system/collections/generic/dictionary.cs,61).
In addition it has a `buckets` array, which stores an int for each entry. In total that is 3x4 = 12 bytes extra for each entry.
If I only want to store a small struct (18 bytes at the moment) but millions of it, then a Dictionary would waste a lot of memory.

So I decided to do something about it. I implemented a hash table that stores my `HashState` (terrible name, I know) structs in
an array, so there's no overhead for each entry. It uses the already present `zHash` as hash code and indexes the array with it.
For collision resolution I tried [quadratic probing with alternating signs](https://en.wikipedia.org/wiki/Quadratic_probing), 
but it is actually slower than linear probing. It allows for a higher load factor while maintaining the speed, but I'm using
linear probing for now.

I also tried [Hopscotch hashing](https://en.wikipedia.org/wiki/Hopscotch_hashing), which keeps its performance with very high load factors,
but again, it is not much improvement, just complicates the code a lot.

I limited the load factor to 75% to keep the performance up, and it grows by a factor of 1.75 when more items are added.

Note: For the first original level, in phase 4, the solver needed ~1800MB RAM to store all the State objects, 
with this new hash table, it only needs ~100MB.

### Walking the Tree
Since we store the visited states in a hash table and it also stores the previous state (from where we arrived to the current state)
and the move that is needed to get to the state, we don't need to store the whole State object. The idea is that we only have 
**one active state object** which represents the state we're processing. After generating the possible moves, we apply each move
on our state, then we generate the Z-hash for that sate and store it in the `visitedStates` table. We can *undo* the move to get
back to the previous state so we can use it to process the next move.

When all moves are processed on the current state, we take the next state from the queue. Remember, it's just a Z-hash value, so
we need to "move" our only state object into that state. Fortunately we can do that by finding a path in the `visitedStates` 
table which stores our search tree. We know the Z-hash value for the current state and the one for the target state. We need to 
walk up the "parent" chain from both states (current and target) and eventually we'll find a common node on the paths. We
apply the steps in reverse (`ApplyPullMove`) when walking upward from the current state to the common node. Then
we apply the steps forward when going down on the common -> target path. This is done by the `MoveStateInto` function.

### MoveRanges
First, I realized that when a state is reached by applying a move, we need to generate the Z-hash which requires the normalized 
player position, so we `CalculatePlayerReachableMap` and it is a costly operation. Then we add the state to our `statesToProcess`
queue and some time later we take that state out and need to generate the possible moves, which also needs to 
`CalculatePlayerReachableMap` - so we calculate the same reachable map twice for each state.

I had an idea that when we reach a state, we do have the reachable map, so we might as well generate the possible moves and store
them together with the sate (Z-hash) in our queue, so when we take the state from the queue we already have the moves list.

I am not sure if it is worth doing, however, because this increases the memory requirement and also we generate the possible 
moves for states that we might never need to process - if we reach the solution, then all states in the `statesToProcess` 
queue can be dropped without processing. I'll leave this in for now, the solving speed seems to be the same either way.

I didn't want to store many List&lt;Move&gt; objects (possibly millions) when a move list could be as small as 2-3 moves, each
of them a 2 byte struct, so that would need 3*2=6 bytes and a pointer to my List object is 8 bytes (with a x64 runtime).
The `MoveRanges` class can store multiple lists in a single array. When we generate a moves list it allocates a range from its 
underlying array and gives back the index. I didn't want to store the length of the list separately, so instead I sacrificed
one bit from the 16bit `ushort` that the move is encoded into, to indicate whether it's the last one in the list. This way
knowing the index where the range starts identifies the list, because we know when we reach the last move item.

Now, in order to be able to free up ranges and reuse them I had to do a little bit of maintenance logic. I know it sounds like 
I want to implement the Garbage Collector again, but my solution is more space efficient than allocating objects on the heap
(and it's fun, shhh..). When a range is freed up I store an index to it in the `firstFree` array, which is indexed by the size
of the range. Basically each size has a linked list starting at `firstFree[size]` and if there is a next range then its index
is encoded in the first two move items (needed two, because it's only 16bits and the index can be bigger). This also means I
can't allocte a 1-item range because I wouldn't be able to "reclaim" it, so the minimum size is 2.

### Fill algorithm improvements
I also wanted to improve the flood-fill algorithm which is used by `CalculatePlayerReachableMap`. I always knew that the naive
4-way fill was inefficient and it's on a hot path, so optimizing it would speed up the program a lot. 

I tried implementing a scanline fill, but surprisingly it was actually slower - maybe I'm doing it wrong? It's also possible
that the sokoban levels I'm feeding it have very narrow pathways, like a maze, and the scanline fill is only better in
cases where there are large empty spaces to fill.

I settled with a slightly improved 4-way fill - inlined the testing condition, which performs well for now. I have plans to 
reduce the use of flood-fill in the future.

## Phase 6 - Deadlocks and A* search

### Freeze deadlock detection

The basic idea is that we should detect a case when some boxes are not movable and the level cannot be solved.
See a [good description here](http://sokobano.de/wiki/index.php?title=Deadlocks#Freeze_deadlocks).
If we can detect a situation like this, we can ignore the current state, we don't need to expand it.
Deadlock detection is possible in both forward and backward solves.
First we need to detect if a box is immovable and then check if it rests on a goal position. There are cases when multiple 
boxes constitute a deadlock pattern.

#### Forward case - push deadlock

A box is not movable if it is blocked both horizontally and vertically. Blocking can occur either by a wall being next to it, 
or another box that cannot be moved. If the neighbor box can be moved, we treat it as if it was an empty cell. 
The opposite directions (left-right, up-down) have the same condition: the cell next to the box in both directions is empty. 
For example: if both the left and right neighbor of a box is empty, it is pushable in both left and right directions (horizontally).

Example deadlock: two boxes next to each other at a wall. If they are not on goal positions then this is a deadlock situation.
```
####
 $$
```
Let's assume we just pushed the left box, so we first examine if the box is movable:
- it is not movable vertically because of the wall above it
- it is movable horizontall ONLY IF the right box is movable
Next we need to check if the right box is movable:
- it is not movable vertically because of the wall above it
- it is movable horizontall ONLY IF the left box is movable

You can see that this is a circular dependency between the two boxes, and that means neither can be moved.

The deadlock check is implemented with a recursive function that does simple checks first (is there a wall blocking the box, 
or an empty cell on both sides), then if it finds that there is a box next to the current box, it needs to calculate if 
that neighbor box is movable by calling itself.

How do we avoid infinite recursion here? We mark the boxes that we're currently visiting - just like in a DFS (depth-first search) in a graph.
This means when the recursive calls circle back and ask `isBoxMovable(box)` for one of the boxes that we're currently evaluating (marked), 
then the answer is NO, it is not movable, they are stuck together (like in the example above).

After discovering that the box we just pushed is not movable, we need to look at *all the boxes* that are marked, because they participate in the deadlock.
Check each marked box and if any of them is not on a goal position, then this is a deadlock.

#### Backward case - pull deadlock

This is very similar to the push deadlock, but the is-pullable condition is not symmetric for opposite directions. For a direction there needs to be two empty
cells next to it. Pull deadlocks are less frequent, and can be unintuitively strange configurations, but they occur.

Example: the box can be pulled up and then right, but after it gets into the middle cell, it is a pull deadlock (actually a dead cell).

```
..#..      ..#..
.....      .....
#...#  =>  #.$.#
.$...      .....
..#..      ..#..
```

### A* search

I decided to try the [A* search algorithm](https://en.wikipedia.org/wiki/A*_search_algorithm) instead of 
[the BFS](https://en.wikipedia.org/wiki/Breadth-first_search) that we used this far.
The `statesToProcess` queue is responsible for the order we processed (expanded) each board state, and 
it made sure we process the states in their discovery order, which is basically a BFS. This finds the 
shortest path (push optimal solution) to an end state, but it needs to process a lot of states.

A* on the other hand tries to process the states first that are more promising to lead to a solution sooner.
However, it relies on a heuristic function that estimates the remaining distance from a state to the end state.
"Distance" here means the number of steps or box pushes.

How do we calculate the number of pushes required to "finish" the game from the current state?
A number of ways [are described here](http://sokobano.de/wiki/index.php?title=Solver#Pushes_Lowerbound_Calculation).

We start by calculating the absolute minimum pushes needed to move every box to one of the goal 
positions. We treat each box as if there were no other boxes on the board. We can pre-calculate the minimum 
push distance from every cell to every goal position and use a lookup table when doing the heuristic calculation.
We also need to match every box to one of the goal positions, and *exactly* one box for each goal.

The optimal minimum cost perfect matching can be calculated, but it's a very expensive algorithm, so instead I just
use a greedy approach: choose the box-goal pair that has the lowest cost (shortest push distance) and continue with 
the next lowest that includes a box and a goal that has not been selected yet. This is not optimal, but it is fast
enough that we can perform this for every state. (Needs sorting all possible k^2 pairs by distance)

This could still be improved in different ways:
* Incrementally update the matching since only one box is pushed at every step
* Handle [linear conflicts](http://sokobano.de/wiki/index.php?title=Solver#Linear_Conflicts)
* Handle [frozen boxes](http://sokobano.de/wiki/index.php?title=Solver#Frozen_Boxes)

Now we have a heuristic distance, we can calulate the `f(n) = g(n) + h(n)` value for the current state and use it as
the priority in a *priority queue*. This means we add the pushes required to reach the current state and the heuristic
distance, which gives us an estimated total push count. This resulted in finding the solution quicker in most cases.

I had an idea: why do we use the total distance, why not just the remaining (heuristic) distance `h(n)`? 
Picking the state with a lower remaining distance should take us closer to the end state, right? Indeed, it seems to 
find the solution even faster if I use just the heuristic distance instead of the total distance.
I am not entirely sure why it gives better results, but I have some ideas. Probably the fact that every "edge" in our 
graph has a cost of 1 push makes it a special case compared to a graph with arbitrary costs. Another fact is that
the number of states (graph nodes) is so big and there is a high chance that a solution exists going through
any specific state. This finds a less optimal solution, but does it quicker.


