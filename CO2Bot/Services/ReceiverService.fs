module CO2Bot.Services.ReceiverService

open System
open System.Threading
open CO2Bot.Config
open CO2Bot.Services.Internal
open Microsoft.Extensions.Logging
open Telegram.Bot
open Telegram.Bot.Polling
open Telegram.Bot.Types

type ReceiverService<'T when 'T :> IUpdateHandler>
    (botClient: ITelegramBotClient, updateHandler: UpdateHandler, logger: ILogger<'T>) =
    let { Telegram = telegramCfg } = Config.getConfig ()

    interface IReceiverService with
        member this.Receive(ct: CancellationToken) =
            task {
                logger.LogInformation("ReceiveAsync called")

                let options = ReceiverOptions(AllowedUpdates = [||], DropPendingUpdates = true)

                try
                    let! me = botClient.GetMe(ct)
                    do! botClient.DeleteWebhook()
                    do! botClient.DropPendingUpdates()

                    let username =
                        match me.Username with
                        | null -> "Unknown bot"
                        | username -> username

                    logger.LogInformation("Started receiving updates for {username}", username)

                    do!
                        botClient.SetMyCommands(
                            [ BotCommand(
                                  command = $"/%s{telegramCfg.Command.Name}",
                                  description = telegramCfg.Command.Description
                              ) ]
                        )

                    updateHandler.botMe <- Some me

                    return!
                        botClient.ReceiveAsync(
                            updateHandler = updateHandler,
                            receiverOptions = options,
                            cancellationToken = ct
                        )
                with
                | :? TaskCanceledException -> logger.LogInformation("Receive cancelled")
                | e -> logger.LogError(e, "Failed to receive updates")
            }
