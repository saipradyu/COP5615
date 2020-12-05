module Utils

open Akka.Actor
open Akka.FSharp
open System.Text.RegularExpressions

let hpat = @"\B#\w\w+"
let mpat = @"\B@\w\w+"
let system = ActorSystem.Create("FSharp")
let random = System.Random()

let pickRandLen l c = 
    l |> List.sortBy (fun _ -> random.Next()) |> List.take c

let pickRandom (l: List<_>) =
    let r = System.Random()
    let randomItem = l.[r.Next(l.Length)]
    randomItem

let rec remove n lst =
    match lst with
    | h :: tl when h = n -> tl
    | h :: tl -> h :: (remove n tl)
    | [] -> []

type Tweet = { Id: int; Message: string }

type User =
    { Id: string
      Followers: List<string>
      TweetList: List<Tweet> }

type Mention = { Id: string; TweetList: List<Tweet> }

type HashTag = { Id: string; TweetList: List<Tweet> }

type Command =
    | Register of string
    | Login of string
    | Logout of string
    | Subscribe of string * string
    | CmdTweet of string * string
    | CmdRetweet of string * int
    | QueryHashtag of string
    | QueryMention of string

type Response =
    | TweetFeed of string * Tweet
    | RetweetFeed of string * Tweet
    | MentionFeed of string * Tweet
    | SendTweet of string
    | SendRetweet
    | GetHashtag of string
    | HashtagList of string * Tweet List
    | GetMention of string
    | MentionList of string * Tweet List 
    | ViewTimeline 

type SimResponse = 
    | Start
    | RegisterAck
    | SubscribeAck
    | TweetAck    
    
let getUserRef u =
    let actorPath = @"akka://FSharp/user/" + string u
    select actorPath system

let getUserId id = 
    "User" + string id

let sanitize l =
    List.map ((string) >> (fun x -> x.Substring(1))) l

let patternMatch m p = 
    Regex.Matches(m, p) |> Seq.toList |> sanitize

