#time "on"
#r "nuget: Akka.Fsharp"

open System
open Akka.Actor
open Akka.FSharp

let system = ActorSystem.Create("FSharp")
let mutable flag = true

let mutable activeUsers = List.empty

type Messages = 
    | CreateUser of string
    | AddFollower
    | Login of string
    | Logout of string
    | QueryMentions of string
    | QueryHashTags of string
    | LogoutAll
    | DeleteUser of string
    | Tweet of string*string

type TwitterTableValues = 
    | UsersTableVal of int List * string List
    | MentionsTableVal of string List
    | HashTagsTableVal of string List
    | TweetsTableVal of string
(**************************Utility*********************************)
let rec remove n lst = 
    match lst with
    | h::tl when h = n -> tl
    | h::tl -> h :: (remove n tl)
    | []    -> []

let getRandom =
    let rand = System.Random()
    let num = rand.Next()
    num
(*******************Twitter Utility*****************)
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

let deleteTweets (tweetList: int List) = 
    let mapFinal = List.fold (fun mapPrev key -> Map.remove key mapPrev) tweetsTable tweetList
    tweetsTable<-mapFinal

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

(*******************Driver code ********************)
match fsi.CommandLineArgs.Length with
| 3 -> ignore
| _ -> failwith "Requires number of users and number of tweets"

let args = fsi.CommandLineArgs |> Array.tail
let numberOfUsers = args.[0] |> int
let numberOfTweets = args.[1] |> int

let getServerRef = 
    let actorPath = @"akka://FSharp/user/twitterServer"
    select actorPath system

// let getAllWorkerRef = 
//     let actorPath = @"akka://FSharp/user/*"
//     select actorPath system

// let getWorkerRef s =
//     let actorPath = @"akka://FSharp/user/" + string s
//     select actorPath system

let serverFunction message = 
    match message with 
    | Init ->
        //TODO : Read from files and insert into tables
    | CreateUser(username) ->
        spawn system username 
    | AddFollower (username, follower)->
         let mapValue = usersTable.TryFind username
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
    | Login (username)->
        activeUsers <- username::activeUsers
    | Logout(username)->
        let newActiveUsers = (remove username activeUsers)
        activeUsers <- newActiveUsers
    | QueryMentions (queryString) ->
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
    | QueryHashTags (queryString) ->
        let mapValue = hashTagsTable.TryFind queryString
        match mapValue with 
        | Some twitterTableVal ->
            match twitterTableVal with
            | HashTagsTableVal (tweetList) ->            
                tweetList      
        | None ->
            printfn "Error"
    | LogoutAll ->
        activeUsers <- List.empty;
    | DeleteUser (username)->
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
    | Tweet (tweet,username)->
        let tweetID = generateTweetID
        insertTweet tweetID tweet
        updateUserTweet tweetID username
        //TODO: should see regex matching
        

let serverBehaviour serverFunction numOfNodes numOfRequests (inbox: Actor<'T>) = 
    let rec loop=
        actor {
            let! msg = inbox.Receive()
            let newindex = serverFunction msg
            return! loop
        }
    loop 

let serverNode = spawn system "twitterServer" (serverBehaviour serverFunction  numNodes numRequests)
serverNode <! Init
while flag do
    ignore ()