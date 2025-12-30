module CO2Bot.Program

open System
open CO2Bot.Cleargrass.Tokens
open CO2Bot.Log
open CO2Bot.Bot
open CO2Bot.Config

open Telegram.Bot
open Telegram.Bot.Types


[<EntryPoint>]
let main _ =
    Log.Information("Starting bot...")
    Tokens.readFromFile ()

    let { Telegram = telegramCfg } = Config.getConfig ()

    let onTermination _ =
        Bot.CTS.Cancel()
        Tokens.saveToFile ()

    Console.CancelKeyPress.Add onTermination
    AppDomain.CurrentDomain.ProcessExit.Add onTermination

    async {
        do! Async.SwitchToThreadPool()

        let! me = Bot.instance.GetMe() |> Async.AwaitTask

        do!
            Bot.instance.SetMyCommands(
                [ BotCommand(command = $"/%s{telegramCfg.Command.Name}", description = telegramCfg.Command.Description) ]
            )
            |> Async.AwaitTask

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
