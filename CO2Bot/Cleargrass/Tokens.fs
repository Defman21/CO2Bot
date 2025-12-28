namespace CO2Bot.Cleargrass.Tokens

open System
open System.Buffers.Text
open System.Collections.Generic
open System.IO
open System.Net.Http
open System.Net.Http.Headers
open System.Net.Http.Json
open System.Text
open System.Text.Json
open CO2Bot.Cleargrass.Types
open CO2Bot.Config
open CO2Bot.Log

type private TokenCache = Dictionary<string, string * DateTime>

module Tokens =
    let private oauthHttpClient =
        new HttpClient(BaseAddress = Uri "https://oauth.cleargrass.com/")

    let private appConfig = Config.getConfig().Cleargrass.Apps

    let private tokenCache = TokenCache()

    let private getToken appKey appSecret =
        async {
            let authString =
                $"%s{appKey}:%s{appSecret}"
                |> Encoding.UTF8.GetBytes
                |> Base64Url.EncodeToString

            use authData =
                new FormUrlEncodedContent(dict [ "scope", "device_full_access"; "grant_type", "client_credentials" ])

            use httpReq = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
            httpReq.Headers.Authorization <- AuthenticationHeaderValue("Basic", authString)
            httpReq.Content <- authData

            let! response = oauthHttpClient.SendAsync(httpReq) |> Async.AwaitTask

            match response.IsSuccessStatusCode with
            | true ->
                let! oauthData = response.Content.ReadFromJsonAsync<OAuthResponse>() |> Async.AwaitTask
                return Some oauthData.AccessToken
            | false -> return None
        }

    let readFromFile () =
        use stream = File.Open("./cache/tokens.json", FileMode.OpenOrCreate)

        let tokensJson =
            try
                Some(JsonSerializer.Deserialize<TokenCache>(stream))
            with :? JsonException ->
                Log.Error("Failed to parse tokens.json")
                None

        match tokensJson with
        | None -> ()
        | Some json ->
            for entry in json do
                Log.Debug("Adding {username} to cache...", entry.Key)
                tokenCache.Add(entry.Key, entry.Value)

    let saveToFile () =
        use stream = File.Open("./cache/tokens.json", FileMode.OpenOrCreate)
        stream.Seek(0L, SeekOrigin.Begin) |> ignore
        let tokensJson = JsonSerializer.Serialize(tokenCache)
        stream.Write(Encoding.UTF8.GetBytes(tokensJson))
        stream.Close()

        Log.Information("Saved tokens successfully...")

    let getAccessToken (username: string) =
        async {
            let retrieveToken () =
                let config = appConfig[username]
                Log.Debug("Retrieving tokens for {username}...", username)
                let! token = getToken config.Key config.Secret |> Async.RunSynchronously

                match token with
                | None ->
                    Log.Error("Failed to retrieve token for {username}!", username)
                    None
                | Some token ->
                    Log.Information("Successfully retrieved token for {username}!", username)
                    tokenCache[username] <- token, DateTime.UtcNow.AddHours(1.0)
                    saveToFile ()
                    Some token

            let token =
                match tokenCache.ContainsKey(username) with
                | true ->
                    let token, expireTime = tokenCache[username]
                    Log.Debug("Trying cached token for {username} (expires {at})", username, expireTime)

                    match expireTime < DateTime.UtcNow with
                    | true -> retrieveToken ()
                    | false -> Some token
                | false ->
                    match appConfig.ContainsKey(username) with
                    | false ->
                        Log.Warning("Unable to find app config for {username}", username)
                        None
                    | true -> retrieveToken ()

            return token
        }
