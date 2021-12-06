module Types
open System
open System.IO
open Newtonsoft.Json


type Item =
    {
        Name :string
        Description : string
    }

type Compass =
       | North
       | East
       | South
       | West

type Direction =
    {
        Direction : Compass
        RoomId : Guid
    }

type Room =
    {
        Id : Guid
        Description: string
        LongDescription : string Option
        Exits : Direction list
        Items : Item list option
    }

type Message =
    | GoRoom of Compass * AsyncReplyChannel<string>
    | Back of AsyncReplyChannel<string>
    | Look of AsyncReplyChannel<string list>
    | Inspect of ItemName : string * AsyncReplyChannel<string>
    | Pickup of ItemName : string * AsyncReplyChannel<string>

type LoopData = 
    {
        CurrentRoom : Room
        VisitedRooms : Guid list
        Rooms : Room list
        Inventory : Item list
    }

[<Literal>]
let configName = "fuul.conf"

let configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configName)

let loadConfig() =
    if not <| File.Exists configPath then
        
        let book = {Name = "Necronomicon"; Description = "It reminds you of the old movie Army of Darkness."}
        
        let library = {Id = Guid.NewGuid(); Description = "The library"; LongDescription =  Some "It is an old library, full of interesting books."; Exits = [];Items = Some [book]}
        let smokeroom = {Id = Guid.NewGuid(); Description = "the smokeroom"; LongDescription =  Some "The stench of smoke lingers...."; Exits = []; Items = None}
        let hall = {Id = Guid.NewGuid(); Description = "Main hallway."; LongDescription =  Some "It sure is a hallway."; Exits = [{Direction = West; RoomId = library.Id}; {Direction = East; RoomId = smokeroom.Id}]; Items = None}
        let start = {Id = Guid.NewGuid(); Description = "Main starting point."; LongDescription =  Some "A starting point, nothing special"; Exits = [ {Direction = North; RoomId = hall.Id} ]; Items = None}
        
        let rooms = [start; hall; library; smokeroom]
        use writer =  new StreamWriter(new FileStream(configPath, FileMode.CreateNew))
        JsonConvert.SerializeObject(rooms) |> writer.Write
        writer.Flush()
        Ok rooms
    else
        try
            use reader = new StreamReader(new FileStream(configPath, FileMode.Open))
            JsonConvert.DeserializeObject<Room list>(reader.ReadToEnd())
            |> Ok
        with
        | e -> Error e
        




