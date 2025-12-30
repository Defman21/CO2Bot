namespace CO2Bot.Bot

open System.Collections.Immutable
open CO2Bot.Cleargrass.Api
open CO2Bot.Log
open CO2Bot.Cleargrass.Tokens
open CO2Bot.Config

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Telegram.Bot
open Telegram.Bot.Requests
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums


module Bot =
    let { Cleargrass = private cleargrassCfg
          Telegram = private telegramCfg
          App = private appCfg } =
        Config.getConfig ()

    let { Placeholder = textPlaceholder } = Config.getLocale ()

    let CTS = new CancellationTokenSource()

    let instance = TelegramBotClient(telegramCfg.Token, cancellationToken = CTS.Token)

    let private antispam = Dictionary<int64, DateTime>()

    let onUpdate (update: Update) : Task =
        task { Log.Debug("Received an update ID {id}", update.Id) }

    let handleCommand (_me: User) (message: Message) =
        task {
            let nextCallAt = antispam.GetValueOrDefault(message.Chat.Id, DateTime.UtcNow)
            let now = DateTime.UtcNow

            if now < nextCallAt then
                Log.Debug(
                    "Antispam for {chatId}! Current time = {ct}, next call at = {nca}",
                    message.Chat.Id,
                    now,
                    nextCallAt
                )

                return ()
            else
                antispam[message.Chat.Id] <- DateTime.UtcNow.AddSeconds(telegramCfg.AntispamDuration)

                let! notifyMessage =
                    instance.SendMessage(message.Chat, text = textPlaceholder, replyParameters = message)

                do! instance.SendChatAction(message.Chat, ChatAction.Typing)

                let! tokens =
                    cleargrassCfg.Apps.Keys.ToImmutableList()
                    |> Seq.map Tokens.getAccessToken
                    |> Async.Parallel


                let! devicesData =
                    tokens
                    |> Seq.filter (fun token ->
                        match token with
                        | None ->
                            Log.Warning("Failed to retrieve token!")
                            false
                        | Some _ -> true)
                    |> Seq.map _.Value
                    |> Seq.map Api.getDevices
                    |> Async.Parallel

                do! instance.DeleteMessage(chatId = notifyMessage.Chat.Id, messageId = notifyMessage.Id)

                let! messages =
                    devicesData
                    |> Seq.fold
                        (fun state value ->
                            match value with
                            | None -> state
                            | Some value -> (Api.buildMarkdownMessage value) :: state)
                        []
                    |> Seq.map (fun text ->
                        instance.SendRequest(
                            SendMessageRequest(
                                ChatId = message.Chat,
                                Text = text,
                                ParseMode = ParseMode.Html,
                                ReplyParameters = message
                            )
                        ))
                    |> Task.WhenAll

                Log.Debug("Sent {num} messages!", messages.Length)
                antispam[message.Chat.Id] <- DateTime.UtcNow.AddSeconds(telegramCfg.AntispamDuration)
                return ()
        }

    let onCommand (me: User) (message: Message) =
        task {
            let space =
                match message.Text.IndexOf ' ' with
                | idx when idx < 0 -> message.Text.Length
                | idx -> idx

            let rawCommand = message.Text[..space].ToLower()

            let command, botUsername =
                match rawCommand.LastIndexOf '@' with
                | at when at > 0 -> (rawCommand[1 .. at - 1], rawCommand[(at + 1) ..])
                | _ -> (rawCommand[1..], me.Username)

            if not (botUsername.Equals(me.Username, StringComparison.OrdinalIgnoreCase)) then
                return ()

            if command = telegramCfg.Command.Name then
                do! handleCommand me message

            return ()
        }

    let onMessage (me: User) (message: Message) (_: UpdateType) : Task =
        task {
            match telegramCfg.IsChatAllowed message.Chat.Id with
            | true ->
                Log.Information("{chat}: {message}", message.Chat, message.Text)

                if message.Text.StartsWith('/') then
                    do! onCommand me message
            | false -> Log.Warning("Chat {chat} with ID={id} is not allowed!", message.Chat, message.Chat.Id)
        }
