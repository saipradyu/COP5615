module TwitterClient

open Utils
open Akka.Actor
open Akka.FSharp

let mutable timelineTweets = List.empty

let sendTweet (sender:string) (tweetStr:string)= 
    let engineActor = getUserRef "engine"
    engineActor <! TweetCommand (sender,tweetStr)
        
        
let sendRetweet (sender:string) (tweetID:int) = 
    let engineActor = getUserRef "engine"
    engineActor <! Retweet (sender,tweetID)

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
            | Update (sender, receiver, tweetType,tweetObj) ->
                if tweetType.Equals("Tweet") then
                    printfn "%s's Timeline \n %s has tweeted : %s" receiver sender tweetObj.Message
                    timelineTweets <- (tweetObj)::timelineTweets
                elif tweetType.Equals("Retweet") then
                    printfn "%s's Timeline \n %s has retweeted : %s" receiver sender tweetObj.Message
                    timelineTweets <- (tweetObj)::timelineTweets
                elif tweetType.Equals("Mention") then
                    printfn "%s's Timeline \n %s has mentioned you : %s" receiver sender tweetObj.Message
                    timelineTweets <- (tweetObj)::timelineTweets

            return! loop ()
        }
    loop ()


