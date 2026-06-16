namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models;

/// <summary>
/// Represents a Dataverse connection configuration.
/// Authentication is performed via OAuth with MFA support.
/// </summary>
/// <summary>Class description.</summary>
public class DataverseConnection
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

