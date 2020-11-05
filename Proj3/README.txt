# Author Info
Sri Greeshma Avadhootha (UFID:1613-6609)
Sai Pradyumna Reddy Chegireddy (UFID: 3463-1711)


### Run syntax
```
dotnet fsi --langversion:preview proj3.fsx <numofNodes> <numofRequests>

dotnet fsi --langversion:preview proj3.fsx 100 40
```
# Results

Number of nodes 	Number of Requests	  Average Hop Count
------------------------------------------------------------
	50						15					1.956
	100						40					2.29125
	300						120					2.88725
	500						200					3.13944
	1000					300					3.54613
	2000					500					3.943742
	3000					1000				4.184323
	350						1200				4.289222
	10000					100					4.952315



# Largest Network
In this project, we tested the functionality of routing and joining aspects of the pastry protocol. 
The network has been tested for a maximum of 10000 nodes with 100 requests.

