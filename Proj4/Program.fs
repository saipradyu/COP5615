open System
open Utils
open Akka.FSharp
open TwitterEngine
open TwitterClient
open System.IO
open System.Threading

[<EntryPoint>]
let main argv =
    let mutable flag = true
    // let numOfUsers = argv.[0] |>int
    // let numOfTweets = argv.[1] |>int
    let numOfUsers = 30
    let numOfTweets = 5
    let tweetLines = File.ReadAllLines(@"gentweets.txt")
    let tweetList = Seq.toList tweetLines
    // printfn " %A " tweetList
    let hashTagLines = File.ReadAllLines(@"hashtags.txt")
    let hashtagList = Seq.toList hashTagLines
    // printfn " %A " hashtagList
    let userLines = File.ReadAllLines(@"usernames.txt")
    let userList = Seq.toList userLines
    let engineRef = spawn system "engine" engineBehavior
    for user in userList do
        engineRef <! Register user
        spawn system user (clientBehavior user) |> ignore
    let mutable looping = true
    let mutable activeUserList = List.empty
    let mutable count = 0
    let mutable activeTweets = List.empty
    while looping do
        let user = pickRandom userList
        activeUserList<-user::activeUserList
        count<-count+1
        if(count=userList.Length/2) then
            looping<-false
    for user in activeUserList do
        engineRef <! Login user
        
    for user in activeUserList do
        let mutable followerList = List.empty
        for i = 0 to (userList.Length/2) do
            let follower = pickRandom(userList);
            // Follower shouldnt be the currect username and the follower actor should be present in the system
            if not (follower.Equals(user))  then 
                followerList<- follower::followerList
        for follower in followerList do
            engineRef<! Subscribe (follower,user)
    
    for i=0 to (numOfTweets-1) do
        let ref = pickRandom(userList);
        let tweet = pickRandom(tweetList)
        let actorRef = getUserRef ref
        activeTweets<-tweet::activeTweets
        actorRef <! SendTweet(tweet)

    // engineRef <! DebugTweetTable
    // Send retweet
    // for i=0 to numOfTweets-1 do
    //     let ref = pickRandom(activeUserList);
    //     let tweet = pickRandom(tweetList)
    //     // let actorRef = getUserRef ref
    //     // engineRef <! Register ref
    //     // engineRef <! Login ref
    //     printfn "[MAIN] RETWEET : %s" ref
    //     let actorRefTest = getUserRef ref
    //     actorRefTest <! SendRetweet
 
    // Query mentions and Hashtags
    let tweetStr = pickRandom(activeTweets)
    let tags = patternMatch tweetStr hpat
    let tagStr = pickRandom tags
    let ref = pickRandom(activeUserList);
    let actorRef = getUserRef ref
    actorRef<! GetHashtag tagStr
    // printfn "HASHTAG TABLE : %s" tagStr
    // engineRef<! DebugHashtagTable

    let tweetStr2 = pickRandom(activeTweets)
    let mens = patternMatch tweetStr2 mpat
    let mentionStr = pickRandom mens
    let ref2 = pickRandom(activeUserList);
    let actorRef2 = getUserRef ref2
    actorRef2<! GetMention mentionStr
    // printfn "MENTION TABLE : %s" mentionStr
    // engineRef<! DebugMentionTable

    while flag do ignore()
    0