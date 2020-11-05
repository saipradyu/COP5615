# Author Info
Sri Greeshma Avadhootha (UFID:1613-6609)
Sai Pradyumna Reddy Chegireddy (UFID: 3463-1711)


### Run syntax
```

dotnet fsi --langversion:preview proj3Bonus.fsx <numofNodes> <numofRequests> <numOfFailingNodes>


dotnet fsi --langversion:preview proj3Bonus.fsx 100 30 10

```
# Results

Number of nodes 	Number of Requests	 Number of failure nodes    Average Hop Count
-------------------------------------------------------------------------------------------
	50						10						3					1.925532
	100						30						10					2.306667
	200						50						30					2.625059
	500						10						2					3.117671
	700						10						3					3.319369
	1000					20						3					3.548044




# Largest Network
In this project, we tested the functionality of routing and joining aspects of the pastry protocol. 
The network has been tested for a maximum of 10000 nodes with 100 requests.

