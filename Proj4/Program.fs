open System
open Utils
open Akka.FSharp
open TwitterEngine
open TwitterClient
open System.IO
open System.Threading
open MathNet.Numerics.Distributions


[<EntryPoint>]
let main argv =
    let mutable flag = true
    let numOfUsers = 100
    let maxSubscribers = 50
    let sw = System.Diagnostics.Stopwatch()
    let hashTagLines = File.ReadAllLines(@"hashtags.txt")
    let hashtagList = Seq.toList hashTagLines
    let actorOfSink (f : 'a -> unit) = actorOf f
    let print msg =  printfn "%s" msg
    let printref = actorOfSink print |> spawn system "print"
    let engineRef = spawn system "engine" engineBehavior    
    let mutable subCountMap = Map.empty
    let mutable userSubMap = Map.empty
    let mutable activeTweets = List.empty
    sw.Start()
    let zipfSub = Zipf(0.8, maxSubscribers)   

    for i in [1..numOfUsers] do
      let sample = zipfSub.Sample()
      subCountMap <- subCountMap.Add(i, sample)
 
    for i in [1..numOfUsers] do
      let pool = [1..numOfUsers] |> List.except (List.singleton i)
      let scount = subCountMap.TryFind(i).Value
      let slist = pickRandLen pool scount
      userSubMap <- userSubMap.Add(i, slist)

    for i in [1..numOfUsers] do
      let uid = getUserId i
      engineRef <! Register uid
      spawn system uid (clientBehavior uid) |> ignore

    Thread.Sleep(1000)

    for i in [1..numOfUsers] do
      let sid = getUserId i
      let slist = userSubMap.TryFind(i).Value
      for j in slist do 
        let uid = getUserId j
        engineRef <! Subscribe (uid, sid)

    Thread.Sleep(1000)   

    let composeRandTweet u = 
      let pool =  [1..numOfUsers] |> List.except(List.singleton u)
      let randMen = getUserId (pickRandom pool)
      let randHash = pickRandom hashtagList
      "This is a random tweet with a random mention @" + randMen + " and a random hashtag #" + randHash

    let performTweet u = 
      let uid = getUserId u
      let cref = getUserRef uid
      let rmsg = composeRandTweet u
      activeTweets <-rmsg::activeTweets
      cref <! SendTweet rmsg

    let performRetweet u =
      let uid = getUserId u
      let cref = getUserRef uid
      cref <! SendRetweet

    let mutable boolMap = Map.empty
    [1..numOfUsers] |> List.iter (fun x -> (boolMap <- boolMap.Add(x,false)))    

    let simCompleteCheck b = 
      b |> Map.toSeq |> Seq.map snd |> Seq.fold (&&) true

    let getRandom() =
        let rand = System.Random()
        rand.Next(100)
    let generateRandProb = 
        let probVal = getRandom
        probVal
    
    while (not (simCompleteCheck boolMap)) do
      for KeyValue(key, value) in subCountMap do
        if (value > 0) then 
          let probVal = generateRandProb()
          printfn "[PROGRAM] %i" probVal
          if(probVal<=80) then
            performTweet key
            subCountMap <- subCountMap.Add(key, value - 1)
          else
            performRetweet key
            subCountMap <- subCountMap.Add(key, value - 1)
        else 
          boolMap <- boolMap.Add(key, true)  
    
    let tweetStr = pickRandom(activeTweets)
    let tags = (patternMatch tweetStr hpat) 
    if (tags.Length > 0) then
      let tagStr:string = pickRandom tags
      printfn "[PROGRAM] tagStr : %s  " tagStr
      let userID = pickRandom([1..numOfUsers])
      let ref = getUserId userID;
      let actorRef = getUserRef ref
      actorRef<! GetHashtag tagStr


    let tweetStr2 = pickRandom(activeTweets)
    let mens = (patternMatch tweetStr2 mpat)
    if (mens.Length > 0) then
      let mentionStr = pickRandom mens
      let userID = pickRandom([1..numOfUsers])
      let ref2 = getUserId userID;
      let actorRef2 = getUserRef ref2
      actorRef2<! GetMention mentionStr

    sw.Stop()
    Thread.Sleep(5000)   
    printfn "Simulation completed in %A" sw.ElapsedMilliseconds
         
    while flag do ignore()
    0