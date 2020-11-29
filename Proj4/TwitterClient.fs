module TwitterClient

open Utils
open Akka.Actor
open Akka.FSharp

let mutable timelineTweets = List.empty

let sendTweet (sender:string) (tweetStr:string)= 
    let engineActor = getUserRef "engine"
    engineActor <! TweetCommand (sender,tweetStr)

let receiveTweet (sender:string) (inbox: Actor<_>) =
    let rec loop () =
        actor {
            let! msg = inbox.Receive()
            match msg with
            | Feed (newTweet) ->
                timelineTweets <- newTweet::timelineTweets
            | Mention (mentionedTweet) ->  
                let tweet = mentionedTweet.Message
                printfn "%s has mentioned you in tweet : %s" sender tweet
            | Update (sender,tweetType,tweetObj) ->
                if tweetType.Equals("Tweet") then
                    printfn "%s has tweeted : %s" sender tweetObj.Message
                    timelineTweets <- (tweetObj)::timelineTweets
            return! loop ()
        }
    loop ()


