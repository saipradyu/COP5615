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
        let samePre = CountMatches(ConvertNumToBase(currNodeID,maxRows),ConvertNumToBase(i,maxRight))
        //TODO




(*--------------------------Master and Pastry------------------------------------*)
let pastryProcess msg numNodes numRequests id maxRows count =
    let currNodeID = id
    let LeftNode = List.empty
    let RightNode = List.empty
    // TODO
    let table = Array2D.init maxRows 4 (fun x y -> (x,y))
    let IDSpace = Math.pow(4.0,maxRows) |>int
    let i = 0
    for i


let pastryBehaviour numNodes numRequests id maxRows ref (inbox: Actor<Message>) =
    let rec loop count =
        actor {
            let! msg = inbox.Receive()
            let newCount = pastryProcess msg numNodes numRequests id maxrows count
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