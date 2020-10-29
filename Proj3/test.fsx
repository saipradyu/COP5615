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
    printfn "%s" str
    str
    
ConvertNumToBase 10 10

let CompleteLeafSet (all: int list) currNodeID i RightNode LeftNode maxRows table =
//TODO recheck logic for i. Should i be passed as param or not?


(***********************Master and Pastry Logic***********************)
let pastryProcess msg numNodes numRequests senderID id maxRows count =
    let currNodeID = id
    let LeftNode = List.empty
    let RightNode = List.empty
    let mutable numOfBack = 0
    // TODO See how many columns are needed
    let table = Array2D.init maxRows 4 (fun x y -> -1)
    let IDSpace = Math.Pow(4.0,maxRows|>double) |>int
    printfn "%A" table
    match msg with
    | AddFirstNode(firstGroup) -> 
        let newFirstGroup =  (remove currNodeID firstGroup)
        // CompleteLeafSet newFirstGroup
        for i = 0 to maxRows do
            // let col = ((ConvertNumToBase currNodeID maxRows).Chars(i)) |> string |> int 
            // Array2D.set table i col currNodeID
            // let sender = getWorkerRef senderID
            // sender <! JoinFinish
            //NOTE sender is implicit in scala.See what to do here
    count+1
let pastryBehaviour (inbox: Actor<Message>) =
    let rec loop count =
        actor {
            let! msg = inbox.Receive()
            let newCount = pastryProcess msg numNodes numRequests id maxRows count
            return! loop newCount
        }
    loop 0
let masterProcess msg numNodes numRequests count =
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
                                spawn system name (pastryBehaviour))
                         |> ignore
    match msg with
    | StartTask (s) ->
        printfn "\n Initial node created.\n Pastry started.\n"
        let ref = Nodelist.Item(0)
        // printfn "start node %i" ref 
        let firstNode = getWorkerRef ref
        //TODO firstNode <! AddFirstNode
        count

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