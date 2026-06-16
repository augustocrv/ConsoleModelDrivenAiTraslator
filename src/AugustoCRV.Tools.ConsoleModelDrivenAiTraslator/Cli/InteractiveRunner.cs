using System.Reflection;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Extensions;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Dataverse;
using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Infrastructure;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Cli;

internal sealed class InteractiveRunner
{
    private static readonly NullabilityInfoContext NullabilityInfo = new();
    private readonly IDataverseClientFactory dataverseClientFactory;
    private readonly ILastGeneratedPathCache lastGeneratedPathCache;

    public InteractiveRunner(
        IDataverseClientFactory dataverseClientFactory,
        ILastGeneratedPathCache lastGeneratedPathCache)
    {
        this.dataverseClientFactory = dataverseClientFactory;
        this.lastGeneratedPathCache = lastGeneratedPathCache;
    }

    public async Task<int> RunAsync(CommandApp app)
    {
        AnsiConsole.MarkupLine("[bold green]AI Translator Interactive CLI[/]");

        while (true)
        {
            var selection = await PromptForCommandAsync(app, Configurator.GetCommandTree(), new List<string>(), false);
            if (selection.Action == SelectionAction.Exit)
            {
                return 0;
            }

            var args = new List<string>();

            if (selection.Action == SelectionAction.Command)
            {
                args.AddRange(selection.CommandPath);
                BuildArgumentsFromSettings(selection.CommandType, args);
                
                var exitCode = await app.RunAsync(args.ToArray());
                if (exitCode != 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]Command returned exit code {exitCode}.[/]");
                }
            }
        }
    }

    private async Task<SelectionResult> PromptForCommandAsync(CommandApp app, IReadOnlyList<CliCommandNode> nodes, List<string> prefix, bool allowBack)
    {
        while (true)
        {
            var menuChoices = nodes
                .Select(n => new InteractiveChoice(n, $"{n.Name} - {n.Description}"))
                .ToList();

            if (allowBack)
            {
                menuChoices.Add(new InteractiveChoice(null, "back", isBack: true));
            }
            menuChoices.Add(new InteractiveChoice(null, "exit", isExit: true));

            var titlePrefix = prefix.Count == 0 ? string.Empty : $" ({string.Join(' ', prefix)})";
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<InteractiveChoice>()
                    .Title($"Select the command to execute{titlePrefix}")
                    .UseConverter(c => c.Label)
                    .AddChoices(menuChoices));

            if (choice.IsExit)
            {
                return new SelectionResult(SelectionAction.Exit, [], null);
            }

            if (choice.IsBack)
            {
                return new SelectionResult(SelectionAction.Back, [], null);
            }

            var node = choice.Node;
            if (node == null)
            {
                continue;
            }

            var currentPath = new List<string>(prefix) { node.Name };

            if (node.Children is { Count: > 0 })
            {
                // Loop within submenu until back or exit is selected
                while (true)
                {
                    var nested = await PromptForCommandAsync(app, node.Children, currentPath, true);
                    if (nested.Action == SelectionAction.Back)
                    {
                        break; // Exit the submenu loop and return to parent menu
                    }

                    if (nested.Action == SelectionAction.Exit)
                    {
                        return nested; // Propagate exit up
                    }

                    // Execute the command and loop back to submenu
                    if (nested.Action == SelectionAction.Command)
                    {
                        var args = new List<string>();
                        args.AddRange(nested.CommandPath);
                        BuildArgumentsFromSettings(nested.CommandType, args);
                        
                        var exitCode = await app.RunAsync(args.ToArray());
                        if (exitCode != 0)
                        {
                            AnsiConsole.MarkupLine($"[yellow]Command returned exit code {exitCode}.[/]");
                        }
                        
                        // Continue the submenu loop to show menu again
                        continue;
                    }
                }
                
                // Continue to show parent menu after exiting submenu
                continue;
            }

            return new SelectionResult(SelectionAction.Command, currentPath, node.CommandType);
        }
    }

    private void BuildArgumentsFromSettings(Type? commandType, List<string> args)
    {
        var settingsType = ResolveSettingsType(commandType);
        if (!typeof(CommandSettings).IsAssignableFrom(settingsType))
        {
            return;
        }

        if (settingsType == typeof(ConnCreateSettings))
        {
            BuildConnCreateArguments(args);
            return;
        }

        if (settingsType == typeof(GenerateSettings))
        {
            BuildGenerateArgumentsAsync(args).GetAwaiter().GetResult();
            return;
        }

        if (settingsType == typeof(PushSettings))
        {
            BuildPushArguments(args);
            return;
        }

        if (settingsType == typeof(ExportOriginalSettings))
        {
            BuildExportOriginalArguments(args);
            return;
        }

        if (settingsType == typeof(ConnDynCreateSettings))
        {
            BuildConnDynCreateArguments(args);
            return;
        }

        if (settingsType == typeof(ConnDeleteSettings) || settingsType == typeof(ConnDynDeleteSettings))
        {
            // Let executors prompt from existing connections list when name is omitted.
            return;
        }

        var optionProperties = settingsType
            .GetProperties()
            .Select(p => new { Property = p, Template = GetCommandOptionTemplate(p) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Template))
            .OrderBy(x => x.Property.MetadataToken)
            .ToList();

        foreach (var optionInfo in optionProperties)
        {
            var property = optionInfo.Property;

            var template = optionInfo.Template!;
            var optionName = ExtractOptionName(template);
            if (string.IsNullOrWhiteSpace(optionName))
            {
                continue;
            }

            var description = property.GetCustomAttributes(typeof(DescriptionAttribute), true)
                .OfType<DescriptionAttribute>()
                .FirstOrDefault()?.Description ?? property.Name;

            var expectsValue = template.Contains("<", StringComparison.Ordinal);
            if (!expectsValue)
            {
                if (AnsiConsole.Confirm($"{description}?", false))
                {
                    args.Add(optionName);
                }
                continue;
            }

            var isRequired = IsRequired(property);
            if (!isRequired && !AnsiConsole.Confirm($"{description}?", false))
            {
                continue;
            }

            var value = AskOptionValue(property, description);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            args.Add(optionName);
            args.Add(value);
        }
    }

    private void BuildPushArguments(List<string> args)
    {
        var cachedPath = lastGeneratedPathCache.GetAsync().GetAwaiter().GetResult();

        string workbookPath;
        if (!string.IsNullOrWhiteSpace(cachedPath) && (File.Exists(cachedPath) || Directory.Exists(cachedPath)))
        {
            var useCached = AnsiConsole.Confirm(
                $"Use last generated workbook [cyan]{cachedPath}[/]?", true);

            if (useCached)
            {
                workbookPath = cachedPath;
            }
            else
            {
                workbookPath = AnsiConsole.Ask<string>("Path to the translated workbook file:");
            }
        }
        else
        {
            workbookPath = AnsiConsole.Ask<string>("Path to the translated workbook file:");
        }

        args.Add("--workbook-path");
        args.Add(workbookPath);

        if (AnsiConsole.Confirm("Change default import batch size from 100?", false))
        {
            var batch = AnsiConsole.Ask<string>("Batch size for import:");
            if (!string.IsNullOrWhiteSpace(batch))
            {
                args.Add("--import-batch-size");
                args.Add(batch);
            }
        }

        if (AnsiConsole.Confirm("Force import even if hash matches?", false))
        {
            args.Add("--force");
        }

        if (AnsiConsole.Confirm("Source LCID override?", false))
        {
            var slc = AnsiConsole.Ask<string>("Source LCID override (e.g. 1033):");
            if (!string.IsNullOrWhiteSpace(slc))
            {
                args.Add("--source-language");
                args.Add(slc);
            }
        }

        if (AnsiConsole.Confirm("Target LCID override?", false))
        {
            var tlc = AnsiConsole.Ask<string>("Target LCID override (e.g. 1036):");
            if (!string.IsNullOrWhiteSpace(tlc))
            {
                args.Add("--target-language");
                args.Add(tlc);
            }
        }
    }

    private static void BuildExportOriginalArguments(List<string> args)
    {
        string solutionName;
        while (true)
        {
            solutionName = AnsiConsole.Ask<string>("Name of the Dataverse solution:");
            if (!string.IsNullOrWhiteSpace(solutionName)) break;
            AnsiConsole.MarkupLine("[red]Value required.[/]");
        }

        args.Add("--solution-name");
        args.Add(solutionName);

        string sourceLcid;
        while (true)
        {
            sourceLcid = AnsiConsole.Ask<string>("Source LCID of the labels to export (e.g. 1033):");
            if (!string.IsNullOrWhiteSpace(sourceLcid)) break;
            AnsiConsole.MarkupLine("[red]Value required.[/]");
        }

        args.Add("--source-language-code");
        args.Add(sourceLcid);

        if (AnsiConsole.Confirm("Specify export folder?", false))
        {
            var folder = AnsiConsole.Ask<string>("Export folder:");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                args.Add("--export-folder");
                args.Add(folder);
            }
        }

        if (AnsiConsole.Confirm("Include managed Dynamics components?", false))
        {
            args.Add("--enable-managed");
        }
    }

    private static void BuildConnDynCreateArguments(List<string> args)
    {
        // Step 1: URL first
        string url;
        while (true)
        {
            url = AnsiConsole.Ask<string>("Dynamics environment URL:");
            if (!string.IsNullOrWhiteSpace(url)) break;
            AnsiConsole.MarkupLine("[red]Value required.[/]");
        }

        args.Add("--url");
        args.Add(url);

        // Step 2: Name with default derived from URL host
        var defaultName = ExtractOrgNameFromUrl(url);

        string name;
        while (true)
        {
            name = string.IsNullOrWhiteSpace(defaultName)
                ? AnsiConsole.Ask<string>("Connection name:")
                : AnsiConsole.Prompt(new TextPrompt<string>("Connection name:").DefaultValue(defaultName));

            if (!string.IsNullOrWhiteSpace(name)) break;
            AnsiConsole.MarkupLine("[red]Value required.[/]");
        }

        args.Add("--name");
        args.Add(name);
    }

    private static string? ExtractOrgNameFromUrl(string url)
    {
        try
        {
            if (!url.Contains("://", StringComparison.Ordinal))
            {
                url = "https://" + url;
            }

            var host = new Uri(url.Trim().TrimEnd('/')).Host;
            var firstDot = host.IndexOf('.', StringComparison.Ordinal);
            return firstDot > 0 ? host[..firstDot] : host;
        }
        catch
        {
            return null;
        }
    }

    private void BuildConnCreateArguments(List<string> args)
    {
        // Step 1: Connection type - FIRST
        var selectedType = AnsiConsole.Prompt(
            new SelectionPrompt<AiConnectionType>()
                .Title("Select the AI connection type:")
                .UseConverter(t => t switch
                {
                    AiConnectionType.AzureOpenAi => "Azure OpenAI",
                    AiConnectionType.GitHubCopilot => "GitHub Copilot",
                    _ => t.ToString()
                })
                .AddChoices(Enum.GetValues<AiConnectionType>()));

        args.Add("--type");
        args.Add(selectedType.ToString());

        // Step 2: Connection name (required, common to all types)
        string name;
        while (true)
        {
            name = AnsiConsole.Ask<string>("Connection name:");
            if (!string.IsNullOrWhiteSpace(name)) break;
            AnsiConsole.MarkupLine("[red]Value required.[/]");
        }

        args.Add("--name");
        args.Add(name);

        // Step 3: Description (optional, common to all types)
        if (AnsiConsole.Confirm("Add a description?", false))
        {
            var description = AnsiConsole.Ask<string>("Connection description:");
            if (!string.IsNullOrWhiteSpace(description))
            {
                args.Add("--description");
                args.Add(description);
            }
        }

        // Remaining type-specific params (endpoint, apikey, model) are
        // prompted interactively inside CreateAsync after type is confirmed.
    }

    private async Task BuildGenerateArgumentsAsync(List<string> args)
    {
        try
        {
            var service = await dataverseClientFactory.CreateAsync(CancellationToken.None).ConfigureAwait(false);

            // Step 1: Select solution
            AnsiConsole.Console.WriteInfo("Retrieving solutions from Dataverse...");
            var solutions = service.GetUnmanagedSolutions();
            
            if (solutions.Count == 0)
            {
                AnsiConsole.Console.WriteError("No unmanaged solutions found in Dataverse.");
                return;
            }

            var selectedSolution = AnsiConsole.Prompt(
                new SelectionPrompt<DataverseSolutionInfo>()
                    .Title("Select the Dataverse solution:")
                    .PageSize(15)
                    .UseConverter(solution =>
                        string.IsNullOrWhiteSpace(solution.FriendlyName)
                            ? solution.UniqueName
                            : $"{solution.FriendlyName} ({solution.UniqueName})")
                    .AddChoices(solutions));

            args.Add("--solution-name");
            args.Add(selectedSolution.UniqueName);
            
            AnsiConsole.MarkupLine($"[green]✓[/] Solution chosen: [cyan]{selectedSolution.UniqueName}[/]");

            // Step 2: Get provisioned languages
            AnsiConsole.Console.WriteInfo("Retrieving provisioned languages from Dataverse...");
            var languages = service.GetProvisionedLanguages();
            
            if (languages.Count == 0)
            {
                AnsiConsole.Console.WriteError("No provisioned languages found in Dataverse.");
                return;
            }

            // Step 3: Select source language
            var sourceLanguage = AnsiConsole.Prompt(
                new SelectionPrompt<DataverseLanguageInfo>()
                    .Title("Select the source language:")
                    .PageSize(15)
                    .UseConverter(l => l.DisplayName)
                    .AddChoices(languages));

            args.Add("--source-language-code");
            args.Add(sourceLanguage.Lcid.ToString());
            
            AnsiConsole.MarkupLine($"[green]✓[/] Source LCID chosen: [cyan]{sourceLanguage.DisplayName}[/]");

            // Step 4: Select target languages (multi-select with All option)
            var targetLanguagesAvailable = languages.Where(l => l.Lcid != sourceLanguage.Lcid).ToList();
            
            // Create a special "All" option
            var allOption = new DataverseLanguageInfo(-1, "[[All Languages]]");
            var targetChoices = new List<DataverseLanguageInfo> { allOption };
            targetChoices.AddRange(targetLanguagesAvailable);
            
            var selectedTargets = AnsiConsole.Prompt(
                new MultiSelectionPrompt<DataverseLanguageInfo>()
                    .Title("Select target language(s) (use [blue]<space>[/] to toggle, [green]<enter>[/] to confirm):")
                    .PageSize(15)
                    .UseConverter(l => l.DisplayName)
                    .AddChoices(targetChoices)
                    .InstructionsText("[grey](Press [blue]<space>[/] to toggle selection, [green]<enter>[/] to confirm)[/]"));

            if (selectedTargets.Count == 0)
            {
                AnsiConsole.Console.WriteError("At least one target language must be selected.");
                return;
            }
            
            List<DataverseLanguageInfo> targetLanguages;
            
            // Check if "All" was selected
            if (selectedTargets.Any(t => t.Lcid == -1))
            {
                targetLanguages = targetLanguagesAvailable;
                AnsiConsole.MarkupLine($"[green]✓[/] All target languages selected: [cyan]{targetLanguages.Count} languages[/]");
            }
            else
            {
                targetLanguages = selectedTargets;
                var targetNames = string.Join(", ", targetLanguages.Select(t => t.Lcid.ToString()));
                AnsiConsole.MarkupLine($"[green]✓[/] Target LCIDs chosen: [cyan]{targetNames}[/]");
            }

            var targetLcids = string.Join(",", targetLanguages.Select(t => t.Lcid));
            args.Add("--target-language-codes");
            args.Add(targetLcids);

            // Step 5: Optional parameters
            if (AnsiConsole.Confirm("Provide source CSV path?", false))
            {
                var csvPath = AnsiConsole.Ask<string>("Source CSV path:");
                if (!string.IsNullOrWhiteSpace(csvPath))
                {
                    args.Add("--source-csv-path");
                    args.Add(csvPath);
                }
            }

            if (AnsiConsole.Confirm("Add translation context/instructions?", false))
            {
                var context = AnsiConsole.Ask<string>("Translation context:");
                if (!string.IsNullOrWhiteSpace(context))
                {
                    args.Add("--translation-context");
                    args.Add(context);
                }
            }

            if (AnsiConsole.Confirm("Specify export folder?", false))
            {
                var folder = AnsiConsole.Ask<string>("Export folder:");
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    args.Add("--export-folder");
                    args.Add(folder);
                }
            }

            if (AnsiConsole.Confirm("Force overwrite existing translations?", false))
            {
                args.Add("--force");
            }

            if (AnsiConsole.Confirm("Include managed Dynamics components?", false))
            {
                args.Add("--enable-managed");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.Console.WriteError($"Error building generate arguments: {ex.Message}");
        }
    }

    private static Type ResolveSettingsType(Type? commandType)
    {
        if (commandType == null)
        {
            return typeof(CommandSettings);
        }

        var currentType = commandType;
        while (currentType != null)
        {
            if (currentType.IsGenericType)
            {
                var definition = currentType.GetGenericTypeDefinition();
                if (definition == typeof(AsyncCommand<>) || definition == typeof(Command<>))
                {
                    return currentType.GetGenericArguments()[0];
                }
            }

            currentType = currentType.BaseType;
        }

        return typeof(CommandSettings);
    }

    private string AskOptionValue(PropertyInfo property, string description)
    {
        while (true)
        {
            var value = IsSecret(property)
                ? AnsiConsole.Prompt(new TextPrompt<string>($"{description}:").Secret())
                : AnsiConsole.Ask<string>($"{description}:");

            if (!IsRequired(property) || !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            AnsiConsole.MarkupLine("[red]Value required.[/]");
        }
    }

    private static bool IsRequired(PropertyInfo property)
    {
        if (property.PropertyType == typeof(bool))
        {
            return false;
        }

        if (property.PropertyType.IsValueType)
        {
            return Nullable.GetUnderlyingType(property.PropertyType) == null;
        }

        return NullabilityInfo.Create(property).ReadState == NullabilityState.NotNull;
    }

    private static bool IsSecret(PropertyInfo property)
    {
        var name = property.Name;
        return name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
               || name.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Secret", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractOptionName(string template)
    {
        var firstSegment = template.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var aliases = firstSegment.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var longName = aliases.FirstOrDefault(a => a.StartsWith("--", StringComparison.Ordinal));
        return longName ?? aliases.FirstOrDefault() ?? string.Empty;
    }

    private static string? GetCommandOptionTemplate(PropertyInfo property)
    {
        var optionAttributeData = property.CustomAttributes
            .FirstOrDefault(a => a.AttributeType == typeof(CommandOptionAttribute));

        if (optionAttributeData == null || optionAttributeData.ConstructorArguments.Count == 0)
        {
            return null;
        }

        return optionAttributeData.ConstructorArguments[0].Value?.ToString();
    }

    private enum SelectionAction
    {
        Command,
        Back,
        Exit
    }

    private sealed class SelectionResult
    {
        public SelectionResult(SelectionAction action, IReadOnlyList<string> commandPath, Type? commandType)
        {
            Action = action;
            CommandPath = commandPath;
            CommandType = commandType;
        }

        public SelectionAction Action { get; }

        public IReadOnlyList<string> CommandPath { get; }

        public Type? CommandType { get; }
    }

    private sealed class InteractiveChoice
    {
        public InteractiveChoice(CliCommandNode? node, string label, bool isExit = false, bool isBack = false)
        {
            Node = node;
            Label = label;
            IsExit = isExit;
            IsBack = isBack;
        }

        public CliCommandNode? Node { get; }

        public string Label { get; }

        public bool IsBack { get; }

        public bool IsExit { get; }
    }
}