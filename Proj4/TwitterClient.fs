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
                let pmsg = "@" + ref + "'s hashtag query: " + hashStr + "\n"
                printRef <! pmsg  
                let mutable count = 1
                for tweetObj in tweetList do
                    let currMsg = "\t HT"+(string count)+") " + tweetObj.Message + "\n"
                    printRef <! currMsg  
                    count<-count+1 
                               
            | MentionList (mentionStr,tweetList) ->
                let pmsg = "@" + ref + "'s mention query: " + mentionStr + "\n"
                printRef <! pmsg  
                let mutable count = 1
                for tweetObj in tweetList do
                    let currMsg = "\t MT"+(string count)+") " + tweetObj.Message + "\n"
                    printRef <! currMsg 
                    count<-count+1
            | SendTweet (m) -> engineRef <! CmdTweet(ref, m)
            | SendRetweet -> 
                if (timelineTweets.Length > 0) then
                  let randomTweet = pickRandom timelineTweets
                  engineRef <! CmdRetweet (ref,randomTweet.Id)
                else 
                  printfn "Nothing to Retweet here"  
            | GetHashtag (hashtagStr) ->
                engineRef <! QueryHashtag (hashtagStr)
            | GetMention (mentionStr) ->
                engineRef <! QueryMention (mentionStr)
            | ViewTimeline ->
                let pmsg = "View @" + ref + "'s timeline : " + "\n"
                printRef <! pmsg  
                let mutable count = 1
                for tweetObj in timelineTweets do
                    let currMsg = "\t TT"+(string count)+") " + tweetObj.Message + "\n"
                    printRef <! currMsg 
                    count<-count+1
            return! loop ()
        }

    loop ()
