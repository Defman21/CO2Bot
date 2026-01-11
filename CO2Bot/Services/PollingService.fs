namespace CO2Bot.Services

open System
open System.Threading
open CO2Bot.Cleargrass.Tokens
open CO2Bot.Services.Internal
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type PollingService<'T when 'T :> IReceiverService>(sp: IServiceProvider, logger: ILogger<PollingService<'T>>) =
    inherit BackgroundService()

    member this.DoWork(ct: CancellationToken) =
        task {
            let getReceiverService _ =
                use scope = sp.CreateScope()
                let service: 'T = scope.ServiceProvider.GetRequiredService<'T>()
                service

            let cancellationNotRequested _ = not ct.IsCancellationRequested

            try
                return
                    Seq.initInfinite getReceiverService
                    |> Seq.takeWhile cancellationNotRequested
                    |> Seq.iter (fun r -> r.Receive ct |> Async.AwaitTask |> Async.RunSynchronously)
            with e ->
                logger.LogError(e, "Polling failed with exception")
        }

    override this.ExecuteAsync(ct: CancellationToken) =
        logger.LogInformation("Started polling service")
        this.DoWork ct

    override this.StopAsync(ct: CancellationToken) =
        let tokens = sp.GetRequiredService<TokensService>()
        tokens.saveToFile ()
        base.StopAsync(ct)
