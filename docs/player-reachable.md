# Maintaining the PlayerReachable positions
Instead of recalculating the player-reachable positions after every box move, we could try to maintain it with a few small changes to the `reachableTable`.

## The Cases
Consider the case that the player is on the left of a box and pushes it right.
```
 ....  |  ####  |  #..#
 @$..  |  @$.#  |  @$..
 ....  |  ####  |  ....
   ↓        ↓        ↓ 
 ....  |  ####  |  #..#
 .@$.  |  .@$#  |  .@$.
 ....  |  ####  |  ....

 All empty cells are reachable before and after.
```

If there are no boxes/walls on the surrounding cells, it is obvious that we don't need to recalculate the whole `reachableTable`, we just set the old position of the box to be reachable - after all, the player is standing on that cell. The same is true if all surrounding cells are walls, or only a few like in the above cases.

But if there is an opening that the box closes during the move, we need to recalculate whether we can reach the cell with the question marks.
```
 .#.#  |  ...#  |  .#..
 @$..  |  @$..  |  @$..
 ....  |  ...#  |  ...#
   ↓        ↓        ↓  
 .#?#  |  ...#  |  .#??
 .@$.  |  .@$?  |  .@$?
 ....  |  ...#  |  ...#
 The box might block access to the cells marked with '?'.
```

In the following cases the opposite happens. Moving the box opens up previously unreachable (marked with 'x') cells.
```
 #x#.  |  #xxx  |  #xxx
 @$..  |  @$xx  |  @$xx
 ....  |  ..#x  |  #xxx
   ↓        ↓        ↓  
 #.#.  |  #...  |  #...
 .@$.  |  .@$.  |  .@$.
 ....  |  ..#.  |  #...
 The box opens up access to the cells marked with 'x'.
```

And there are cases when pushing the box both opens and closes an area.
```
 #x#.  
 @$..  
 ...#  
   ↓   
 #.#?  
 .@$?  
 ...#  
```

I don't see a *simple* logic that can determine which case it is, based on the box positions. It seems that we need to calculate whether each cell is reachable before and after the move. If there are cells that change their reachable state, then we need to act.

Doing a (small) reachable search every time we push a box probably doesn't help. However, we can generate the decision for all possible wall configurations. There are 9 cells that can be empty or box/wall. That means 2^9 = 512 different cases.

## Possible Outcomes

1. The best possible outcome is that we don't need to do a fill, like in the first few cases, just update the reachableTable at the new player position.
1. A slightly worse scenario is when the box push opens a room that was not reachable before. In this case we need to do a fill, but only for that room (starting from the opened cell). We don't need to re-fill the whole table.
1. The worst case is when the push closes a room, because we cannot be sure that it is not reachable via another path. However, we can start filling it with a number that is not the currentReachable (maybe currentReachable+1) and if the player position gets filled with that number, then we know that it's the new "reachable" number. Otherwise it represents a non-reachable area.

```
.#.#
@$..
.#.#

.#.#
.@$.
.#.#
```

Need to fill closed areas first, then the opened ones (if both are present).
```
      p123456   
#22#   12222#   
1$22            
1#2#            
      p123456   
#11#   11145#   
11$4              In theory, don't need to fill both 4 and 5, becuase even though they got separated from 2 (P3),
1#5#              they are different from 1 (P2) after filling P2 - need optimization
```

If there's no opening, just closing: need to fill P1, instead of the closed cell, to get the new normalized player position.

```
.#.#    P345 = 333
.$..
.#.#

.#.#    P345 = 345 -> Instead of (Fill P4, P5), Fill P1, 
..$.
.#.#

```
