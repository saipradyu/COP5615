module TwitterClient

open Utils
open Akka.FSharp

let mutable timelineTweets = List.empty

let clientBehavior ref (inbox: Actor<Response>) =
    let rec loop () =
        actor {
            let! msg = inbox.Receive()
            let engineRef = getUserRef "engine"

            match msg with
            | TweetFeed (s, t) ->
                printfn "%s's Timeline update:" ref
                printfn "%s has tweeted with message: %s" s t.Message
                timelineTweets <- t :: timelineTweets
            | RetweetFeed (s, t) ->
                printfn "%s's Timeline update:" ref
                printfn "%s has retweeted: %s" s t.Message
                timelineTweets <- t :: timelineTweets
            | MentionFeed (s, t) ->
                printfn "%s's Timeline update:" ref
                printfn "%s has mentioned you in tweet with message: %s" s t.Message
                timelineTweets <- t :: timelineTweets
            | SendTweet (m) -> engineRef <! CmdTweet(ref, m)
            | SendRetweet -> failwith "Not Implemented"

            return! loop ()
        }

    loop ()
