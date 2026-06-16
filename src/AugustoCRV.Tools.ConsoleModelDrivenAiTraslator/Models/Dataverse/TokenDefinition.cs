using System.Text.Json.Serialization;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Dataverse;

/// <summary>
/// Represents a cached OAuth token for a Dataverse connection.
/// </summary>
internal sealed record TokenDefinition
{
    public TokenDefinition(Uri serviceUri, string accessToken)
    {
        ArgumentNullException.ThrowIfNull(serviceUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        ServiceUri = serviceUri;
        AccessToken = accessToken;
    }

    public TokenDefinition()
    {
    }

    public Uri? ServiceUri { get; init; }

    public string? AccessToken { get; init; }

    [JsonIgnore]
    public bool IsValid => ServiceUri is not null && !string.IsNullOrWhiteSpace(AccessToken);
}
