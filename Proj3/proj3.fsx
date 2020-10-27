#time "on"
#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("FSharp")
let mutable flag = true

type Message =
    | StartTask of string   
    | StartRouting of string
    | JoinNodesInDT of string
    | Ack of string
    | JoinFinish of string
    | NodeNotFound of string
    | RouteToNodeNotFound of string

// let Task msg fromID toID hops = 

// match fsi.CommandLineArgs.Length with
// | 3 -> ignore
// | _ -> failwith "Requires number of nodes and number of requests to be sent"

// let args = fsi.CommandLineArgs |> Array.tail
// let numNodes = args.[0]
// let numRequests = args.[1]
let numNodes = 10
let numRequests = 5
(*--------------------------Utility------------------------------------*)
let pickRandom (l: List<_>) =
    let r = System.Random()
    l.[r.Next(l.Length)]

let getWorkerRef s =
    let actorPath = @"akka://FSharp/user/pastryNode" + string s
    select actorPath system

let getMasterRef = 
    let actorPath = @"akka://FSharp/user/masterProcess"
    select actorPath system

let getAllWorkerRef = 
    let actorPath = @"akka://FSharp/user/*"
    select actorPath system

let random = System.Random()

let shuffle elements = elements |> List.sortBy (fun _ -> random.Next())

let clone elements = elements |> List.map (fun element -> element) 

//TODO
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
            str <- '0' + str
            j <- j+1
    str
    
    printfn "%s" str
ConvertNumToBase 10 10

let CountMatches (str1:string) (str2:string)= 
    let mutable count = 0;
    while count<str1.Length && count<str2.Length && str1.Chars(count) = str2.Chars(count) do
        count <- count+1
    count

let rec remove n lst = 
    match lst with
    | h::tl when h = n -> tl
    | h::tl -> h :: (remove n tl)
    | []    -> []

let addOne (one:int) currNodeID RightNode LeftNode maxRows table=
    let mutable newRightNode = List.empty
    let mutable newLeftNode = List.empty
    if(one> currNodeID && (not (List.contains one RightNode))) then
        if(RightNode.Length <4) then
            newRightNode <- one::RightNode
        else
            if(one < List.max(RightNode)) then
                let maxRight = List.max (RightNode)
                newRightNode <- (remove maxRight RightNode)
                newRightNode <- i::RightNode
    elif (one < currNodeID && (not (List.contains i LeftNode))) then
        if(LeftNode.Length < 4) then
             newLeftNode <- one::LeftNode
        else
            if(i> List.min (LeftNode)) then 
                let minLeft = List.min (LeftNode)
                newLeftNode <- (remove minLeft LeftNode)
                newLeftNode <- one::LeftNode
    let str1 = ConvertNumToBase currNodeID maxRows
    let str2 = ConvertNumToBase one maxRows
    let samePre = CountMatches str1 str2
    let col = (ConvertNumToBase one maxRows).Chars(samePre) |> string |> int 
    if(table.[samePre,col]=-1) then
        Array2D.set table samePre col one

let CompleteLeafSet (all: int list) currNodeID i RightNode LeftNode maxRows table=
//TODO recheck logic for i. Should i be passed as param or not?
    let mutable newRightNode = List.empty
    let mutable newLeftNode = List.empty
    for i in all do
        if(i>currNodeID && (not (List.contains i RightNode)) then
            if(RightNode.Length<4) then
              // TODO check mutable or not
               newRightNode <- i::RightNode
            else
                if(i<List.max (RightNode)) then
                    let maxRight = List.max (RightNode)
                    newRightNode <- (remove maxRight RightNode)
                    newRightNode <- i::RightNode
        elif (i< currNodeID && (not (List.contains i LeftNode))) then
            if(LeftNode.Length < 4 ) then
                 newLeftNode <- i::LeftNode
            else
                if(i> List.min (LeftNode)) then 
                    let minLeft = List.min (LeftNode)
                    newLeftNode <- (remove minLeft LeftNode)
                    newLeftNode <- i::LeftNode
        else
        let str1 = ConvertNumToBase currNodeID maxRows
        let str2 = ConvertNumToBase i maxRows
        let samePre = CountMatches str1 str2
        let col = (ConvertNumToBase i maxRows).Chars(samePre) |> string |> int 
        if(table.[samePre,col]=-1) then
            Array2D.set table same col one
        //TODO

(*--------------------------Master and Pastry------------------------------------*)
let pastryProcess msg numNodes numRequests senderID id maxRows count =
    let currNodeID = id
    let LeftNode = List.empty
    let RightNode = List.empty
    let mutable numOfBack = 0
    // TODO See how many columns are needed
    let table = Array2D.init maxRows 4 (fun x y -> -1)
    let IDSpace = Math.pow(4.0,maxRows) |>int
    printfn "%A" table
    
    match msg with
    | AddFirstNode(firstGroup) -> 
        let newFirstGroup =  (remove currNodeID firstGroup)
        CompleteLeafSet newFirstGroup
        for i = 0 to maxRows
            let col = (ConvertNumToBase currNodeID maxRows).Chars(i) |> string |> int 
            Array2D.set table i col currNodeID
            let sender = getWorkerRef senderID
            sender <! JoinFinish
            //NOTE sender is implicit in scala.See what to do here
    | Task (msg, fromID, toID, hops) ->
        if(msg == "Join") then 
            let str1 = ConvertNumToBase currNodeID maxRows
            let str2 = ConvertNumToBase toID maxRows
            let samePre = CountMatches str1 str2
            if(hops=-1 && samePre) then 
                for i= 0 to samePre do
                    let nextNode = getWorkerRef toID
                    nextNode <! AddRow i (clone table.[i])
            let nextNewNode = getWorkerRef toID
            nextNewNode <! AddRow samePre (clone table.[samePre])
            //TODO continue from line 134 in github code
    | AddRow (rowNum, newRow) ->
        for i=0 to 4 do
            if table.[rowNum,i] == -1
                Array2D.set table rowNum i newRow.[i]
    | AddNodesInNeighborhood (nodesSet) ->
        CompleteLeafSet nodesSet
        for i in LeftNode do 
            numOfBack <- numOfBack+1
            let nextNode = getWorkerRef i
            nextNode <! SendAcktoMaster currNodeID
        for i in RightNode do
            numOfBack <-numOfBack+1
            let nextNode = getWorkerRef i
            nextNode <! SendAcktoMaster currNodeID
        for i=0 to maxRows do
            let j = 0
            for j=0 to 4 do 
                if (table.[i,j] != -1) then
                    numOfBack <- numOfBack+1
                    let nextNodeID = table.[i,j]
                    let nextNode = getWorkerRef nextNodeID
                    nextNode <! SendAcktoMaster currNodeID
        for i=0 to maxRows do
            let col = (ConvertNumToBase currNodeID maxRows).Chars(i) |> string |> int 
            Array2D.set table i col currNodeID

    | SendAcktoMaster (newNodeID) ->
        addOne newNodeID
        let sender = getWorkerRef senderID
        sender <! Ack
    | Ack ->
        numOfBack<-numOfBack -1
        if(numOfBack==0) then
        //TODO parent is master? send ack to master?
            let masterRef = getMasterRef
            masterRef <! JoinFinish
    | StartRouting ->
    //TODO : Check for shceduler?
        for i=1 to numRequests

    | message ->


let pastryBehaviour numNodes numRequests id maxRows ref (inbox: Actor<Message>) =
    let rec loop count =
        actor {
            let! msg = inbox.Receive()
            let newCount = pastryProcess msg numNodes numRequests id maxRows count
            return! loop newCount
        }
    loop 0

let masterProcess msg numNodes numRequests count =
    let maxRows = Math.Ceiling(Math.Log(numNodes |> double)/Math.Log(4.0)) 
    let maxNodes = Math.Pow(4.0,maxRows) |> int
    let mutable GrpOne = Array.init
    // let groupOneSize = if (numNodes <= 1024) then numNodes else 1024
    let i = -1
    let mutable numHops = 0
    let mutable numJoined = 0
    let mutable numNotInBoth = 0
    let mutable numRouted = 0
    let mutable numofRouteNotFound = 0
    let NodeListType = shuffle [0 .. maxNodes]
    // let Nodelist = NodeListType |> List.toArray
    printfn "%A" Nodelist

    GrpOne <- Nodelist.Item(0) :: GrpOne
    printfn "%A" GrpOne
    [ 1 .. maxNodes ]
    |> List.map (fun x ->
        let name = "pastryNode" + string x
        spawn system name (pastryBehaviour numNodes  numRequests x maxRows x))
    |> ignore
    match msg with 
    | StartTask (s)->
         printfn("\n Initial node created.\n Pastry started.\n")
         let ref = Nodelist.[0]
         let firstNode = getWorkerRef ref
         firstNode <! AddFirstNode(clone GrpOne)
    | JoinFinish (s)->
        numJoined <- numJoined+1
        if(numJoined = 1) then 
            printfn("Node Join started.")
        if(numJoined > 0) then 
            if(numJoined == totalNodes) then
                getMasterRef !> StartRouting
            else 
                getMasterRef !> JoinNodesInDT
    | JoinNodesInDT (s)->
        let startID = Nodelist.Item(Random.next(numJoined))
        let startWorker = getWorkerRef startID
        printfn "Start ID %i" startID
        startWorker <! Task ("Join", startID, Nodelist.Item(numJoined) ,-1)
    | StartRouting (s)->
        printfn "Node Join Finished.\n"
        printfn "Routing started."
        let allWorkers = getAllWorkerRef 
        allWorkers <! StartRouting
    | NodeNotFound (s)->
        numNotInBoth <- numNotInBoth + 1
    | RouteFinish (fromId, toID, hops) ->
        numRouted <- numRouted + 1
        numHops <- numHops + 1
        if(numRouted >= totalNodes * numRequests) then 
            printfn "Routing finished \n"
            printfn "Total number of routes : %i" numRouted
            printfn "Total number of hops : %i " numHops
            printfn "Average number of hops per route : %f" numHops |> double / numRouted |>double
            flag = false
    | RoutetoNodeNotFound ->
        numofRouteNotFound <- numofRouteNotFound + 1
           
    count+1

(*
https://github.com/abhishek1811/Pastry-Protocol-Microsoft-/blob/master/Project3.scala
Master Class that will  construct all the actors and  help them 
to join the network
Base variable used calculate the max possible rows in Routing Table 
with base b=4
randomList is an arraybuffer used to evaluate the random id of
 the all
 *)
let masterBehavior numNodes numRequests (inbox: Actor<Message>) =
    let rec loop count =
        actor {
            let! msg = inbox.Receive()
            let newCount = masterProcess msg numNodes numRequests count
            return! loop newCount
        }
    loop 0

let masterNode = spawn system "masterProcess" (masterBehavior numNodes numRequests)

masterNode <! StartTask "start"

while flag do
    ignore ()