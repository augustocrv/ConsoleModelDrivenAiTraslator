namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models
{
    /// <summary>Class description.</summary>
    public sealed class AiConnection
    {
        public string Name { get; set; } = string.Empty;

        public AiConnectionType Type { get; set; } = AiConnectionType.AzureOpenAi;

        public string DeploymentEndpoint { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTimeOffset? LastValidatedUtc { get; set; }
    }
}

