# Student info
Sai Pradyumna Reddy Chegireddy (UFID: 3463-1711)
Sri Greeshma Avadhootha (UFID: 1613-6609)

# Instructions
The main source code is in **Proj2**:
* project2.fsx : This file defines main routines and network topology


### Run syntax
```
dotnet fsi --langversion:preview project2.fsx <numNode> <topology> <algorithm>
```
- numNode: number of nodes in the network
- topology: one of **full, 2d, line, imp2d**
- algorithm: one of **gossip, pushsum**

### Output 
Time it takes to converge in milliseconds

### Example
```
dotnet fsi --langversion:preview project2.fsx 1000 imp2d gossip
Gossip algorithm has converged in 13742L

```

# What is working
- All 4 topologies have been implemented for both, gossip and pushsum algorithms
- For _2d_ and _imp2d_ topologies, the number of nodes is rounded to the square of the square root of the input nodes.
- The convergence time is measured from the point when the program initiates the first message to the point when the 
network has achieved convergence

# Largest network

### Gossip
- Line: 5000. It takes about 636.657 seconds to achieve convergence
- 2D: 5000. It takes about 430.347 seconds to achieve convergence
- full: 5000. It takes about 276.347 seconds to achieve convergence
- imp2D: 5000. It takes about 421.646 seconds to achieve convergence

### Push-sum
- Line: 600. It takes about 1103.321 seconds (~ 18 minutes) to achieve convergence
- 2D: 1225. It takes about 164.639 seconds to achieve convergence
- full: 1000. It takes about 3.422 seconds to achieve convergence
- imp2D: 1225. It takes about 4.668 seconds to achieve convergence


