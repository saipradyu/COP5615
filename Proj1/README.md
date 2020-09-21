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
$ time dotnet fsi --langversion:preview Proj1.fsx 1000000 4
Real: 00:00:00.000, CPU: 00:00:00.000, GC gen0: 0, gen1: 0, gen2: 0


Proj1.fsx(104,5): warning FS0193: This expression is a function value, i.e. is missing arguments. Its type is 'a -> unit.

Real: 00:00:02.521, CPU: 00:00:06.453, GC gen0: 136, gen1: 13, gen2: 0

real    0m7.736s
user    0m0.000s
sys     0m0.046s

CPU Time : 6.453sec
Real Time : 2.521sec
Ratio : 2.559
```
# Work Unit Selection
Our algorithms for determining the sum of squares of a sequence and determining whether it is a perfect square run in linear time. We determined that for large problems, a work unit size that is an order of 10/100 smaller than the problem size was ideal as the work is distributed evenly across the actors and as a result all the cores of the processor are in use while for smaller work units, some actors finish computation earlier than the rest which results in loss of some parallelism. 

# Largest Problem Solved
The largest problem we managed to solve was *N* = 500,000,000 with a sequence length of *k* = 24:


```
$ time dotnet fsi --langversion:preview Proj1.fsx 500000000 24
Real: 00:00:00.000, CPU: 00:00:00.000, GC gen0: 0, gen1: 0, gen2: 0


Proj1.fsx(104,5): warning FS0193: This expression is a function value, i.e. is missing arguments. Its type is 'a -> unit.

1
9
20
25
44
76
121
197
304
353
540
856
1301
202289
2053
3112
54032
3597
5448
306060
353585
8576
12981
20425
128601
30908
35709
84996
534964
841476
1273121
2002557
3029784
3500233
5295700
8329856
12602701
19823373
29991872
34648837
52422128
82457176
124753981
196231265
296889028
342988229
Real: 00:03:49.909, CPU: 00:14:37.437, GC gen0: 195918, gen1: 5432, gen2: 5

real    3m55.464s
user    0m0.000s
sys     0m0.030s

CPU Time: 877.437sec
Real Time: 229.909sec
Ratio: 3.815
```

