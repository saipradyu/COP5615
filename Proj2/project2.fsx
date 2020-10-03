#time "on"
#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("FSharp")

type Message =
    | Rumor of string
    | Converge of string
    | Gossip of string

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

            let remlist =
                [ 1 .. n ]
                |> List.filter (fun m -> m <> x && not (nlist |> List.contains m))

            let random = pickRandom remlist
            let randomNList = random :: nlist
            map <- map.Add(x, randomNList))
        |> ignore
        map
    | _ -> map

match fsi.CommandLineArgs.Length with
| 4 -> ignore
| _ -> failwith "Requires number of nodes, topology and algorithm as input"

let args = fsi.CommandLineArgs |> Array.tail
let topology = args.[1]
let algorithm = args.[2]
let nodes = roundNodes (args.[0] |> int) topology
let topologyMap = buildTopology nodes topology

let getWorkerRef s =
    let actorPath = @"akka://FSharp/user/worker" + string s
    select actorPath system

let getRandomNeighbor x =
    let nlist = (topologyMap.TryFind x).Value
    let random = pickRandom nlist
    getWorkerRef random

let observerBehavior count (inbox: Actor<Message>) =
    let rec loop count =
        actor {
            let! msg = inbox.Receive()

            match msg with
            | Converge (s) ->
                printfn "%s" s
                if (count + 1 = nodes) then
                    printfn "Gossip protocol converged"
                    exit 0
            | _ -> failwith "Observer received unsupported message"

            return! loop (count + 1)
        }

    loop count

let observerRef =
    spawn system "observer" (observerBehavior 0)


let spreadGossip ref s =
    let self = getWorkerRef ref
    let neighRef = getRandomNeighbor ref
    neighRef <! Rumor(s)
    self <! Gossip s

let processGossip msg ref count =
    match msg with
    | Rumor (s) ->
        if (count >= 0 && count < 20) then spreadGossip ref s
        if (count = 20) then
            let conmsg =
                "Worker " + string ref + " has converged"

            observerRef <! Converge conmsg
        count + 1
    | Gossip (s) ->
        if (count <= 20) then spreadGossip ref s
        count
    | _ -> failwith "Worker received unsupported message"
    
let gossipBehavior ref count (inbox: Actor<Message>) =
    let rec loop count =
        actor {
            let! msg = inbox.Receive()
            let newCount = processGossip msg ref count
            return! loop newCount
        }

    loop count

let workerRef =
    [ 1 .. nodes ]
    |> List.map (fun x ->
        let name = "worker" + string x
        spawn system name (gossipBehavior x 0))
    |> pickRandom

workerRef <! Rumor "starting a random rumor"

System.Console.ReadLine |> ignore
