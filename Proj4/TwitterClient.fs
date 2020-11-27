module TwitterClient

open Utils
open Akka.Actor
open Akka.FSharp

let clientBehavior (inbox: Actor<_>) =
    let rec loop () =
        actor {
            let! msg = inbox.Receive()
            return! loop ()
        }

    loop ()
