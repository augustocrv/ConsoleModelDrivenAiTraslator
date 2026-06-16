namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class FormTranslationRecordImporter
{
    private readonly FormRecordExporter formTranslator;

    public FormTranslationRecordImporter(KeyedServiceFactory keyedServiceFactory)
    {
        formTranslator = keyedServiceFactory.GetRequired<FormRecordExporter>(TranslationServiceKind.Form);
    }

    public void Import(
        IReadOnlyDictionary<string, List<TranslationRecord>> recordsByDataset,
        IOrganizationService service,
        bool forceUpdate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recordsByDataset);
        ArgumentNullException.ThrowIfNull(service);

        var formsTable = BuildTable(recordsByDataset, "Forms");
        var formsTabsTable = BuildTable(recordsByDataset, "Forms Tabs");
        var formsSectionsTable = BuildTable(recordsByDataset, "Forms Sections");
        var formsFieldsTable = BuildTable(recordsByDataset, "Forms Fields");

        var forms = new List<Entity>();
        var hasFormContent = false;

        cancellationToken.ThrowIfCancellationRequested();
        if (formsTable?.Dimension is not null)
        {
            formTranslator.ImportFormName(formsTable, service, forceUpdate);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (formsTabsTable?.Dimension is not null)
        {
            formTranslator.PrepareFormTabs(formsTabsTable, service, forms, forceUpdate);
            hasFormContent = true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (formsSectionsTable?.Dimension is not null)
        {
            formTranslator.PrepareFormSections(formsSectionsTable, service, forms, forceUpdate);
            hasFormContent = true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (formsFieldsTable?.Dimension is not null)
        {
            formTranslator.PrepareFormLabels(formsFieldsTable, service, forms, forceUpdate);
            hasFormContent = true;
        }

        if (hasFormContent)
        {
            formTranslator.ImportFormsContent(service, forms);
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
