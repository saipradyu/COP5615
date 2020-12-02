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
            Thread.Sleep(2000)
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
            | HashtagList (tweetList)->
                printfn "%s's hashtag query:" ref
                for tweetObj in tweetList do
                    printfn "%s" tweetObj.Message                    
            | MentionList (tweetList) ->
                printfn "%s's mention query:" ref
                for tweetObj in tweetList do
                    printfn "%s" tweetObj.Message 
            | SendTweet (m) -> engineRef <! CmdTweet(ref, m)
            | SendRetweet -> 
                let randomTweet = pickRandom timelineTweets
                printfn "%s TIMELINE RANDOM TWEET : %s and tweet ID %i" ref randomTweet.Message randomTweet.Id
                engineRef <! CmdRetweet (ref,randomTweet.Id)
            | GetHashtag (hashtagStr) ->
                printfn "CLIENT Query hashtag : %s" hashtagStr
                engineRef <! QueryHashtag (ref,hashtagStr)
            | GetMention (mentionStr) ->
                engineRef <! QueryMention (ref, mentionStr)

            return! loop ()
        }

    loop ()
