namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Cli;

internal sealed record CliCommandNode(
    string Name,
    string Description,
    Type? CommandType,
    IReadOnlyList<CliCommandNode>? Children = null,
    IReadOnlyList<string>? Aliases = null,
    Action<object, CliCommandNode>? ConfigureCommand = null);

internal static class Configurator
{
    private static readonly IReadOnlyList<CliCommandNode> CommandTree = new List<CliCommandNode>
    {
        new(
            Name: "gen",
            Description: "Generate source and translated CSV files using AI",
            CommandType: typeof(GenerateCliCommand),
            Aliases: ["generate"],
            ConfigureCommand: RegisterCommand<GenerateCliCommand>),
        new CliCommandNode(
            Name: "push",
            Description: "Apply translations from CSV back into Dataverse",
            CommandType: typeof(PushCliCommand),
            ConfigureCommand: RegisterCommand<PushCliCommand>),
        new(
            Name: "export-original",
            Description: "Export only source Dataverse metadata to CSV",
            CommandType: typeof(ExportOriginalCliCommand),
            Aliases: ["gen-original"],
            ConfigureCommand: RegisterCommand<ExportOriginalCliCommand>),
        new CliCommandNode(
            Name: "conn",
            Description: "Connection management",
            CommandType: null,
            Children:
            [
                new CliCommandNode(
                    Name: "ai",
                    Description: "Manage AI connections",
                    CommandType: null,
                    Children:
                    [
                        new CliCommandNode(
                            Name: "create",
                            Description: "Create a new AI connection",
                            CommandType: typeof(ConnCreateCliCommand),
                            ConfigureCommand: RegisterCommand<ConnCreateCliCommand>),
                        new CliCommandNode(
                            Name: "delete",
                            Description: "Delete a stored AI connection",
                            CommandType: typeof(ConnDeleteCliCommand),
                            ConfigureCommand: RegisterCommand<ConnDeleteCliCommand>),
                        new CliCommandNode(
                            Name: "list",
                            Description: "List stored AI connections",
                            CommandType: typeof(ConnListCliCommand),
                            ConfigureCommand: RegisterCommand<ConnListCliCommand>),
                        new CliCommandNode(
                            Name: "select",
                            Description: "Select connection to the all main operations",
                            CommandType: typeof(ConnAiSelectCliCommand),
                            ConfigureCommand: RegisterCommand<ConnAiSelectCliCommand>)
                    ]),
                new CliCommandNode(
                    Name: "dataverse",
                    Description: "Manage Dynamics environment connections",
                    CommandType: null,
                    Children:
                    [
                        new CliCommandNode(
                            Name: "create",
                            Description: "Create a new Dynamics connection",
                            CommandType: typeof(ConnDynCreateCliCommand),
                            ConfigureCommand: RegisterCommand<ConnDynCreateCliCommand>),
                        new CliCommandNode(
                            Name: "delete",
                            Description: "Delete a stored Dynamics connection",
                            CommandType: typeof(ConnDynDeleteCliCommand),
                            ConfigureCommand: RegisterCommand<ConnDynDeleteCliCommand>),
                        new CliCommandNode(
                            Name: "list",
                            Description: "List stored Dynamics connections",
                            CommandType: typeof(ConnDynListCliCommand),
                            ConfigureCommand: RegisterCommand<ConnDynListCliCommand>),
                        new CliCommandNode(
                            Name: "test",
                            Description: "Test a Dynamics connection",
                            CommandType: typeof(ConnDynTestCliCommand),
                            ConfigureCommand: RegisterCommand<ConnDynTestCliCommand>),
                        new CliCommandNode(
                            Name: "select",
                            Description: "Select connection to the all main operations",
                            CommandType: typeof(ConnDynSelectCliCommand),
                            ConfigureCommand: RegisterCommand<ConnDynSelectCliCommand>)
                    ])
            ])
    };

    internal static IReadOnlyList<CliCommandNode> GetCommandTree()
    {
        return CommandTree;
    }

    public static void ConfigureTranslatorCli(this IConfigurator config)
    {
        config.SetApplicationName("ai-translator");

        RegisterCommands(config, CommandTree);

        config.SetExceptionHandler((ex, _) =>
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return -1;
        });

        config.AddExample(new[]
        {
            "gen", "--solution-name", "MySolution", "--source-language-code", "1033", "--target-language-codes", "1040"
        });

        config.AddExample(new[]
        {
            "export-original", "--solution-name", "MySolution"
        });

        config.AddExample(new[]
        {
            "push", "--translated-csv-path", "C:/temp/translations.1040.csv",
            "--dataverse-connection-string", "AuthType=ClientSecret;..."
        });

        config.AddExample(new[]
        {
            "conn", "ai", "create", "--name", "my-conn", "--deployment-endpoint",
            "https://example.openai.azure.com/openai/deployments/mydep", "--api-key", "***"
        });
    }

    private static void RegisterCommands(object configurator, IEnumerable<CliCommandNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.CommandType != null)
            {
                node.ConfigureCommand!(configurator, node);
            }
            else if (node.Children is { Count: > 0 })
            {
                switch (configurator)
                {
                    case IConfigurator rootConfigurator:
                        rootConfigurator.AddBranch(node.Name, branch => RegisterCommands(branch, node.Children));
                        break;
                    case IConfigurator<CommandSettings> branchConfigurator:
                        branchConfigurator.AddBranch(node.Name, branch => RegisterCommands(branch, node.Children));
                        break;
                }
            }
        }
    }

    private static void RegisterCommand<TCommand>(object configurator, CliCommandNode node)
        where TCommand : class, ICommand, ICommandLimiter<CommandSettings>
    {
        var command = configurator switch
        {
            IConfigurator rootConfigurator => rootConfigurator.AddCommand<TCommand>(node.Name),
            IConfigurator<CommandSettings> branchConfigurator => branchConfigurator.AddCommand<TCommand>(node.Name),
            _ => throw new InvalidOperationException("Unsupported configurator type")
        };

        command.WithDescription(node.Description);

        if (node.Aliases != null)
        {
            foreach (var alias in node.Aliases)
            {
                command.WithAlias(alias);
            }
        }
    }
}


