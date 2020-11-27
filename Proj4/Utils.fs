module Utils

open Akka.Actor
open Akka.FSharp
open System.Text.RegularExpressions

let system = ActorSystem.Create("FSharp")
let random = System.Random()
let hpat = @"\B#\w\w+"
let mpat = @"\B@\w\w+"

type Tweet = { Id: int; Message: string }

type User =
    { Id: string
      Followers: List<string>
      Tweets: List<Tweet> }

type Mention = { Id: string; Tweets: List<Tweet> }

type HashTag = { Id: string; Tweets: List<Tweet> }

type Command = 
  | Register of string
  | Login of string
  | Logout of string
  | Subscribe of string * string
  | Tweet of string * string
  | Retweet of string * int
  | GetMention of string
  | GetHashTag of string
  | GetSubFeed of string
  
type Response =
  | Feed of Tweet List
  | Mention of Tweet List
  | Hashtag of Tweet List
  | Update of string * Tweet * bool  

let getUserRef u =
    let actorPath = @"akka://FSharp/user/" + string u
    select actorPath system

let patternMatch m p = 
  Regex.Match(m, p) |> string
