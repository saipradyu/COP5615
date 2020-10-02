#time "on"
#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("FSharp")

type Message =
    | Rumor of string
    | Converge of string

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
            | Converge (_) -> if (count + 1 = nodes) then printfn "Algorithm has converged"
            | _ -> failwith "Observer received unsupported message"

            return! loop (count + 1)
        }

    loop count

let observerRef =
    spawn system "observer" (observerBehavior 0)

System.Console.ReadLine |> ignore
