namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

/// <summary>
/// Executes the GitHub OAuth Device Flow to obtain a user access token.
/// </summary>
internal interface IGitHubDeviceFlowService
{
    /// <summary>
    /// Runs the full device authorization flow: requests device/user codes,
    /// prompts the user, and polls for the access token.
    /// </summary>
    Task<string?> AuthenticateAsync(CancellationToken cancellationToken);
}
