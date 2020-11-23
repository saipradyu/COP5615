
#time "on"
#r "nuget: Akka.Fsharp"
#load "datamodel.fs"

open System
open Akka.Actor
open Akka.FSharp
open Datamodel.TwitterDatamodel

let system = ActorSystem.Create("FSharp")
(*
    usersTable - username, tweets, followers
    tweetsTable - tweetid,tweet
    hashTagsTable - hashtag,tweet
    mentionsTable - mention, tweet
*)
(********************Utility*********************)
let rec remove n lst = 
    match lst with
    | h::tl when h = n -> tl
    | h::tl -> h :: (remove n tl)
    | []    -> []

let getRandom =
    let rand = System.Random()
    let num = rand.Next()
    num
(********************Twitter**********************)
//Complex types for table values
// type TwitterTableValues = 
//     | UsersTableVal of int List * string List
//     | MentionsTableVal of string List
//     | HashTagsTableVal of string List
//     | TweetsTableVal of string

let createUser (username:string) = 
    spawn system username 

let mutable usersTable  = Map.empty
let mutable mentionsTable = Map.empty
let mutable hashTagsTable = Map.empty
let mutable tweetsTable = Map.empty

let mutable activeUsers = List.empty
// let getWorkerRef s =
//     let actorPath = @"akka://TwitterServer/user/" + string s
//     select actorPath system

// random, {"this is random","random day"}

// User table <username, (tweetList,followerList)>
let insertUser (username:string) (tweetList:int List) (followerList:string List) = 
    let value = UsersTableVal(tweetList, followerList)
    usersTable <- usersTable.Add(username,value)
    printfn "%s" username

// Tweet table <tweetID, tweet>
let insertTweet (tweetID:string) (tweet:string) = 
    let value = TweetsTableVal(tweet)
    tweetsTable <- tweetsTable.Add(tweetID,value)

// Hashtags table <hashTag, tweetList>
let insertHashTags (hashTag:string) (tweet:string )  = 
    let mapValue = hashTagsTable.TryFind hashTag
    match mapValue with 
    | Some twitterTableVal ->
        match twitterTableVal with
        | HashTagsTableVal (tList) ->
            let newTweetList = tweet::tList
            let newValue = HashTagsTableVal(newTweetList)
            hashTagsTable<-hashTagsTable.Add(hashTag,newValue)
        | _-> 
            printfn "Error"
    | None ->
        printfn "Error" 

// Mentions table <mention, tweetList>
let insertMention (mention:string) (tweet:string) = 
    let mapValue = mentionsTable.TryFind mention
    match mapValue with 
    | Some twitterTableVal ->
        match twitterTableVal with
        | MentionsTableVal (tList) ->
            let newTweetList = tweet::tList
            mentionsTable<-mentionsTable.Add(mention,newTweetList)
        | _-> 
            printfn "Error"
    | None ->
        printfn "Error" 

let addFollower (username:string) (follower:string) =
    let mapValue = usersTable.TryFind username
    match mapValue with 
    | Some twitterTableVal ->
        match twitterTableVal with
        | UsersTableVal ( tList, fList) ->
            let newFollowerList = follower::fList
            insertUser username tList newFollowerList
            printfn "addfollower %A %A" tList newFollowerList 
        | _-> 
            printfn "Error"       
    | None ->
        printfn "Error"    

let login (username:string) = 
    activeUsers <- username::activeUsers

let logout (username:string)  = 
    let newActiveUsers = (remove username activeUsers)
    activeUsers <- newActiveUsers

let getActiveUsers = activeUsers

let queryMentions (queryString:string) = 
    let mapValue = mentionsTable.TryFind queryString
    match mapValue with 
    | Some twitterTableVal ->
        match twitterTableVal with
        | MentionsTableVal (tweetList) ->            
            tweetList
        |_->
            printfn "Error"
    | None ->
        printfn "Error"  

let queryHashTags (queryString:string) = 
    let mapValue = hashTagsTable.TryFind queryString
    match mapValue with 
    | Some twitterTableVal ->
        match twitterTableVal with
        | HashTagsTableVal (tweetList) ->            
            tweetList      
    | None ->
        printfn "Error"

let logoutAll = 
    activeUsers <- List.empty;

let deleteTweets (tweetList: int List) = 
    let mapFinal = List.fold (fun mapPrev key -> Map.remove key mapPrev) tweetsTable tweetList
    tweetsTable<-mapFinal

let delete (username:string) =
    let mapValue = usersTable.TryFind username
    match mapValue with 
    | Some twitterTableVal ->
        match twitterTableVal with
        | UsersTableVal ( tList, fList) ->
            deleteTweets tList
            activeUsers<-(remove username activeUsers)
        | _-> 
            printfn "Error"       
    | None ->
        printfn "Error"


let generateTweetID = 
    let mutable tweetID = getRandom
    let mutable continueLooping = true
    let tweetIDList = tweetsTable |> Map.toSeq |> Seq.map fst |> Seq.toList
    while continueLooping do
        if(not(List.contains tweetID tweetIDList)) then
            continueLooping<-false
        else
            tweetID <- getRandom
    tweetID

let updateUserTweet (tweetID:int) (username:string)
    let mapValue = usersTable.TryFind username
        match mapValue with 
        | Some twitterTableVal ->
            match twitterTableVal with
            | UsersTableVal ( tList, fList) ->
                let newTweetList = tweetID::tList
                insertUser username newTweetList fList
        | None ->
            printfn "Error"

//TODO tweet
let tweet (tweet:string) (username:string) = 
    let tweetID = generateTweetID
    insertTweet tweetID tweet
    updateUserTweet tweetID username


    
    
