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
    | AddFirstNode of int List

let numNodes = 10
let numRequests = 5
(**************************Utility*********************************)
let getMasterRef = 
    let actorPath = @"akka://FSharp/user/masterProcess"
    select actorPath system

let getAllWorkerRef = 
    let actorPath = @"akka://FSharp/user/*"
    select actorPath system

let getWorkerRef s =
    let actorPath = @"akka://FSharp/user/pastryNode" + string s
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
    printfn "convert %i base 4 %s" raw str
    str
    
ConvertNumToBase 10 10

//TODO referenec 2d array type: https://stackoverflow.com/questions/2909310/f-multidimensional-array-types
let CompleteLeafSet (all: int list) currNodeID  (LeftNode:int list) (RightNode:int list) maxRows (table:int[,]) =
    //TODO recheck logic for i. Should i be passed as param or not?
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
        if (table.[samePre, col]=(-1)) then
             Array2D.set table samePre col i
             ignore()
            //  printfn "Updating leafset"
        // else
        //     printfn "Error in leafset"
    (newLeftNode,newRightNode,table)

(***********************Master and Pastry Logic***********************)
let pastryProcess (msg:Message) numNodes numRequests id maxRows count =
    let currNodeID = id
    let LeftNode = List.empty
    let RightNode = List.empty
    let mutable numOfBack = 0
    // TODO See how many columns are needed
    let table = Array2D.init maxRows 4 (fun x y -> -1)
    let IDSpace = Math.Pow(4.0,maxRows|>double) |>int
    printfn "currNodeID %i \n table %A \n" currNodeID table
    match msg with
    | AddFirstNode (firstGroup) ->
        let newFirstGroup =  (remove currNodeID firstGroup)
        let newLeftNode, newRightNode, newTable = CompleteLeafSet newFirstGroup currNodeID LeftNode RightNode maxRows table
        for i=0 to maxRows do
            let col = (ConvertNumToBase currNodeID maxRows).Chars(i) |> string |> int 
            Array2D.set newTable i col currNodeID
            // TODO recheck who the sender is. Is it the master node always?
            let sender = getMasterRef
            sender <! JoinFinish
            //NOTE sender is implicit in scala.See what to do here
    count+1
let pastryBehaviour numNodes numRequests id maxRows ref (inbox: Actor<Message>) =
    let rec loop count =
        actor {
            let! msg = inbox.Receive()
            let newCount = pastryProcess msg numNodes numRequests id maxRows count
            return! loop newCount
        }
    loop 0

let masterProcess msg numNodes numRequests (count:int) =
    let maxRows = (Math.Ceiling(Math.Log(numNodes |> double)/Math.Log(4.0)))|> int
    let maxNodes = Math.Pow(4.0,maxRows|>double)|> int
    // printfn "maxRows %i maxNodes %i" maxRows maxNodes
    let mutable GrpOne = List.empty
    let i = -1
    let mutable numHops = 0
    let mutable numJoined = 0
    let mutable numNotInBoth = 0
    let mutable numRouted = 0
    let mutable numOfRouteNotFound = 0
    //TODO : check in scala doing 0 "until" maxNodes ie last node not included
    let Nodelist = shuffle [0 .. maxNodes-1]
    // printfn "Nodelist %A" Nodelist
    GrpOne <- Nodelist.Item(0) :: GrpOne
    let pastryNodelist = Nodelist 
                         |> List.map (fun x ->
                                let name = "pastryNode" + string x
                                 // printfn "%s" name
                                spawn system name (pastryBehaviour numNodes numRequests x maxRows x))
                         |> ignore
    match msg with
    | StartTask (s) ->
        printfn "\n Initial node created.\n Pastry started.\n"
        let ref = Nodelist.Item(0)
        // printfn "start node %i" ref 
        let firstNode = getWorkerRef ref
        //TODO 
        firstNode <! AddFirstNode (clone GrpOne)
        count
    | JoinFinish (s) -> 
        numJoined <- numJoined + 1
        if (numJoined=1) then
            printfn "Node Join started."
        if(numJoined>0) then
            if(numJoined=numNodes)then
                printfn "Start routing"
        count+1

let masterBehavior numNodes numRequests (inbox: Actor<Message>) =
    let rec loop (count:int) =
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