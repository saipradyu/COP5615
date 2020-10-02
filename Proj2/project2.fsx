#time "on"
#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("FSharp")

let roundNodes n s =
    match s with
    | "2d"
    | "imp2d" -> Math.Pow(Math.Round(sqrt (float n)), 2.0) |> int
    | _ -> n

let pickRandom (l: List<_>) =
    let r = System.Random()
    l.[r.Next(l.Length)]

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
            let root = sqrt (float n) |> int

            let nlist =
                List.filter (fun y ->
                    if (x % root = 0) then
                        (y = x + root || y = x - 1 || y = x - root)
                    elif (x % root = 1) then
                        (y = x + root || y = x + 1 || y = x - root)
                    else
                        (y = x
                         + root
                         || y = x - 1
                         || y = x + 1
                         || y = x - root)) [ 1 .. n ]

            map <- map.Add(x, nlist))
        |> ignore
        map
    | "imp2d" ->
        [ 1 .. n ]
        |> List.map (fun x ->
            let root = sqrt (float n) |> int

            let nlist =
                List.filter (fun y ->
                    if (x % root = 0) then
                        (y = x + root || y = x - 1 || y = x - root)
                    elif (x % root = 1) then
                        (y = x + root || y = x + 1 || y = x - root)
                    else
                        (y = x
                         + root
                         || y = x - 1
                         || y = x + 1
                         || y = x - root)) [ 1 .. n ]

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
