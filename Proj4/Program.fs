open System
open Utils
open Akka.FSharp
open TwitterEngine
open TwitterClient
open System.IO

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
        spawn system user (clientProcess user) |> ignore
    let mutable looping = true
    let mutable activeUserList = List.empty
    let mutable count = 0
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
                followerList<- pickRandom(userList)::followerList
        for follower in followerList do
            let followerActor = getUserRef follower
            engineRef<! Subscribe (follower,user)
    for i=0 to numOfTweets-1 do
        let userRef = pickRandom(userList);
        let tweet = pickRandom(tweetList)
        let userActor = getUserRef userRef
        sendTweet userRef  tweet
    // for i=0 to numOfTweets-1 do
    //     let userRef = pickRandom(userList);
    //     let tweet = pickRandom(tweetList)
    //     let userActor = getUserRef userRef
    //     sendTweet userRef tweet
        // engineRef <! TweetCommand (userRef,tweet)
    
    // engineRef <! Register("user1")
    while flag do ignore()
    0