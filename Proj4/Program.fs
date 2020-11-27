open System
open Utils
open Akka.FSharp
open TwitterEngine

[<EntryPoint>]
let main argv =
    let flag = true
    let engineRef = spawn system "engine" engineBehavior
    engineRef <! Register("user1")
    while flag do ignore()
    0