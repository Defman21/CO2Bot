module CO2Bot.Program

open System
open CO2Bot.Cleargrass.Tokens
open CO2Bot.Log
open CO2Bot.Bot
open CO2Bot.Config

open Telegram.Bot


[<EntryPoint>]
let main _ =
    Log.Information("Starting bot...")
    Config.readConfig ()
    Tokens.readFromFile ()

    let onTermination _ =
        Bot.CTS.Cancel()
        Tokens.saveToFile ()

    Console.CancelKeyPress.Add onTermination
    AppDomain.CurrentDomain.ProcessExit.Add onTermination

    async {
        do! Async.SwitchToThreadPool()

        let! me = Bot.instance.GetMe() |> Async.AwaitTask

        Log.Debug("I am {bot}", me)

        do! Bot.instance.DeleteWebhook() |> Async.AwaitTask
        do! Bot.instance.DropPendingUpdates() |> Async.AwaitTask

        Bot.instance.add_OnMessage (Bot.onMessage me)
        Bot.instance.add_OnUpdate Bot.onUpdate

        while true do
            do! Async.Sleep 1000
    }
    |> Async.RunSynchronously

    0
