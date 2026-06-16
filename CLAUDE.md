# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**AI Translator CLI** — a .NET 10 console application and NuGet dotnet tool (`ai-translator`) for Dataverse localization workflows. It exports translatable metadata from Dataverse to Excel, calls an AI service to translate it, and pushes it back.

## Commands

### Build

```bash
dotnet build .\AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.slnx -nologo -v:minimal
```

### Run (debug binary)

```bash
.\src\AugustoCRV.Tools.ConsoleModelDrivenAiTraslator\bin\Debug\net10.0\win-x64\AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.exe --help
```

### Run via dnx

```bash
dnx AugustoCRV.Tools.ConsoleModelDrivenAiTraslator --help
```

### Publish (self-contained, single file)

```bash
dotnet publish src/AugustoCRV.Tools.ConsoleModelDrivenAiTraslator/AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.csproj -c Release -r win-x64
```

### Tests

No test project exists yet. When adding one, use `AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Tests` as the project name and mirror class names (e.g. `GenerateExecutor` → `GenerateExecutorTests`). Use xUnit/NUnit/MSTest (whichever is chosen first) and run with:

```bash
dotnet test
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

## Architecture

### Entry point and wiring

`Program.cs` builds the DI container via `ServiceCollectionExtensions.cs`, creates a `Spectre.Console.Cli` `CommandApp`, and registers commands through `Cli/Configurator.cs`. If the executable is run with no arguments, `InteractiveRunner` takes over instead.

### CLI layer (`Cli/`)

- **`Configurator.cs`** — single source of truth for the command tree (`List<CliCommandNode>`). Both `CommandApp` registration and `InteractiveRunner` traverse the same tree.
- **`Settings.cs`** — `CommandSettings` subclasses with `[CommandOption]` and `[Description]` attributes. These attributes drive both CLI help and the interactive prompt (via reflection).
- **`Commands.cs`** — one `AsyncCommand<TSettings>` per leaf node; each validates settings then delegates to an executor service.
- **`InteractiveRunner.cs`** — walks the command tree, uses `NullabilityInfoContext` reflection on `Settings` types to collect option values, assembles `args[]`, and feeds them back through `app.RunAsync()` so the same handlers run in both modes.

### Executor services (`Services/`)

| Service | Responsibility |
|---|---|
| `GenerateExecutor` | Orchestrates export: connects to Dataverse, invokes all 13 translators to write an `_original.xlsx`, batches labels for AI, writes `_translated.xlsx` |
| `PushExecutor` | Reads translated Excel, connects to Dataverse, routes rows through translator push paths using `ExecuteMultipleRequest` |
| `ConnectionExecutors` | CRUD + select/list for both AI and Dataverse connections |

### Domain translators (`Services/Translation/`)

Thirteen translator classes (`EntityTranslator`, `AttributeTranslator`, `FormTranslator`, `SiteMapTranslator`, etc.) all extend `BaseTranslator`, which provides:
- `ExecuteMultiple()` — batched `ExecuteMultipleRequest` to Dataverse
- SHA-256 checksum columns — skip unchanged values on re-push
- EPPlus Excel row helpers

Translators are registered as keyed transient services and resolved at runtime by `KeyedServiceFactory` using the `TranslationServiceKind` enum.

### Connection and credential persistence

- `AiConnectionStoreService` / `DataverseConnectionStoreService` — persist JSON to `%APPDATA%\AugustoCRV\Tools\ConsoleModelDrivenAiTraslator\`. Thread-safe via `SemaphoreSlim`.
- `ApiKeyProtectorService` — AES-CBC encryption of API keys before write.
- `AiConnectionSelectionService` / `DataverseConnectionSelectionService` — persist and resolve the currently selected connection from a separate `selected-*.json` file.

### AI translation services

- `AzureOpenAiTranslationService` — direct `HttpClient` POST to the Azure OpenAI chat completions endpoint (5-minute timeout).
- `GitHubCopilotTranslationService` — uses the `GitHub.Copilot.SDK` package; supports logged-in user auth or a GitHub token.
- `PromptTemplateService` — loads the embedded `Resources/PromptTemplate.md` and substitutes `{{TARGET_LANGUAGE}}`, `{{ADDITIONAL_INSTRUCTIONS}}`, and `{{BATCH_DATA}}` placeholders.

## Key conventions from the csharp-expert agent

- **Interfaces only** when used for external dependencies or testing. Don't wrap existing abstractions.
- **Least exposure**: `private` > `internal` > `protected` > `public`.
- **Null checks**: `ArgumentNullException.ThrowIfNull(x)` for objects; `string.IsNullOrWhiteSpace(x)` for strings. Avoid blanket `!`.
- **No silent catches**: log and rethrow or let exceptions bubble.
- **Async end-to-end**: all async methods end with `Async`; pass `CancellationToken` through; no sync-over-async; use `ConfigureAwait(false)` in helper/library code.
- **DTOs**: prefer `record` over `class`.
- **Modern C#**: file-scoped namespaces, raw string literals, switch expressions, ranges — all valid on .NET 10 / C# 14.
- Don't change `<TargetFramework>`, SDK version, or `<LangVersion>` unless asked.
- Don't edit auto-generated code (`*.g.cs`, `// <auto-generated>`).

## Stack summary

| Concern | Library |
|---|---|
| CLI | Spectre.Console 0.49.1 + Spectre.Console.Cli |
| DI | Microsoft.Extensions.DependencyInjection 10.0 |
| Dataverse | Microsoft.PowerPlatform.Dataverse.Client 1.1.32 |
| CSV | Sep 0.12.2 (nietras.SeparatedValues) |
| AI — Azure | Direct REST (HttpClient) |
| AI — Copilot | GitHub.Copilot.SDK 0.1.32 |
| JSON | System.Text.Json |
