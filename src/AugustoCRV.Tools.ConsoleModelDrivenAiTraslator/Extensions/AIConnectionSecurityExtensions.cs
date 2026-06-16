namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Extensions;

/// <summary>Class description.</summary>

public static class AiConnectionSecurityExtensions
{
    public static AiConnection EncryptApiKey(this AiConnection connection, IApiKeyProtectorService apiKeyProtector)
    {
        return new AiConnection
        {
            Name = connection.Name,
            Type = connection.Type,
            DeploymentEndpoint = connection.DeploymentEndpoint,
            ApiKey = apiKeyProtector.Encrypt(connection.ApiKey),
            Model = connection.Model,
            Description = connection.Description,
            LastValidatedUtc = connection.LastValidatedUtc
        };
    }

    public static AiConnection DecryptApiKey(this AiConnection connection, IApiKeyProtectorService apiKeyProtector)
    {
        return new AiConnection
        {
            Name = connection.Name,
            Type = connection.Type,
            DeploymentEndpoint = connection.DeploymentEndpoint,
            ApiKey = apiKeyProtector.Decrypt(connection.ApiKey),
            Model = connection.Model,
            Description = connection.Description,
            LastValidatedUtc = connection.LastValidatedUtc
        };
    }
}

