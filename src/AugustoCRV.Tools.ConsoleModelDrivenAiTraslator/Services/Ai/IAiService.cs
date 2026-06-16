namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

/// <summary>
/// Sends generic prompts to the configured AI provider and returns text responses.
/// </summary>
internal interface IAiService
{
    /// <summary>
    /// Sends a translation prompt using the specified AI connection.
    /// </summary>
    Task<string> TranslateAsync(string prompt, AiConnection connection, CancellationToken cancellationToken);
}
