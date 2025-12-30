namespace CO2Bot.Config

open System.Collections.Generic

open System.IO
open Serilog.Events
open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions

[<CLIMutable>]
type Config =
    { Telegram: TelegramConfig
      Cleargrass: CleargrassConfig
      App: AppConfig }

and AppConfig() =
    member val LogLevel: LogEventLevel = LogEventLevel.Information with get, set

    member val Locale: AppLocaleConfig =
        { Placeholder = "Probing devices..."
          Measurements = AppLocaleMeasurementConfig() } with get, set

and [<CLIMutable>] AppLocaleConfig =
    { Placeholder: string
      Measurements: AppLocaleMeasurementConfig }

and AppLocaleMeasurementConfig() =
    [<YamlMember(Alias = "co2")>]
    member val CO2: AppLocaleEntry = { Name = "CO2"; Measurement = "ppm" } with get, set

    member val Temp: AppLocaleEntry = { Name = "Temp"; Measurement = "C" } with get, set
    member val Humidity: AppLocaleEntry = { Name = "Humidity"; Measurement = "%" } with get, set

    [<YamlMember(Alias = "pm25")>]
    member val PM25: AppLocaleEntry =
        { Name = "PM2.5"
          Measurement = "ug/m3" } with get, set

    [<YamlMember(Alias = "pm10")>]
    member val PM10: AppLocaleEntry = { Name = "PM10"; Measurement = "ug/m3" } with get, set

    [<YamlMember(Alias = "tvoc")>]
    member val TVOC: AppLocaleEntry = { Name = "TVOC"; Measurement = "ppb" } with get, set

    [<YamlMember(Alias = "etvoc")>]
    member val ETVOC: AppLocaleEntry =
        { Name = "ETVOC"
          Measurement = "VOC Index" } with get, set

    member val Battery: AppLocaleEntry = { Name = "Battery"; Measurement = "%" } with get, set
    member val Noise: AppLocaleEntry = { Name = "Noise"; Measurement = "dB" } with get, set

// TODO: omitting measurement in YAML does not set the default value from AppLocaleMeasurementConfig defaults :/
and [<CLIMutable>] AppLocaleEntry = { Name: string; Measurement: string }

and TelegramConfig() =
    member val Token: string = "" with get, set
    member val AntispamDuration: float = 30.0 with get, set
    member val AllowedChats: List<int64> = List() with get, set

    member val Command: TelegramCommandConfig =
        { Name = "temp"
          Description = "Get thermostat data" } with get, set

    member private this.AllowAnyChat = this.AllowedChats[0] = 0

    member this.IsChatAllowed(chatId: int64) =
        this.AllowAnyChat || this.AllowedChats.Contains chatId

and [<CLIMutable>] TelegramCommandConfig = { Name: string; Description: string }

and [<CLIMutable>] CleargrassConfig =
    { Apps: Dictionary<string, CleargrassAppConfig>
      Devices: Dictionary<string, CleargrassDeviceConfig> }

and [<CLIMutable>] CleargrassAppConfig = { Key: string; Secret: string }

and [<CLIMutable>] CleargrassDeviceConfig =
    { RoomName: string
      OwnerUsername: string }

module Config =
    let mutable private config: Config option = None

    let private readConfig () =
        let deserializer =
            DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build()

        use stream = File.Open("./config/config.yaml", FileMode.Open)
        use streamReader = new StreamReader(stream)
        config <- deserializer.Deserialize<Config>(streamReader) |> Some
        ()

    let rec private _getConfig failOnNone : Config =
        match config with
        | Some config -> config
        | None ->
            match failOnNone with
            | true -> failwithf "Could not load configuration"
            | false ->
                readConfig ()
                _getConfig true

    let getConfig () = _getConfig false

    let getLocale () = getConfig().App.Locale
