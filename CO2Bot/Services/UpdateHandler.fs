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
        member this.HandleUpdateAsync(_, update: Update, ct: CancellationToken) =
            match this.botMe with
            | Some bot ->
                UpdateHandlerFuncs.handleUpdateAsync botClient logger cleargrassApi cleargrassTokens ct bot update
            | None -> failwith "botMe is not set!"

        override this.HandleErrorAsync(_, exc: Exception, _: HandleErrorSource, ct: CancellationToken) =
            UpdateHandlerFuncs.handleErrorAsync botClient logger ct cleargrassApi cleargrassTokens exc
