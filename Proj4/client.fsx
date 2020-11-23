#time "on"
#r "nuget: Akka.Fsharp"
#load "server.fsx"
open System
open Akka.Actor
open Akka.FSharp
(**************Utility*****************)
let rec remove n lst = 
    match lst with
    | h::tl when h = n -> tl
    | h::tl -> h :: (remove n tl)
    | []    -> []
(***************Twitter******************)
let mutable activeUsers = List.empty
let mutable timeLine = List.empty
type TwitterTableValues = 
    | UsersTableVal of int List * string List
    | MentionsTableVal of string List
    | HashTagsTableVal of string List
    | TweetsTableVal of string

let mutable usersTable  = Map.empty
let mutable mentionsTable = Map.empty
let mutable hashTagsTable = Map.empty
let mutable tweetsTable = Map.empty

let register (username:string) =
    createUser username
    printfn "%s has been registered!" username

let logout (username:string) =
    logout username
    printfn "%s has logged out!" username

let login (username:string)=
    login username
    printfn "%s has logged in!" username

let delete (username:string) =
    delete username

//TODO  retweet
//TODO tweet
let addToTimeline (tweet:string) = 
    let newTimeline = tweet::timeLine
    newTimeline
