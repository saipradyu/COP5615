module Utils

open Akka.Actor
open Akka.FSharp
open System.Text.RegularExpressions

let system = ActorSystem.Create("FSharp")
let random = System.Random()
let hpat = @"\B#\w\w+"
let mpat = @"\B@\w\w+"

let pickRandom (l: List<_>) =
    let r = System.Random()
    l.[r.Next(l.Length)]

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
  | TweetCommand of string * string
  | Retweet of string * int
  
type Response =
  | Feed of Tweet
  | Mention of Tweet
  | Update of string * string * Tweet  // sender, type(tweet, RT, mention), tweet
  //| Hashtag of Tweet

let getUserRef u =
    let actorPath = @"akka://FSharp/user/" + string u
    select actorPath system

let patternMatch m p = 
  Regex.Match(m, p) |> string
