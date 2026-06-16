
namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Cli;

internal sealed class GenerateSettings : CommandSettings
{
    [CommandOption("--solution-name|--sn <SOLUTION_NAME>")]
    [Description("Name of the Dataverse solution")]
    public string SolutionName { get; set; } = string.Empty;

    [CommandOption("--source-language-code|--slc <SOURCE_LCID>")]
    [Description("Source LCID (e.g. 1033)")]
    public string SourceLanguageCode { get; set; } = string.Empty;

    [CommandOption("--target-language-codes|--tlc <TARGET_LCIDS>")]
    [Description("Target LCIDs comma separated")]
    public string TargetLanguageCodes { get; set; } = string.Empty;

    [CommandOption("--source-csv-path|--csv <PATH>")]
    [Description("Existing source CSV path")]
    public string? SourceCsvFile { get; set; }

    [CommandOption("--translation-context|--context <TEXT>")]
    [Description("Optional translation context/instructions saved to config and added to AI prompt")]
    public string? TranslationContext { get; set; }

    [CommandOption("--include-view-types|--includeviewtypes <VIEW_TYPES>")]
    [Description("Comma-separated view types to include (only for Views sheet)")]
    public string? IncludeViewTypes { get; set; }

    [CommandOption("--exclude-view-types|--excludedviewtype <VIEW_TYPES>")]
    [Description("Comma-separated view types to exclude (only for Views sheet)")]
    public string? ExcludeViewTypes { get; set; }

    [CommandOption("--export-folder|--output <PATH>")]
    [Description("Folder where output CSV files are written")]
    public string? ExportFolder { get; set; }

    [CommandOption("--force|-f")]
    [Description("Overwrite existing translations")]
    public bool Force { get; set; }

    [CommandOption("--enable-managed")]
    [Description("Include managed Dynamics components in export/translation")]
    public bool EnableManaged { get; set; }
}

internal sealed class ExportOriginalSettings : CommandSettings
{
    [CommandOption("--solution-name|--sn <SOLUTION_NAME>")]
    [Description("Name of the Dataverse solution")]
    public string SolutionName { get; set; } = string.Empty;

    [CommandOption("--source-language-code|--slc <SOURCE_LCID>")]
    [Description("Source LCID of the labels to export (e.g. 1033)")]
    public string SourceLanguageCode { get; set; } = string.Empty;

    [CommandOption("--source-csv-path|--csv <PATH>")]
    [Description("Output path for the exported source CSV")]
    public string? SourceCsvFile { get; set; }

    [CommandOption("--export-folder|--output <PATH>")]
    [Description("Folder where output CSV files are written")]
    public string? ExportFolder { get; set; }

    [CommandOption("--enable-managed")]
    [Description("Include managed Dynamics components in export")]
    public bool EnableManaged { get; set; }
}

internal sealed class PushSettings : CommandSettings
{
    [CommandOption("--workbook-path|--path <PATH>")]
    [Description("Path to the translated workbook file")]
    public string WorkbookPath { get; set; } = string.Empty;

    [CommandOption("--import-batch-size|--batch <SIZE>")]
    [Description("Batch size for import")]
    public int? ImportBatchSize { get; set; }

    [CommandOption("--force|-f")]
    [Description("Force import even if hash matches")]
    public bool Force { get; set; }

    [CommandOption("--source-language|--slc <LCID>")]
    [Description("Source LCID override (e.g. 1033); uses workbook file value if omitted")]
    public string? SourceLanguage { get; set; }

    [CommandOption("--target-language|--tlc <LCID>")]
    [Description("Target LCID override (e.g. 1036); uses workbook file value if omitted")]
    public string? TargetLanguage { get; set; }
}

internal sealed class ConnCreateSettings : CommandSettings
{
    [CommandOption("--type|-t <TYPE>")]
    [Description("AI connection type (AzureOpenAi or GitHubCopilot)")]
    public AiConnectionType? Type { get; set; }

    [CommandOption("--name|-n <NAME>")]
    [Description("Connection name")]
    public string Name { get; set; } = string.Empty;

    [CommandOption("--deployment-endpoint|--endpoint <ENDPOINT>")]
    [Description("Azure OpenAI deployment endpoint (optional for GitHub Copilot)")]
    public string? DeploymentEndpoint { get; set; }

    [CommandOption("--api-key|--key <API_KEY>")]
    [Description("Azure OpenAI API key (optional for GitHub Copilot)")]
    public string? ApiKey { get; set; }

    [CommandOption("--model|-m <MODEL>")]
    [Description("Model name override for this connection")]
    public string? Model { get; set; }

    [CommandOption("--description|--desc <TEXT>")]
    [Description("Optional connection description")]
    public string? Description { get; set; }
}

internal sealed class ConnDeleteSettings : CommandSettings
{
    [CommandOption("--name|-n <NAME>")]
    [Description("Connection name to delete")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class ConnListSettings : CommandSettings
{
}

internal sealed class ConnAiSelectSettings : CommandSettings
{
}

internal sealed class ConnDynCreateSettings : CommandSettings
{
    [CommandOption("--name|-n <NAME>")]
    [Description("Connection name")]
    public string Name { get; set; } = string.Empty;

    [CommandOption("--url|-u <URL>")]
    [Description("Dynamics environment URL (OAuth MFA authentication)")]
    public string Url { get; set; } = string.Empty;
}

internal sealed class ConnDynDeleteSettings : CommandSettings
{
    [CommandOption("--name|-n <NAME>")]
    [Description("Connection name to delete")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class ConnDynListSettings : CommandSettings
{
}

internal sealed class ConnDynTestSettings : CommandSettings
{
}

internal sealed class ConnDynSelectSettings : CommandSettings
{
}
