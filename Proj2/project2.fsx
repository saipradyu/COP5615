#time "on"
#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("FSharp")
let mutable flag = true

type Message =
    | Rumor of string
    | Converge of string
    | Gossip of string
    | Update of int
    | PushSum of float*float

let roundNodes n s =
    match s with
    | "2d"
    | "imp2d" -> Math.Pow(Math.Round(sqrt (float n)), 2.0) |> int
    | _ -> n

let pickRandom (l: List<_>) =
    let r = System.Random()
    l.[r.Next(l.Length)]

let gridNeighbors x n =
    let r = sqrt (float n) |> int
    [ 1 .. n ]
    |> List.filter (fun y ->
        if (x % r = 0)
        then (y = x + r || y = x - 1 || y = x - r)
        elif (x % r = 1)
        then (y = x + r || y = x + 1 || y = x - r)
        else (y = x + r || y = x - 1 || y = x + 1 || y = x - r))

let buildTopology n s =
    let mutable map = Map.empty
    match s with
    | "full" ->
        [ 1 .. n ]
        |> List.map (fun x ->
            let nlist = List.filter (fun y -> x <> y) [ 1 .. n ]
            map <- map.Add(x, nlist))
        |> ignore
        map
    | "line" ->
        [ 1 .. n ]
        |> List.map (fun x ->
            let nlist =
                List.filter (fun y -> (y = x + 1 || y = x - 1)) [ 1 .. n ]

            map <- map.Add(x, nlist))
        |> ignore
        map
    | "2d" ->
        [ 1 .. n ]
        |> List.map (fun x ->
            let nlist = gridNeighbors x n
            map <- map.Add(x, nlist))
        |> ignore
        map
    | "imp2d" ->
        [ 1 .. n ]
        |> List.map (fun x ->
            let nlist = gridNeighbors x n

            let random =
                [ 1 .. n ]
                |> List.filter (fun m -> m <> x && not (nlist |> List.contains m))
                |> pickRandom

            let randomNList = random :: nlist
            map <- map.Add(x, randomNList))
        |> ignore
        map
    | _ -> map

// match fsi.CommandLineArgs.Length with
// | 4 -> ignore
// | _ -> failwith "Requires number of nodes, topology and algorithm as input"

// let args = fsi.CommandLineArgs |> Array.tail
// let topology = args.[1]
// let algorithm = args.[2]
// let nodes = roundNodes (args.[0] |> int) topology
// let topologyMap = buildTopology nodes topology
// let gossipcount = 10


let topology = "imp2d"
let algorithm = "gossip"
let nodes = 1000
let topologyMap = buildTopology nodes topology
let gossipcount = if topology = "imp2d" then nodes else 10
let intialMessage = 
    if algorithm ="pushsum" then
        PushSum([1..nodes]|> pickRandom|>float, 1.0)
    else
        Rumor "starting a random rumor"

let getWorkerRef s =
    let actorPath = @"akka://FSharp/user/worker" + string s
    select actorPath system

let getRandomNeighbor x l =
    let nlist = (topologyMap.TryFind x).Value

    let rem =
        nlist
        |> List.filter (fun a -> not (l |> List.contains a))

    let alive =
        [ 1 .. nodes ]
        |> List.filter (fun b -> not (l |> List.contains b))

    let random =
        if rem.IsEmpty then pickRandom alive else pickRandom rem

    getWorkerRef random

let broadcastConvergence x =
    [ 1 .. nodes ]
    |> List.map (getWorkerRef)
    |> List.iter (fun ref -> ref <! Update x)

let observerBehavior count (inbox: Actor<Message>) =
    let rec loop count =
        actor {
            let! msg = inbox.Receive()

            match msg with
            | Converge (s) ->
                printfn "%s" s
                if (count + 1 = nodes) then
                    printfn "%s algorithm has converged" algorithm
                    flag <- false
            | _ -> failwith "Observer received unsupported message"

            return! loop (count + 1)
        }

    loop count

let observerRef =
    spawn system "observer" (observerBehavior 0)

let spreadRumor ref s dlist =
    let neighRef = getRandomNeighbor ref dlist
    neighRef <! Rumor(s)

let processGossip msg ref count dlist =
    let self = getWorkerRef ref
    if count < gossipcount then
        match msg with
        | Rumor (s) ->
            spreadRumor ref s dlist
            if count = 0 then self <! Gossip(s)
            if count + 1 = gossipcount then
                let conmsg =
                    "Worker " + string ref + " has converged"
                observerRef <! Converge conmsg
                broadcastConvergence ref
            count + 1, dlist
        | Gossip (s) ->
            spreadRumor ref s dlist
            self <! Gossip(s)
            count, dlist
        | Update (s) -> count, s :: dlist
        | _ -> failwith "Worker received unsupported message"
    else
        count + 1, dlist

let gossipBehavior ref (inbox: Actor<Message>) =
    let rec loop count dlist =
        actor {
            let! msg = inbox.Receive()
            let newCount, newList = processGossip msg ref count dlist
            return! loop newCount newList
        }

    loop 0 List.Empty

let workerRef =
    [ 1 .. nodes ]
    |> List.map (fun x ->
        let name = "worker" + string x
        spawn system name (gossipBehavior x))
    |> pickRandom

workerRef <! Rumor "starting a random rumor"

while flag do
    ignore ()
