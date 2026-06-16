
namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation
{
    /// <summary>Class description.</summary>
    public class CrmFormTab
    {
        public string Entity { get; set; } = string.Empty;
        public string Form { get; set; } = string.Empty;
        public Guid FormId { get; set; }
        public Guid FormUniqueId { get; set; }
        public Guid Id { get; set; }
        public Dictionary<int, string> Names { get; set; } = new();
    }
}

