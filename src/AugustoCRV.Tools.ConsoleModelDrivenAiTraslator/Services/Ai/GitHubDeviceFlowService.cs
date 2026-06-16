using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

/// <summary>
/// Implements the GitHub OAuth Device Authorization Grant (RFC 8628) to obtain
/// a user access token for the GitHub Copilot bundled CLI connection.
/// </summary>
internal sealed class GitHubDeviceFlowService : IGitHubDeviceFlowService
{
    private const string ClientId = "Ov23liGiX5iv13O0SLY3";
    private const string DeviceCodeUrl = "https://github.com/login/device/code";
    private const string AccessTokenUrl = "https://github.com/login/oauth/access_token";
    private const string GrantType = "urn:ietf:params:oauth:grant-type:device_code";

    private readonly HttpClient httpClient;

    public GitHubDeviceFlowService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<string?> AuthenticateAsync(CancellationToken cancellationToken)
    {
        // Step 1: Request device and user verification codes.
        var deviceResponse = await RequestDeviceCodeAsync(cancellationToken).ConfigureAwait(false);

        if (deviceResponse is null)
        {
            AnsiConsole.Console.WriteError("Failed to initiate the GitHub device flow.");
            return null;
        }

        // Step 2: Show the user code and open the verification URL.
        AnsiConsole.Console.WriteInfo("GitHub OAuth Device Flow");
        AnsiConsole.Console.MarkupLine("");
        AnsiConsole.Console.MarkupLine($"  [yellow]1.[/] Open [link={deviceResponse.VerificationUri}]{deviceResponse.VerificationUri}[/] in your browser");
        AnsiConsole.Console.MarkupLine($"  [yellow]2.[/] Enter the code: [bold green]{deviceResponse.UserCode}[/]");
        AnsiConsole.Console.MarkupLine("");

        TryOpenBrowser(deviceResponse.VerificationUri);

        // Step 3: Poll for the access token.
        var token = await PollForAccessTokenAsync(
            deviceResponse.DeviceCode,
            deviceResponse.Interval,
            deviceResponse.ExpiresIn,
            cancellationToken).ConfigureAwait(false);

        return token;
    }

    private async Task<DeviceCodeResponse?> RequestDeviceCodeAsync(CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, DeviceCodeUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["scope"] = string.Empty
            })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DeviceCodeResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> PollForAccessTokenAsync(
        string deviceCode,
        int intervalSeconds,
        int expiresInSeconds,
        CancellationToken cancellationToken)
    {
        var pollInterval = Math.Max(intervalSeconds, 5);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);

        return await AnsiConsole.Console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("[blue]Waiting for browser authorization...[/]", async _ =>
            {
                while (DateTimeOffset.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await Task.Delay(TimeSpan.FromSeconds(pollInterval), cancellationToken).ConfigureAwait(false);

                    var request = new HttpRequestMessage(HttpMethod.Post, AccessTokenUrl)
                    {
                        Content = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            ["client_id"] = ClientId,
                            ["device_code"] = deviceCode,
                            ["grant_type"] = GrantType
                        })
                    };
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var tokenResponse = JsonSerializer.Deserialize<AccessTokenResponse>(content);

                    if (tokenResponse is null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
                    {
                        return tokenResponse.AccessToken;
                    }

                    switch (tokenResponse.Error)
                    {
                        case "authorization_pending":
                            // User hasn't authorized yet — keep polling.
                            continue;

                        case "slow_down":
                            // GitHub asks us to back off 5 extra seconds.
                            pollInterval += 5;
                            continue;

                        case "expired_token":
                            AnsiConsole.Console.WriteError("The device code expired. Please try again.");
                            return null;

                        case "access_denied":
                            AnsiConsole.Console.WriteError("Authorization was denied by the user.");
                            return null;

                        default:
                            AnsiConsole.Console.WriteError($"Unexpected error: {tokenResponse.Error} — {tokenResponse.ErrorDescription}");
                            return null;
                    }
                }

                AnsiConsole.Console.WriteError("Authorization timed out. Please try again.");
                return null;
            }).ConfigureAwait(false);
    }

    private static void TryOpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Non-critical — user can manually navigate.
        }
    }

    // ---------- DTOs ----------

    private sealed class DeviceCodeResponse
    {
        [JsonPropertyName("device_code")]
        public string DeviceCode { get; set; } = string.Empty;

        [JsonPropertyName("user_code")]
        public string UserCode { get; set; } = string.Empty;

        [JsonPropertyName("verification_uri")]
        public string VerificationUri { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("interval")]
        public int Interval { get; set; }
    }

    private sealed class AccessTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }
}
