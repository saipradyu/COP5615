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
    | StartRouting of int List
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
    | StartRoutingWorker
    | RecoverLeaf of int List * int
    | RecoverRoutingTable of int*int*int
    | RemoveNode of int
    | RequestInRoutingTable of int*int
    | RequestLeafWithout of int
    | FailNodes
    | NodeDie

(**************************Utility*********************************)
match fsi.CommandLineArgs.Length with
| 4 -> ignore
| _ -> failwith "Requires number of nodes and number of requests"

let args = fsi.CommandLineArgs |> Array.tail
let numNodes = args.[0] |> int
let numRequests = args.[1] |> int
let numFailingNodes = args.[2] |> int

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
    

//TODO reference 2d array type: https://stackoverflow.com/questions/2909310/f-multidimensional-array-types
(***********************Master and Pastry Logic***********************)

let pastryBehaviour numNodes numRequests id maxRows (currNodelist:int List) (inbox: Actor<Message>) =
    let currNodeID = id
    let mutable LeftNode = List.empty
    let mutable RightNode = List.empty
    let mutable numOfBack = 0
    let mutable table = Array2D.create maxRows 4 -1
    let mutable IDSpace = Math.Pow(4.0,maxRows|>double) |>int
    let mutable activeNodes = currNodelist
    let mutable isDead = false
    let CompleteLeafSet (all: int list)  =
        // let mutable (newRightNode:int list)= List.empty
        // let mutable (newLeftNode:int list) = List.empty
        for i in all do
            if (i>currNodeID && (not(List.contains i RightNode))) then
                if(RightNode.Length<4) then
                    RightNode <- i::RightNode
                else
                    if(i<List.max(RightNode)) then
                        let maxRight = List.max(RightNode)
                        RightNode <- (remove maxRight RightNode)
                        RightNode <- i::RightNode
            elif (i<currNodeID && (not(List.contains i LeftNode))) then
                if(LeftNode.Length < 4) then
                    LeftNode<- i::LeftNode
                else
                    if(i>List.min(LeftNode)) then
                        let minLeft = List.min(LeftNode)
                        LeftNode <- (remove minLeft LeftNode)
                        LeftNode <- i::LeftNode
            let str1 = ConvertNumToBase currNodeID maxRows
            let str2 = ConvertNumToBase i maxRows
            let samePre = (CountMatches str1 str2)
            let col = (ConvertNumToBase i maxRows).Chars(samePre)|>string|>int
            if (table.[samePre,col]= (-1)) then
                table.[samePre, col] <- i

    let addOne (one:int) =
        // let mutable newRightNode = List.empty
        // let mutable newLeftNode = List.empty
        if(one> currNodeID && (not (List.contains one RightNode))) then
            if(RightNode.Length <4) then
                RightNode <- one::RightNode
            else
                if(one < List.max(RightNode)) then
                    let maxRight = List.max (RightNode)
                    RightNode <- (remove maxRight RightNode)
                    RightNode <- one::RightNode
        elif (one < currNodeID && (not (List.contains one LeftNode))) then
            if(LeftNode.Length < 4) then
                 LeftNode <- one::LeftNode
            else
                if(one> List.min (LeftNode)) then 
                    let minLeft = List.min (LeftNode)
                    LeftNode <- (remove minLeft LeftNode)
                    LeftNode <- one::LeftNode
        let str1 = ConvertNumToBase currNodeID maxRows
        let str2 = ConvertNumToBase one maxRows
        let samePre = (CountMatches str1 str2) |>int
        let col = str2.Chars(samePre) |> string |> int 
        if(table.[samePre,col] = (-1)) then
            table.[samePre, col] <- one
        
    
    let rec loop =
        actor {
            let! msg = inbox.Receive()
            let sender = inbox.Sender()
            let self = inbox.Self
            match msg with
            | AddFirstNode (firstGroup) ->
                // if(not isDead) then
                    let newFirstGroup =  (remove currNodeID firstGroup)
                    CompleteLeafSet newFirstGroup
                    for i=0 to maxRows-1 do
                        let col = (ConvertNumToBase currNodeID maxRows).Chars(i) |> string |> int 
                        table.[i, col] <- currNodeID
                    sender <! JoinFinish
            | Task (msg, fromID, toID, hops) ->
                // if(not isDead) then
                    if(msg.Equals("Join")) then 
                        let str1 = ConvertNumToBase currNodeID maxRows
                        let str2 = ConvertNumToBase toID maxRows
                        let samePre = CountMatches str1 str2
                        if (hops = -1 && samePre > 0) then
                            for i = 0 to samePre-1 do
                                let nextNode = getWorkerRef toID
                                let mutable currRow = table.[i,*]
                                printfn "Before currID %i  newRow %A" currNodeID currRow
                                nextNode <! AddRow (i,currRow)
                        let nextNewNode = getWorkerRef toID
                        let mutable samePrefixRow = table.[samePre,*]
                        printfn "Before currID %i  newRow %A" currNodeID samePrefixRow
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
                        elif (table.[samePre, str2.Chars(samePre) |> string |> int ] <> -1) then
                            let col = str2.Chars(samePre) |> string |> int 
                            let nextNodeID = table.[samePre,col]
                            let nextNode = getWorkerRef nextNodeID
                            nextNode <! Task (msg,fromID,toID,hops+1)
                        else (***********Failures********)
                            let mutable diff = IDSpace + 10
                            let mutable nearest = -1
                            for i =0 to 3 do
                                if((table.[samePre,i]<> -1) && (abs(table.[samePre,i]-toID)<diff)) then
                                    diff <- abs(table.[samePre,i]-toID)
                                    nearest <- table.[samePre,i]
                            if (nearest <> -1) then
                                if nearest=currNodeID then
                                    if toID>currNodeID then 
                                        let maxRight = List.max (RightNode)
                                        let nextNode = getWorkerRef maxRight
                                        nextNode<! Task (msg, fromID, toID, hops+1)
                                        getMasterRef<! NodeNotFound
                                    elif toID<currNodeID then
                                        let minLeft = List.min (LeftNode)
                                        let nextNode = getWorkerRef minLeft
                                        nextNode<! Task (msg, fromID, toID, hops+1)
                                        getMasterRef<! NodeNotFound
                                    else
                                        printfn "Impossible"
                                else
                                    let nextNodeID = nearest
                                    let nextNode = getWorkerRef nextNodeID
                                    nextNode <! Task (msg, fromID, toID, hops+1)
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
                            else (*********Failure*********)
                                let mutable diff = IDSpace + 10
                                let mutable nearest = -1
                                for i =0 to 3 do
                                    if((table.[samePre,i]<> -1) && (abs(table.[samePre,i]-toID)<diff)) then
                                        diff <- abs(table.[samePre,i]-toID)
                                        nearest <- table.[samePre,i]
                                if nearest <> -1 then
                                    if nearest=currNodeID then
                                        if toID>currNodeID then 
                                            let maxRight = List.max (RightNode)
                                            let nextNode = getWorkerRef maxRight
                                            nextNode<! Task (msg, fromID, toID, hops+1)
                                            getMasterRef<! RouteToNodeNotFound
                                        elif toID<currNodeID then
                                            let minLeft = List.min (LeftNode)
                                            let nextNode = getWorkerRef minLeft
                                            nextNode<! Task (msg, fromID, toID, hops+1)
                                            getMasterRef<! RouteToNodeNotFound
                                    else
                                        let nextNode = getWorkerRef nearest
                                        nextNode <! Task (msg, fromID, toID, hops+1)

            | AddRow (rowNum,(newRow:int[])) ->
                // if(not isDead) then
                    printfn "After currID %i  newRow %A" currNodeID newRow
                    for i=0 to 3 do
                        if (table.[rowNum,i] = -1) then
                            table.[rowNum, i] <- newRow.[i]
            | AddNodesInNeighborhood (nodesSet) ->
                // if(not isDead) then
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
                // if(not isDead) then
                    addOne newNodeID
                    sender<!Ack
            | Ack ->
                // if(not isDead) then
                    numOfBack<-numOfBack-1
                    if(numOfBack=0) then
                        let masterRef = getMasterRef
                        masterRef <! JoinFinish
            | StartRoutingWorker  ->
                // if(not isDead) then
                    for i = 1 to numRequests do
                        printfn "CurrID %i request %i " currNodeID i
                        Async.Sleep(100) |> Async.RunSynchronously
                        //TODO : Check if this works
                        self <! Task ("Route",currNodeID,System.Random().Next(IDSpace),-1)
            | NodeDie ->
                // isDead<-true
                printfn "Dead %i" currNodeID
                for node in activeNodes do
                    let nextNode = getWorkerRef node
                    let currNode = getWorkerRef currNodeID
                    nextNode<!RemoveNode currNodeID
                // for node in activeNodes do
                //     // TODO :Check shoudl pass nodelist or not
                //     // let nodeIDInt = (Nodelist.Item(i))
                //     let nextNode = getWorkerRef node
                //     printfn "Dead %i" currNodeID
                //     let currNode = getWorkerRef currNodeID
                //     nextNode<!RemoveNode currNodeID
                //     // self.Stop()
                // self <! PoisonPill.Instance //TODO :RECHECK
                    //TODO : HOW TO STOP ACTOR self?
            | RemoveNode (removeID) ->
                // if(not isDead) then
                    if(removeID>currNodeID && List.contains removeID RightNode) then
                        let newRightNode = remove removeID RightNode
                        if(newRightNode.Length>0) then
                            let maxRight = List.max (newRightNode)
                            let nextNode = getWorkerRef maxRight
                            nextNode <! RequestLeafWithout removeID
                    if (removeID<currNodeID && List.contains removeID LeftNode) then
                        let newLeftNode = remove removeID LeftNode
                        if(newLeftNode.Length>0) then
                            let minLeft = List.min (newLeftNode)
                            let nextNode = getWorkerRef minLeft
                            nextNode <! RequestLeafWithout removeID
                    let str1 = ConvertNumToBase currNodeID maxRows
                    let str2 = ConvertNumToBase removeID maxRows
                    let samePre = (CountMatches str1 str2)
                    let col = str2.Chars(samePre)|>string|>int
                    if (table.[samePre,col]= removeID) then
                        table.[samePre, col] <- -1
                        for i = 0 to 3 do
                            if(table.[samePre, i] <> currNodeID && table.[samePre,i]<>removeID && table.[samePre,i] <> -1) then
                                let nextNodeID = table.[samePre,i]
                                let nextNode = getWorkerRef nextNodeID
                                nextNode<!RequestInRoutingTable(samePre,col)
            | RequestLeafWithout (theID) ->
                // if(not isDead) then
                    let mutable temp = List.append LeftNode RightNode
                    temp <- remove theID temp
                    let newTemp = temp
                    sender<!RecoverLeaf(newTemp, theID)
            | RecoverLeaf (newList, theDead)->
                // if(not isDead) then
                    for i in newList do
                        if( i>currNodeID && not(List.contains i RightNode)) then
                            if (RightNode.Length < 4) then
                                RightNode<-i::RightNode
                            else
                                if(i<List.max(RightNode)) then
                                    let maxRight = List.max(RightNode)
                                    RightNode<-(remove maxRight RightNode)
                                    RightNode<-i::RightNode
                        elif (i<currNodeID && not(List.contains i LeftNode)) then
                            if(LeftNode.Length<4) then
                                LeftNode<-i::LeftNode
                            else 
                                if(i>List.min(LeftNode)) then
                                    let minLeft = List.min(LeftNode)
                                    LeftNode<-(remove minLeft LeftNode)
                                    LeftNode<-i::LeftNode
            | RequestInRoutingTable (samePrefix, column)->
                // if(not isDead) then
                    if(table.[samePrefix,column]<> -1) then
                        sender<! RecoverRoutingTable(samePrefix,column,table.[samePrefix,column])
            | RecoverRoutingTable(row,col,newID)->
                // if(not isDead) then
                    if(table.[row,col]= -1) then
                        table.[row,col] <- newID
            return! loop 
        }
    loop 

let masterBehavior numNodes numRequests numFailingNodes (inbox: Actor<Message>) =
    printfn "Num Nodes : %i  Num Requests : %i " numNodes numRequests;
    let maxRows = (Math.Ceiling(Math.Log(numNodes |> double)/Math.Log(4.0)))|> int
    let maxNodes = Math.Pow(4.0,maxRows|>double)|> int
    let mutable GrpOne = List.empty
    let mutable numHops = 0
    let mutable numJoined = 0
    let mutable numNotInBoth = 0
    let mutable numRouted = 0
    let mutable numRouteNotInBoth = 0
    let mutable set = Set.empty
    let groupOneSize = if numNodes<=1024 then numNodes else 1024
    let mutable activeNodes = List.empty
    let Nodelist = shuffle [0 .. maxNodes-1]
    for i=0 to groupOneSize-1 do
        GrpOne <- Nodelist.Item(i)::GrpOne
            
    for i=0 to numNodes-1 do
        let nodeIDInt = (Nodelist.Item(i))
        activeNodes<- nodeIDInt::activeNodes
        let name = string nodeIDInt
        spawn system name (pastryBehaviour numNodes numRequests nodeIDInt maxRows activeNodes) |>ignore    
    
    let rec loop =
        actor {
            let! msg = inbox.Receive()
            let self = inbox.Self
            match msg with
            | StartTask  ->
                // printfn "\n Initial node created.\n Pastry started.\n"
                printfn "Joining.\n"
                for i=0 to groupOneSize-1 do
                    let nodeIDInt = (Nodelist.Item(i))
                    let worker = getWorkerRef nodeIDInt
                    // let mutable cloneGroupOne = GrpOne
                    // TODO : check clone ?
                    worker<! AddFirstNode(clone GrpOne)
            | JoinFinish ->
                numJoined <- numJoined + 1
                if (numJoined = groupOneSize) then
                    if(numJoined>=numNodes) then
                    //TODO : Check self?
                        self<! FailNodes
                    else
                        self<! JoinNodesInDT
                if(numJoined>groupOneSize) then
                    if(numJoined=numNodes) then
                        self<!FailNodes
                    else
                        self<!JoinNodesInDT   
            | JoinNodesInDT ->
                // TODO : https://docs.microsoft.com/en-us/dotnet/api/system.random.next?view=netcore-3.1#System_Random_Next_System_Int32_
                let startID = Nodelist.Item(random.Next(numJoined))
                let startWorker = getWorkerRef startID
                // printfn "JoinNodesInDT startID %i" startID
                startWorker <! Task ("Join", startID, Nodelist.Item(numJoined) ,-1)
            | StartRouting (failNodes)->
                //  printfn "Node Join Finished.\n"
                //  printfn "Routing started."
                printfn "Joined" 
                printfn "Routing"
                // printfn "Startrouting to all nodes %A " Nodelist
                 //TODO : check all workers?
                for i=0 to numNodes-1 do
                    let nodeIDInt = (Nodelist.Item(i))
                    if(not (List.contains nodeIDInt failNodes)) then
                        let nextNode = getWorkerRef nodeIDInt
                        nextNode <! StartRoutingWorker
                // let allWorkers = getAllWorkerRef
                // allWorkers <! StartRoutingWorker
            | NodeNotFound ->
                numNotInBoth <- numNotInBoth + 1
            | RouteFinish(fromID,toID, hops)->
                numRouted <- numRouted+1
                numHops <- numHops+hops
                for i = 1 to 10 do 
                    if (numRouted= ((numNodes - numFailingNodes) * numRequests * i / 10)) then
                        for j=1 to i do
                            printf "."
                        printf "|"
                if(numRouted >= (numNodes - numFailingNodes) * numRequests) then
                    printfn "Routing finished.\n"
                    printfn "Total number of routes:%u " numRouted
                    printfn "Total number of hops:%i " numHops
                    printfn "Routing finished.\n"
                    let avgHops= (numHops|>double)/(numRouted|>double)
                    printfn "Average number of hops per route: %f"  avgHops
                    flag<-false
            | RouteToNodeNotFound->
                numRouteNotInBoth<-numRouteNotInBoth+1
            |FailNodes ->
                let mutable failNodes = List.empty
                for i=0 to numFailingNodes-1 do
                    failNodes <- Nodelist.Item(i)::failNodes
                    let nextNode = getWorkerRef (Nodelist.Item(i))
                    nextNode<! NodeDie
                    //TODO : Check if this works
                Async.Sleep(100) |> Async.RunSynchronously
                //TODO : Action try StartRoutingWorker
                self <! StartRouting failNodes
            return! loop
        }
    loop 

let masterNode = spawn system "master" (masterBehavior numNodes numRequests numFailingNodes)

masterNode <! StartTask

while flag do
    ignore ()