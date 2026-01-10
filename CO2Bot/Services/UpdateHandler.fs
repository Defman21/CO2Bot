namespace CO2Bot.Services

open System
open System.Threading
open CO2Bot.Cleargrass.Api
open CO2Bot.Cleargrass.Tokens
open CO2Bot.Services.Internal
open Microsoft.Extensions.Logging
open Telegram.Bot
open Telegram.Bot.Polling
open Telegram.Bot.Types


type UpdateHandler
    (
        botClient: ITelegramBotClient,
        logger: ILogger<UpdateHandler>,
        cleargrassApi: ApiService,
        cleargrassTokens: TokensService
    ) =
    member val botMe: User option = None with get, set

    interface IUpdateHandler with
        member this.HandleUpdateAsync(_, update: Update, cts: CancellationToken) =
            match this.botMe with
            | Some bot ->
                UpdateHandlerFuncs.handleUpdateAsync botClient logger cleargrassApi cleargrassTokens cts bot update
                |> Async.StartAsTask
                :> Tasks.Task
            | None -> failwith "botMe is not set!"

        override this.HandleErrorAsync(_, exc: Exception, _: HandleErrorSource, cts: CancellationToken) =
            UpdateHandlerFuncs.handleErrorAsync botClient logger cts cleargrassApi cleargrassTokens exc
            |> Async.StartAsTask
            :> Tasks.Task
