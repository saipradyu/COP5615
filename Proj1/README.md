# Author Info

Sai Pradyumna Reddy Chegireddy (UFID: 3463-1711)
Sri Greeshma Avadhootha (UFID:1613-6609)

# Instructions
To configure the desired number of workers, change line #9 in Proj1.fsx

### Run syntax
```
dotnet fsi --langversion:preview test.fsx <n> <k>
```
# Results
When running
```
$ dotnet fsi --langversion:preview test.fsx 1000000 4
```
There are **no** sequences that satisfy the condition in this input range

### Runtime
```
$ time dotnet fsi --langversion:preview test.fsx 1000000 4

CPU Time: 
Clock Time:
Ratio:
```
# Work Unit Selection
Our algorithms for determining the sum of squares of a sequence and determining whether it is a perfect square run in linear time. We determined that for large problems, a work unit size that is an order of 10/100 smaller than the problem size was ideal as the work is distributed evenly across the actors and as a result all the cores of the processor are in use while for smaller work units, some actors finish computation earlier than the rest which results in loss of some parallelism. 

# Largest Problem Solved
The largest problem we managed to solve was *N* = 500,000,000 with a sequence length of *k* = 24:

```
$ time dotnet fsi --langversion:preview test.fsx 1000000 4
1
9
20
25

CPU Time: 
Clock Time:
Ratio:
```

