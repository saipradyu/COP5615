#time "on"
#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp
// open System.Collections.Generic

let system = ActorSystem.Create("FSharp")
let mutable flag = true

type Message =
    | StartTask    
    | StartRouting 
    | JoinNodesInDT
    | Ack
    | JoinFinish
    | NodeNotFound 
    | RouteToNodeNotFound
    | AddFirstNode of int List
    | Task of string*int*int*int
    | AddRow of int*int[]
    | AddNodesInNeighborhood of int List
    | SendAckToMaster of int
    | RouteFinish of int*int*int


let numNodes = 10
let numRequests = 5
(**************************Utility*********************************)
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

let CountMatches (str1:string) (str2:string)= 
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

let ConvertNumToBase raw length =
    let mutable str  = base10tobase4 raw
    let diff = length - str.Length
    if(diff>0) then
        let mutable j =0
        while(j<diff) do
            str <- "0" + str
            j <- j+1
    str
    

//TODO referenec 2d array type: https://stackoverflow.com/questions/2909310/f-multidimensional-array-types

    
(***********************Master and Pastry Logic***********************)

let pastryBehaviour numNodes numRequests id maxRows (inbox: Actor<Message>) =
    let currNodeID = id
    let mutable LeftNode = List.empty
    let mutable RightNode = List.empty
    let mutable numOfBack = 0
    let mutable table = Array2D.create maxRows 4 -1
    let IDSpace = Math.Pow(4.0,maxRows|>double) |>int
    let CompleteLeafSet (all: int list)  =
        let mutable (newRightNode:int list)= List.empty
        let mutable (newLeftNode:int list) = List.empty
        for i in all do
            if (i>currNodeID && (not(List.contains i RightNode))) then
                if(RightNode.Length<4) then
                    newRightNode <- i::RightNode
                else
                    if(i<List.max(RightNode)) then
                        let maxRight = List.max(RightNode)
                        newRightNode <- (remove maxRight RightNode)
                        newRightNode <- i::RightNode
            elif (i<currNodeID && (not(List.contains i LeftNode))) then
                if(LeftNode.Length < 4) then
                    newLeftNode<- i::LeftNode
                else
                    if(i>List.min(LeftNode)) then
                        let minLeft = List.min(LeftNode)
                        newLeftNode <- (remove minLeft LeftNode)
                        newLeftNode <- i::LeftNode
            let str1 = ConvertNumToBase currNodeID maxRows
            let str2 = ConvertNumToBase i maxRows
            let samePre = (CountMatches str1 str2)
            let col = (ConvertNumToBase i maxRows).Chars(samePre)|>string|>int
            if (table.[samePre,col]= (-1)) then
                table.[samePre, col] <- i

    let addOne (one:int) =
        let mutable newRightNode = List.empty
        let mutable newLeftNode = List.empty
        if(one> currNodeID && (not (List.contains one RightNode))) then
            if(RightNode.Length <4) then
                newRightNode <- one::RightNode
            else
                if(one < List.max(RightNode)) then
                    let maxRight = List.max (RightNode)
                    newRightNode <- (remove maxRight RightNode)
                    newRightNode <- one::RightNode
        elif (one < currNodeID && (not (List.contains one LeftNode))) then
            if(LeftNode.Length < 4) then
                 newLeftNode <- one::LeftNode
            else
                if(one> List.min (LeftNode)) then 
                    let minLeft = List.min (LeftNode)
                    newLeftNode <- (remove minLeft LeftNode)
                    newLeftNode <- one::LeftNode
        let str1 = ConvertNumToBase currNodeID maxRows
        let str2 = ConvertNumToBase one maxRows
        let samePre = (CountMatches str1 str2) |>int
        let col = str2.Chars(samePre) |> string |> int 
        if(table.[samePre,col] = -1) then
            table.[samePre, col] <- one
        
    
    let rec loop =
        actor {
            let! msg = inbox.Receive()
            let sender = inbox.Sender()
            let self = inbox.Self
            
            
            match msg with
            | AddFirstNode (firstGroup) ->
                let newFirstGroup =  (remove currNodeID firstGroup)
                CompleteLeafSet newFirstGroup
                for i=0 to maxRows-1 do
                    let col = (ConvertNumToBase currNodeID maxRows).Chars(i) |> string |> int 
                    table.[i, col] <- currNodeID
                sender <! JoinFinish
            | Task (msg, fromID, toID, hops) ->
                if(msg.Equals("Join")) then 
                    let str1 = ConvertNumToBase currNodeID maxRows
                    let str2 = ConvertNumToBase toID maxRows
                    let samePre = CountMatches str1 str2
                    if (hops = -1 && samePre > 0) then
                        for i = 0 to samePre-1 do
                            let nextNode = getWorkerRef toID
                            let mutable currRow = table.[i,*]
                            printfn "ID %i currRow %A" currNodeID currRow
                            nextNode <! AddRow (i,currRow)
                    let nextNewNode = getWorkerRef toID
                    let mutable samePrefixRow = table.[samePre,*]
                    nextNewNode <! AddRow (samePre, samePrefixRow)
                    if((LeftNode.Length>0 && toID>=List.min(LeftNode)&& toID<=currNodeID)||(RightNode.Length>0 && toID<=List.max(RightNode)&& toID>=currNodeID)) then
                        let mutable diff = IDSpace + 10
                        let mutable nearest = -1
                        if toID<currNodeID then
                            for i in LeftNode do
                                if abs(toID-i)<diff then
                                    nearest <- i
                                    diff <- abs(toID-i)
                        else
                            for i in RightNode do
                                if abs(toID-i)<diff then
                                    nearest <- i
                                    diff <- abs(toID-i)
                        if abs(toID-currNodeID) > diff then
                            let nextNode = getWorkerRef nearest
                            nextNode<! Task (msg,fromID,toID,hops+1)
                        else
                            let mutable list = List.append LeftNode RightNode
                            list <- currNodeID::list
                            let mutable nodesSet = list
                            let nextNode = getWorkerRef toID
                            nextNode<! AddNodesInNeighborhood nodesSet 
                    elif(LeftNode.Length<4 && LeftNode.Length>0 && toID<List.min (LeftNode)) then
                        let minLeft = List.min (LeftNode)
                        let nextNode = getWorkerRef minLeft
                        nextNode<! Task (msg, fromID, toID, hops+1)
                    elif (RightNode.Length<4 && RightNode.Length>0 && toID>List.max (RightNode)) then
                        let maxRight = List.max (RightNode)
                        let nextNode = getWorkerRef maxRight
                        nextNode<! Task (msg, fromID, toID, hops+1)
                    elif ((LeftNode.Length = 0 && toID<currNodeID)||(RightNode.Length = 0 && toID>currNodeID)) then
                        let mutable list = List.append LeftNode RightNode
                        list <- currNodeID::list
                        let mutable nodesSet = list
                        // //TODO Dbt? nodesSet += currNodeID ++= LeftNode ++= RightNode What does it do? append all together?
                        let nextNode = getWorkerRef toID
                        nextNode<! AddNodesInNeighborhood nodesSet
                    elif table.[samePre, str2.Chars(samePre) |> string |> int ] <> -1 then
                        let col = str2.Chars(samePre) |> string |> int 
                        let nextNodeID = table.[samePre,col]
                        let nextNode = getWorkerRef nextNodeID
                        nextNode <! Task (msg,fromID,toID,hops+1)
                    elif toID> currNodeID then
                        let maxRight = List.max (RightNode)
                        let nextNode = getWorkerRef maxRight
                        nextNode<! Task(msg, fromID, toID, hops+1)
                        let masterRef = getMasterRef
                        masterRef <! NodeNotFound
                    elif toID<currNodeID then
                        let minLeft = List.min (LeftNode)
                        let nextNode = getWorkerRef minLeft
                        nextNode<! Task (msg ,fromID ,toID, hops+1)
                        let masterRef = getMasterRef
                        masterRef <! NodeNotFound
                    else
                        printfn "Impossible"
                elif(msg.Equals("Route")) then
                    if(currNodeID = toID) then
                        let masterRef = getMasterRef
                        masterRef<! RouteFinish (fromID,toID ,hops+1)
                    else
                        let str1 = ConvertNumToBase currNodeID maxRows
                        let str2 = ConvertNumToBase toID maxRows
                        let samePre = CountMatches str1 str2
                        if((LeftNode.Length >0 && toID>=List.min (LeftNode) && toID<currNodeID)||(RightNode.Length>0 && toID<=List.max (RightNode) && toID>currNodeID)) then
                            let mutable diff = IDSpace + 10
                            let mutable nearest = -1
                            if toID<currNodeID then
                                for i in LeftNode do
                                    if (abs(toID-i) < diff) then
                                        nearest <- i
                                        diff <- abs(toID-i)
                            else
                                for i in RightNode do
                                    if(abs(toID-i)<diff) then
                                        nearest <- i
                                        diff <- abs(toID-i)
                            if(abs(toID - currNodeID)>diff) then
                                let nextNode = getWorkerRef nearest
                                nextNode<! Task (msg, fromID, toID, hops+1)
                            else
                                getMasterRef<! RouteFinish (fromID, toID ,hops+1)
                        elif (LeftNode.Length<4 && LeftNode.Length > 0 && toID<List.min (LeftNode)) then
                            let minLeft = List.min (LeftNode)
                            let nextNode = getWorkerRef minLeft
                            nextNode <! Task (msg,fromID,toID,hops+1)
                        elif (RightNode.Length <4 && RightNode.Length>0 && toID>List.max (RightNode)) then
                            let maxRight = List.max (RightNode)
                            let nextNode = getWorkerRef maxRight
                            nextNode<! Task (msg ,fromID ,toID, hops+1)
                        elif ((LeftNode.Length = 0 && toID <currNodeID)||(RightNode.Length = 0 && toID > currNodeID)) then
                            getMasterRef<! RouteFinish (fromID, toID, hops+1)
                        elif (table.[samePre,str2.Chars(samePre) |> string |> int ] <> -1) then
                            let col = str2.Chars(samePre) |> string |> int 
                            let nextNode = getWorkerRef table.[samePre,col]
                            nextNode<! Task (msg, fromID, toID, hops+1)
                        elif toID >currNodeID then
                            let maxRight = List.max (RightNode)
                            let nextNode = getWorkerRef maxRight
                            nextNode<!Task (msg, fromID, toID, hops+1)
                            getMasterRef<!RouteToNodeNotFound
                        elif toID<currNodeID then
                            let minLeft = List.min (LeftNode)
                            let nextNode = getWorkerRef minLeft
                            nextNode<! Task (msg,fromID,toID,hops+1)
                            let masterRef = getMasterRef
                            masterRef <! RouteToNodeNotFound
                        else 
                            printfn "Impossible!"
            | AddRow (rowNum,(newRow:int[])) ->
                for i=0 to 3 do
                    if table.[rowNum,i] = -1 then
                        table.[rowNum, i] <- newRow.[i]
            | AddNodesInNeighborhood (nodesSet) ->
                CompleteLeafSet nodesSet
                for i in LeftNode do 
                    numOfBack <- numOfBack+1
                    let nextNode = getWorkerRef i
                    nextNode <! SendAckToMaster currNodeID
                for i in RightNode do
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
                    let col = (ConvertNumToBase currNodeID maxRows).Chars(i) |> string |> int 
                    table.[i, col] <- currNodeID
            | SendAckToMaster(newNodeID) ->
                addOne newNodeID
                sender<!Ack
            | Ack ->
                numOfBack<-numOfBack-1
                if(numOfBack=0) then
                    let masterRef = getMasterRef
                    masterRef <! JoinFinish
            | StartRouting  ->
                for i = 1 to numRequests do
                    Async.Sleep(100) |> Async.RunSynchronously
                    self <! Task ("Route",currNodeID,random.Next(0,(IDSpace)),-1)
                    // let newCount = pastryProcess msg numNodes numRequests id maxRows count sender self
                    
            return! loop 
        }
    loop 

let masterBehavior numNodes numRequests (inbox: Actor<Message>) =
    printfn "Num Nodes : %i  Num Requests : %i " numNodes numRequests;
    let self = inbox.Self
    let maxRows = (Math.Ceiling(Math.Log(numNodes |> double)/Math.Log(4.0)))|> int
    let maxNodes = Math.Pow(4.0,maxRows|>double)|> int
    let mutable GrpOne = List.empty
    let i = -1
    let mutable numHops = 0
    let mutable numJoined = 0
    let mutable numNotInBoth = 0
    let mutable numRouted = 0
    let mutable numOfRouteNotFound = 0
    let mutable set = Set.empty
    let groupOneSize = if numNodes<=1024 then numNodes else 1024
    let mutable Nodelist = List.empty 
    while Nodelist.Length<maxNodes do
        let currID = random.Next(0,maxNodes)
        if( not(Set.contains currID set)) then
            Nodelist <- currID::Nodelist
            set<-Set.add currID set
            
            // let Nodelist = shuffle [0 .. maxNodes-1]
    for i=0 to groupOneSize-1 do
        GrpOne <- Nodelist.Item(i)::GrpOne
            
    for i=0 to numNodes-1 do
        let nodeIDInt = (Nodelist.Item(i))
        let name = string nodeIDInt
        printfn "name %s" name
        spawn system name (pastryBehaviour numNodes numRequests nodeIDInt maxRows) |>ignore    
    
    let rec loop =
        actor {
            let! msg = inbox.Receive()
            match msg with
            | StartTask  ->
                printfn "\n Initial node created.\n Pastry started.\n"
                for i=0 to groupOneSize-1 do
                    let nodeIDInt = (Nodelist.Item(i))
                    let worker = getWorkerRef nodeIDInt
                    worker<! AddFirstNode(clone GrpOne)
            | JoinFinish ->
                numJoined <- numJoined + 1
                if (numJoined = groupOneSize) then
                    if(numJoined>=numNodes) then
                        self<! StartRouting
                    else
                        self<! JoinNodesInDT
                if(numJoined>groupOneSize) then
                    if(numJoined=numNodes) then
                        self<!StartRouting
                    else
                        self<!JoinNodesInDT   
            | JoinNodesInDT ->
                let startID = Nodelist.Item(random.Next(0,numJoined))
                let startWorker = getWorkerRef startID
                printfn "JoinNodesInDT startID %i" startID
                startWorker <! Task ("Join", startID, Nodelist.Item(numJoined) ,-1)
            | StartRouting->
                 printfn "Node Join Finished.\n"
                 printfn "Routing started."
                 let allWorkers = getAllWorkerRef
                 allWorkers <! StartRouting
            | NodeNotFound ->
                numNotInBoth <- numNotInBoth + 1
            | RouteFinish(fromID,toID, hops)->
                numRouted<-numRouted+1
                numHops<-numHops+hops
                for i = 1 to 10 do 
                    if (numRouted= (numNodes* numRequests * i/10)) then
                        for j=1 to i do
                            printfn "."
                        printfn "|"
                if(numRouted >= (numNotInBoth*numRouted)) then
                    printfn "Routing finished.\n"
                    printfn "Total number of routes:%u " numRouted
                    printfn "Total number of hops:%i " numHops
                    printfn "Routing finished.\n"
                    // printfn "Average number of hops per route: %f"  (numHops|>double) / (numRouted|>double)
                    flag<-false
            | RouteToNodeNotFound->
                numOfRouteNotFound<-numOfRouteNotFound+1
            return! loop
        }
    loop 

let masterNode = spawn system "master" (masterBehavior numNodes numRequests)

masterNode <! StartTask

while flag do
    ignore ()