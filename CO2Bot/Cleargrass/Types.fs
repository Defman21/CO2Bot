namespace CO2Bot.Cleargrass.Types

open System.Text.Json.Serialization

type OAuthResponse =
    { [<JsonPropertyName("access_token")>]
      AccessToken: string }

type DevicesResponse =
    { [<JsonPropertyName("devices")>]
      Devices: Device list }

and Device =
    { [<JsonPropertyName("info")>]
      Info: DeviceInfo
      [<JsonPropertyName("data")>]
      Data: DeviceData option }

and DeviceInfo =
    { [<JsonPropertyName("mac")>]
      MAC: string }

and DeviceData =
    { [<JsonPropertyName("co2")>]
      CO2: ResponseValue option
      [<JsonPropertyName("temperature")>]
      Temperature: ResponseValue option
      [<JsonPropertyName("humidity")>]
      Humidity: ResponseValue option
      [<JsonPropertyName("pm25")>]
      PM25: ResponseValue option
      [<JsonPropertyName("pm10")>]
      PM10: ResponseValue option
      [<JsonPropertyName("tvoc")>]
      TVOC: ResponseValue option
      [<JsonPropertyName("tvoc_index")>]
      ETVOC: ResponseValue option
      [<JsonPropertyName("battery")>]
      Battery: ResponseValue option
      [<JsonPropertyName("noise")>]
      Noise: ResponseValue option }

and ResponseValue = { Value: float }
