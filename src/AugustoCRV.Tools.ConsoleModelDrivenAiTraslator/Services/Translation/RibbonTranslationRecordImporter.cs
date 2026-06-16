namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class RibbonTranslationRecordImporter
{
    private readonly RibbonRecordExporter ribbonTranslator;

    public RibbonTranslationRecordImporter(KeyedServiceFactory keyedServiceFactory)
    {
        ribbonTranslator = keyedServiceFactory.GetRequired<RibbonRecordExporter>(TranslationServiceKind.Ribbon);
    }

    public void Import(
        IReadOnlyDictionary<string, List<TranslationRecord>> recordsByDataset,
        IOrganizationService service,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recordsByDataset);
        ArgumentNullException.ThrowIfNull(service);

        if (!recordsByDataset.TryGetValue("Ribbon", out var records) || records.Count == 0)
        {
            return;
        }

        var table = BuildTable("Ribbon", records);
        if (table.Dimension is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        ribbonTranslator.Import(table, service);
    }

    private static TranslationTable BuildTable(string datasetName, IReadOnlyList<TranslationRecord> records)
    {
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
