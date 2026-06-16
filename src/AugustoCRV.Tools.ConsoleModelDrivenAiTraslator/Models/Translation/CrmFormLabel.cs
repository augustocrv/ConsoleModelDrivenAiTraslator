
namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation
{
    /// <summary>Class description.</summary>
    public class CrmFormLabel
    {
        public string Attribute { get; set; } = string.Empty;
        public Dictionary<int, string> Descriptions { get; set; } = new();
        public string Entity { get; set; } = string.Empty;
        public string Form { get; set; } = string.Empty;
        public Guid FormId { get; set; }
        public Guid FormUniqueId { get; set; }
        public Guid Id { get; set; }
        public Dictionary<int, string> Names { get; set; } = new();
        public string Section { get; set; } = string.Empty;
        public string Tab { get; set; } = string.Empty;
        public string AttributeDisplayName { get; internal set; } = string.Empty;
    }
}

