using Microsoft.Extensions.Options;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

/// <summary>
/// Default Azure OpenAI client used for translation requests.
/// </summary>
internal sealed class AzureOpenAiTranslationService : IAiService
{
    private readonly HttpClient httpClient;

    public AzureOpenAiTranslationService(HttpClient httpClient, IOptions<TranslatorCliOptions> options)
    {
        this.httpClient = httpClient;
        var timeoutMinutes = Math.Max(1, options.Value.AzureOpenAiTimeoutMinutes);
        this.httpClient.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
    }

    public async Task<string> TranslateAsync(string prompt, AiConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.Type != AiConnectionType.AzureOpenAi)
        {
            throw new ArgumentException("Invalid connection type for Azure OpenAI translation service.", nameof(connection));
        }

        if (string.IsNullOrWhiteSpace(connection.DeploymentEndpoint) || string.IsNullOrWhiteSpace(connection.ApiKey))
        {
            throw new InvalidOperationException("Azure OpenAI connection requires a deployment endpoint and an API key.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, connection.DeploymentEndpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.3
                }),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);

        var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Azure OpenAI request failed: {response.StatusCode} - {error}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(responseContent);

        if (document.RootElement.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.String)
            {
                return content.GetString()?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}







