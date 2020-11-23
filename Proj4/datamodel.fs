namespace Datamodel


module TwitterDatamodel =
    type TwitterTableValues = 
        | UsersTableVal of int List * string List
        | MentionsTableVal of string List
        | HashTagsTableVal of string List
        | TweetsTableVal of string


    type ServerMessages = 
        | CreateUser
        | GetUser
        | AddFollower

