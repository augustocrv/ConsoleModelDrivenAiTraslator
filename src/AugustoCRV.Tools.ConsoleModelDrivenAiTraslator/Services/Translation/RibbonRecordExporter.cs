namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class RibbonRecordExporter : BaseRecordExporter
{
    private const string DatasetName = "Ribbon";

    /// <summary>Exports ribbon diff label titles as TranslationRecord objects.</summary>
    public IReadOnlyList<TranslationRecord> Export(
        int sourceLcid,
        IReadOnlyList<int> allLcids,
        IOrganizationService service,
        ExportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(allLcids);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(settings);

        var sourceLcidStr = sourceLcid.ToString(CultureInfo.InvariantCulture);
        var records = new List<TranslationRecord>();
        var rowNumber = 2;

        var qe = new QueryByAttribute("ribbondiff") { ColumnSet = new ColumnSet(true) };
        qe.Attributes.AddRange("difftype");
        qe.Values.AddRange(3);
        qe.AddOrder("entity", OrderType.Ascending);

        var ribbonEntities = service.RetrieveMultiple(qe);

        foreach (var record in ribbonEntities.Entities)
        {
            if (!settings.EnableManaged && record.GetAttributeValue<bool>("ismanaged"))
            {
                continue;
            }

            var ribbonDiffId = record.Id.ToString("B");
            var entityLogicalName = record.GetAttributeValue<string>("entity") ?? string.Empty;
            var diffId = record.GetAttributeValue<string>("diffid") ?? string.Empty;

            var xml = new XmlDocument();
            xml.LoadXml(record.GetAttributeValue<string>("rdx") ?? string.Empty);

            var sourceText = xml.SelectSingleNode(
                string.Format("LocLabel/Titles/Title[@languagecode='{0}']", sourceLcid))
                ?.Attributes?["description"]?.Value ?? string.Empty;

            var metadataJson = SerializeMetadata(ribbonDiffId, entityLogicalName, diffId);
            var currentRowNumber = rowNumber++;

            foreach (var targetLcid in allLcids)
            {
                var targetLcidStr = targetLcid.ToString(CultureInfo.InvariantCulture);
                var targetText = xml.SelectSingleNode(
                    string.Format("LocLabel/Titles/Title[@languagecode='{0}']", targetLcid))
                    ?.Attributes?["description"]?.Value ?? string.Empty;

                records.Add(new TranslationRecord
                {
                    Dataset = DatasetName,
                    RecordKey = $"{DatasetName}|{ribbonDiffId}|{targetLcidStr}",
                    RowNumber = currentRowNumber,
                    SourceLcid = sourceLcidStr,
                    TargetLcid = targetLcidStr,
                    SourceText = sourceText,
                    TargetText = targetText,
                    Checksum = string.IsNullOrEmpty(targetText) ? string.Empty : CalculateChecksum(targetText),
                    MetadataJson = metadataJson
                });
            }
        }

        return records;
    }

    /// <summary>Imports ribbon diff label titles from a TranslationTable back to Dataverse.</summary>
    public void Import(TranslationTable sheet, IOrganizationService service)
    {
        var rowsCount = sheet.Dimension?.Rows ?? 0;
        var cellsCount = sheet.Dimension?.Columns ?? 0;

        for (var rowI = 1; rowI < rowsCount; rowI++)
        {
            // col 2 (0-based) = "Ribbon Component" = diffid used as LocLabel Id attribute
            var diffIdValue = sheet.GetCell(rowI + 1, 3).Value?.ToString() ?? string.Empty;
            var xml = new StringBuilder(string.Format("<LocLabel Id=\"{0}\"><Titles>", diffIdValue));

            var columnIndex = 3;
            while (columnIndex < cellsCount)
            {
                var lcidValue = sheet.GetCell(1, columnIndex + 1).Value?.ToString();
                if (!int.TryParse(lcidValue, out var lcid))
                {
                    columnIndex++;
                    continue;
                }

                xml.Append(string.Format("<Title description=\"{0}\" languagecode=\"{1}\"/>",
                    sheet.GetCell(rowI + 1, columnIndex + 1).Value,
                    lcid));

                columnIndex++;
            }

            xml.Append("</Titles></LocLabel>");

            var ribbonIdValue = sheet.GetCell(rowI + 1, 1).Value?.ToString();
            if (!Guid.TryParse(ribbonIdValue, out var ribbonId)) continue;

            var ribbonDiff = new Entity("ribbondiff") { Id = ribbonId };
            ribbonDiff["rdx"] = xml.ToString();

            service.Update(ribbonDiff);
        }
    }

    // Ribbon: col 0=Ribbon Diff Id, 1=Entity Logical Name, 2=Ribbon Component, 3+=LCIDs
    private static string SerializeMetadata(string ribbonDiffId, string entityLogicalName, string ribbonComponent)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Ribbon Diff Id"] = ribbonDiffId,
            ["Entity Logical Name"] = entityLogicalName,
            ["Ribbon Component"] = ribbonComponent
        });
}
