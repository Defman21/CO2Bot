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
open Microsoft.Extensions.Logging

type private TokenCache = Dictionary<string, string * DateTime>

type TokensHttpService(httpClient: HttpClient) =
    member val tokenCache = TokenCache() with get, set

    member _.getToken appKey appSecret =
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

            let! response = httpClient.SendAsync(httpReq) |> Async.AwaitTask

            match response.IsSuccessStatusCode with
            | true ->
                let! oauthData = response.Content.ReadFromJsonAsync<OAuthResponse>() |> Async.AwaitTask
                return Some oauthData.AccessToken
            | false -> return None
        }


type TokensService(httpService: TokensHttpService, logger: ILogger<TokensService>) =
    let appConfig = Config.getConfig().Cleargrass.Apps

    let readFromFile () =
        use stream = File.Open("./cache/tokens.json", FileMode.OpenOrCreate)

        let tokensJson =
            try
                Some(JsonSerializer.Deserialize<TokenCache>(stream))
            with :? JsonException as ex ->
                logger.LogError(ex, "Failed to parse tokens.json")
                None

        match tokensJson with
        | None -> ()
        | Some json ->
            for entry in json do
                logger.LogDebug("Adding {username} to cache...", entry.Key)
                httpService.tokenCache.Add(entry.Key, entry.Value)

    do readFromFile ()

    member _.saveToFile () =
        use stream = File.Open("./cache/tokens.json", FileMode.OpenOrCreate)
        stream.Seek(0L, SeekOrigin.Begin) |> ignore
        let tokensJson = JsonSerializer.Serialize(httpService.tokenCache)
        stream.Write(Encoding.UTF8.GetBytes(tokensJson))
        stream.Close()

        logger.LogInformation("Saved tokens successfully...")

    member _.getAccessToken(username: string) =
        async {
            let retrieveToken () =
                let config = appConfig[username]
                logger.LogDebug("Retrieving tokens for {username}...", username)
                let! token = httpService.getToken config.Key config.Secret |> Async.RunSynchronously

                match token with
                | None ->
                    logger.LogError("Failed to retrieve token for {username}!", username)
                    None
                | Some token ->
                    logger.LogInformation("Successfully retrieved token for {username}!", username)
                    httpService.tokenCache[username] <- token, DateTime.UtcNow.AddHours(1.0)
                    Some token

            let token =
                match httpService.tokenCache.ContainsKey(username) with
                | true ->
                    let token, expireTime = httpService.tokenCache[username]
                    logger.LogDebug("Trying cached token for {username} (expires {at})", username, expireTime)

                    match expireTime < DateTime.UtcNow with
                    | true -> retrieveToken ()
                    | false -> Some token
                | false ->
                    match appConfig.ContainsKey(username) with
                    | false ->
                        logger.LogWarning("Unable to find app config for {username}", username)
                        None
                    | true -> retrieveToken ()

            return token
        }
