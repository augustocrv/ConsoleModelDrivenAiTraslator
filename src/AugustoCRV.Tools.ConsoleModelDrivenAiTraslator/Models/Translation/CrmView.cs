
namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation
{
    /// <summary>Class description.</summary>
    public class CrmView
    {
        public Dictionary<int, string> Descriptions { get; set; } = new();
        public string Entity { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public Dictionary<int, string> Names { get; set; } = new();
        public int Type { get; set; }
    }
}

