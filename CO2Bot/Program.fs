module CO2Bot.Program

open System
open CO2Bot.Cleargrass.Api
open CO2Bot.Config
open CO2Bot.Services
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

open CO2Bot.Cleargrass.Tokens

open Microsoft.Extensions.Options
open Serilog
open Telegram.Bot


let createAndRunHostBuilder args =
    try
        try
            let host = Host.CreateApplicationBuilder()
            host.Configuration.AddYamlFile("config/config.yaml", optional = false) |> ignore

            host.Services.AddSerilog(fun config -> config.ReadFrom.Configuration(host.Configuration) |> ignore)
            |> ignore

            host.Services.Configure<TelegramConfig>(host.Configuration.GetSection("Telegram"))
            |> ignore

            host.Services.Configure<CleargrassConfig>(host.Configuration.GetSection("Cleargrass"))
            |> ignore

            host.Services.Configure<AppConfig>(host.Configuration.GetSection("App"))
            |> ignore

            host.Services
                .AddHttpClient("TelegramBotClient")
                .AddTypedClient<ITelegramBotClient>(fun client services ->
                    let telegramCfg = services.GetRequiredService<IOptions<TelegramConfig>>().Value
                    let options = TelegramBotClientOptions(telegramCfg.Token)
                    TelegramBotClient(options, client) :> ITelegramBotClient)
            |> ignore

            host.Services
                .AddHttpClient("CleargrassAuth")
                .AddTypedClient<TokensHttpService>(fun client ->
                    client.BaseAddress <- Uri "https://oauth.cleargrass.com/"
                    client.Timeout <- TimeSpan.FromSeconds 10.0
                    TokensHttpService(client))
            |> ignore

            host.Services
                .AddHttpClient("CleargrassAPI")
                .AddTypedClient<ApiHttpService>(fun client services ->
                    client.BaseAddress <- Uri "https://apis.cleargrass.com/"
                    client.Timeout <- TimeSpan.FromSeconds 10.0
                    ApiHttpService(client, services.GetRequiredService()))
            |> ignore

            host.Services.AddSingleton<TokensService>() |> ignore
            host.Services.AddSingleton<ApiService>() |> ignore
            host.Services.AddScoped<UpdateHandler>() |> ignore
            host.Services.AddHostedService<PollingService>() |> ignore
            host.Build().Run()
            0
        with e ->
            Log.Fatal(e, "Host terminated unexpectedly")
            1
    finally
        Log.CloseAndFlush()



[<EntryPoint>]
let main args = createAndRunHostBuilder args
