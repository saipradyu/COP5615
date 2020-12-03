open System
open Utils
open Akka.FSharp
open TwitterEngine
open TwitterClient
open System.IO
open System.Threading
open MathNet.Numerics.Distributions

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
    let numOfUsers = 10
    let numOfTweets = 5
    let maxSubscribers = 5
    let actorOfSink (f : 'a -> unit) = actorOf f
    let print msg =  printfn "%s" msg
    let printref = actorOfSink print |> spawn system "print"
    let engineRef = spawn system "engine" engineBehavior
    
    let userList = generateUsers numOfUsers
    for user in userList do
        engineRef <! Register user
        spawn system user (clientBehavior user) |> ignore
    Thread.Sleep(1000)

    let hashTagLines = File.ReadAllLines(@"hashtags.txt")
    let hashtagList = Seq.toList hashTagLines
    
    let mutable subCountMap = Map.empty
    let mutable userSubMap = Map.empty

    let mutable tweetList = List.empty
    let len = userList.Length
    for i=0 to len-1 do
        let mention  = pickRandom(userList)
        let hashtag = pickRandom(hashtagList).Trim()
        let msg = "Lorem ipsum tweet. @"+mention+" "+hashtag
        tweetList<-msg::tweetList
    // printfn " %A " tweetList

    let zipfSub = Zipf(0.8, maxSubscribers)   

    for i in [1..numOfUsers] do
      let sample = zipfSub.Sample()
      subCountMap <- subCountMap.Add(i, sample)
 
    for i in [1..numOfUsers] do
      let pool = [1..numOfUsers] |> List.except (List.singleton i)
      let scount = subCountMap.TryFind(i).Value
      let slist = pickRandLen pool scount
      let mutable subscriberList = List.empty
      for followerID in slist do
        subscriberList<- ("user"+(string followerID)):: subscriberList
      let userName = "user"+(string i)
      userSubMap <- userSubMap.Add(userName, subscriberList)
  
    let mutable looping = true
    let mutable activeUserList = List.empty
    let mutable count = 0
    let mutable activeTweets = List.empty

    for userRecord in userSubMap do
        let followerList = userRecord.Value
        let currUser = userRecord.Key
        activeUserList<-currUser::activeUserList
        for follower in followerList do
            engineRef<! Subscribe (follower,currUser)
    Thread.Sleep(1000)

    for i=0 to (20*numOfTweets-1) do
        let ref = pickRandom(userList);
        let tweet = pickRandom(tweetList)
        let actorRef = getUserRef ref
        activeTweets<-tweet::activeTweets
        actorRef <! SendTweet(tweet)

    // Send retweet
    for i=0 to 20*numOfTweets-1 do
        let ref = pickRandom(activeUserList);
        let tweet = pickRandom(tweetList)
        printfn "[MAIN] RETWEET : %s" ref
        let actorRefTest = getUserRef ref
        actorRefTest <! SendRetweet
 
    // Query mentions and Hashtags
    let tweetStr = pickRandom(activeTweets)
    let tags = (patternMatch tweetStr hpat) 
    if (tags.Length > 0) then
      let tagStr:string = pickRandom tags
      printfn "[PROGRAM] tagStr : %s  " tagStr
      let ref = pickRandom(activeUserList);
      let actorRef = getUserRef ref
      actorRef<! GetHashtag tagStr
  

    let tweetStr2 = pickRandom(activeTweets)
    let mens = (patternMatch tweetStr2 mpat)
    if (mens.Length > 0) then
      let mentionStr = pickRandom mens
      let ref2 = pickRandom(activeUserList);
      let actorRef2 = getUserRef ref2
      actorRef2<! GetMention mentionStr
  
    while flag do ignore()
    0