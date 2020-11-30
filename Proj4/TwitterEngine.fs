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
    
    let handleLogin (username:string)=
      activeUsers<- username::activeUsers

    let handleLogout (username:string)=
      let newActiveUsers = (remove username activeUsers)
      activeUsers <- newActiveUsers

    let extractString (inputStr:string) = 
      let len = inputStr.Length;
      let outputStr = inputStr.[1..len-1]
      outputStr

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

    let insertTag tag t = 
      if (hashtags.TryFind tag).IsSome then
        let hrec = (hashtags.TryFind tag).Value
        let hup = { Id = tag; TweetList = t::hrec.TweetList}
        hashtags <- hashtags.Add(tag, hup)
      else 
        let hup = { Id = tag; TweetList = List.singleton t}
        hashtags <- hashtags.Add(tag, hup)

    let insertMention men t = 
      if (mentions.TryFind men).IsSome then
        let mrec = (hashtags.TryFind men).Value
        let mup = { Id = men; TweetList = t::mrec.TweetList}
        mentions <- mentions.Add(men, mup)
      else 
        let mup = { Id = men; TweetList = List.singleton t}
        mentions <- mentions.Add(men, mup)
      
    let handleTweet (sender:string) (tweetStr:string) = 
      let htag = patternMatch tweetStr hpat
      let men = patternMatch tweetStr mpat
      let record = (users.TryFind sender).Value
      let tid = genUniqueTID
      let tweet = { Id = tid; Message = tweetStr}
      let mutable mentionList = List.empty
      //Notify mentioned users
      let extractUser = extractString men
      mentionList<-extractUser::mentionList
      let extractHashtag = extractString htag
      // Send mention to user if he is logged in
      if(List.contains extractUser activeUsers) then
          let userActor = getUserRef extractUser
          userActor <! Update(sender,extractUser,"Mention",tweet)
      let update = { record with TweetList = tweet::record.TweetList }
      users <- users.Add(sender, update)
      tweets <- tweets.Add(tid, tweet)
      if not (isNull htag) then (insertTag extractHashtag tweet)  
      if not (isNull men) then (insertMention extractUser tweet)
      let followerList = update.Followers
      for follower in followerList do 
        let followerActor = getUserRef follower
        followerActor <! Update (sender,follower,"Tweet",tweet)

    let handleRetweet sender tweetID = 
      let tweet = (tweets.TryFind tweetID).Value  
      let record = (users.TryFind sender).Value
      let update = { record with TweetList = tweet::record.TweetList }
      users <- users.Add(sender, update)
      let followerList = update.Followers
      for follower in followerList do 
        let followerActor = getUserRef follower
        followerActor <! Update (sender,follower,"Retweet",tweet)

    let handleGetMention m = 
      if (mentions.TryFind m).IsSome then
        let mrec = (hashtags.TryFind m).Value
        mrec.TweetList
      else
        List.empty  

    let handleGetHashtag h = 
      if (hashtags.TryFind h).IsSome then
        let hrec = (hashtags.TryFind h).Value
        hrec.TweetList
      else
        List.empty

    // let getRandomTweet = 
    //   let tweetIDList = tweets |> Map.toSeq |> Seq.map fst |> Seq.toList
    //   let randomTweetID = pickRandom tweetIDList
    //   randomTweetID

    let rec loop () =
        actor {
            let! msg = inbox.Receive()
            let sender = inbox.Sender()
            match msg with
            | Register (u) -> handleRegister u
            | Login (senderActor) -> handleLogin senderActor
            | Logout (senderActor) -> handleLogout senderActor
            | Subscribe(u, s) -> handleSubscribe u s
            | TweetCommand(sendingActor, tweetStr) -> handleTweet sendingActor tweetStr
            | Retweet(sendingActor, tweetID) -> handleRetweet sendingActor tweetID
            // | GetRandomTweetID (sender) -> getRandomTweet sender
            return! loop ()
        }

    loop ()
