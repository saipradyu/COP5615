#time "on"
#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("FSharp")
let mutable flag = true

type Message =
    | Start    
    | StartRouting 
    | JoinNodes
    | Ack
    | JoinFinish
    | NodeNotFound 
    | RouteToNodeNotFound
    | AddFirstNode of int List
    | Action of string*int*int*int
    | AddRow of int*int[]
    | AddNodesInNeighborhood of int List
    | SendAckToMaster of int
    | RouteFinish of int*int*int
    | StartRoutingWorker



(**************************Utility*********************************)
match fsi.CommandLineArgs.Length with
| 3 -> ignore
| _ -> failwith "Requires number of nodes and number of requests"

let args = fsi.CommandLineArgs |> Array.tail
let numNodes = args.[0] |> int
let numRequests = args.[1] |> int

let getMasterRef = 
    let actorPath = @"akka://FSharp/user/master"
    select actorPath system

let getAllWorkerRef = 
    let actorPath = @"akka://FSharp/user/*"
    select actorPath system

let getWorkerRef s =
    let actorPath = @"akka://FSharp/user/" + string s
    select actorPath system

let random = System.Random()

let shuffle elements = elements |> List.sortBy (fun _ -> random.Next())

let clone elements = elements |> List.map (fun element -> element) 

let rec remove n lst = 
    match lst with
    | h::tl when h = n -> tl
    | h::tl -> h :: (remove n tl)
    | []    -> []

let shl (str1:string) (str2:string)= 
    let mutable count = 0;
    while count<str1.Length && count<str2.Length && str1.Chars(count) = str2.Chars(count) do
        count <- count+1
    count

let base10tobase4 (base10 : int) =
    let rec helper (int_, base4string) =
        if int_ = 0 then
            (int_, base4string)
        else
            let quotient = int_ / 4
            let remainder = int_ % 4
            let remainderstring = remainder.ToString()
            let newString = String.concat "" [remainderstring;base4string]
            helper (quotient, newString)
    let (_, answer) = helper (base10, "")
    answer

let numToBase4 raw length =
    let mutable str  = base10tobase4 raw
    let diff = length - str.Length
    if(diff>0) then
        let mutable j =0
        while(j<diff) do
            str <- "0" + str
            j <- j+1
    str
    

(***********************Master and Pastry Logic***********************)

let pastryBehaviour numNodes numRequests id maxRows (inbox: Actor<Message>) =
    let currNodeID = id
    let mutable smallNodeList = List.empty
    let mutable largeNodeList = List.empty
    let mutable numOfBack = 0
    let mutable table = Array2D.create maxRows 4 -1
    let mutable IDSpace = Math.Pow(4.0,maxRows|>double) |>int
    
    let buildLeafSet (completeNodesList: int list)  =
        for node in completeNodesList do
            if (node>currNodeID && (not(List.contains node largeNodeList))) then
                if(largeNodeList.Length<4) then
                    largeNodeList <- node::largeNodeList
                else
                    if(node<List.max(largeNodeList)) then
                        let maxRight = List.max(largeNodeList)
                        largeNodeList <- (remove maxRight largeNodeList)
                        largeNodeList <- node::largeNodeList
            elif (node<currNodeID && (not(List.contains node smallNodeList))) then
                if(smallNodeList.Length < 4) then
                    smallNodeList<- node::smallNodeList
                else
                    if(node>List.min(smallNodeList)) then
                        let minLeft = List.min(smallNodeList)
                        smallNodeList <- (remove minLeft smallNodeList)
                        smallNodeList <- node::smallNodeList
            let str1 = numToBase4 currNodeID maxRows
            let str2 = numToBase4 node maxRows
            let samePre = (shl str1 str2)
            let col = (numToBase4 node maxRows).Chars(samePre)|>string|>int
            if (table.[samePre,col]= (-1)) then
                table.[samePre, col] <- node

    let addNodeToLeafSet (node:int) =
        if(node> currNodeID && (not (List.contains node largeNodeList))) then
            if(largeNodeList.Length <4) then
                largeNodeList <- node::largeNodeList
            else
                if(node < List.max(largeNodeList)) then
                    let maxRight = List.max (largeNodeList)
                    largeNodeList <- (remove maxRight largeNodeList)
                    largeNodeList <- node::largeNodeList
        elif (node < currNodeID && (not (List.contains node smallNodeList))) then
            if(smallNodeList.Length < 4) then
                 smallNodeList <- node::smallNodeList
            else
                if(node> List.min (smallNodeList)) then 
                    let minLeft = List.min (smallNodeList)
                    smallNodeList <- (remove minLeft smallNodeList)
                    smallNodeList <- node::smallNodeList
        let str1 = numToBase4 currNodeID maxRows
        let str2 = numToBase4 node maxRows
        let samePre = (shl str1 str2) |>int
        let col = str2.Chars(samePre) |> string |> int 
        if(table.[samePre,col] = (-1)) then
            table.[samePre, col] <- node
        
    
    let rec loop =
        actor {
            let! message = inbox.Receive()
            let sender = inbox.Sender()
            let self = inbox.Self
            match message with
            | AddFirstNode (firstGroup) ->
                let newFirstGroup =  (remove currNodeID firstGroup)
                buildLeafSet newFirstGroup
                for i=0 to maxRows-1 do
                    let col = (numToBase4 currNodeID maxRows).Chars(i) |> string |> int 
                    table.[i, col] <- currNodeID
                sender <! JoinFinish
            | Action (message, fromID, toID, hops) ->
                if(message.Equals("Join")) then 
                    let str1 = numToBase4 currNodeID maxRows
                    let str2 = numToBase4 toID maxRows
                    let samePre = shl str1 str2
                    if (hops = -1 && samePre > 0) then
                        for i = 0 to samePre-1 do
                            let nextNode = getWorkerRef toID
                            let mutable currRow = table.[i,*]
                            nextNode <! AddRow (i,currRow)
                    let nextNewNode = getWorkerRef toID
                    let mutable samePrefixRow = table.[samePre,*]
                    nextNewNode <! AddRow (samePre, samePrefixRow)
                    if((smallNodeList.Length>0 && toID>=List.min(smallNodeList)&& toID<=currNodeID)||(largeNodeList.Length>0 && toID<=List.max(largeNodeList)&& toID>=currNodeID)) then
                        let mutable diff = IDSpace + 10
                        let mutable nearest = -1
                        if toID<currNodeID then
                            for i in smallNodeList do
                                if abs(toID-i)<diff then
                                    nearest <- i
                                    diff <- abs(toID-i)
                        else
                            for i in largeNodeList do
                                if abs(toID-i)<diff then
                                    nearest <- i
                                    diff <- abs(toID-i)
                        if abs(toID-currNodeID) > diff then
                            let nextNode = getWorkerRef nearest
                            nextNode<! Action (message,fromID,toID,hops+1)
                        else
                            let mutable list = List.append smallNodeList largeNodeList
                            list <- currNodeID::list
                            let mutable nodesSet = list
                            let nextNode = getWorkerRef toID
                            nextNode<! AddNodesInNeighborhood nodesSet 
                    elif(smallNodeList.Length<4 && smallNodeList.Length>0 && toID<List.min (smallNodeList)) then
                        let minLeft = List.min (smallNodeList)
                        let nextNode = getWorkerRef minLeft
                        nextNode<! Action (message, fromID, toID, hops+1)
                    elif (largeNodeList.Length<4 && largeNodeList.Length>0 && toID>List.max (largeNodeList)) then
                        let maxRight = List.max (largeNodeList)
                        let nextNode = getWorkerRef maxRight
                        nextNode<! Action (message, fromID, toID, hops+1)
                    elif ((smallNodeList.Length = 0 && toID<currNodeID)||(largeNodeList.Length = 0 && toID>currNodeID)) then
                        let mutable list = List.append smallNodeList largeNodeList
                        list <- currNodeID::list
                        let mutable nodesSet = list
                        let nextNode = getWorkerRef toID
                        nextNode<! AddNodesInNeighborhood nodesSet
                    elif table.[samePre, str2.Chars(samePre) |> string |> int ] <> -1 then
                        let col = str2.Chars(samePre) |> string |> int 
                        let nextNodeID = table.[samePre,col]
                        let nextNode = getWorkerRef nextNodeID
                        nextNode <! Action (message,fromID,toID,hops+1)
                    elif toID> currNodeID then
                        let maxRight = List.max (largeNodeList)
                        let nextNode = getWorkerRef maxRight
                        nextNode<! Action(message, fromID, toID, hops+1)
                        let masterRef = getMasterRef
                        masterRef <! NodeNotFound
                    elif toID<currNodeID then
                        let minLeft = List.min (smallNodeList)
                        let nextNode = getWorkerRef minLeft
                        nextNode<! Action (message ,fromID ,toID, hops+1)
                        let masterRef = getMasterRef
                        masterRef <! NodeNotFound
                    else
                        printfn "Impossible"
                elif(message.Equals("Route")) then
                    if(currNodeID = toID) then
                        let masterRef = getMasterRef
                        masterRef<! RouteFinish (fromID,toID ,hops+1)
                    else
                        let str1 = numToBase4 currNodeID maxRows
                        let str2 = numToBase4 toID maxRows
                        let samePre = shl str1 str2
                        if((smallNodeList.Length >0 && toID>=List.min (smallNodeList) && toID<currNodeID)||(largeNodeList.Length>0 && toID<=List.max (largeNodeList) && toID>currNodeID)) then
                            let mutable diff = IDSpace + 10
                            let mutable nearest = -1
                            if toID<currNodeID then
                                for i in smallNodeList do
                                    if (abs(toID-i) < diff) then
                                        nearest <- i
                                        diff <- abs(toID-i)
                            else
                                for i in largeNodeList do
                                    if(abs(toID-i)<diff) then
                                        nearest <- i
                                        diff <- abs(toID-i)
                            if(abs(toID - currNodeID)>diff) then
                                let nextNode = getWorkerRef nearest
                                nextNode<! Action (message, fromID, toID, hops+1)
                            else
                                getMasterRef<! RouteFinish (fromID, toID ,hops+1)
                        elif (smallNodeList.Length<4 && smallNodeList.Length > 0 && toID<List.min (smallNodeList)) then
                            let minLeft = List.min (smallNodeList)
                            let nextNode = getWorkerRef minLeft
                            nextNode <! Action (message,fromID,toID,hops+1)
                        elif (largeNodeList.Length <4 && largeNodeList.Length>0 && toID>List.max (largeNodeList)) then
                            let maxRight = List.max (largeNodeList)
                            let nextNode = getWorkerRef maxRight
                            nextNode<! Action (message ,fromID ,toID, hops+1)
                        elif ((smallNodeList.Length = 0 && toID <currNodeID)||(largeNodeList.Length = 0 && toID > currNodeID)) then
                            getMasterRef<! RouteFinish (fromID, toID, hops+1)
                        elif (table.[samePre,str2.Chars(samePre) |> string |> int ] <> -1) then
                            let col = str2.Chars(samePre) |> string |> int 
                            let nextNode = getWorkerRef table.[samePre,col]
                            nextNode<! Action (message, fromID, toID, hops+1)
                        elif toID >currNodeID then
                            let maxRight = List.max (largeNodeList)
                            let nextNode = getWorkerRef maxRight
                            nextNode<!Action (message, fromID, toID, hops+1)
                            getMasterRef<!RouteToNodeNotFound
                        elif toID<currNodeID then
                            let minLeft = List.min (smallNodeList)
                            let nextNode = getWorkerRef minLeft
                            nextNode<! Action (message,fromID,toID,hops+1)
                            let masterRef = getMasterRef
                            masterRef <! RouteToNodeNotFound
                        else 
                            printfn "Impossible!"
            | AddRow (rowNum,(newRow:int[])) ->
                for i=0 to 3 do
                    if (table.[rowNum,i] = -1) then
                        table.[rowNum, i] <- newRow.[i]
            | AddNodesInNeighborhood (nodesSet) ->
                buildLeafSet nodesSet
                for i in smallNodeList do 
                    numOfBack <- numOfBack+1
                    let nextNode = getWorkerRef i
                    nextNode <! SendAckToMaster currNodeID
                for i in largeNodeList do
                    numOfBack <-numOfBack+1
                    let nextNode = getWorkerRef i
                    nextNode <! SendAckToMaster currNodeID
                for i=0 to maxRows-1 do
                    for j=0 to 3 do 
                        if (table.[i,j] <> -1) then
                            numOfBack <- numOfBack+1
                            let nextNodeID = table.[i,j]
                            let nextNode = getWorkerRef nextNodeID
                            nextNode <! SendAckToMaster currNodeID
                for i=0 to maxRows-1 do
                    let col = (numToBase4 currNodeID maxRows).Chars(i) |> string |> int 
                    table.[i, col] <- currNodeID
            | SendAckToMaster(newNodeID) ->
                addNodeToLeafSet newNodeID
                sender<!Ack
            | Ack ->
                numOfBack<-numOfBack-1
                if(numOfBack=0) then
                    let masterRef = getMasterRef
                    masterRef <! JoinFinish
            | StartRoutingWorker  ->
                for i = 1 to numRequests do
                    Async.Sleep(1000) |> Async.RunSynchronously
                    self <! Action ("Route",currNodeID,System.Random().Next(IDSpace),-1)
            | _ -> 
                printfn "Invalid case!"
            return! loop 
        }
    loop 

let masterBehavior numNodes numRequests (inbox: Actor<Message>) =
    printfn "Num Nodes : %i  Num Requests : %i " numNodes numRequests;
    let maxRows = (Math.Ceiling(Math.Log(numNodes |> double)/Math.Log(4.0)))|> int
    let maxNodes = Math.Pow(4.0,maxRows|>double)|> int
    let mutable initialNetwork = List.empty
    let mutable numHops = 0
    let mutable numJoined = 0
    let mutable numNotInBoth = 0
    let mutable numRouted = 0
    let mutable numRouteNotInBoth = 0
    let initialNetworkSize = if numNodes<=1024 then numNodes else 1024
    let nodeList = shuffle [0 .. maxNodes-1]
    for i=0 to initialNetworkSize-1 do
        initialNetwork <- nodeList.Item(i)::initialNetwork
            
    for i=0 to numNodes-1 do
        let nodeIDInt = (nodeList.Item(i))
        let name = string nodeIDInt
        spawn system name (pastryBehaviour numNodes numRequests nodeIDInt maxRows) |>ignore    
    
    let rec loop =
        actor {
            let! message = inbox.Receive()
            let self = inbox.Self
            match message with
            | Start  ->
                printfn "Pastry started.\nInitial node created."
                printfn "Joining."
                for i=0 to initialNetworkSize-1 do
                    let nodeIDInt = (nodeList.Item(i))
                    let worker = getWorkerRef nodeIDInt
                    worker<! AddFirstNode(clone initialNetwork)
            | JoinFinish ->
                numJoined <- numJoined + 1
                if (numJoined = initialNetworkSize) then
                    if(numJoined>=numNodes) then
                        self<! StartRouting
                    else
                        self<! JoinNodes
                if(numJoined>initialNetworkSize) then
                    if(numJoined=numNodes) then
                        self<!StartRouting
                    else
                        self<!JoinNodes   
            | JoinNodes ->
                let startID = nodeList.Item(random.Next(numJoined))
                let startWorker = getWorkerRef startID
                startWorker <! Action ("Join", startID, nodeList.Item(numJoined) ,-1)
            | StartRouting->
                printfn "Node Join Finished."
                printfn "Routing has started."
                for i=0 to numNodes-1 do
                    let nodeIDInt = (nodeList.Item(i))
                    let nextNode = getWorkerRef nodeIDInt
                    nextNode<!StartRoutingWorker
            | NodeNotFound ->
                numNotInBoth <- numNotInBoth + 1
            | RouteFinish(fromID,toID, hops)->
                numRouted <- numRouted+1
                numHops <- numHops+hops
                for i = 1 to 10 do 
                    if (numRouted= (numNodes* numRequests * i/10)) then
                        for j=1 to i do
                            printf "*"
                        printf "|"
                if(numRouted >= (numNodes*numRequests)) then
                    printfn "Routing finished.\n"
                    printfn "Total number of routes:%u " numRouted
                    printfn "Total number of hops:%i " numHops
                    let avgHops= (numHops|>double)/(numRouted|>double)
                    printfn "Average number of hops per route: %f"  avgHops
                    flag<-false
            | RouteToNodeNotFound->
                numRouteNotInBoth<-numRouteNotInBoth+1
            | _ ->
                printfn "Invalid case"
            return! loop
        }
    loop 

let masterNode = spawn system "master" (masterBehavior numNodes numRequests)

masterNode <! Start

while flag do
    ignore ()