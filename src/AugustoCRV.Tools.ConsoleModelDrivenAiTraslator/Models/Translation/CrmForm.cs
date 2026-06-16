
namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation
{
    /// <summary>Class description.</summary>
    public class CrmForm
    {
        public Dictionary<int, string> Descriptions { get; set; } = new();
        public string Entity { get; set; } = string.Empty;
        public Guid FormUniqueId { get; set; }
        public Guid Id { get; set; }
        public Dictionary<int, string> Names { get; set; } = new();
        public string Type { get; set; } = string.Empty;
    }
}

