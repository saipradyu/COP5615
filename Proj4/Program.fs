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
    if (argv.Length <> 2) then failwith "Need two arguments i.e numOfUsers and maxSubscribers"
    let numOfUsers = argv.[0] |> int 
    let maxSubscribers = argv.[1] |> int
    let sw = System.Diagnostics.Stopwatch()
    let hashTagLines = File.ReadAllLines(@"hashtags.txt")
    let hashtagList = Seq.toList hashTagLines
    let actorOfSink (f : 'a -> unit) = actorOf f
    let print msg =  printfn "%s" msg
    let printref = actorOfSink print |> spawn system "print"
    let engineRef = spawn system "engine" engineBehavior    
    let mutable subCountMap = Map.empty
    let mutable userSubMap = Map.empty
    sw.Start()
    let zipfSub = Zipf(0.8, maxSubscribers)   

    let mutable subCount = 0
    for i in [1..numOfUsers] do
      let sample = zipfSub.Sample()
      subCount <- subCount + sample
      subCountMap <- subCountMap.Add(i, sample)
 
    for i in [1..numOfUsers] do
      let pool = [1..numOfUsers] |> List.except (List.singleton i)
      let scount = subCountMap.TryFind(i).Value
      let slist = pickRandLen pool scount
      userSubMap <- userSubMap.Add(i, slist)

    let composeRandTweet u = 
      let pool =  [1..numOfUsers] |> List.except(List.singleton u)
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

    let mutable boolMap = Map.empty
    [1..numOfUsers] |> List.iter (fun x -> (boolMap <- boolMap.Add(x,false)))    

    let simCompleteCheck b = 
      b |> Map.toSeq |> Seq.map snd |> Seq.fold (&&) true
        
    let simulatorBehavior (inbox: Actor<SimResponse>) = 
      let mutable rcount = 0
      let mutable scount = 0
      let mutable tcount = 0

      let rec loop() = 
        actor {
          let! msg = inbox.Receive()
          match msg with 
          | Start -> 
            sw.Start()
            for i in [1..numOfUsers] do
             let uid = getUserId i
             engineRef <! Register uid
             spawn system uid (clientBehavior uid) |> ignore
          | RegisterAck -> 
            rcount <- rcount + 1
            if (rcount = numOfUsers) then 
              printfn "Registration of user completed"
              for i in [1..numOfUsers] do
                let sid = getUserId i
                let slist = userSubMap.TryFind(i).Value
                for j in slist do 
                  let uid = getUserId j
                  engineRef <! Subscribe (uid, sid)              
          | SubscribeAck -> 
            scount <- scount + 1
            if (scount = subCount) then
              printfn "Subscription for each user completed"
              while (not (simCompleteCheck boolMap)) do
                for KeyValue(key, value) in subCountMap do
                  if (value > 0) then 
                    if(value % 5 = 0) then
                      performRetweet key
                      subCountMap <- subCountMap.Add(key, value - 1)
                    else
                      performTweet key
                      subCountMap <- subCountMap.Add(key, value - 1)
                  else 
                    boolMap <- boolMap.Add(key, true) 
          | TweetAck -> 
            tcount <- tcount + 1
            if (tcount = subCount) then
              printfn "Tweets and Retweets completed for each user"
              sw.Stop()
              printfn "Simulation completed in %A" sw.ElapsedMilliseconds
              Thread.Sleep(1000)
              flag <- false
          return! loop()
        }
      loop()

    let simRef = spawn system "simulation" simulatorBehavior  
    simRef <! Start
         
    while flag do ignore()
    0