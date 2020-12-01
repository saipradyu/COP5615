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
            | HashtagList (tweetList)->
                printfn "%s's hashtag query:" ref
                for tweetStr in tweetList do
                    printfn "%s" tweetStr.Message                    
            | MentionList (tweetList) ->
                printfn "%s's mention query:" ref
                for tweetStr in tweetList do
                    printfn "%s" tweetStr.Message 
            | SendTweet (m) -> engineRef <! CmdTweet(ref, m)
            | SendRetweet -> 
                let randomTweet = pickRandom timelineTweets
                engineRef <! CmdRetweet (ref,randomTweet.Id)
            | GetHashtag (hashtagStr) ->
                engineRef <! QueryHashtag (ref,hashtagStr)
            | GetMention (mentionStr) ->
                engineRef <! QueryMention (ref, mentionStr)
            return! loop ()
        }

    loop ()