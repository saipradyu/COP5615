module TwitterEngine

open Utils
open Akka.Actor
open Akka.FSharp
open System.Text.RegularExpressions

let hpat = @"\B#\w\w+"
let mpat = @"\B@\w\w+"

let engineBehavior (inbox: Actor<Command>) =

    let mutable tweets = Map.empty
    let mutable users = Map.empty
    let mutable mentions = Map.empty
    let mutable hashtags = Map.empty
    let mutable activeUsers = List.empty

    let availableUsers l = 
      Set.intersect (Set.ofList l) (Set.ofList activeUsers) |> Set.toList |> List.map (getUserRef)    

    let broadcastResponse l m = 
      let refs = availableUsers l
      for ref in refs do 
        ref <! m    

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

    let insertTags tags t = 
      for tag in tags do
        if (hashtags.TryFind tag).IsSome then
          let hrec = (hashtags.TryFind tag).Value
          let hup = { Id = tag; TweetList = t::hrec.TweetList }
          hashtags <- hashtags.Add(tag, hup)
        else 
          let hup = { Id = tag; TweetList = List.singleton t }
          hashtags <- hashtags.Add(tag, hup)

    let insertMentions mens t = 
      for men in mens do
        if (mentions.TryFind men).IsSome then
          let mrec = (mentions.TryFind men).Value
          let mup = { Id = men; TweetList = t::mrec.TweetList }
          mentions <- mentions.Add(men, mup)
        else 
          let mup = { Id = men; TweetList = List.singleton t }
          mentions <- mentions.Add(men, mup)            

    let handleLogin u =
      activeUsers<- u::activeUsers

    let handleLogout u =
      let newActiveUsers = (remove u activeUsers)
      activeUsers <- newActiveUsers
      
    let handleRegister u =
      if users.ContainsKey u then
        printfn "%s is already registered" u
      else 
        let record = { Id = u; Followers = List.empty; TweetList = List.empty }
        users <- users.Add(u, record)
        printfn "%s has been registered" u

    let handleSubscribe u s = 
      let record = (users.TryFind s).Value
      let update = { record with Followers = u::record.Followers }
      users <- users.Add(s, update)
      printfn "%s has subscribed to %s's Tweets" u s

    let handleTweet s m = 
      let record = (users.TryFind s).Value
      let tid = genUniqueTID
      let tweet = { Id = tid; Message = m}
      let mens = patternMatch m mpat
      let tags = patternMatch m hpat
      insertTags tags tweet
      insertMentions mens tweet
      let update = { record with TweetList = tweet::record.TweetList }
      users <- users.Add(s, update)
      tweets <- tweets.Add(tid, tweet)
      let tmsg = TweetFeed (s, tweet)
      broadcastResponse update.Followers tmsg
      let mmsg = MentionFeed (s, tweet)
      broadcastResponse update.Followers mmsg

    let handleRetweet s tid = 
      let tweet = (tweets.TryFind tid).Value  
      let record = (users.TryFind s).Value
      let update = { record with TweetList = tweet::record.TweetList }
      users <- users.Add(s, update)
      let rmsg = RetweetFeed (s, tweet)
      broadcastResponse update.Followers rmsg

    let rec loop () =
        actor {
            let! msg = inbox.Receive()
            match msg with
            | Register (u) -> handleRegister u
            | Login (u) -> handleLogin u
            | Logout (u) -> handleLogout u
            | Subscribe(u, s) -> handleSubscribe u s
            | CmdTweet(s, m) -> handleTweet s m
            | CmdRetweet(s, tid) -> handleRetweet s tid
            return! loop ()
        }

    loop ()
