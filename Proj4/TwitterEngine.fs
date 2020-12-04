module TwitterEngine

open Utils
open Akka.FSharp

let engineBehavior (inbox: Actor<Command>) =

    let mutable tweets = Map.empty
    let mutable users = Map.empty
    let mutable mentions = Map.empty
    let mutable hashtags = Map.empty
    let mutable activeUsers = List.empty

    let generateConnected (l: string list) = 
      let len = l.Length |> float
      let picklen = (len * 0.80) |> int
      pickRandLen l picklen
    
    let broadcastReady l = 
      let uids = users |> Map.toSeq |> Seq.map fst |> Seq.toList
      let connected = generateConnected uids
      Set.intersect (Set.ofList l) (Set.ofList connected) |> Set.toList |> List.map (getUserRef)    

    let broadcastResponse l m = 
      let refs = broadcastReady l
      for ref in refs do 
        ref <! m    

    let genUniqueTID ids = 
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
      printfn "%s has subscribed to %s's Tweets " u s

    let handleTweet sender tweetStr = 
      let record = (users.TryFind sender).Value
      let ids = tweets |> Map.toSeq |> Seq.map fst 
      let tid = genUniqueTID ids
      let tweet = { Id = tid; Message = tweetStr}
      let mens = patternMatch tweetStr mpat
      let tags = patternMatch tweetStr hpat
      insertTags tags tweet
      insertMentions mens tweet
      let update = { record with TweetList = tweet::record.TweetList }
      users <- users.Add(sender, update)
      tweets <- tweets.Add(tid, tweet)
      let tmsg = TweetFeed (sender, tweet)
      broadcastResponse update.Followers tmsg 
      let mmsg = MentionFeed (sender, tweet)
      broadcastResponse mens mmsg
     
    let handleRetweet s tid = 
      if (tweets.TryFind tid).IsSome then
        let tweet = (tweets.TryFind tid).Value  
        let record = (users.TryFind s).Value
        let update = { record with TweetList = tweet::record.TweetList }
        users <- users.Add(s, update)
        let rmsg = RetweetFeed (s, tweet)
        broadcastResponse update.Followers rmsg
      else
        printfn "ENGINE:RETWEET - Error"
    
    let queryHashtag senderRef hashStr = 
      if (hashtags.TryFind hashStr).IsSome then
        let hashtagObj = (hashtags.TryFind hashStr).Value 
        senderRef <! HashtagList(hashStr,hashtagObj.TweetList)
      else
        printfn "Cannot find hashtag : %s" hashStr
    
    let queryMention senderRef mentionStr =
      if (mentions.TryFind mentionStr).IsSome then 
        let mentionObj = (mentions.TryFind mentionStr).Value 
        senderRef <! MentionList(mentionStr,mentionObj.TweetList)
      else
        printfn "Cannot find mention : %s" mentionStr

    let rec loop () =
        actor {
            let! msg = inbox.Receive()
            let senderRef = inbox.Sender()
            match msg with
            | Register (u) -> handleRegister u
            | Login (u) -> handleLogin u
            | Logout (u) -> handleLogout u
            | Subscribe(u, s) -> handleSubscribe u s
            | CmdTweet(sender, message) -> handleTweet sender message
            | CmdRetweet(s, tid) -> handleRetweet s tid
            | QueryHashtag (hashStr) -> queryHashtag senderRef hashStr
            | QueryMention(mentionStr) -> queryMention senderRef mentionStr
            return! loop ()
        }

    loop ()
