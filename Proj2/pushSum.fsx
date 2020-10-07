#time "on"
#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("FSharp")
let mutable flag = true


(*******************Topology************************)
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


(*******************Initialization************************)

let topology = "imp2d"
let algorithm = "pushsum"
let nodes = 10
let topologyMap = buildTopology nodes topology
let gossipcount = if topology = "imp2d" then nodes else 10
let intialMessage : Message = 
    if algorithm ="pushsum" then
        PushSum([1..nodes]|> pickRandom|>float, 1.0)
    else
        Rumor "starting a random rumor"
(*******************Utility************************)
let getWorkerRef s =
    let actorPath = @"akka://FSharp/user/worker" + string s
    select actorPath system

let getRandomNeighbor x l =
    let nlist = (topologyMap.TryFind x).Value

    let rem =
        nlist
        |> List.filter (fun x -> not (l |> List.contains x))

    let random =
        if rem.IsEmpty then x else pickRandom rem

    getWorkerRef random

let broadcastConvergence x =
    [ 1 .. nodes ]
    |> List.map (getWorkerRef)
    |> List.iter (fun ref -> ref <! Update x)

let isNeighbor x s =
    let nlist = (topologyMap.TryFind x).Value
    nlist |> List.contains s

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

(*******************Gossip************************)

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
        | Update (s) -> if isNeighbor ref s then count, s :: dlist else count, dlist
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

(*******************Pushsum************************)

let checkConvergence oldRatio newRatio = 
    abs (oldRatio - newRatio) <= 1.0e-10

let getPSRandomNeighbour x =
    // let key = x |> int
    let nlist = (topologyMap.TryFind x).Value
    let random = nlist |> pickRandom
    getWorkerRef random

let processPushsum ref msg convergenceCount s w =
    if convergenceCount < 3 then
        match msg with
        | Rumor (_) ->
            let neighRef = getPSRandomNeighbour ref
            neighRef <! PushSum(s / 2.0, w / 2.0)
            (convergenceCount, (s/2.0), (w/2.0))
        | PushSum (a, b) ->
            // printfn "Worker %i received push sum message with %A and %A" ref a b
            let ss = s + a
            let ww = w + b
            let cc =
                if checkConvergence (s/w) (ss/ww) then 
                    convergenceCount + 1 
                else 
                    0
            let neighRef = getPSRandomNeighbour ref
            neighRef <! PushSum(ss / 2.0, ww / 2.0)
            if cc = 3 then
                let conmsg =
                    "Worker " + string ref + " has converged"
                observerRef <! Converge conmsg
            let e, f = s/w, ss/ww
            // printfn "Worker %i : %b  %A and %A \n" ref (checkConvergence e f) e f
            (cc, ss / 2.0, ww / 2.0)
        | _ -> failwith "Worker received unsupported message"
    else
        (convergenceCount, s, w)

let pushSumProcessor ref (inbox: Actor<Message>) =
    let rec loop count s w =
        actor {
            let! msg = inbox.Receive()
            let cc, ss, ww = processPushsum ref msg count s w
            return! loop cc ss ww
        }

    loop 0 (ref |> double) 1.0

(*******************************************)

// match fsi.CommandLineArgs.Length with
// | 4 -> ignore
// | _ -> failwith "Requires number of nodes, topology and algorithm as input"

// let args = fsi.CommandLineArgs |> Array.tail
// let topology = args.[1]
// let algorithm = args.[2]
// let nodes = roundNodes (args.[0] |> int) topology
// let topologyMap = buildTopology nodes topology
// let gossipcount = 10



// let args = fsi.CommandLineArgs |> Array.tail


let workerRef msg=
    match msg with
    | Rumor (_) ->
        [ 1 .. nodes ]
        |> List.map (fun x ->
            let name = "worker" + string x
            spawn system name (gossipBehavior x))
        |> pickRandom
    | PushSum(a,b) -> 
        [ 1 .. nodes ]
        |> List.map (fun x ->
            let name = "worker" + string x
            spawn system name (pushSumProcessor x))
        |> pickRandom

let workerThread = workerRef intialMessage
workerThread

while flag do
    ignore ()
