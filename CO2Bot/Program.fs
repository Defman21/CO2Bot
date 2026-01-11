module CO2Bot.Program

open System
open CO2Bot.Cleargrass.Api
open CO2Bot.Config
open CO2Bot.Services
open CO2Bot.Services.ReceiverService
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

open CO2Bot.Cleargrass.Tokens

open Microsoft.Extensions.Options
open Serilog
open Telegram.Bot


type RcvService = ReceiverService<UpdateHandler>

let createAndRunHostBuilder args =
    try
        try
            Host
                .CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(fun context builder ->
                    builder.AddYamlFile("config/config.yaml", optional = false) |> ignore)
                .ConfigureServices(fun context services ->
                    services.AddSerilog(fun sp loggerConfiguration ->
                        loggerConfiguration.ReadFrom.Configuration(context.Configuration) |> ignore)
                    |> ignore

                    services.Configure<TelegramConfig>(context.Configuration.GetSection("Telegram"))
                    |> ignore

                    services.Configure<CleargrassConfig>(context.Configuration.GetSection("Cleargrass"))
                    |> ignore

                    services.Configure<AppConfig>(context.Configuration.GetSection("App")) |> ignore

                    services
                        .AddHttpClient("telegram_bot_client")
                        .AddTypedClient<ITelegramBotClient>(fun httpClient sp ->
                            let telegramCfg = sp.GetRequiredService<IOptions<TelegramConfig>>().Value
                            let options = TelegramBotClientOptions(telegramCfg.Token)
                            TelegramBotClient(options, httpClient) :> ITelegramBotClient)
                    |> ignore

                    services
                        .AddHttpClient("cleargrass_auth")
                        .AddTypedClient<TokensHttpService>(fun httpClient ->
                            httpClient.BaseAddress <- Uri "https://oauth.cleargrass.com/"
                            httpClient.Timeout <- TimeSpan.FromSeconds 10.0
                            TokensHttpService(httpClient))
                    |> ignore

                    services
                        .AddHttpClient("cleargrass_api")
                        .AddTypedClient<ApiHttpService>(fun httpClient sp ->
                            httpClient.BaseAddress <- Uri "https://apis.cleargrass.com/"
                            httpClient.Timeout <- TimeSpan.FromSeconds 10.0
                            ApiHttpService(httpClient, sp.GetRequiredService()))
                    |> ignore

                    services.AddSingleton<TokensService>() |> ignore
                    services.AddSingleton<ApiService>() |> ignore
                    services.AddScoped<UpdateHandler>() |> ignore
                    services.AddScoped<RcvService>() |> ignore

                    services.AddHostedService<PollingService<RcvService>>() |> ignore)
                .Build()
                .Run()

            0
        with e ->
            Log.Fatal(e, "Host terminated unexpectedly")
            1
    finally
        Log.CloseAndFlush()



[<EntryPoint>]
let main args = createAndRunHostBuilder args
