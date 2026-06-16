namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

internal sealed class DashboardTranslationRecordImporter
{
    private readonly DashboardRecordExporter dashboardTranslator;

    public DashboardTranslationRecordImporter(KeyedServiceFactory keyedServiceFactory)
    {
        dashboardTranslator = keyedServiceFactory.GetRequired<DashboardRecordExporter>(TranslationServiceKind.Dashboard);
    }

    public void Import(
        IReadOnlyDictionary<string, List<TranslationRecord>> recordsByDataset,
        IOrganizationService service,
        bool forceUpdate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recordsByDataset);
        ArgumentNullException.ThrowIfNull(service);

        var dashboardsTable = BuildTable(recordsByDataset, "Dashboards");
        var dashboardsTabsTable = BuildTable(recordsByDataset, "Dashboards Tabs");
        var dashboardsSectionsTable = BuildTable(recordsByDataset, "Dashboards Sections");
        var dashboardsFieldsTable = BuildTable(recordsByDataset, "Dashboards Fields");

        var dashboards = new List<Entity>();
        var hasDashboardContent = false;

        cancellationToken.ThrowIfCancellationRequested();
        if (dashboardsTable?.Dimension is not null)
        {
            dashboardTranslator.ImportFormName(dashboardsTable, service, forceUpdate);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (dashboardsTabsTable?.Dimension is not null)
        {
            dashboardTranslator.PrepareFormTabs(dashboardsTabsTable, service, dashboards, forceUpdate);
            hasDashboardContent = true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (dashboardsSectionsTable?.Dimension is not null)
        {
            dashboardTranslator.PrepareFormSections(dashboardsSectionsTable, service, dashboards, forceUpdate);
            hasDashboardContent = true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (dashboardsFieldsTable?.Dimension is not null)
        {
            dashboardTranslator.PrepareFormLabels(dashboardsFieldsTable, service, dashboards, forceUpdate);
            hasDashboardContent = true;
        }

        if (hasDashboardContent)
        {
            dashboardTranslator.ImportFormsContent(service, dashboards);
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
