namespace CO2Bot.Services.Internal

open System
open System.Collections.Generic
open System.Collections.Immutable
open System.Threading
open System.Threading.Tasks
open CO2Bot.Cleargrass.Api
open CO2Bot.Cleargrass.Tokens
open CO2Bot.Config
open Microsoft.Extensions.Logging
open Telegram.Bot
open Telegram.Bot.Requests
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums


module UpdateHandlerFuncs =
    let private antispam = Dictionary<int64, DateTime>()

    let handleErrorAsync _ (logger: ILogger) _ _ _ _ _ _ (err: Exception) =
        task { logger.LogError(err, "Update handler error") }

    let private unknownUpdateHandlerAsync
        (botClient: ITelegramBotClient)
        (logger: ILogger)
        (ct: CancellationToken)
        _
        _
        _
        _
        _
        (update: Update)
        =
        task { logger.LogDebug("Unknown update type: {updateType}", update.Type) }

    let handleCommand
        (botClient: ITelegramBotClient)
        (logger: ILogger)
        (ct: CancellationToken)
        (telegramCfg: TelegramConfig)
        (appCfg: AppConfig)
        (cleargrassCfg: CleargrassConfig)
        (cleargrassApi: ApiService)
        (cleargrassTokens: TokensService)
        (message: Message)
        =
        task {
            let nextCallAt = antispam.GetValueOrDefault(message.Chat.Id, DateTime.UtcNow)
            let now = DateTime.UtcNow

            match now < nextCallAt with
            | true ->
                logger.LogDebug(
                    "Antispam for {chatId}! Current time = {ct}, next call at = {nca}",
                    message.Chat.Id,
                    now,
                    nextCallAt
                )

                ()
            | false ->
                antispam[message.Chat.Id] <- DateTime.UtcNow.AddSeconds(telegramCfg.AntispamDuration)

                let! notifyMessage =
                    botClient.SendMessage(message.Chat, text = appCfg.Locale.Placeholder, replyParameters = message)

                do! botClient.SendChatAction(message.Chat, ChatAction.Typing)

                let! tokens =
                    cleargrassCfg.Apps.Keys.ToImmutableList()
                    |> Seq.map cleargrassTokens.getAccessToken
                    |> Async.Parallel
                    |> Async.StartAsTask

                let! devicesData =
                    tokens
                    |> Seq.filter (fun token ->
                        match token with
                        | None ->
                            logger.LogWarning("Failed to retrieve token!")
                            false
                        | Some _ -> true)
                    |> Seq.map _.Value
                    |> Seq.map cleargrassApi.getDevices
                    |> Async.Parallel
                    |> Async.StartAsTask

                do! botClient.DeleteMessage(chatId = notifyMessage.Chat.Id, messageId = notifyMessage.Id)

                let! messages =
                    devicesData
                    |> Seq.fold
                        (fun state value ->
                            match value with
                            | None -> state
                            | Some value -> (cleargrassApi.buildMarkdownMessage value) :: state)
                        []
                    |> Seq.map (fun text ->
                        botClient.SendRequest(
                            SendMessageRequest(
                                ChatId = message.Chat,
                                Text = text,
                                ParseMode = ParseMode.Html,
                                ReplyParameters = message
                            )
                        ))
                    |> Task.WhenAll

                logger.LogDebug("Sent {num} messages!", messages.Length)
                antispam[message.Chat.Id] <- DateTime.UtcNow.AddSeconds(telegramCfg.AntispamDuration)
                ()
        }

    let private onCommand
        (botClient: ITelegramBotClient)
        (logger: ILogger)
        (ct: CancellationToken)
        (telegramCfg: TelegramConfig)
        (appCfg: AppConfig)
        (cleargrassCfg: CleargrassConfig)
        (cleargrassApi: ApiService)
        (cleargrassTokens: TokensService)
        (bot: User)
        (message: Message)
        =
        let space =
            match message.Text.IndexOf ' ' with
            | idx when idx < 0 -> message.Text.Length
            | idx -> idx

        let rawCommand = message.Text[..space].ToLower()

        let command, botUsername =
            match rawCommand.LastIndexOf '@' with
            | at when at > 0 -> (rawCommand[1 .. at - 1], rawCommand[(at + 1) ..])
            | _ -> (rawCommand[1..], bot.Username)

        match botUsername.Equals(bot.Username, StringComparison.OrdinalIgnoreCase) with
        | true ->
            match command = telegramCfg.Command.Name with
            | true ->
                handleCommand
                    botClient
                    logger
                    ct
                    telegramCfg
                    appCfg
                    cleargrassCfg
                    cleargrassApi
                    cleargrassTokens
                    message
            | false -> Task.FromResult()
        | false -> Task.FromResult()

    let private botOnMessageReceived
        (botClient: ITelegramBotClient)
        (logger: ILogger)
        (ct: CancellationToken)
        (telegramCfg: TelegramConfig)
        (appCfg: AppConfig)
        (cleargrassCfg: CleargrassConfig)
        (cleargrassApi: ApiService)
        (cleargrassTokens: TokensService)
        (bot: User)
        (message: Message)
        =
        task {
            match telegramCfg.IsChatAllowed message.Chat.Id with
            | true ->
                logger.LogInformation $"{message.Chat}: {message.Text}"

                match message.Text.StartsWith '/' with
                | true ->
                    do!
                        onCommand
                            botClient
                            logger
                            ct
                            telegramCfg
                            appCfg
                            cleargrassCfg
                            cleargrassApi
                            cleargrassTokens
                            bot
                            message
                | false -> ()
            | false -> logger.LogWarning $"Chat {message.Chat} with ID={message.Chat.Id} is not allowed!"
        }

    let handleUpdateAsync
        (botClient: ITelegramBotClient)
        (logger: ILogger)
        (ct: CancellationToken)
        (telegramCfg: TelegramConfig)
        (appCfg: AppConfig)
        (cleargrassCfg: CleargrassConfig)
        (cleargrassApi: ApiService)
        (cleargrassTokens: TokensService)
        (bot: User)
        (update: Update)
        =
        task {
            let handleUpdate f =
                f botClient logger ct telegramCfg appCfg cleargrassCfg cleargrassApi cleargrassTokens

            try
                let fn =
                    match update.Type with
                    | UpdateType.Message -> handleUpdate botOnMessageReceived bot update.Message
                    | _ -> handleUpdate unknownUpdateHandlerAsync update

                return! fn
            with ex ->
                do! handleUpdate handleErrorAsync ex
        }
