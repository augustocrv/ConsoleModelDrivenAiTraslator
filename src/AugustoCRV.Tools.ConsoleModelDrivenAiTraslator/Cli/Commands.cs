using ValidationResult = Spectre.Console.ValidationResult;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Cli;

internal sealed class GenerateCliCommand : AsyncCommand<GenerateSettings>
{
    private readonly IGenerateExecutor executor;

    public GenerateCliCommand(IGenerateExecutor executor)
    {
        this.executor = executor;
    }

    public override ValidationResult Validate(CommandContext context, GenerateSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SolutionName) ||
            string.IsNullOrWhiteSpace(settings.SourceLanguageCode) ||
            string.IsNullOrWhiteSpace(settings.TargetLanguageCodes))
        {
            return ValidationResult.Error("Missing required options. Use 'ai-translator gen --help'.");
        }

        if (!string.IsNullOrWhiteSpace(settings.IncludeViewTypes) &&
            !string.IsNullOrWhiteSpace(settings.ExcludeViewTypes))
        {
            return ValidationResult.Error("Use either '--include-view-types' or '--exclude-view-types', not both.");
        }

        return ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, GenerateSettings settings)
    {
        return executor.ExecuteAsync(
            settings.SolutionName,
            settings.SourceLanguageCode,
            settings.TargetLanguageCodes,
            settings.SourceCsvFile,
            settings.TranslationContext,
            settings.IncludeViewTypes,
            settings.ExcludeViewTypes,
            settings.ExportFolder,
            settings.EnableManaged,
            settings.Force,
            CancellationToken.None);
    }
}

internal sealed class ExportOriginalCliCommand : AsyncCommand<ExportOriginalSettings>
{
    private readonly IExportOriginalExecutor executor;

    public ExportOriginalCliCommand(IExportOriginalExecutor executor)
    {
        this.executor = executor;
    }

    public override ValidationResult Validate(CommandContext context, ExportOriginalSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SolutionName) ||
            string.IsNullOrWhiteSpace(settings.SourceLanguageCode))
        {
            return ValidationResult.Error("Missing required options. Use 'ai-translator export-original --help'.");
        }

        return ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, ExportOriginalSettings settings)
    {
        return executor.ExecuteAsync(
            settings.SolutionName,
            settings.SourceLanguageCode,
            settings.SourceCsvFile,
            settings.ExportFolder,
            settings.EnableManaged,
            CancellationToken.None);
    }
}

internal sealed class PushCliCommand : AsyncCommand<PushSettings>
{
    private readonly IPushExecutor executor;

    public PushCliCommand(IPushExecutor executor)
    {
        this.executor = executor;
    }

    public override ValidationResult Validate(CommandContext context, PushSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.WorkbookPath))
        {
            return ValidationResult.Error("Missing required options. Use 'ai-translator push --help'.");
        }

        return ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, PushSettings settings)
    {
        return executor.ExecuteAsync(
            settings.WorkbookPath,
            settings.SourceLanguage,
            settings.TargetLanguage,
            settings.ImportBatchSize,
            settings.Force,
            CancellationToken.None);
    }
}

internal sealed class ConnCreateCliCommand : AsyncCommand<ConnCreateSettings>
{
    private readonly IConnectionExecutors executors;

    public ConnCreateCliCommand(IConnectionExecutors executors)
    {
        this.executors = executors;
    }

    public override ValidationResult Validate(CommandContext context, ConnCreateSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            return ValidationResult.Error("Missing required options. Use 'ai-translator conn ai create --help'.");
        }

        return ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, ConnCreateSettings settings)
    {
        return executors.CreateAsync(
            settings.Name,
            settings.Type,
            settings.DeploymentEndpoint ?? string.Empty,
            settings.ApiKey ?? string.Empty,
            settings.Model ?? string.Empty,
            settings.Description ?? string.Empty,
            CancellationToken.None);
    }
}

internal sealed class ConnDeleteCliCommand : AsyncCommand<ConnDeleteSettings>
{
    private readonly IConnectionExecutors executors;

    public ConnDeleteCliCommand(IConnectionExecutors executors)
    {
        this.executors = executors;
    }

    public override ValidationResult Validate(CommandContext context, ConnDeleteSettings settings) => ValidationResult.Success();

    public override Task<int> ExecuteAsync(CommandContext context, ConnDeleteSettings settings)
    {
        return executors.DeleteAsync(settings.Name, CancellationToken.None);
    }
}

internal sealed class ConnListCliCommand : AsyncCommand<ConnListSettings>
{
    private readonly IConnectionExecutors executors;

    public ConnListCliCommand(IConnectionExecutors executors)
    {
        this.executors = executors;
    }

    public override Task<int> ExecuteAsync(CommandContext context, ConnListSettings settings)
    {
        return executors.ListAsync(CancellationToken.None);
    }
}

internal sealed class ConnDynCreateCliCommand : AsyncCommand<ConnDynCreateSettings>
{
    private readonly IConnectionExecutors executors;

    public ConnDynCreateCliCommand(IConnectionExecutors executors)
    {
        this.executors = executors;
    }

    public override ValidationResult Validate(CommandContext context, ConnDynCreateSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Name) ||
            string.IsNullOrWhiteSpace(settings.Url))
        {
            return ValidationResult.Error("Missing required options. Use 'ai-translator conn dataverse create --help'.");
        }

        return ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, ConnDynCreateSettings settings)
    {
        return executors.CreateDataverseConnectionAsync(
            settings.Name,
            settings.Url,
            CancellationToken.None);
    }
}

internal sealed class ConnAiSelectCliCommand : AsyncCommand<ConnAiSelectSettings>
{
    private readonly IConnectionExecutors executors;
    private readonly IAiConnectionStoreService connectionStore;

    public ConnAiSelectCliCommand(
        IConnectionExecutors executors,
        IAiConnectionStoreService connectionStore)
    {
        this.executors = executors;
        this.connectionStore = connectionStore;
    }

    public override ValidationResult Validate(CommandContext context, ConnAiSelectSettings settings)
    {
        return ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ConnAiSelectSettings settings)
    {
        var connections = await connectionStore.LoadAsync().ConfigureAwait(false);
        if (connections.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No AI connections found. Create one first.[/]");
            return 0;
        }

        var choices = new List<string>();
        choices.AddRange(connections.Select(c => c.Name));
        choices.Add("back");
        choices.Add("exit");

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]AI Connections - Select a connection for main operations:[/]")
                .AddChoices(choices));

        if (selection == "exit" || selection == "back")
        {
            return 0;
        }

        return await executors.SelectAiConnectionAsync(selection, CancellationToken.None).ConfigureAwait(false);
    }
}

internal sealed class ConnDynDeleteCliCommand : AsyncCommand<ConnDynDeleteSettings>
{
    private readonly IConnectionExecutors executors;

    public ConnDynDeleteCliCommand(IConnectionExecutors executors)
    {
        this.executors = executors;
    }

    public override ValidationResult Validate(CommandContext context, ConnDynDeleteSettings settings) => ValidationResult.Success();

    public override Task<int> ExecuteAsync(CommandContext context, ConnDynDeleteSettings settings)
    {
        return executors.DeleteDataverseConnectionAsync(settings.Name, CancellationToken.None);
    }
}

internal sealed class ConnDynListCliCommand : AsyncCommand<ConnDynListSettings>
{
    private readonly IConnectionExecutors executors;

    public ConnDynListCliCommand(IConnectionExecutors executors)
    {
        this.executors = executors;
    }

    public override Task<int> ExecuteAsync(CommandContext context, ConnDynListSettings settings)
    {
        return executors.ListDataverseConnectionsAsync(CancellationToken.None);
    }
}

internal sealed class ConnDynTestCliCommand : AsyncCommand<ConnDynTestSettings>
{
    private readonly IConnectionExecutors executors;

    public ConnDynTestCliCommand(IConnectionExecutors executors)
    {
        this.executors = executors;
    }

    public override ValidationResult Validate(CommandContext context, ConnDynTestSettings settings)
    {
        return ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ConnDynTestSettings settings)
    {
        return await executors.TestDataverseConnectionAsync(CancellationToken.None);
    }
}

internal sealed class ConnDynSelectCliCommand : AsyncCommand<ConnDynSelectSettings>
{
    private readonly IConnectionExecutors executors;
    private readonly IDataverseConnectionStoreService connectionStore;

    public ConnDynSelectCliCommand(
        IConnectionExecutors executors,
        IDataverseConnectionStoreService connectionStore)
    {
        this.executors = executors;
        this.connectionStore = connectionStore;
    }

    public override ValidationResult Validate(CommandContext context, ConnDynSelectSettings settings)
    {
        return ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ConnDynSelectSettings settings)
    {
        var connections = await connectionStore.LoadAsync().ConfigureAwait(false);
        if (connections.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No Dynamics connections found. Create one first.[/]");
            return 0;
        }

        var choices = new List<string>();
        choices.AddRange(connections.Select(c => c.Name));
        choices.Add("back");
        choices.Add("exit");

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Dataverse Connections - Select a connection for main operations:[/]")
                .AddChoices(choices));

        if (selection == "exit" || selection == "back")
        {
            return 0;
        }

        return await executors.SelectDataverseConnectionAsync(selection, CancellationToken.None).ConfigureAwait(false);
    }
}

