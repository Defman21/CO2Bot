namespace CO2Bot.Services

open System
open System.Threading
open CO2Bot.Cleargrass.Tokens
open CO2Bot.Config
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Telegram.Bot
open Telegram.Bot.Polling
open Telegram.Bot.Types

type PollingService
    (
        botClient: ITelegramBotClient,
        updateHandler: UpdateHandler,
        telegramCfg: IOptions<TelegramConfig>,
        sp: IServiceProvider,
        logger: ILogger<PollingService>
    ) =
    inherit BackgroundService()

    member this.DoWork(ct: CancellationToken) =
        task {
            let telegramCfg = telegramCfg.Value
            let options = ReceiverOptions(AllowedUpdates = [||], DropPendingUpdates = true)

            try
                let! me = botClient.GetMe(ct)
                do! botClient.DeleteWebhook(cancellationToken = ct)
                do! botClient.DropPendingUpdates(cancellationToken = ct)

                let username =
                    match me.Username with
                    | null -> "Unknown bot"
                    | username -> username

                do!
                    botClient.SetMyCommands(
                        [ BotCommand(
                              command = $"/%s{telegramCfg.Command.Name}",
                              description = telegramCfg.Command.Description
                          ) ],
                        cancellationToken = ct
                    )

                updateHandler.botMe <- Some me

                botClient.StartReceiving(
                    updateHandler = updateHandler,
                    receiverOptions = options,
                    cancellationToken = ct
                )

                logger.LogInformation("Started receiving updates for {username}", username)
            with
            | :? OperationCanceledException -> logger.LogInformation("Receive cancelled")
            | e -> logger.LogError(e, "Failed to receive updates")
        }

    override this.ExecuteAsync(ct: CancellationToken) =
        logger.LogInformation("Started polling service, ServerGC = {ServerGC}", System.Runtime.GCSettings.IsServerGC)
        this.DoWork ct

    override this.StopAsync(ct: CancellationToken) =
        let tokens = sp.GetRequiredService<TokensService>()
        tokens.saveToFile ()
        base.StopAsync(ct)
