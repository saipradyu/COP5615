open System
open Utils
open Akka.FSharp
open TwitterEngine
open TwitterClient
open System.IO
open System.Threading

let generateUsers numOfUsers = 
    let mutable (userList:string List) = List.empty
    for i=1 to numOfUsers do
        userList<- ("user"+(string i))::userList
    userList

// let generateTweets userList hashtagList = 
//     let mutable tweetList = List.empty
//     let len = userList.Length
//     for i=0 to len-1 do
//         let mention  = pickRandom(userList)
//         let hashtag = pickRandom(hashtagList)
//         let user = pickRandom(userList)
//         let msg = user+" has tweeted. @"+mention+" #"+hashtag
//         tweetList<-msg::tweetList
//     tweetList

[<EntryPoint>]
let main argv =
    let mutable flag = true
    // let numOfUsers = argv.[0] |>int
    // let numOfTweets = argv.[1] |>int
    let numOfUsers = 30
    let numOfTweets = 5
    let actorOfSink (f : 'a -> unit) = actorOf f
    let print msg =  printfn "%s" msg
    let printref = actorOfSink print |> spawn system "print"

    // let tweetLines = File.ReadAllLines(@"gentweets.txt")
    // let tweetList = Seq.toList tweetLines

    let userList = generateUsers numOfUsers
    let hashTagLines = File.ReadAllLines(@"hashtags.txt")
    let hashtagList = Seq.toList hashTagLines
    // printfn " %A " hashtagList
    // let tweetList = generateTweets userList hashtagList
    let mutable tweetList = List.empty
    let len = userList.Length
    for i=0 to len-1 do
        let mention  = pickRandom(userList)
        let hashtag = pickRandom(hashtagList).Trim()
        let msg = "Lorem ipsum tweet. @"+mention+" "+hashtag
        tweetList<-msg::tweetList
    printfn " %A " tweetList
  
    // let userLines = File.ReadAllLines(@"usernames.txt")
    // let userList = Seq.toList userLines
   
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
    
    for i=0 to (20*numOfTweets-1) do
        let ref = pickRandom(userList);
        let tweet = pickRandom(tweetList)
        let actorRef = getUserRef ref
        activeTweets<-tweet::activeTweets
        actorRef <! SendTweet(tweet)

    // engineRef <! DebugTweetTable
    // Send retweet
    for i=0 to 20*numOfTweets-1 do
        let ref = pickRandom(activeUserList);
        let tweet = pickRandom(tweetList)
        // let actorRef = getUserRef ref
        // engineRef <! Register ref
        // engineRef <! Login ref
        printfn "[MAIN] RETWEET : %s" ref
        let actorRefTest = getUserRef ref
        actorRefTest <! SendRetweet
 
    // Query mentions and Hashtags
    // let tweetStr = pickRandom(activeTweets)
    // let tags = (patternMatch tweetStr hpat) 
    // if (tags.Length > 0) then
    //   let tagStr = pickRandom tags
    //   let ref = pickRandom(activeUserList);
    //   let actorRef = getUserRef ref
    //   actorRef<! GetHashtag tagStr
    // printfn "HASHTAG TABLE : %s" tagStr
    // engineRef<! DebugHashtagTable

    // let tweetStr2 = pickRandom(activeTweets)
    // let mens = (patternMatch tweetStr2 mpat)
    // if (mens.Length > 0) then
    //   let mentionStr = pickRandom mens
    //   let ref2 = pickRandom(activeUserList);
    //   let actorRef2 = getUserRef ref2
    //   actorRef2<! GetMention mentionStr
    // printfn "MENTION TABLE : %s" mentionStr
    // engineRef<! DebugMentionTable

    while flag do ignore()
    0