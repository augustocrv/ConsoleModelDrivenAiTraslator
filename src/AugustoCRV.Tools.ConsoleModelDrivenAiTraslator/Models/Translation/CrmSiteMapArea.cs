
namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation
{
    internal class CrmSiteMapArea
    {
        public CrmSiteMapArea()
        {
            Titles = new Dictionary<int, string>();
            Descriptions = new Dictionary<int, string>();
        }

        public Dictionary<int, string> Descriptions { get; set; }
        public string Id { get; set; } = string.Empty;

        public Guid SiteMapId { get; set; }
        public string SiteMapName { get; set; } = string.Empty;
        public Dictionary<int, string> Titles { get; set; }
    }
}
