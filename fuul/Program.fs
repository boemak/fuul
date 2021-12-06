// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.IO
open Types

let getRoomProcessor config =
    let roomProcessor = new MailboxProcessor<Message>(fun inbox ->
        let rec loop(data) =
            async {
                match! inbox.Receive() with
                | GoRoom (direction,rc) ->
                        let d = 
                            data.CurrentRoom.Exits
                            |> List.tryFind(fun x -> x.Direction = direction)
                        
                        match d with
                        | Some direction ->
                            let newCurrentRoom = data.Rooms |> List.find(fun x -> x.Id = direction.RoomId)
                            rc.Reply(sprintf "You are in: %s" newCurrentRoom.Description)
                            return! loop({data with CurrentRoom = newCurrentRoom; VisitedRooms = data.CurrentRoom.Id :: data.VisitedRooms})
    
                        | None ->
                            rc.Reply "There is no exit"
                            return! loop(data)

                | Back rc ->
                    match data.VisitedRooms with
                    | h :: t ->
                       data.Rooms
                       |> List.find(fun x -> x.Id = h)
                       |> fun x -> rc.Reply x.Description
                       return! loop({data with VisitedRooms = t; CurrentRoom = data.Rooms |> List.find(fun x -> x.Id = h) }) 
                    | [] ->
                        rc.Reply "You are at the start"
                        return! loop(data)

                | Look rc ->
                     match data.CurrentRoom.LongDescription with
                     | Some d ->
                        d ::
                        [                      
                            match data.CurrentRoom.Items with
                            | Some items ->
                                for item in items do
                                    yield sprintf "Item: %s "item.Name
                            | None ->
                                ()
                        ]
                        |> rc.Reply 
                     | None -> rc.Reply ["Nothing draws your attention"]
                     return! loop(data)

                | Inspect (itemName, rc) ->
                    match data.CurrentRoom.Items with
                    | Some items ->
                        match items |> List.tryFind(fun item -> item.Name.ToLower() = itemName.ToLower()) with
                        | Some item ->
                            rc.Reply item.Description
                            return! loop(data)
                        | None ->
                            sprintf "the item: %s Cannot be found in this room" itemName
                            |> rc.Reply
                            return! loop(data)
                    | None ->
                        rc.Reply "there are no items in this room"
                        return! loop(data)

                | Pickup (itemName, rc) ->
                    match data.CurrentRoom.Items with
                    | Some items ->
                        match items |> List.tryFind(fun item -> item.Name.ToLower() = itemName.ToLower()) with
                        | Some item ->
                            let newItems = data.CurrentRoom.Items.Value |> List.choose(fun x -> if x.Name = itemName then None else Some x)
                            return! loop({data with Inventory = item :: data.Inventory; CurrentRoom = {data.CurrentRoom with Items = if List.isEmpty newItems then None else Some newItems}})
                        | None ->
                            sprintf "There is no item called %s in this room." itemName
                            |> rc.Reply
                            return! loop(data)
                    | None -> 
                        rc.Reply "There are no items in this room"
                        return! loop(data)
        }
        loop({CurrentRoom = config |> List.head; VisitedRooms = []; Rooms = config; Inventory = []}))
    roomProcessor

let getDu compass =
    match compass with
    | "north" -> Ok North
    | "east" -> Ok East
    | "south" -> Ok South
    | "west" -> Ok West
    | _ -> Error "Can't convert to direction"

let commands = ["go DIRECTION"; "back"; "look"; "inspect OBJECTNAME"; "quit"; "help (this command)" ]

let rec getInput(roomProcessor:MailboxProcessor<Message>) =
    printf ":==>"
    let input = Console.ReadLine().Split(" ", StringSplitOptions.RemoveEmptyEntries)
    match input with
    | [||] -> 
        printfn "You have to give me something to work with."
        getInput(roomProcessor)

    | [|"go"; direction|] ->
        match getDu direction with
        | Ok dirDu ->
            let result = roomProcessor.PostAndReply(fun rc -> GoRoom(dirDu, rc))
            printfn "%s" result
            getInput(roomProcessor)
        | Error e ->
            printfn "I don't know that direction"
            getInput(roomProcessor)

    | [|"go"|] ->
        printfn "Go where?"
        getInput(roomProcessor)

    | [|"back"|] ->
            roomProcessor.PostAndReply Back
            |> printfn "%s"           
            getInput(roomProcessor)
    
    | [|"look"|] ->
            roomProcessor.PostAndReply Look
            |> List.iter(fun x -> printfn "%s" x)
            getInput(roomProcessor)
    
    | [|"inspect"; itemName|] ->
            roomProcessor.PostAndReply(fun rc -> Inspect(itemName,rc))
            |> printfn "%s"               
            getInput(roomProcessor)
    
    | [|"inspect"|] ->
            printfn "What do you want me to inspect?"
            getInput(roomProcessor)
    


    | [|"quit"|] ->
        printfn "Goodbye!"
        (roomProcessor :> IDisposable).Dispose()
        Environment.Exit(0)

    | [|"help"|] ->
        printfn "Possible commands are:"
        for command in commands do
            printf " %s " command
        printfn ""
        getInput(roomProcessor)

    | _ ->
        printfn "I don't quite understand this command"
        getInput(roomProcessor)
     


[<EntryPoint>]
let main argv =
    match loadConfig() with
    | Ok cfg ->
        let roomProcessor = getRoomProcessor cfg
        roomProcessor.Start()
        printfn "Welcome to Fuul"
        printfn "You are at %s, type help for a command list" (cfg |> List.head |> fun x -> x.Description)
        getInput(roomProcessor)
        0 // return an integer exit code
    | Error e ->
        printfn "Error readin config file. Reason: %s" e.Message
        1