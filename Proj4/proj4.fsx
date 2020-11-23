#time "on"
#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("FSharp")
let mutable flag = true

(**************************Utility*********************************)
match fsi.CommandLineArgs.Length with
| 3 -> ignore
| _ -> failwith "Requires number of users and number of tweets"

let args = fsi.CommandLineArgs |> Array.tail
let numberOfUsers = args.[0] |> int
let numberOfTweets = args.[1] |> int

let getMasterRef = 
    let actorPath = @"akka://FSharp/user/master"
    select actorPath system

let getAllWorkerRef = 
    let actorPath = @"akka://FSharp/user/*"
    select actorPath system

let getWorkerRef s =
    let actorPath = @"akka://FSharp/user/" + string s
    select actorPath system

// let masterNode = spawn system "master" (masterBehavior numNodes numRequests)
// masterNode <! Start
while flag do
    ignore ()