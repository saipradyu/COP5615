module Utils

open Akka.Actor
open Akka.FSharp
open System.Text.RegularExpressions

let system = ActorSystem.Create("FSharp")
let random = System.Random()

let pickRandom (l: List<_>) =
    let r = System.Random()
    l.[r.Next(l.Length)]

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

type Response =
    | TweetFeed of string * Tweet
    | RetweetFeed of string * Tweet
    | MentionFeed of string * Tweet
    | SendTweet of string
    | SendRetweet

let getUserRef u =
    let actorPath = @"akka://FSharp/user/" + string u
    select actorPath system

let sanitize l =
    List.map ((string) >> (fun x -> x.Substring(1))) l

let patternMatch m p = 
    Regex.Matches(m, p) |> Seq.toList |> sanitize

