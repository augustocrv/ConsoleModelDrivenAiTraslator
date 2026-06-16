# AI Translator CLI

AI Translator is a .NET 10 console application and NuGet dotnet tool (`ai-translator`) for Dataverse localization workflows. It exports translatable metadata from Dataverse to CSV, calls an AI service to translate it, and pushes the results back — all from the command line or an interactive menu.

## Overview

Localizing a Dataverse solution involves hundreds of labels scattered across entities, attributes, option sets, forms, views, dashboards, site maps, ribbons, and charts. Doing this manually is slow and error-prone.

AI Translator automates the entire cycle:

1. **Export** — connects to Dataverse, reads all translatable strings from a solution, and writes them to a structured CSV workbook.
2. **Translate** — batches source text and sends it to Azure OpenAI or GitHub Copilot; applies static rules first, then AI for the remainder.
3. **Review** — outputs are plain CSV files that can be opened in Excel, reviewed by a human, and corrected before import.
4. **Push** — reads the approved CSV and writes translations back to Dataverse via batched `ExecuteMultipleRequest` calls.

The tool supports both **standard CLI mode** (for scripts and CI/CD) and **interactive mode** (menu-driven prompts with no arguments required).

## Features

- Exports **13 Dataverse component types** in a single pass (see [Supported component types](#supported-component-types))
- Two AI backends: **Azure OpenAI** (direct REST) and **GitHub Copilot SDK**
- Static translation rules applied before AI (field name overrides, view type templates, gender-aware patterns)
- In-memory deduplication: identical source text is translated once and the result is reused
- SHA-256 checksums on every row to skip unchanged labels on re-push
- AES-encrypted API key and password storage in `%APPDATA%`
- Configurable batch sizes for both AI requests and Dataverse imports
- Automatic retry with stricter format hints when the AI response is malformed
- Diagnostic dump of raw AI responses on persistent failure (`./ai-translator-diagnostics/`)

## Quick start

```bash
# 1. Build
dotnet build .\AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.slnx -nologo -v:minimal

# 2. Create connections
ai-translator conn ai create --name my-ai --type AzureOpenAi \
  --deployment-endpoint https://<resource>.openai.azure.com/openai/deployments/<deployment> \
  --api-key <secret>

ai-translator conn dataverse create --name my-dv \
  --url https://org.crm.dynamics.com \
  --username user@contoso.com --password <secret>

# 3. Select connections
ai-translator conn ai select
ai-translator conn dataverse select

# 4. Generate translations (English -> Italian + Spanish)
ai-translator gen --solution-name MySolution \
  --source-language-code 1033 \
  --target-language-codes 1040,1034

# 5. Review the generated *.canonical.csv files in Excel

# 6. Push approved translations
ai-translator push --csv .\MySolution_20260303_120000_translated_1040.canonical.csv
```

---

# How to run it

## Run modes

### Standard CLI mode

```powershell
.\src\AugustoCRV.Tools.ConsoleModelDrivenAiTraslator\bin\Debug\net10.0\win-x64\AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.exe --help
```

The application command name is `ai-translator`.

### Run with `dnx`

You can execute the tool on demand with `dnx` (no prior `dotnet tool install` required), as long as the package is available from your configured NuGet sources.

```powershell
dnx AugustoCRV.Tools.ConsoleModelDrivenAiTraslator --help
```

- Package ID: `AugustoCRV.Tools.ConsoleModelDrivenAiTraslator`
- Tool command name: `ai-translator`

### Interactive mode

Run without arguments to enter the interactive menu:

```powershell
.\src\AugustoCRV.Tools.ConsoleModelDrivenAiTraslator\bin\Debug\net10.0\win-x64\AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.exe
```

Interactive mode:
- Walks the same command tree defined in `Configurator.cs`
- Prompts for each option by reflecting on `Settings` metadata (`CommandOption` + `Description` attributes)
- Hides sensitive values (API keys, passwords) from display
- Supports `back` to return to the parent menu and `exit` to quit

## Commands

### 1) Connection management

Connections are stored locally and persisted across sessions. You must create and select at least one AI connection and one Dataverse connection before running `gen` or `push`.

#### AI connections

```bash
ai-translator conn ai create --name <NAME> [--type <AzureOpenAi|GitHubCopilot>] [options]
ai-translator conn ai delete --name <NAME>
ai-translator conn ai list
ai-translator conn ai select
```

| Option | Description |
|--------|-------------|
| `--name \| -n` | Connection name (required) |
| `--type \| -t` | `AzureOpenAi` or `GitHubCopilot` |
| `--deployment-endpoint \| --endpoint` | Azure OpenAI deployment URL (required for Azure) |
| `--api-key \| --key` | API key (required for Azure; optional for Copilot token mode) |
| `--model \| -m` | Model override (GitHub Copilot; default `gpt-4.1`) |
| `--use-logged-in-user` | GitHub Copilot auth mode (default `true`) |
| `--description \| --desc` | Free-text description |

#### Dataverse connections

```bash
ai-translator conn dataverse create --name <NAME> --url <URL> --username <USERNAME> --password <PASSWORD>
ai-translator conn dataverse delete --name <NAME>
ai-translator conn dataverse list
ai-translator conn dataverse test
ai-translator conn dataverse select
```

| Option | Description |
|--------|-------------|
| `--name \| -n` | Connection name (required) |
| `--url \| -u` | Environment URL (required) |
| `--username \| --user` | Username (required) |
| `--password \| --pwd` | Password (required) |

#### Selected connections

Before running main operations, select both connections:

```bash
ai-translator conn ai select
ai-translator conn dataverse select
```

- `gen`, `push`, and `export-original` require a selected Dataverse connection.
- `gen` and `push` also require a selected AI connection.
- When CLI connection arguments are omitted, the selected connection is used automatically.

### 2) Generate translations

```bash
ai-translator gen --solution-name <SOLUTION_NAME> --source-language-code <SOURCE_LCID> --target-language-codes <TARGET_LCIDS> [options]
```

Alias: `ai-translator generate`

#### Required options

| Option | Description |
|--------|-------------|
| `--solution-name \| --sn` | Dataverse solution unique name |
| `--source-language-code \| --slc` | Source LCID (e.g. `1033` for English) |
| `--target-language-codes \| --tlc` | Comma-separated target LCIDs (e.g. `1040,1034`) |

#### Optional options

| Option | Description |
|--------|-------------|
| `--source-csv-path \| --csv` | Reuse an existing source CSV instead of querying Dataverse |
| `--translation-context \| --context` | Custom instructions appended to the AI prompt |
| `--include-view-types \| --includeviewtypes` | Filter: only export these view types |
| `--exclude-view-types \| --excludedviewtype` | Filter: skip these view types |
| `--export-folder \| --output` | Output directory (default: current directory) |
| `--force \| -f` | Overwrite existing translations |

Notes:
- `--include-view-types` and `--exclude-view-types` are mutually exclusive.
- One translated file is created per target LCID.
- If `--source-csv-path` is not provided, `gen` runs `export-original` internally first.

#### Output files

```
<SolutionName>_<yyyyMMdd_HHmmss>_source.canonical.csv
<SolutionName>_<yyyyMMdd_HHmmss>_translated_<TargetLcid>.canonical.csv  (one per target)
```

### 3) Export original only

```bash
ai-translator export-original --solution-name <SOLUTION_NAME> [options]
```

Alias: `ai-translator gen-original`

Exports source-language metadata only (no AI translation). Useful for reviewing what will be translated before running `gen`.

| Option | Description |
|--------|-------------|
| `--solution-name \| --sn` | Solution unique name (required) |
| `--source-csv-path \| --csv` | Explicit output file path |
| `--export-folder \| --output` | Output directory |

### 4) Push translations

```bash
ai-translator push --translated-csv-path <PATH> [options]
```

Reads a `*.canonical.csv` file produced by `gen` and writes translations back to Dataverse.

| Option | Description |
|--------|-------------|
| `--translated-csv-path \| --csv` | Path to canonical CSV (required) |
| `--dataverse-url \| --url` | Dataverse URL override |
| `--dataverse-connection-string \| --dcs` | Full connection string override |
| `--import-batch-size \| --batch` | Records per `ExecuteMultipleRequest` batch (default `10`) |
| `--force \| -f` | Skip checksum validation and re-import all rows |

## Typical workflow

1. **Create and select connections**

```bash
ai-translator conn ai create --name my-ai --type AzureOpenAi \
  --deployment-endpoint https://<resource>.openai.azure.com/openai/deployments/<deployment> \
  --api-key <secret>

ai-translator conn dataverse create --name my-dv \
  --url https://org.crm.dynamics.com \
  --username user@contoso.com --password <secret>

ai-translator conn ai select
ai-translator conn dataverse select
```

2. **Generate translated workbooks**

```bash
ai-translator gen --solution-name MySolution --source-language-code 1033 --target-language-codes 1040,1034
```

3. **Review** the generated `*.canonical.csv` files in Excel or any text editor.

4. **Push to Dataverse**

```bash
ai-translator push --csv .\MySolution_20260303_120000_translated_1040.canonical.csv
```

---

# Specific information

## Configuration file (`ConsoleModelDrivenAiTraslator.config.json`)

The `gen` command looks for `ConsoleModelDrivenAiTraslator.config.json` in the current working directory. This file controls static translation rules that are applied **before** calling the AI service.

- If the file does not exist, it is created automatically from embedded `Resources/DefaultConsoleModelDrivenAiTraslatorConfig.json`.
- On first creation, target-language sections are filtered to the requested target LCIDs.

### Structure

| Section | Purpose |
|---------|---------|
| `translationContext` | Custom instructions appended to every AI prompt |
| `fieldTranslation.logicalNameTranslations` | Per-language map of field logical names to constant translations. Supports `*` wildcard key as a template with `{logicalName}` placeholder |
| `viewTranslation.exactTranslations` | Per-language source-to-target constants for view names (highest priority) |
| `viewTranslation.sourcePatterns` | Regex patterns to infer a rule key from view text (e.g. "Active" → `active`) |
| `viewTranslation.viewTypeRuleMap` | Maps exported view type text to a logical rule key (e.g. "Lookup view" → `lookup`) |
| `viewTranslation.templates` | Per-language templates with optional gender variants (`default`, `masculine`, `feminine`, `neutral`) |
| `viewTranslation.entityGenderByLanguage` | Per-language entity gender map (logical name → `masculine`/`feminine`/`neutral`) |

LCID validation and human-readable language names are loaded from embedded `Resources/WindowsLcid.json`.

## Defaults and runtime options

Defaults are configured through `TranslatorCliOptions`:

| Setting | Default | Description |
|---------|---------|-------------|
| Translation batch size | `30` | Rows per AI request |
| Import batch size | `10` | Records per Dataverse `ExecuteMultipleRequest` |
| Azure OpenAI timeout | `5` minutes | HTTP request timeout |
| Connections path | `%APPDATA%\AugustoCRV\Tools\ConsoleModelDrivenAiTraslator` | Storage location for connection JSON files |
| GitHub Copilot model | `gpt-4.1` | Default model for Copilot sessions |

## Connection persistence and security

All connection data is stored locally under `%APPDATA%\AugustoCRV\Tools\ConsoleModelDrivenAiTraslator\`:

| File | Content |
|------|---------|
| `connections.json` | AI connections (name, type, endpoint, encrypted API key, model) |
| `dataverse-connections.json` | Dataverse connections (name, URL, credentials) |
| `selected-ai-connection.json` | Currently active AI connection |
| `selected-dataverse-connection.json` | Currently active Dataverse connection |

- API keys and passwords are encrypted at rest using **AES-256-CBC** via `IApiKeyProtectorService` before being written to disk.
- Connection stores use `SemaphoreSlim` for thread-safe file access.
- OAuth tokens for Dataverse are cached separately by `ITokenCacheService`.

## Canonical CSV schema

Canonical files (`*.canonical.csv`) are fixed-schema records represented by `TranslationRecord` in code. The `push` command only accepts this format.

| Column | Description |
|--------|-------------|
| `schemaVersion` | Schema version for forward compatibility |
| `dataset` | Component type (Entity, Attribute, View, Form, …) |
| `recordKey` | Unique key for the translatable string |
| `rowNumber` | Row ordinal within the dataset |
| `entityLogicalName` | Parent entity logical name |
| `objectId` | Dataverse object identifier |
| `objectPath` | Hierarchical path (e.g. Form → Tab → Section → Field) |
| `fieldLogicalName` | Field or label logical name |
| `sourceLcid` | Source language LCID |
| `targetLcid` | Target language LCID |
| `sourceText` | Original text |
| `targetText` | Translated text |
| `checksum` | SHA-256 of source + target for change detection |
| `metadataJson` | Additional context serialized as JSON |

## Troubleshooting

| Problem | Solution |
|---------|----------|
| `dotnet tool install` fails with Azure DevOps feed error | Add `--add-source https://api.nuget.org/v3/index.json --ignore-failed-sources` to the install command, or temporarily disable the private feed: `dotnet nuget disable source <NAME>` |
| AI response format error | The tool retries once automatically. If it persists, check `./ai-translator-diagnostics/` for the raw response. Reduce `--translation-context` length or batch size |
| Checksum skip on push | Rows with unchanged content are skipped by design. Use `--force` to re-import all rows |
| Target language not found | Ensure the target LCID is provisioned in the Dataverse organization settings |
| Timeout on large solutions | Increase `AzureOpenAiTimeoutMinutes` in `TranslatorCliOptions` or reduce batch size |

---

# Project structure

## What's in this repo

| Path | Purpose |
|------|---------|
| `AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.slnx` | Solution file |
| `src/AugustoCRV.Tools.ConsoleModelDrivenAiTraslator/` | Main project |
| `Cli/Configurator.cs` | Single source of truth for the command tree |
| `Cli/Settings.cs` | `CommandSettings` subclasses with `[CommandOption]` and `[Description]` attributes |
| `Cli/Commands.cs` | One `AsyncCommand<TSettings>` per leaf command; delegates to executor services |
| `Cli/InteractiveRunner.cs` | Menu-driven interactive mode; reflects on settings metadata |
| `Services/Executors/` | `GenerateExecutor`, `ExportOriginalExecutor`, `PushExecutor`, connection executors |
| `Services/Translation/` | 13 record exporters/importers, one per Dataverse component type |
| `Services/TranslationPipeline/` | AI batching, static rules, in-memory cache |
| `Services/Ai/` | Azure OpenAI REST client, GitHub Copilot SDK client, prompt template service |
| `Services/Infrastructure/` | Token cache, API key encryption, path helpers |
| `Resources/` | Embedded prompt template, default config JSON, Windows LCID catalog |
| `docs/API-Translator.md` | Auto-generated API reference (Xml2Doc) |
| `xml2doc.json` | Xml2Doc configuration for documentation regeneration |

## Architecture

```
Program.cs
  └─ DI container (ServiceCollectionExtensions)
       └─ Spectre.Console.Cli CommandApp
            ├─ Configurator (command tree)
            ├─ Commands (handlers)
            │    ├─ GenerateExecutor
            │    │    ├─ 13 Record Exporters (EntityRecordExporter, AttributeRecordExporter, …)
            │    │    ├─ StaticTranslationService (config-driven rules)
            │    │    ├─ TranslationCacheService (deduplication)
            │    │    └─ AiPromptTranslationService → IAiService
            │    │         ├─ AzureOpenAiTranslationService (REST)
            │    │         └─ GitHubCopilotTranslationService (SDK)
            │    ├─ ExportOriginalExecutor
            │    └─ PushExecutor
            │         └─ 13 Record Importers → Dataverse ExecuteMultipleRequest
            └─ InteractiveRunner (no-arg mode)
```

| Layer | Directory | Responsibility |
|-------|-----------|----------------|
| CLI | `Cli/*` | Command tree, settings, handlers, interactive runner |
| Executors | `Services/Executors/*` | Orchestrate generate, export, push, and connection CRUD |
| Translation | `Services/Translation/*` | Extract and import labels by Dataverse component type |
| Pipeline | `Services/TranslationPipeline/*` | Batch AI calls, apply static rules, deduplicate |
| AI | `Services/Ai/*` | Azure OpenAI REST, GitHub Copilot SDK, prompt template |
| CSV | `Services/Csv/*` | Read/write canonical CSV via Sep library |
| Infrastructure | `Services/Infrastructure/*` | Token cache, API key encryption, last-generated-path cache |
| DI wiring | `Extensions/ServiceCollectionExtensions.cs` | Registers all services (singletons, transients, keyed) |

## Supported component types

The `gen` command extracts and translates all of these in a single run:

| # | Component | What is exported |
|---|-----------|-----------------|
| 1 | **Entities** | Display names, collection names, descriptions |
| 2 | **Attributes** | Field display names and descriptions |
| 3 | **Option Sets** | Table-scoped choice labels |
| 4 | **Global Option Sets** | Global choice labels |
| 5 | **Booleans** | True/False option labels |
| 6 | **Relationships (1:N)** | One-to-many relationship labels |
| 7 | **Relationships (N:N)** | Many-to-many relationship labels |
| 8 | **Views** | SavedQuery display names (with view type pattern matching) |
| 9 | **Charts** | Visualization names and descriptions |
| 10 | **Forms** | Form names, tab names, section names, field labels |
| 11 | **Site Maps** | Area, group, and sub-area labels |
| 12 | **Dashboards** | Dashboard display names |
| 13 | **Ribbons** | Ribbon/command bar labels |

## Translation pipeline

The `gen` command follows this pipeline:

1. **Connect** to Dataverse via OAuth (MFA supported) and resolve the target solution.
2. **Export** — invoke all 13 record exporters to extract translatable strings.
3. **Deduplicate** — identical source text is cached so the same string is only translated once.
4. **Static rules** — apply constant translations from `ConsoleModelDrivenAiTraslator.config.json` (field name overrides, view type templates, gender-aware patterns). Matching rows skip the AI call entirely.
5. **AI translation** — partition remaining rows into batches (default 30 rows/request), build a prompt from the embedded template, call the AI service, and parse the markdown table response.
6. **Retry** — if the AI response is malformed, retry once with a stricter format reminder. On persistent failure, save the raw response to `./ai-translator-diagnostics/`.
7. **Write outputs** — emit `*_source.canonical.csv` and `*_translated_<LCID>.canonical.csv` files with SHA-256 checksums.

The `push` command:

1. **Load** the canonical CSV and group rows by dataset (Entity, Attribute, View, etc.).
2. **Route** each group to the corresponding record importer.
3. **Skip** rows where the checksum matches the previously pushed value (no change since last export). Use `--force` to override.
4. **Batch** create/update operations via `ExecuteMultipleRequest` (default batch size 10).
5. **Report** results: created, updated, skipped, failed.

---

# How to build and install

## Requirements

- .NET SDK 10.0+
- Access to a Dataverse environment with **target languages provisioned** (e.g. LCID 1040 for Italian)
- At least one configured AI provider:
  - **Azure OpenAI**: deployment endpoint URL + API key
  - **GitHub Copilot**: logged-in user auth or GitHub personal access token

## Build

```powershell
dotnet build .\AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.slnx -nologo -v:minimal
```

## Publish (self-contained, single file)

```powershell
dotnet publish src/AugustoCRV.Tools.ConsoleModelDrivenAiTraslator/AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.csproj -c Release -r win-x64
```

Output: `src/AugustoCRV.Tools.ConsoleModelDrivenAiTraslator/bin/Release/net10.0/win-x64/publish/`
The published executable is self-contained (no .NET SDK required on the target machine) and bundled as a single file.

## Technical documentation (Xml2Doc)

API reference is auto-generated from C# XML doc comments using [Xml2Doc](https://github.com/mod-posh/xml2doc).

[View the API documentation](docs/API-Translator.md)

### Install Xml2Doc CLI

```powershell
dotnet tool install -g Xml2Doc.Cli --add-source https://api.nuget.org/v3/index.json --ignore-failed-sources
```

### Regenerate documentation

The repository includes a `xml2doc.json` configuration file. After building, run:

```powershell
xml2doc --config xml2doc.json
```

This reads `src/.../AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.xml` from the build output and writes a single consolidated Markdown file to `docs/API-Translator.md`.

Alternative command (without config file):

```powershell
xml2doc --xml .\src\AugustoCRV.Tools.ConsoleModelDrivenAiTraslator\bin\Debug\net10.0\win-x64\AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.xml --out .\docs\API-Translator.md --single --rootns AugustoCRV.Tools.ConsoleModelDrivenAiTraslator --file-names clean
```

> **Note:** The project requires `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in the `.csproj` (already configured) so that the XML file is produced on every build.

## Technology stack

| Concern | Library |
|---------|---------|
| CLI framework | Spectre.Console 0.49.1 + Spectre.Console.Cli |
| Dependency injection | Microsoft.Extensions.DependencyInjection 10.0 |
| Dataverse SDK | Microsoft.PowerPlatform.Dataverse.Client 1.1.32 |
| CSV I/O | Sep 0.12.2 (nietras.SeparatedValues) |
| AI — Azure OpenAI | Direct REST via `HttpClient` |
| AI — GitHub Copilot | GitHub.Copilot.SDK 0.1.32 |
| JSON | System.Text.Json |

## Notes

- `gen` and `push` are intentionally separate to allow human review before import.
- The CLI shares command metadata between standard and interactive flows via the same `Configurator` command tree.
- `ai-translator --help` is the canonical help entry point.
- `dnx AugustoCRV.Tools.ConsoleModelDrivenAiTraslator ...` is supported when the package is available on NuGet feeds.
