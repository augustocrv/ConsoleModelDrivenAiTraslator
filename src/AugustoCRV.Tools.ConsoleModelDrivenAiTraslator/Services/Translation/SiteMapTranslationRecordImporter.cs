namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class SiteMapTranslationRecordImporter
{
    private readonly SiteMapRecordExporter siteMapTranslator;

    public SiteMapTranslationRecordImporter(KeyedServiceFactory keyedServiceFactory)
    {
        siteMapTranslator = keyedServiceFactory.GetRequired<SiteMapRecordExporter>(TranslationServiceKind.SiteMap);
    }

    public void Import(
        IReadOnlyDictionary<string, List<TranslationRecord>> recordsByDataset,
        IOrganizationService service,
        bool forceUpdate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recordsByDataset);
        ArgumentNullException.ThrowIfNull(service);

        var siteMapAreasTable = BuildTable(recordsByDataset, "SiteMap Areas");
        var siteMapGroupsTable = BuildTable(recordsByDataset, "SiteMap Groups");
        var siteMapSubAreasTable = BuildTable(recordsByDataset, "SiteMap SubAreas");

        var hasSiteMapContent = false;

        cancellationToken.ThrowIfCancellationRequested();
        if (siteMapAreasTable?.Dimension is not null)
        {
            siteMapTranslator.PrepareAreas(siteMapAreasTable, service, forceUpdate);
            hasSiteMapContent = true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (siteMapGroupsTable?.Dimension is not null)
        {
            siteMapTranslator.PrepareGroups(siteMapGroupsTable, service, forceUpdate);
            hasSiteMapContent = true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (siteMapSubAreasTable?.Dimension is not null)
        {
            siteMapTranslator.PrepareSubAreas(siteMapSubAreasTable, service, forceUpdate);
            hasSiteMapContent = true;
        }

        if (hasSiteMapContent)
        {
            siteMapTranslator.Import(service);
        }
    }

    private static TranslationTable? BuildTable(
        IReadOnlyDictionary<string, List<TranslationRecord>> recordsByDataset,
        string datasetName)
    {
        if (!recordsByDataset.TryGetValue(datasetName, out var records) || records.Count == 0)
        {
            return null;
        }

        var table = new TranslationTable(datasetName);
        var headers = new List<string>();

        foreach (var record in records)
        {
            var values = TranslationRecordImportHelper.DeserializeMetadata(record.MetadataJson);

            if (!string.IsNullOrWhiteSpace(record.SourceLcid))
            {
                values[record.SourceLcid] = record.SourceText;
            }

            if (!string.IsNullOrWhiteSpace(record.TargetLcid))
            {
                values[record.TargetLcid] = record.TargetText;
                values[$"Checksum_{record.TargetLcid}"] = record.Checksum;
            }

            foreach (var key in values.Keys)
            {
                if (!headers.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    headers.Add(key);
                }
            }

            var row = record.RowNumber <= 0 ? 2 : record.RowNumber;
            foreach (var pair in values)
            {
                var column = headers.FindIndex(h => h.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
                if (column < 0)
                {
                    continue;
                }

                table.GetCell(row, column + 1).Value = pair.Value;
            }
        }

        for (var column = 0; column < headers.Count; column++)
        {
            table.GetCell(1, column + 1).Value = headers[column];
        }

        return table;
    }

}
