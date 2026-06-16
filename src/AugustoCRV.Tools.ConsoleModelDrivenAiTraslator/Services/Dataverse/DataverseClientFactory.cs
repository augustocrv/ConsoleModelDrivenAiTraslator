using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Dataverse;

internal sealed class DataverseClientFactory : IDataverseClientFactory
{
    private readonly ILogger<DataverseClientFactory> logger;
    private readonly ITokenCacheService tokenCacheService;
    private readonly IDataverseConnectionSelectionService dataverseConnectionSelectionService;

    public DataverseClientFactory(
        ILogger<DataverseClientFactory> logger,
        ITokenCacheService tokenCacheService,
        IDataverseConnectionSelectionService dataverseConnectionSelectionService)
    {
        this.logger = logger;
        this.tokenCacheService = tokenCacheService;
        this.dataverseConnectionSelectionService = dataverseConnectionSelectionService;
    }

    public async Task<IOrganizationService> CreateAsync(CancellationToken cancellationToken)
    {
        var selectedDataverseConnection = await dataverseConnectionSelectionService.GetSelectedConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (selectedDataverseConnection is null || string.IsNullOrWhiteSpace(selectedDataverseConnection.Url))
        {
            throw new InvalidOperationException("No Dataverse connection selected. Run 'conn dataverse select' first.");
        }

        var cleanUrl = selectedDataverseConnection.Url.TrimEnd('/', ' ');

        const int maxRetries = 2;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // First attempt tries cached token, then falls back to OAuth Auto prompt.
                if (attempt == 1)
                {
                    var cachedClient = await TryCreateFromCachedTokenAsync(cleanUrl, cancellationToken).ConfigureAwait(false);
                    if (cachedClient is not null)
                    {
                        return cachedClient;
                    }
                }

                if (attempt > 1)
                {
                    AnsiConsole.Console.WriteInfo("Retrying authentication with forced login prompt...");
                }

                var loginPrompt = attempt == 1 ? "Auto" : "Always";
                var connectionString = BuildConnectionString(cleanUrl, loginPrompt);

                ServiceClient newClient;
                try
                {
                    newClient = new ServiceClient(connectionString);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize ServiceClient");
                    if (await TryHandleAuthenticationRetryAsync(attempt, maxRetries, cleanUrl, cancellationToken).ConfigureAwait(false) && IsAuthenticationError(ex))
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Failed to initialize Dataverse connection. Verify the URL is valid. Error: {ex.Message}", ex);
                }

                if (!newClient.IsReady)
                {
                    var errorMessage = newClient.LastError ?? "Unknown error connecting to Dataverse";
                    throw new InvalidOperationException($"Failed to connect to Dataverse. Error: {errorMessage}");
                }

                try
                {
                    ValidateAuthentication(newClient);
                }
                catch (Exception ex) when (IsAuthenticationError(ex) && attempt < maxRetries)
                {
                    await TryHandleAuthenticationRetryAsync(attempt, maxRetries, cleanUrl, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                catch (Exception ex) when (IsAuthenticationError(ex))
                {
                    throw new InvalidOperationException(
                        $"Authentication failed after {maxRetries} attempts. Please ensure you complete the OAuth login in the browser window.", ex);
                }

                logger.LogInformation("Dataverse connection established with new authentication");
                AnsiConsole.Console.WriteSuccess("Connected to Dataverse successfully");

                // Cache the new token
                if (newClient.ConnectedOrgUriActual is not null && 
                    !string.IsNullOrWhiteSpace(newClient.CurrentAccessToken))
                {
                    await tokenCacheService.SaveTokenAsync(
                        cleanUrl,
                        newClient.ConnectedOrgUriActual,
                        newClient.CurrentAccessToken,
                        cancellationToken).ConfigureAwait(false);
                    logger.LogInformation("Access token cached for future use");
                    AnsiConsole.Console.WriteInfo("Authentication token saved to cache for future use");
                }

                return newClient;
            }
            catch (InvalidOperationException)
            {
                if (attempt >= maxRetries)
                {
                    throw;
                }
                // Continue to retry
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create Dataverse client");

                if (IsAuthenticationError(ex))
                {
                    if (await TryHandleAuthenticationRetryAsync(attempt, maxRetries, cleanUrl, cancellationToken).ConfigureAwait(false))
                    {
                        continue;
                    }
                }
                
                throw new InvalidOperationException($"Unable to connect to Dataverse: {ex.Message}", ex);
            }
        }

        throw new InvalidOperationException("Failed to establish connection after multiple attempts");
    }

    private static void ValidateAuthentication(IOrganizationService service)
    {
        _ = (WhoAmIResponse)service.Execute(new WhoAmIRequest());
    }

    private static string BuildConnectionString(string dataverseUrl, string loginPrompt)
    {
        return $"AuthType=OAuth;Url={dataverseUrl};RedirectUri=http://localhost;LoginPrompt={loginPrompt}";
    }

    private static bool IsAuthenticationError(Exception ex)
    {
        if (ex.GetType().Name == "MessageSecurityException")
        {
            return true;
        }

        if (ex.Message.Contains("not authorized", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("anonymous", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("bearer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ex.InnerException is not null && IsAuthenticationError(ex.InnerException);
    }

    private async Task<bool> TryHandleAuthenticationRetryAsync(
        int attempt,
        int maxRetries,
        string? url,
        CancellationToken cancellationToken)
    {
        if (attempt >= maxRetries)
        {
            return false;
        }

        AnsiConsole.Console.WriteWarning($"Authentication failed on attempt {attempt}");
        AnsiConsole.Console.WriteInfo("Clearing cached token and retrying...");
        await TryClearCachedTokenAsync(url, cancellationToken).ConfigureAwait(false);
        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<IOrganizationService?> TryCreateFromCachedTokenAsync(string url, CancellationToken cancellationToken)
    {
        var cachedToken = await tokenCacheService.TryGetTokenAsync(url, cancellationToken).ConfigureAwait(false);
        if (cachedToken is null)
        {
            AnsiConsole.Console.WriteInfo("No cached token found, requesting OAuth authentication...");
            return null;
        }

        logger.LogInformation("Using cached token for Dataverse connection");
        AnsiConsole.Console.WriteInfo("Using cached authentication token...");

        try
        {
            var client = new ServiceClient(
                cachedToken.ServiceUri,
                _ => Task.FromResult(cachedToken.AccessToken ?? string.Empty));

            if (!client.IsReady)
            {
                logger.LogWarning("Cached token produced a non-ready ServiceClient");
                await TryClearCachedTokenAsync(url, cancellationToken).ConfigureAwait(false);
                return null;
            }

            ValidateAuthentication(client);
            logger.LogInformation("Dataverse connection established using cached token");
            AnsiConsole.Console.WriteSuccess("Connected to Dataverse using cached token (no authentication required)");
            return client;
        }
        catch (Exception ex) when (IsAuthenticationError(ex))
        {
            logger.LogWarning(ex, "Cached token failed authentication validation");
            AnsiConsole.Console.WriteWarning("Cached authentication is no longer valid, requesting login...");
            await TryClearCachedTokenAsync(url, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create ServiceClient with cached token");
            await TryClearCachedTokenAsync(url, cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private async Task TryClearCachedTokenAsync(string? url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            await tokenCacheService.ClearTokenAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Ignore cache clear errors during recovery attempts.
        }
    }

}
