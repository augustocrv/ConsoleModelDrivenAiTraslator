using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Infrastructure;
using GitHub.Copilot.SDK;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

internal sealed class ConnectionExecutors : IConnectionExecutors
{
    private readonly IAiConnectionStoreService connectionStore;
    private readonly IDataverseConnectionStoreService dataverseConnectionStore;
    private readonly IAiConnectionSelectionService aiConnectionSelectionService;
    private readonly IDataverseConnectionSelectionService dataverseConnectionSelectionService;
    private readonly IDataverseClientFactory dataverseClientFactory;
    private readonly IGitHubDeviceFlowService gitHubDeviceFlowService;

    public ConnectionExecutors(
        IAiConnectionStoreService connectionStore,
        IDataverseConnectionStoreService dataverseConnectionStore,
        IAiConnectionSelectionService aiConnectionSelectionService,
        IDataverseConnectionSelectionService dataverseConnectionSelectionService,
        IDataverseClientFactory dataverseClientFactory,
        IGitHubDeviceFlowService gitHubDeviceFlowService)
    {
        this.connectionStore = connectionStore;
        this.dataverseConnectionStore = dataverseConnectionStore;
        this.aiConnectionSelectionService = aiConnectionSelectionService;
        this.dataverseConnectionSelectionService = dataverseConnectionSelectionService;
        this.dataverseClientFactory = dataverseClientFactory;
        this.gitHubDeviceFlowService = gitHubDeviceFlowService;
    }

    public async Task<int> CreateAsync(
        string name,
        AiConnectionType? type,
        string deploymentEndpoint,
        string apiKey,
        string model,
        string description,
        CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;

        AnsiConsole.Console.WriteInfo("Creating a new connection...");

        var selectedType = type ?? AnsiConsole.Prompt(
            new SelectionPrompt<AiConnectionType>()
                .Title("Select the AI connection type:")
                .UseConverter(ToConnectionTypeLabel)
                .AddChoices(Enum.GetValues<AiConnectionType>()));

        var endpointToSave = deploymentEndpoint;
        var apiKeyToSave = apiKey;
        var modelToSave = model;
        var descriptionToSave = description;

        if (selectedType == AiConnectionType.AzureOpenAi)
        {
            if (string.IsNullOrWhiteSpace(endpointToSave))
            {
                endpointToSave = AnsiConsole.Ask<string>("Azure OpenAI deployment endpoint:");
            }

            if (string.IsNullOrWhiteSpace(apiKeyToSave))
            {
                apiKeyToSave = AnsiConsole.Prompt(new TextPrompt<string>("Azure OpenAI API key:").Secret());
            }

            if (string.IsNullOrWhiteSpace(endpointToSave) || string.IsNullOrWhiteSpace(apiKeyToSave))
            {
                AnsiConsole.Console.WriteError("Endpoint and API key are required for Azure OpenAI connections.");
                return 1;
            }

        }
        else
        {
            endpointToSave = string.Empty;

            // Auth mode selection for GitHub Copilot
            var authMode = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select GitHub Copilot authentication mode:")
                    .AddChoices("OAuth Device Flow (recommended)", "Logged-in User (no token)", "Manual Token"));

            switch (authMode)
            {
                case "OAuth Device Flow (recommended)":
                    var oauthToken = await gitHubDeviceFlowService.AuthenticateAsync(cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(oauthToken))
                    {
                        AnsiConsole.Console.WriteError("OAuth authentication failed or was cancelled.");
                        return 1;
                    }
                    apiKeyToSave = oauthToken;
                    AnsiConsole.Console.WriteSuccess("GitHub OAuth authentication successful!");
                    break;

                case "Manual Token":
                    apiKeyToSave = AnsiConsole.Prompt(new TextPrompt<string>("GitHub token (gho_/ghu_/github_pat_):").Secret());
                    if (string.IsNullOrWhiteSpace(apiKeyToSave))
                    {
                        AnsiConsole.Console.WriteError("A token is required for manual token authentication.");
                        return 1;
                    }
                    break;

                default:
                    // Logged-in User — no token needed
                    apiKeyToSave = string.Empty;
                    break;
            }

            if (string.IsNullOrWhiteSpace(modelToSave))
            {
                var availableModels = await GetGitHubCopilotModelChoicesAsync(apiKeyToSave, cancellationToken).ConfigureAwait(false);
                if (availableModels.Count > 0)
                {
                    var selectedModel = AnsiConsole.Prompt(
                        new SelectionPrompt<ModelInfo>()
                            .Title("Select GitHub Copilot model:")
                            .PageSize(15)
                            .UseConverter(static modelInfo =>
                                $"{modelInfo.Name} (billing x{modelInfo?.Billing?.Multiplier:0.00})")
                            .AddChoices(availableModels));

                    modelToSave = string.IsNullOrWhiteSpace(selectedModel.Id)
                        ? selectedModel.Name ?? string.Empty
                        : selectedModel.Id;
                }
                else
                {
                    modelToSave = AnsiConsole.Prompt(
                        new TextPrompt<string>("GitHub Copilot model:")
                            .DefaultValue("gpt-4.1"));
                }
            }
        }

        var connections = await connectionStore.LoadAsync().ConfigureAwait(false);

        if (connections.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            AnsiConsole.Console.WriteError($"Error: A connection with the name '{name}' already exists.");
            return 1;
        }

        connections.Add(new AiConnection
        {
            Name = name,
            Type = selectedType,
            DeploymentEndpoint = endpointToSave,
            ApiKey = apiKeyToSave,
            Model = modelToSave,
            Description = descriptionToSave,
            LastValidatedUtc = null
        });

        await connectionStore.SaveAsync(connections).ConfigureAwait(false);
        await aiConnectionSelectionService.SetSelectedConnectionAsync(name, cancellationToken).ConfigureAwait(false);

        AnsiConsole.Console.WriteSuccess($"Connection '{name}' created successfully!");
        AnsiConsole.Console.WriteSuccess($"AI connection '{name}' automatically selected for main operations.");
        return 0;
    }

    public async Task<int> DeleteAsync(string name, CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var connections = await connectionStore.LoadAsync().ConfigureAwait(false);
        var selectedName = name;
        if (string.IsNullOrWhiteSpace(selectedName))
        {
            if (connections.Count == 0)
            {
                AnsiConsole.Console.WriteInfo("No connections found.");
                return 0;
            }

            selectedName = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select the AI connection to delete:")
                    .PageSize(15)
                    .AddChoices(connections.Select(static c => c.Name).OrderBy(static n => n, StringComparer.OrdinalIgnoreCase)));
        }

        var connectionToDelete = connections.FirstOrDefault(c => c.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase));

        if (connectionToDelete == null)
        {
            AnsiConsole.Console.WriteError($"Error: No connection found with the name '{selectedName}'.");
            return 1;
        }

        connections.Remove(connectionToDelete);
        await connectionStore.SaveAsync(connections).ConfigureAwait(false);

        AnsiConsole.Console.WriteSuccess($"Connection '{connectionToDelete.Name}' deleted successfully.");
        return 0;
    }

    public async Task<int> ListAsync(CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;

        AnsiConsole.Console.WriteInfo("Listing all connections...");
        var connections = await connectionStore.LoadAsync().ConfigureAwait(false);
        var selectedConnection = await aiConnectionSelectionService.GetSelectedConnectionAsync(cancellationToken).ConfigureAwait(false);
        var selectedConnectionName = selectedConnection?.Name;

        if (connections.Count == 0)
        {
            AnsiConsole.Console.WriteInfo("No connections found.");
            return 0;
        }

        AnsiConsole.Console.WriteInfo("Connections List:");
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Name")
            .AddColumn("Type")
            .AddColumn("Model")
            .AddColumn("AuthMode")
            .AddColumn("Selected");

        foreach (var conn in connections)
        {
            var isSelected = !string.IsNullOrWhiteSpace(selectedConnectionName)
                && conn.Name.Equals(selectedConnectionName, StringComparison.OrdinalIgnoreCase);

            var authMode = conn.Type == AiConnectionType.GitHubCopilot
                ? (string.IsNullOrWhiteSpace(conn.ApiKey) ? "LoggedInUser" : "Token")
                : "ApiKey";

            table.AddRow(
                conn.Name,
                ToConnectionTypeLabel(conn.Type),
                string.IsNullOrWhiteSpace(conn.Model) ? "-" : conn.Model,
                authMode,
                isSelected ? "Y" : string.Empty);
        }

        AnsiConsole.Write(table);

        return 0;
    }

    public async Task<int> SelectAiConnectionAsync(string name, CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var connections = await connectionStore.LoadAsync().ConfigureAwait(false);
        var connection = connections.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (connection == null)
        {
            AnsiConsole.Console.WriteError($"Error: No AI connection found with the name '{name}'.");
            return 1;
        }

        await aiConnectionSelectionService.SetSelectedConnectionAsync(connection.Name, cancellationToken).ConfigureAwait(false);
        AnsiConsole.Console.WriteSuccess($"AI connection '{connection.Name}' selected for main operations.");
        return 0;
    }

    private static string ToConnectionTypeLabel(AiConnectionType connectionType)
    {
        return connectionType switch
        {
            AiConnectionType.AzureOpenAi => "Azure OpenAI",
            AiConnectionType.GitHubCopilot => "GitHub Copilot",
            _ => connectionType.ToString()
        };
    }

    public async Task<int> CreateDataverseConnectionAsync(string name, string url, CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        AnsiConsole.Console.WriteInfo("Creating a new Dataverse connection...");

        var connections = await dataverseConnectionStore.LoadAsync().ConfigureAwait(false);

        if (connections.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            AnsiConsole.Console.WriteError($"Error: A Dataverse connection with the name '{name}' already exists.");
            return 1;
        }

        var previouslySelectedConnection = await dataverseConnectionSelectionService.GetSelectedConnectionAsync(cancellationToken).ConfigureAwait(false);

        var newConnection = new DataverseConnection
        {
            Name = name,
            Url = url.Trim()
        };

        // Persist and select the new connection so factory-based creation validates this entry.
        connections.Add(newConnection);
        await dataverseConnectionStore.SaveAsync(connections).ConfigureAwait(false);
        await dataverseConnectionSelectionService.SetSelectedConnectionAsync(name, cancellationToken).ConfigureAwait(false);

        // Test the selected connection through the factory
        AnsiConsole.Console.WriteInfo("Testing the connection with OAuth MFA...");
        AnsiConsole.Console.WriteInfo("A browser window will open for authentication.");
        
        var testResult = await TestConnectionAsync(cancellationToken).ConfigureAwait(false);
        
        if (!testResult.Success)
        {
            connections.RemoveAll(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            await dataverseConnectionStore.SaveAsync(connections).ConfigureAwait(false);

            if (previouslySelectedConnection is not null)
            {
                await dataverseConnectionSelectionService
                    .SetSelectedConnectionAsync(previouslySelectedConnection.Name, cancellationToken)
                    .ConfigureAwait(false);
            }

            AnsiConsole.Console.WriteError($"Connection test failed: {testResult.ErrorMessage}");
            AnsiConsole.Console.WriteWarning("The connection was NOT saved.");
            return 1;
        }

        AnsiConsole.Console.WriteSuccess($"Connection test successful! Connected as user: {testResult.UserId}");

        AnsiConsole.Console.WriteSuccess($"Dataverse connection '{name}' created successfully!");
        AnsiConsole.Console.WriteSuccess($"Dataverse connection '{name}' automatically selected for main operations.");
        return 0;
    }

    public async Task<int> DeleteDataverseConnectionAsync(string name, CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var connections = await dataverseConnectionStore.LoadAsync().ConfigureAwait(false);
        var selectedName = name;
        if (string.IsNullOrWhiteSpace(selectedName))
        {
            if (connections.Count == 0)
            {
                AnsiConsole.Console.WriteInfo("No Dynamics connections found.");
                return 0;
            }

            selectedName = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select the Dataverse connection to delete:")
                    .PageSize(15)
                    .AddChoices(connections.Select(static c => c.Name).OrderBy(static n => n, StringComparer.OrdinalIgnoreCase)));
        }

        var connectionToDelete = connections.FirstOrDefault(c => c.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase));

        if (connectionToDelete == null)
        {
            AnsiConsole.Console.WriteError($"Error: No Dynamics connection found with the name '{selectedName}'.");
            return 1;
        }

        connections.Remove(connectionToDelete);
        await dataverseConnectionStore.SaveAsync(connections).ConfigureAwait(false);

        AnsiConsole.Console.WriteSuccess($"Dynamics connection '{connectionToDelete.Name}' deleted successfully.");
        return 0;
    }

    public async Task<int> ListDataverseConnectionsAsync(CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;

        AnsiConsole.Console.WriteInfo("Listing all Dynamics connections...");
        var connections = await dataverseConnectionStore.LoadAsync().ConfigureAwait(false);
        var selectedConnection = await dataverseConnectionSelectionService.GetSelectedConnectionAsync(cancellationToken).ConfigureAwait(false);
        var selectedConnectionName = selectedConnection?.Name;

        if (connections.Count == 0)
        {
            AnsiConsole.Console.WriteInfo("No Dynamics connections found.");
            return 0;
        }

        AnsiConsole.Console.WriteInfo("Dataverse Connections List:");
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Name")
            .AddColumn("URL")
            .AddColumn("Selected");

        foreach (var conn in connections)
        {
            var isSelected = !string.IsNullOrWhiteSpace(selectedConnectionName)
                && conn.Name.Equals(selectedConnectionName, StringComparison.OrdinalIgnoreCase);

            table.AddRow(
                conn.Name,
                conn.Url,
                isSelected ? "Y" : string.Empty);
        }

        AnsiConsole.Write(table);

        return 0;
    }

    public async Task<int> TestDataverseConnectionAsync(CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var selectedConnection = await dataverseConnectionSelectionService.GetSelectedConnectionAsync(cancellationToken).ConfigureAwait(false);
        
        if (selectedConnection == null)
        {
            AnsiConsole.Console.WriteError("Error: No Dataverse connection selected. Use 'conn dataverse select' first.");
            return 1;
        }

        AnsiConsole.Console.WriteInfo($"Testing selected Dataverse connection '{selectedConnection.Name}'...");

        var connections = await dataverseConnectionStore.LoadAsync().ConfigureAwait(false);
        var connection = connections.FirstOrDefault(c => c.Name.Equals(selectedConnection.Name, StringComparison.OrdinalIgnoreCase));

        if (connection == null)
        {
            AnsiConsole.Console.WriteError($"Error: Selected connection '{selectedConnection.Name}' not found in store.");
            return 1;
        }

        AnsiConsole.Console.WriteInfo("Connection Details:");
        var connectionDetailsTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Field")
            .AddColumn("Value");

        connectionDetailsTable.AddRow("Name", connection.Name);
        connectionDetailsTable.AddRow("URL", connection.Url);
        AnsiConsole.Write(connectionDetailsTable);

        AnsiConsole.Console.WriteInfo("Attempting OAuth authentication...");
        AnsiConsole.Console.WriteInfo("A browser window may open for authentication.");

        var testResult = await TestConnectionAsync(cancellationToken).ConfigureAwait(false);

        if (!testResult.Success)
        {
            AnsiConsole.Console.WriteError($"Connection test failed: {testResult.ErrorMessage}");
            return 1;
        }

        AnsiConsole.Console.WriteSuccess("Connection test successful!");
        var testResultTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Field")
            .AddColumn("Value");

        testResultTable.AddRow("User ID", testResult.UserId.ToString());
        testResultTable.AddRow("Organization ID", testResult.OrganizationId.ToString());
        testResultTable.AddRow("Business Unit ID", testResult.BusinessUnitId.ToString());
        AnsiConsole.Write(testResultTable);

        return 0;
    }

    public async Task<int> SelectDataverseConnectionAsync(string name, CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var connections = await dataverseConnectionStore.LoadAsync().ConfigureAwait(false);
        var connection = connections.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (connection == null)
        {
            AnsiConsole.Console.WriteError($"Error: No Dynamics connection found with the name '{name}'.");
            return 1;
        }

        await dataverseConnectionSelectionService.SetSelectedConnectionAsync(connection.Name, cancellationToken).ConfigureAwait(false);
        AnsiConsole.Console.WriteSuccess($"Dynamics connection '{connection.Name}' selected for main operations.");
        return 0;
    }

    private async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            AnsiConsole.Console.WriteInfo("Authenticating with OAuth MFA...");
            
            // The factory resolves the selected Dataverse connection automatically.
            var service = await dataverseClientFactory.CreateAsync(cancellationToken).ConfigureAwait(false);

            AnsiConsole.Console.WriteInfo("Executing WhoAmI request...");

            // Execute WhoAmI to verify authentication
            var response = (WhoAmIResponse)service.Execute(new WhoAmIRequest());

            return new ConnectionTestResult
            {
                Success = true,
                UserId = response.UserId,
                OrganizationId = response.OrganizationId,
                BusinessUnitId = response.BusinessUnitId
            };
        }
        catch (Exception ex)
        {
            AnsiConsole.Console.WriteError($"Exception Type: {ex.GetType().Name}");
            AnsiConsole.Console.WriteError($"Exception Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                AnsiConsole.Console.WriteError($"Inner Exception: {ex.InnerException.Message}");
            }
            
            return new ConnectionTestResult
            {
                Success = false,
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}"
            };
        }
    }

    private class ConnectionTestResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid BusinessUnitId { get; set; }
    }

    private static async Task<List<ModelInfo>> GetGitHubCopilotModelChoicesAsync(string? githubToken, CancellationToken cancellationToken)
    {
        try
        {
            var clientOptions = string.IsNullOrWhiteSpace(githubToken)
                ? new CopilotClientOptions()
                : new CopilotClientOptions { GitHubToken = githubToken };

            await using var client = new CopilotClient(clientOptions);
            var models = await client.ListModelsAsync(cancellationToken).ConfigureAwait(false);

            return models
                .Where(static model => !string.IsNullOrWhiteSpace(model.Id) || !string.IsNullOrWhiteSpace(model.Name))
                .GroupBy(static model => string.IsNullOrWhiteSpace(model.Id) ? model.Name : model.Id, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .OrderBy(static model => string.IsNullOrWhiteSpace(model.Name) ? model.Id : model.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            AnsiConsole.Console.WriteWarning($"Could not load GitHub Copilot models automatically: {ex.Message}");
            return [];
        }
    }

}


