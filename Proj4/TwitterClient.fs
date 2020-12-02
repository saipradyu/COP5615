module TwitterClient

open Utils
open Akka.FSharp
open System.Threading
let mutable timelineTweets = List.empty

let clientBehavior ref (inbox: Actor<Response>) =
    let rec loop () =
        actor {
            let! msg = inbox.Receive()
            let engineRef = getUserRef "engine"
            Thread.Sleep(1000)
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
            | HashtagList (hashStr, tweetList)->
                printfn "%s's hashtag query: %s " ref hashStr
                let mutable count = 1
                for tweetObj in tweetList do
                    printfn "\t %i) %s" count tweetObj.Message
                    count<-count+1                  
            | MentionList (mentionStr,tweetList) ->
                printfn "%s's mention query: %s " ref mentionStr
                let mutable count = 1
                for tweetObj in tweetList do
                    printfn "\t %i) %s" count tweetObj.Message
                    count<-count+1
            | SendTweet (m) -> engineRef <! CmdTweet(ref, m)
            | SendRetweet -> 
                let randomTweet = pickRandom timelineTweets
                printfn "%s TIMELINE RANDOM TWEET : %s  and tweet ID %i" ref randomTweet.Message randomTweet.Id
                engineRef <! CmdRetweet (ref,randomTweet.Id)
            | GetHashtag (hashtagStr) ->
                engineRef <! QueryHashtag (ref,hashtagStr)
            | GetMention (mentionStr) ->
                engineRef <! QueryMention (ref, mentionStr)

            return! loop ()
        }

    loop ()
