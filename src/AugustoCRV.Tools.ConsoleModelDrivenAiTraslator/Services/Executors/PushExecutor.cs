using Microsoft.Extensions.Options;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Csv;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

internal sealed class PushExecutor : IPushExecutor
{
    private readonly IDataverseClientFactory dataverseClientFactory;
    private readonly ITranslationWorkbookStorage workbookStorage;
    private readonly EntityTranslationRecordImporter entityTranslationRecordImporter;
    private readonly AttributeTranslationRecordImporter attributeTranslationRecordImporter;
    private readonly GlobalOptionSetTranslationRecordImporter globalOptionSetTranslationRecordImporter;
    private readonly OptionSetTranslationRecordImporter optionSetTranslationRecordImporter;
    private readonly ViewTranslationRecordImporter viewTranslationRecordImporter;
    private readonly RelationshipTranslationRecordImporter relationshipTranslationRecordImporter;
    private readonly RelationshipNnTranslationRecordImporter relationshipNnTranslationRecordImporter;
    private readonly BooleanTranslationRecordImporter booleanTranslationRecordImporter;
    private readonly VisualizationTranslationRecordImporter visualizationTranslationRecordImporter;
    private readonly FormTranslationRecordImporter formTranslationRecordImporter;
    private readonly DashboardTranslationRecordImporter dashboardTranslationRecordImporter;
    private readonly SiteMapTranslationRecordImporter siteMapTranslationRecordImporter;
    private readonly RibbonTranslationRecordImporter ribbonTranslationRecordImporter;
    private readonly TranslatorCliOptions options;

    public PushExecutor(
        IDataverseClientFactory dataverseClientFactory,
        ITranslationWorkbookStorage workbookStorage,
        EntityTranslationRecordImporter entityTranslationRecordImporter,
        AttributeTranslationRecordImporter attributeTranslationRecordImporter,
        GlobalOptionSetTranslationRecordImporter globalOptionSetTranslationRecordImporter,
        OptionSetTranslationRecordImporter optionSetTranslationRecordImporter,
        ViewTranslationRecordImporter viewTranslationRecordImporter,
        RelationshipTranslationRecordImporter relationshipTranslationRecordImporter,
        RelationshipNnTranslationRecordImporter relationshipNnTranslationRecordImporter,
        BooleanTranslationRecordImporter booleanTranslationRecordImporter,
        VisualizationTranslationRecordImporter visualizationTranslationRecordImporter,
        FormTranslationRecordImporter formTranslationRecordImporter,
        DashboardTranslationRecordImporter dashboardTranslationRecordImporter,
        SiteMapTranslationRecordImporter siteMapTranslationRecordImporter,
        RibbonTranslationRecordImporter ribbonTranslationRecordImporter,
        IOptions<TranslatorCliOptions> options)
    {
        this.dataverseClientFactory = dataverseClientFactory;
        this.workbookStorage = workbookStorage;
        this.entityTranslationRecordImporter = entityTranslationRecordImporter;
        this.attributeTranslationRecordImporter = attributeTranslationRecordImporter;
        this.globalOptionSetTranslationRecordImporter = globalOptionSetTranslationRecordImporter;
        this.optionSetTranslationRecordImporter = optionSetTranslationRecordImporter;
        this.viewTranslationRecordImporter = viewTranslationRecordImporter;
        this.relationshipTranslationRecordImporter = relationshipTranslationRecordImporter;
        this.relationshipNnTranslationRecordImporter = relationshipNnTranslationRecordImporter;
        this.booleanTranslationRecordImporter = booleanTranslationRecordImporter;
        this.visualizationTranslationRecordImporter = visualizationTranslationRecordImporter;
        this.formTranslationRecordImporter = formTranslationRecordImporter;
        this.dashboardTranslationRecordImporter = dashboardTranslationRecordImporter;
        this.siteMapTranslationRecordImporter = siteMapTranslationRecordImporter;
        this.ribbonTranslationRecordImporter = ribbonTranslationRecordImporter;
        this.options = options.Value;
    }

    public async Task<int> ExecuteAsync(
        string workbookPath,
        string? sourceLcid,
        string? targetLcid,
        int? importBatchSize,
        bool force,
        CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (string.IsNullOrWhiteSpace(workbookPath))
        {
            AnsiConsole.Console.WriteError("Workbook path is required.");
            return 1;
        }

        if (!File.Exists(workbookPath) && !Directory.Exists(workbookPath))
        {
            AnsiConsole.Console.WriteError($"Workbook file '{workbookPath}' does not exist.");
            return 1;
        }

        var workbookData = workbookStorage.Load(workbookPath);

        var effectiveSourceLcid = sourceLcid ?? workbookData.SourceLcid;
        var effectiveTargetLcid = targetLcid ?? workbookData.TargetLcid;

        if (string.IsNullOrEmpty(effectiveSourceLcid))
        {
            AnsiConsole.Console.WriteError("Source LCID is required. Provide it via '--source-language' or ensure the workbook file contains it.");
            return 1;
        }

        if (string.IsNullOrEmpty(effectiveTargetLcid))
        {
            AnsiConsole.Console.WriteError("Target LCID is required. Provide it via '--target-language' or ensure the workbook file contains it.");
            return 1;
        }

        try
        {
            BaseRecordExporter.BulkCount = importBatchSize ?? options.DefaultImportBatchSize;
            var service = await dataverseClientFactory.CreateAsync(cancellationToken).ConfigureAwait(false);

            // Filter each dataset to only the records for the target LCID being pushed.
            var records = workbookData.Datasets.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
                    .Where(r => string.Equals(r.TargetLcid, effectiveTargetLcid, StringComparison.OrdinalIgnoreCase))
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

            var entities = new List<EntityMetadata>();

            if (records.TryGetValue("Entities", out var entityRecords) && entityRecords.Count > 0)
            {
                entityTranslationRecordImporter.Import(entityRecords, entities, service, force);
            }

            if (records.TryGetValue("Attributes", out var attributeRecords) && attributeRecords.Count > 0)
            {
                attributeTranslationRecordImporter.Import(attributeRecords, entities, service, force);
            }

            if (records.TryGetValue("OptionSets", out var optionSetRecords) && optionSetRecords.Count > 0)
            {
                optionSetTranslationRecordImporter.Import(optionSetRecords, entities, service, force);
            }

            if (records.TryGetValue("Global OptionSets", out var globalOptionSetRecords) && globalOptionSetRecords.Count > 0)
            {
                globalOptionSetTranslationRecordImporter.Import(globalOptionSetRecords, service, force);
            }

            if (records.TryGetValue("Views", out var viewRecords) && viewRecords.Count > 0)
            {
                viewTranslationRecordImporter.Import(viewRecords, service, force);
            }

            if (records.TryGetValue("Relationships", out var relationshipRecords) && relationshipRecords.Count > 0)
            {
                relationshipTranslationRecordImporter.Import(relationshipRecords, entities, service, force);
            }

            if (records.TryGetValue("RelationshipsNN", out var relationshipNnRecords) && relationshipNnRecords.Count > 0)
            {
                relationshipNnTranslationRecordImporter.Import(relationshipNnRecords, entities, service, force);
            }

            if (records.TryGetValue("Booleans", out var booleanRecords) && booleanRecords.Count > 0)
            {
                booleanTranslationRecordImporter.Import(booleanRecords, entities, service, force);
            }

            if (records.TryGetValue("Charts", out var chartRecords) && chartRecords.Count > 0)
            {
                visualizationTranslationRecordImporter.Import(chartRecords, service, force);
            }

            formTranslationRecordImporter.Import(records, service, force, cancellationToken);
            dashboardTranslationRecordImporter.Import(records, service, force, cancellationToken);
            siteMapTranslationRecordImporter.Import(records, service, force, cancellationToken);
            ribbonTranslationRecordImporter.Import(records, service, cancellationToken);

            service.Execute(new PublishAllXmlRequest());

            AnsiConsole.Console.WriteSuccess("Push completed.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.Console.WriteWarning("Push canceled.");
            return 130;
        }
        catch (Exception ex)
        {
            AnsiConsole.Console.WriteError($"Push failed: {ex.Message}");
            return 1;
        }
    }

}
