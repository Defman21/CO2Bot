namespace CO2Bot.Cleargrass.Api

open System.Linq
open CO2Bot.Cleargrass.Types

open System.Net.Http.Json
open System.Web
open CO2Bot.Log
open CO2Bot.Config

open System
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open Telegram.Bot.Extensions

module Api =
    let private apiHttpClient =
        new HttpClient(BaseAddress = Uri "https://apis.cleargrass.com/")


    let private devicesConfig = Config.getConfig().Cleargrass.Devices

    let getDevices (token: string) =
        async {
            let uri = UriBuilder("http://localhost/v1/apis/devices")
            let query = HttpUtility.ParseQueryString(uri.Query)
            query["ts"] <- DateTime.UtcNow.Ticks.ToString()
            uri.Query <- query.ToString()

            use httpReq = new HttpRequestMessage(HttpMethod.Get, uri.Uri.PathAndQuery)
            httpReq.Headers.Authorization <- AuthenticationHeaderValue("Bearer", token)
            let! response = apiHttpClient.SendAsync(httpReq) |> Async.AwaitTask

            match response.IsSuccessStatusCode with
            | false ->
                let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                Log.Error("Failed to get devices: {body}", body)
                return None
            | true ->
                let! devices = response.Content.ReadFromJsonAsync<DevicesResponse>() |> Async.AwaitTask
                Log.Debug("Devices: {devices}", devices)
                return Some devices
        }

    let buildMarkdownMessage (devices: DevicesResponse) =
        let { Measurements = locale } = Config.getLocale ()

        let appendTo
            (sb: StringBuilder)
            (cond: float -> bool)
            (emoji: string)
            ({ Name = name
               Measurement = measurement }: AppLocaleEntry)
            (value: ResponseValue option)
            (format: string)
            =
            match value with
            | Some { Value = value } when cond value ->
                let escapedValue = value.ToString(format)

                sb.AppendLine $"""<b>%s{emoji} %s{HtmlText.Escape name}</b>: %s{escapedValue} %s{measurement}"""
                |> ignore

                ()
            | _ -> ()

        let sb = StringBuilder()
        let append = appendTo sb (fun _ -> true)

        let pmCond pm = not (pm = 99999.0)
        let batteryCond battery = not (battery = 100.0)

        let devicePerMac =
            devices.Devices
            |> Seq.fold (fun state value -> Map.add value.Info.MAC value state) (Map<string, Device> [])

        devicesConfig
        |> Seq.filter (fun cfg -> Map.containsKey cfg.Key devicePerMac)
        |> Seq.fold
            (fun
                (state:
                    {| Config: CleargrassDeviceConfig
                       Data: DeviceData |} seq)
                deviceCfg ->
                match (Map.find deviceCfg.Key devicePerMac).Data with
                | None -> state
                | Some data ->
                    state.Append
                        {| Config = deviceCfg.Value
                           Data = data |})
            []
        |> Seq.iter (fun device ->
            let config = device.Config
            let data = device.Data

            let deviceName = $"%s{config.RoomName} (%s{config.OwnerUsername})"

            let CO2Emoji =
                match data.CO2 with
                | None -> ""
                | Some { Value = value } ->
                    match value with
                    | v when v > 1200.0 -> "🔴"
                    | v when v > 800.0 -> "🟡"
                    | _ -> "🟢"

            sb.AppendLine $"<i>%s{HtmlText.Escape deviceName}</i>" |> ignore
            append CO2Emoji locale.CO2 data.CO2 "0"
            sb.Append "<blockquote expandable>" |> ignore
            append "🌡" locale.Temp data.Temperature "0.0"
            append "💧" locale.Humidity data.Humidity "0.0"
            appendTo sb pmCond "🌫" locale.PM25 data.PM25 "0"
            appendTo sb pmCond "🌫" locale.PM10 data.PM10 "0"
            append "🧪" locale.TVOC data.TVOC "0"
            append "🧪" locale.ETVOC data.ETVOC "0"
            appendTo sb batteryCond "🔋" locale.Battery data.Battery "0"
            append "🔊" locale.Noise data.Noise "0"
            sb.AppendLine "</blockquote>" |> ignore)

        sb.ToString()
