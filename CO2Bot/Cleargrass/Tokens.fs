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
open System.Threading
open CO2Bot.Cleargrass.Types
open CO2Bot.Config
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options

type private TokenCache = Dictionary<string, string * DateTime>

type TokensHttpService(httpClient: HttpClient) =
    member val tokenCache = TokenCache() with get, set

    member _.getToken appKey appSecret (ct: CancellationToken) =
        task {
            let authString =
                $"%s{appKey}:%s{appSecret}"
                |> Encoding.UTF8.GetBytes
                |> Base64Url.EncodeToString

            use authData =
                new FormUrlEncodedContent(dict [ "scope", "device_full_access"; "grant_type", "client_credentials" ])

            use httpReq = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
            httpReq.Headers.Authorization <- AuthenticationHeaderValue("Basic", authString)
            httpReq.Content <- authData

            let! response = httpClient.SendAsync(httpReq, cancellationToken = ct)

            match response.IsSuccessStatusCode with
            | true ->
                let! oauthData = response.Content.ReadFromJsonAsync<OAuthResponse>(ct)
                return Some oauthData.AccessToken
            | false -> return None
        }


type TokensService
    (httpService: TokensHttpService, logger: ILogger<TokensService>, cleargrassCfg: IOptions<CleargrassConfig>) =
    let cleargrassCfg = cleargrassCfg.Value

    let readFromFile () =
        use stream =
            File.Open("./cache/tokens.json", FileMode.OpenOrCreate, FileAccess.Read)

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

    let retrieveToken (ct: CancellationToken) (username: string) =
        task {
            let cleargrassAppCfg = cleargrassCfg.Apps[username]
            logger.LogDebug("Retrieving tokens for {username}...", username)

            match! httpService.getToken cleargrassAppCfg.Key cleargrassAppCfg.Secret ct with
            | None ->
                logger.LogError("Failed to retrieve token for {username}!", username)
                return None
            | Some token ->
                logger.LogInformation("Successfully retrieved token for {username}!", username)
                httpService.tokenCache[username] <- token, DateTime.UtcNow.AddHours(1.0)
                return Some token
        }


    do readFromFile ()

    member _.saveToFile() =
        use stream = File.Open("./cache/tokens.json", FileMode.Create, FileAccess.Write)
        let tokensJson = JsonSerializer.Serialize(httpService.tokenCache)
        stream.Write(Encoding.UTF8.GetBytes(tokensJson))

        logger.LogInformation("Saved tokens successfully...")

    member _.getAccessToken (ct: CancellationToken) (username: string) =
        let retrieveToken = retrieveToken ct

        task {
            match httpService.tokenCache.ContainsKey(username) with
            | true ->
                let token, expireTime = httpService.tokenCache[username]
                logger.LogDebug("Trying cached token for {username} (expires {at})", username, expireTime)

                match expireTime < DateTime.UtcNow with
                | true -> return! retrieveToken username
                | false -> return Some token
            | false ->
                match cleargrassCfg.Apps.ContainsKey(username) with
                | false ->
                    logger.LogWarning("Unable to find app config for {username}", username)
                    return None
                | true -> return! retrieveToken username
        }
