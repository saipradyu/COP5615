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
    // let numOfUsers = argv.[0] |>int
    // let numOfTweets = argv.[1] |>int
    let numOfUsers = 1000
    let maxSubscribers = 500
    let hashTagLines = File.ReadAllLines(@"hashtags.txt")
    let hashtagList = Seq.toList hashTagLines
    let actorOfSink (f : 'a -> unit) = actorOf f
    let print msg =  printfn "%s" msg
    let printref = actorOfSink print |> spawn system "print"
    let engineRef = spawn system "engine" engineBehavior    
    let mutable subCountMap = Map.empty
    let mutable userSubMap = Map.empty

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
      let pool = [1..numOfUsers] |> List.except(List.singleton u)
      let randMen = getUserId (pickRandom pool) 
      let randHash = pickRandom hashtagList
      "This is a random tweet with a random mention @" + randMen + " and a random hashtag #" + randHash

    let performTweet u = 
      let uid = getUserId u
      let cref = getUserRef uid
      let rmsg = composeRandTweet u
      cref <! SendTweet rmsg

    let performRetweet u =
      let uid = getUserId u
      let cref = getUserRef uid
      cref <! SendRetweet
   
    while flag do ignore()
    0