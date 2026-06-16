using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Csv;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

internal sealed class ExportOriginalExecutor : IExportOriginalExecutor
{
    private readonly IDataverseClientFactory dataverseClientFactory;
    private readonly ITranslationWorkbookStorage workbookStorage;
    private readonly EntityRecordExporter entityTranslator;
    private readonly AttributeRecordExporter attributeTranslator;
    private readonly RelationshipRecordExporter relationshipTranslator;
    private readonly RelationshipNnRecordExporter relationshipNnTranslator;
    private readonly GlobalOptionSetRecordExporter globalOptionSetRecordExporter;
    private readonly OptionSetRecordExporter optionSetTranslator;
    private readonly BooleanRecordExporter booleanTranslator;
    private readonly ViewRecordExporter viewTranslator;
    private readonly VisualizationRecordExporter visualizationTranslator;
    private readonly FormRecordExporter formTranslator;
    private readonly SiteMapRecordExporter siteMapTranslator;
    private readonly DashboardRecordExporter dashboardTranslator;
    private readonly RibbonRecordExporter ribbonTranslator;

    public ExportOriginalExecutor(
        IDataverseClientFactory dataverseClientFactory,
        KeyedServiceFactory keyedServiceFactory,
        ITranslationWorkbookStorage workbookStorage)
    {
        this.dataverseClientFactory = dataverseClientFactory;
        this.workbookStorage = workbookStorage;
        entityTranslator = keyedServiceFactory.GetRequired<EntityRecordExporter>(TranslationServiceKind.Entity);
        attributeTranslator = keyedServiceFactory.GetRequired<AttributeRecordExporter>(TranslationServiceKind.Attribute);
        relationshipTranslator = keyedServiceFactory.GetRequired<RelationshipRecordExporter>(TranslationServiceKind.Relationship);
        relationshipNnTranslator = keyedServiceFactory.GetRequired<RelationshipNnRecordExporter>(TranslationServiceKind.RelationshipNn);
        globalOptionSetRecordExporter = keyedServiceFactory.GetRequired<GlobalOptionSetRecordExporter>(TranslationServiceKind.GlobalOptionSet);
        optionSetTranslator = keyedServiceFactory.GetRequired<OptionSetRecordExporter>(TranslationServiceKind.OptionSet);
        booleanTranslator = keyedServiceFactory.GetRequired<BooleanRecordExporter>(TranslationServiceKind.Boolean);
        viewTranslator = keyedServiceFactory.GetRequired<ViewRecordExporter>(TranslationServiceKind.View);
        visualizationTranslator = keyedServiceFactory.GetRequired<VisualizationRecordExporter>(TranslationServiceKind.Visualization);
        formTranslator = keyedServiceFactory.GetRequired<FormRecordExporter>(TranslationServiceKind.Form);
        siteMapTranslator = keyedServiceFactory.GetRequired<SiteMapRecordExporter>(TranslationServiceKind.SiteMap);
        dashboardTranslator = keyedServiceFactory.GetRequired<DashboardRecordExporter>(TranslationServiceKind.Dashboard);
        ribbonTranslator = keyedServiceFactory.GetRequired<RibbonRecordExporter>(TranslationServiceKind.Ribbon);
    }

    public async Task<int> ExecuteAsync(
        string solutionName,
        string sourceLcid,
        string? sourceCsvFile,
        string? exportFolder,
        bool enableManaged,
        CancellationToken cancellationToken)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            AnsiConsole.Console.WriteInfo("Starting Dataverse metadata export to CSV workbook...");

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var outputPath = GetFilePath(sourceCsvFile, exportFolder, solutionName, timestamp, "_source");
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var service = await dataverseClientFactory.CreateAsync(cancellationToken).ConfigureAwait(false);

            var solutionId = service.GetSolutionId(solutionName);
            var exportSettings = ConfigureExportSettings(outputPath, solutionId, enableManaged);
            var workbookData = ExportOriginalWorkbook(exportSettings, service, sourceLcid, cancellationToken);

            workbookStorage.Save(workbookData, outputPath);
            AnsiConsole.Console.WriteSuccess($"Source workbook CSV generated: {outputPath}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.Console.WriteWarning("Export canceled.");
            return 130;
        }
        catch (Exception ex)
        {
            AnsiConsole.Console.WriteError($"Export failed: {ex.Message}");
            return 1;
        }
    }

    public async Task<TranslationWorkbookData> ExportToMemoryAsync(
        string solutionName,
        string sourceLcid,
        bool enableManaged,
        CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;
        AnsiConsole.Console.WriteInfo("Exporting Dataverse metadata...");

        var service = await dataverseClientFactory.CreateAsync(cancellationToken).ConfigureAwait(false);

        var solutionId = service.GetSolutionId(solutionName);
        var exportSettings = ConfigureExportSettings(string.Empty, solutionId, enableManaged);
        
        return ExportOriginalWorkbook(exportSettings, service, sourceLcid, cancellationToken);
    }

    private TranslationWorkbookData ExportOriginalWorkbook(ExportSettings settings, IOrganizationService service, string sourceLcidCode, CancellationToken cancellationToken)
    {
        var lcidRequest = new RetrieveProvisionedLanguagesRequest();
        var lcidResponse = (RetrieveProvisionedLanguagesResponse)service.Execute(lcidRequest);
        var allLcids = lcidResponse.RetrieveProvisionedLanguages.OrderBy(static x => x).ToList();
        var sourceLcid = int.Parse(sourceLcidCode, CultureInfo.InvariantCulture);
        var sourceLcidStr = sourceLcidCode;

        var entities = LoadSolutionEntities(service, settings, cancellationToken);
        if (entities.Count == 0)
        {
            var managedHint = settings.EnableManaged
                ? ""
                : " Try enabling managed components with '--enable-managed'.";

            throw new InvalidOperationException(
                $"No translatable entities found for the selected solution '{settings.SolutionId}'.{managedHint}");
        }

        var datasets = new Dictionary<string, IReadOnlyList<TranslationRecord>>(StringComparer.OrdinalIgnoreCase);

        if (settings.ExportEntities && entities.Count > 0)
        {
            datasets["Entities"] = entityTranslator.Export(entities, sourceLcid, allLcids, settings);
        }

        if (settings.ExportAttributes && entities.Count > 0)
        {
            datasets["Attributes"] = attributeTranslator.Export(entities, sourceLcid, allLcids, settings);
        }

        if (settings.ExportCustomizedRelationships && entities.Count > 0)
        {
            datasets["Relationships"] = relationshipTranslator.Export(entities, sourceLcid, allLcids, settings);
            datasets["RelationshipsNN"] = relationshipNnTranslator.Export(entities, sourceLcid, allLcids, settings);
        }

        if (settings.ExportGlobalOptionSet)
        {
            datasets["Global OptionSets"] = globalOptionSetRecordExporter.Export(sourceLcid, allLcids, service, settings);
        }

        if (settings.ExportOptionSet && entities.Count > 0)
        {
            datasets["OptionSets"] = optionSetTranslator.Export(entities, sourceLcid, allLcids, settings);
        }

        if (settings.ExportBooleans && entities.Count > 0)
        {
            datasets["Booleans"] = booleanTranslator.Export(entities, sourceLcid, allLcids, settings);
        }

        if (settings.ExportViews && entities.Count > 0)
        {
            datasets["Views"] = viewTranslator.Export(entities, sourceLcid, allLcids, service, settings);
        }

        if (settings.ExportCharts && entities.Count > 0)
        {
            datasets["Charts"] = visualizationTranslator.Export(entities, sourceLcid, allLcids, service, settings);
        }

        if ((settings.ExportForms || settings.ExportFormTabs || settings.ExportFormSections || settings.ExportFormFields) && entities.Count > 0)
        {
            foreach (var (name, records) in formTranslator.Export(entities, sourceLcid, allLcids, service, settings))
            {
                if (records.Count > 0)
                {
                    datasets[name] = records;
                }
            }
        }

        if (settings.ExportSiteMap)
        {
            foreach (var (name, records) in siteMapTranslator.Export(sourceLcid, allLcids, service, settings))
            {
                if (records.Count > 0)
                {
                    datasets[name] = records;
                }
            }
        }

        if (settings.ExportDashboards)
        {
            foreach (var (name, records) in dashboardTranslator.Export(sourceLcid, allLcids, service, settings))
            {
                if (records.Count > 0)
                {
                    datasets[name] = records;
                }
            }
        }

        if (settings.ExportRibbon)
        {
            datasets["Ribbon"] = ribbonTranslator.Export(sourceLcid, allLcids, service, settings);
        }

        var filteredDatasets = datasets
            .Select(static kvp => new
            {
                Name = kvp.Key,
                Records = kvp.Value
                    .Where(static r => !string.IsNullOrWhiteSpace(r.SourceText))
                    .ToList()
            })
            .Where(static x => x.Records.Count > 0)
            .ToDictionary(
                static x => x.Name,
                static x => (IReadOnlyList<TranslationRecord>)x.Records,
                StringComparer.OrdinalIgnoreCase);

        var workbookData = new TranslationWorkbookData
        {
            SourceLcid = sourceLcidStr,
            Datasets = filteredDatasets
        };

        return workbookData;
    }

    private static List<EntityMetadata> LoadSolutionEntities(IOrganizationService service, ExportSettings settings, CancellationToken cancellationToken)
    {
        var logicalNames = service.GetEntityLogicalNamesFromSolution(settings.SolutionId, settings.EnableManaged);
        var metadata = new List<EntityMetadata>();

        var index = 0;
        var page = logicalNames.Take(100).ToList();
        while (page.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var query = new EntityQueryExpression
            {
                Criteria = new MetadataFilterExpression(LogicalOperator.Or),
                Properties = new MetadataPropertiesExpression
                {
                    AllProperties = false,
                    PropertyNames = { "DisplayName", "DisplayCollectionName", "Description", "SchemaName", "LogicalName", "ObjectTypeCode", "IsManaged" }
                }
            };

            if (settings.ExportCustomizedRelationships)
            {
                query.Properties.PropertyNames.Add("OneToManyRelationships");
                query.Properties.PropertyNames.Add("ManyToOneRelationships");
                query.Properties.PropertyNames.Add("ManyToManyRelationships");
            }

            if (settings.ExportAttributes || settings.ExportOptionSet || settings.ExportBooleans || settings.ExportFormFields)
            {
                query.Properties.PropertyNames.Add("Attributes");
            }

            foreach (var logicalName in page)
            {
                query.Criteria.Conditions.Add(new MetadataConditionExpression("LogicalName", MetadataConditionOperator.Equals, logicalName));
            }

            var request = new RetrieveMetadataChangesRequest
            {
                Query = query,
                ClientVersionStamp = null
            };

            var response = (RetrieveMetadataChangesResponse)service.Execute(request);
            metadata.AddRange(response.EntityMetadata);

            index++;
            page = logicalNames.Skip(index * 100).Take(100).ToList();
        }

        return metadata;
    }

    private static ExportSettings ConfigureExportSettings(string sourcePath, Guid solutionId, bool enableManaged)
    {
        return new ExportSettings
        {
            FilePath = sourcePath,
            ExportAttributes = true,
            ExportBooleans = true,
            ExportCharts = true,
            ExportCustomizedRelationships = true,
            ExportDashboards = true,
            ExportDescriptions = true,
            ExportEntities = true,
            ExportFormFields = true,
            ExportForms = true,
            ExportFormSections = true,
            ExportFormTabs = true,
            ExportGlobalOptionSet = true,
            ExportNames = true,
            ExportOptionSet = true,
            ExportSiteMap = true,
            ExportViews = true,
            ExportRibbon = false,
            EnableManaged = enableManaged,
            SolutionId = solutionId
        };
    }

    private static string GetFilePath(string? sourceFile, string? exportFolder, string solutionName, string timestamp, string suffix)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var path = string.IsNullOrWhiteSpace(sourceFile)
            ? Path.Combine(string.IsNullOrWhiteSpace(exportFolder) ? currentDirectory : exportFolder, $"{solutionName}_{timestamp}{suffix}.csv")
            : sourceFile;

        return Path.HasExtension(path) ? path : $"{path}.csv";
    }
}
