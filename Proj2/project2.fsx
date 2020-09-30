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

match fsi.CommandLineArgs.Length with
| 4 -> ignore
| _ -> failwith "Requires number of nodes, topology and algorithm as imput"

let args = fsi.CommandLineArgs |> Array.tail
let topology = args.[1]
let algorithm = args.[2]
let nodes = roundNodes (args.[0] |> int) topology