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
      
    let handleTweet sender (tweetStr:string) = 
      let wordList = tweetStr.Split(" ") |> Array.toList
      let rawHashtagList = wordList |> List.filter (fun s -> s.StartsWith('#'))
      let rawMentionList = wordList |> List.filter (fun s -> s.StartsWith('@'))
      let record = (users.TryFind sender).Value
      let tid = genUniqueTID
      let tweet = { Id = tid; Message = tweetStr}
      let mutable mentionList = List.empty
      let mutable hashTagList = List.empty
      for username in rawMentionList do
        let extractUser = extractString username
        mentionList<-extractUser::mentionList
        //Insert into mention table
        insertMention extractUser tweet
        let userActor = getUserRef extractUser
        //Notify mentioned users
        userActor <! Update(sender,extractUser,"Mention",tweet)
      let update = { record with TweetList = tweet::record.TweetList }
      users <- users.Add(sender, update)
      tweets <- tweets.Add(tid, tweet)
      if (rawHashtagList.Length>0) then
        for htag in rawHashtagList do
          let extractHashtag = extractString htag
          hashTagList<-extractHashtag::hashTagList
           //Insert into hashtag table
          insertTag extractHashtag tweet
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

    // let handleRequest commandStr:string = 
    //   if()

    let rec loop () =
        actor {
            let! msg = inbox.Receive()
            let sender = inbox.Sender()
            match msg with
            | Register (u) -> handleRegister u
            | Login (u) -> failwith "Not Implemented"
            | Logout (u) -> failwith "Not Implemented"
            | Subscribe(u, s) -> handleSubscribe u s
            | TweetCommand(sender, tweetStr) -> handleTweet sender tweetStr
            | Retweet(sender, tweetID) -> handleRetweet sender tweetID
            // | GetTweetID (tweetString) -> handleRequest commandStr
            return! loop ()
        }

    loop ()
