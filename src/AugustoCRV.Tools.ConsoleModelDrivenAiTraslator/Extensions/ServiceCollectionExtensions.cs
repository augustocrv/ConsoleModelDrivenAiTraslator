using Microsoft.Extensions.Options;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Csv;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.TranslationPipeline;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Config;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Infrastructure;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Translation;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.TranslationPipeline;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Extensions;

/// <summary>
/// Registers application services and options for the AI Translator CLI.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds strongly typed default options for CLI execution.
    /// </summary>
    public static IServiceCollection AddTranslatorOptions(this IServiceCollection services)
    {
        services.AddOptions<TranslatorCliOptions>()
            .Configure(options =>
            {
                options.DefaultTranslationBatchSize = 30;
                options.DefaultImportBatchSize = 100;
                options.ConnectionsRelativePath = Path.Combine("AugustoCRV", "Tools", "ConsoleModelDrivenAiTraslator");
                options.PromptTemplateResourceSuffix = "Resources.PromptTemplate.md";
                options.DefaultConsoleModelDrivenAiTraslatorConfigResourceSuffix = "Resources.DefaultConsoleModelDrivenAiTraslatorConfig.json";
                options.WindowsLcidResourceSuffix = "Resources.WindowsLcid.json";
                options.AzureOpenAiTimeoutMinutes = 5;
                options.GitHubCopilotModel = "gpt-4.1";
            });

        return services;
    }

    /// <summary>
    /// Adds all service dependencies used by command handlers and application workflows.
    /// </summary>
    public static IServiceCollection AddTranslatorServices(this IServiceCollection services)
    {
        services.AddSingleton<IApiKeyProtectorService, ApiKeyProtectorService>();
        services.AddSingleton<ITokenCacheService, TokenCacheService>();
        services.AddSingleton<ILastGeneratedPathCache, LastGeneratedPathCache>();
        services.AddSingleton<IDataverseClientFactory, DataverseClientFactory>();
        services.AddSingleton<IAiConnectionStoreService, AiConnectionStoreService>();
        services.AddSingleton<IAiConnectionSelectionService, AiConnectionSelectionService>();
        services.AddSingleton<IDataverseConnectionStoreService, DataverseConnectionStoreService>();
        services.AddSingleton<IDataverseConnectionSelectionService, DataverseConnectionSelectionService>();
        services.AddSingleton<ConsoleModelDrivenAiTraslatorConfigJsonService>();
        services.AddSingleton<ILanguageCatalogService, LanguageCatalogService>();
        services.AddSingleton<ITranslationWorkbookStorage, SepTranslationWorkbookStorage>();
        services.AddSingleton<KeyedServiceFactory>();

        services.AddKeyedTransient<EntityRecordExporter>(TranslationServiceKind.Entity);
        services.AddKeyedTransient<AttributeRecordExporter>(TranslationServiceKind.Attribute);
        services.AddKeyedTransient<RelationshipRecordExporter>(TranslationServiceKind.Relationship);
        services.AddKeyedTransient<RelationshipNnRecordExporter>(TranslationServiceKind.RelationshipNn);
        services.AddKeyedTransient<GlobalOptionSetRecordExporter>(TranslationServiceKind.GlobalOptionSet);
        services.AddKeyedTransient<OptionSetRecordExporter>(TranslationServiceKind.OptionSet);
        services.AddKeyedTransient<BooleanRecordExporter>(TranslationServiceKind.Boolean);
        services.AddKeyedTransient<ViewRecordExporter>(TranslationServiceKind.View);
        services.AddKeyedTransient<VisualizationRecordExporter>(TranslationServiceKind.Visualization);
        services.AddKeyedTransient<FormRecordExporter>(TranslationServiceKind.Form);
        services.AddKeyedTransient<SiteMapRecordExporter>(TranslationServiceKind.SiteMap);
        services.AddKeyedTransient<DashboardRecordExporter>(TranslationServiceKind.Dashboard);
        services.AddKeyedTransient<RibbonRecordExporter>(TranslationServiceKind.Ribbon);
        services.AddTransient<EntityTranslationRecordImporter>();
        services.AddTransient<AttributeTranslationRecordImporter>();
        services.AddTransient<GlobalOptionSetTranslationRecordImporter>();
        services.AddTransient<OptionSetTranslationRecordImporter>();
        services.AddTransient<ViewTranslationRecordImporter>();
        services.AddTransient<RelationshipTranslationRecordImporter>();
        services.AddTransient<RelationshipNnTranslationRecordImporter>();
        services.AddTransient<BooleanTranslationRecordImporter>();
        services.AddTransient<VisualizationTranslationRecordImporter>();
        services.AddTransient<FormTranslationRecordImporter>();
        services.AddTransient<DashboardTranslationRecordImporter>();
        services.AddTransient<SiteMapTranslationRecordImporter>();
        services.AddTransient<RibbonTranslationRecordImporter>();

        services.AddTransient<IPromptTemplateService, PromptTemplateService>();

        services.AddHttpClient<AzureOpenAiTranslationService>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<TranslatorCliOptions>>().Value;
            client.Timeout = TimeSpan.FromMinutes(options.AzureOpenAiTimeoutMinutes);
        });

        services.AddHttpClient<IGitHubDeviceFlowService, GitHubDeviceFlowService>();

        services.AddKeyedTransient<IAiService>(AiConnectionType.AzureOpenAi, (provider, _) =>
            provider.GetRequiredService<AzureOpenAiTranslationService>());
        services.AddKeyedTransient<IAiService, GitHubCopilotTranslationService>(AiConnectionType.GitHubCopilot);

        services.AddKeyedTransient<ITranslationService, StaticTranslationService>(TranslationPipelineKind.Static);
        services.AddKeyedTransient<ITranslationService, AiPromptTranslationService>(TranslationPipelineKind.Ai);
        services.AddTransient<ITranslationCacheService, TranslationCacheService>();

        services.AddTransient<IGenerateExecutor, GenerateExecutor>();
        services.AddTransient<IExportOriginalExecutor, ExportOriginalExecutor>();
        services.AddTransient<IPushExecutor, PushExecutor>();
        services.AddTransient<IConnectionExecutors, ConnectionExecutors>();

        return services;
    }
}

