using GitHub.Copilot.SDK;
using Microsoft.Extensions.Options;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

/// <summary>
/// Translation client backed by the GitHub Copilot SDK.
/// </summary>
internal sealed class GitHubCopilotTranslationService : IAiService
{
    private readonly TranslatorCliOptions options;

    public GitHubCopilotTranslationService(IOptions<TranslatorCliOptions> options)
    {
        this.options = options.Value;
    }

    public async Task<string> TranslateAsync(string prompt, AiConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.Type != AiConnectionType.GitHubCopilot)
        {
            throw new ArgumentException("Invalid connection type for GitHub Copilot translation service.", nameof(connection));
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt is required.", nameof(prompt));
        }

        await using var client = CreateClient(connection.ApiKey);
        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = string.IsNullOrWhiteSpace(connection.Model)
                ? options.GitHubCopilotModel
                : connection.Model,
            OnPermissionRequest = PermissionHandler.ApproveAll
        }).ConfigureAwait(false);

        var response = await session
            .SendAndWaitAsync(new MessageOptions { Prompt = prompt })
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        return response?.Data?.Content?.Trim() ?? string.Empty;
    }

    private static CopilotClient CreateClient(string? githubToken)
    {
        if (string.IsNullOrWhiteSpace(githubToken))
        {
            return new CopilotClient(new CopilotClientOptions());
        }

        return new CopilotClient(new CopilotClientOptions
        {
            GitHubToken = githubToken
        });
    }
}
