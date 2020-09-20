#time "on"
#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("FSharp")
let actors = 10000
let mutable flag = true

type Message =
    | Input of int * int
    | Range of seq<int> * int
    | Done

let squareSum (x: int) (k: int): uint64 =
    [ x .. x + k - 1 ]
    |> List.map (fun x -> uint64 x)
    |> List.map (fun x -> x * x)
    |> List.reduce (+)

let isPerfectSquare (x: uint64) =
    let y = round (sqrt (double x))
    y * y = double x

let split source size =
    seq {
        let r = ResizeArray()
        for x in source do
            r.Add(x)
            if r.Count = size then
                yield r.ToArray()
                r.Clear()
        if r.Count > 0 then yield r.ToArray()
    }

let processWorker msg =
    match msg with
    | Range (range, k) ->
        range
        |> Seq.iter (fun x ->
            let check = squareSum x k |> isPerfectSquare
            if check then printfn "%i" x)
        |> ignore
    | _ -> failwith "Worker received unsupported computation"

let workerBehavior (inbox: Actor<Message>) =
    let rec loop () =
        actor {
            let! msg = inbox.Receive()
            let sender = inbox.Sender()
            processWorker msg
            sender <! Done
            return! loop ()
        }

    loop ()

let processMaster mailbox msg index =
    match msg with
    | Input (n, k) ->
        let size = n / actors
        split { 1 .. n } size
        |> Seq.map (fun x -> (Seq.ofArray x))
        |> Seq.iter (fun x ->
            let h = Seq.head x
            let name = "worker" + string h
            let workerRef = spawn system name workerBehavior
            async { workerRef <! Range(x, k) }
            |> Async.RunSynchronously)
        index + 1
    | Done -> index + 1
    | _ -> failwith "Master recieved unsupported message"


let masterBehavior f index check (inbox: Actor<'T>) =
    let rec loop index =
        actor {
            if index = check then flag <- false
            let! msg = inbox.Receive()
            let newindex = f inbox msg index
            return! loop newindex
        }

    loop index

match fsi.CommandLineArgs.Length with
| 3 ->
    let args = fsi.CommandLineArgs |> Array.tail
    let n = args.[0] |> int
    let k = args.[1] |> int
    let check = if n < actors then 2 else actors + 1
    let input = Input(n, k)

    let masterRef =
        spawn system "master" (masterBehavior processMaster 0 check)

    async { masterRef <! input }
    |> Async.RunSynchronously
| _ -> failwith "Requires a number and sequence length as imput"

while flag do
    ignore