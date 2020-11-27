module TwitterEngine

open Utils
open Akka.Actor
open Akka.FSharp
open TwitterClient

let engineBehavior (inbox: Actor<Command>) =

    let mutable tweets = Map.empty
    let mutable users = Map.empty
    let mutable mentions = Map.empty
    let mutable hashtags = Map.empty
    let mutable activeUsers = List.empty

    let genUniqueTID = 
      let ids = tweets |> Map.toSeq |> Seq.map fst
      let mutable looping = true
      let mutable tid = random.Next()
      while looping do
        if not (Seq.contains tid ids) then
          looping <- false
        else 
          tid <- random.Next()  
      tid

    let handleRegister u =
      if users.ContainsKey u then
        printfn "%s is already registered" u
      else 
        let record = { Id = u; Followers = List.empty; Tweets = List.empty }
        users <- users.Add(u, record)
        printfn "%s has been registered" u

    let handleSubscribe u s = 
      let record = (users.TryFind s).Value
      let update = { record with Followers = u::record.Followers }
      users <- users.Add(s, update)
      printfn "%s has subscribed to %s's tweets" u s

    let insertTag tag t = 
      if (hashtags.TryFind tag).IsSome then
        let hrec = (hashtags.TryFind tag).Value
        let hup = { Id = tag; Tweets = t::hrec.Tweets}
        hashtags <- hashtags.Add(tag, hup)
      else 
        let hup = { Id = tag; Tweets = List.singleton t}
        hashtags <- hashtags.Add(tag, hup)

    let insertMention men t = 
      if (mentions.TryFind men).IsSome then
        let mrec = (hashtags.TryFind men).Value
        let mup = { Id = men; Tweets = t::mrec.Tweets}
        mentions <- mentions.Add(men, mup)
      else 
        let mup = { Id = men; Tweets = List.singleton t}
        mentions <- mentions.Add(men, mup)
      
    let handleTweet u m = 
      let htag = patternMatch m hpat
      let men = patternMatch m mpat
      let record = (users.TryFind u).Value
      let tid = genUniqueTID
      let tweet = { Id = tid; Message = m}
      let update = { record with Tweets = tweet::record.Tweets }
      users <- users.Add(u, update)
      tweets <- tweets.Add(tid, tweet)
      if not (isNull htag) then (insertTag htag tweet)  
      if not (isNull men) then (insertMention men tweet)  

    let handleRetweet u i = 
      let tweet = (tweets.TryFind i).Value  
      let record = (users.TryFind u).Value
      let update = { record with Tweets = tweet::record.Tweets }
      users <- users.Add(u, update)      

    let handleGetMention m = 
      if (mentions.TryFind m).IsSome then
        let mrec = (hashtags.TryFind m).Value
        mrec.Tweets
      else
        List.empty  

    let handleGetHashtag h = 
      if (hashtags.TryFind h).IsSome then
        let hrec = (hashtags.TryFind h).Value
        hrec.Tweets
      else
        List.empty  

    let rec loop () =
        actor {
            let! msg = inbox.Receive()
            let sender = inbox.Sender()

            match msg with
            | Register (u) -> handleRegister u
            | Login (u) -> failwith "Not Implemented"
            | Logout (u) -> failwith "Not Implemented"
            | Subscribe(u, s) -> handleSubscribe u s
            | Tweet(u, m) -> failwith "Not Implemented"
            | Retweet(u, i) -> failwith "Not Implemented"
            | GetMention(m) -> failwith "Not Implemented"
            | GetHashTag(h) -> failwith "Not Implemented"
            | GetSubFeed(s) -> failwith "Not Implemented"
            return! loop ()
        }

    loop ()
