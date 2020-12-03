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
            let printRef = getUserRef "print"
            match msg with
            | TweetFeed (s, t) ->
                let pmsg = "@"+ref + "'s Timeline update: " + "\n @" + s + " has tweeted with message: "+"\n \t" + t.Message
                printRef <! pmsg
                timelineTweets <- t :: timelineTweets
            | RetweetFeed (s, t) ->
                let pmsg = "@"+ref + "'s Timeline update: " + "\n @" + s + " has retweeted with message: "+"\n \t" + t.Message
                printRef <! pmsg
                timelineTweets <- t :: timelineTweets
            | MentionFeed (s, t) ->
                let pmsg = "@"+ref + "'s Timeline update: " + "\n @" + s + " has mentioned you in tweet with message: "+"\n \t" + t.Message
                printRef <! pmsg
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
                if (timelineTweets.Length > 0) then
                  let randomTweet = pickRandom timelineTweets
                  printfn "%s TIMELINE RANDOM TWEET : %s  and tweet ID %i" ref randomTweet.Message randomTweet.Id
                  engineRef <! CmdRetweet (ref,randomTweet.Id)
                else 
                  printfn "Nothing to Retweet here"  
            | GetHashtag (hashtagStr) ->
                engineRef <! QueryHashtag (hashtagStr)
            | GetMention (mentionStr) ->
                engineRef <! QueryMention (mentionStr)

            return! loop ()
        }

    loop ()
