# API Reference
- [ServiceCollectionExtensions](#servicecollectionextensions)
- [ConsoleModelDrivenAiTraslatorConfig](#ConsoleModelDrivenAiTraslatorconfig)
- [TokenDefinition](#tokendefinition)
- [DataverseConnection](#dataverseconnection)
- [FieldTranslationConfig](#fieldtranslationconfig)
- [TranslatorCliOptions](#translatorclioptions)
- [ViewTranslationConfig](#viewtranslationconfig)
- [ViewTranslationTemplateConfig](#viewtranslationtemplateconfig)
- [ApiKeyProtectorService](#apikeyprotectorservice)
- [AzureOpenAiTranslationService](#azureopenaitranslationservice)
- [DataverseConnectionStoreService](#dataverseconnectionstoreservice)
- [GitHubCopilotTranslationService](#githubcopilottranslationservice)
- [GitHubDeviceFlowService](#githubdeviceflowservice)
- [IAiService](#iaiservice)
- [IApiKeyProtectorService](#iapikeyprotectorservice)
- [IExportOriginalExecutor](#iexportoriginalexecutor)
- [IGenerateExecutor](#igenerateexecutor)
- [IGitHubDeviceFlowService](#igithubdeviceflowservice)
- [ILastGeneratedPathCache](#ilastgeneratedpathcache)
- [ITokenCacheService](#itokencacheservice)
- [TokenCacheService](#tokencacheservice)
- [IPromptTemplateService](#iprompttemplateservice)
- [IPushExecutor](#ipushexecutor)

<a id="servicecollectionextensions"></a>
# ServiceCollectionExtensions

Registers application services and options for the AI Translator CLI.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.extensions.servicecollectionextensions.addtranslatoroptions(microsoft.extensions.dependencyinjection.iservicecollection)"></a>
## Method: AddTranslatorOptions(IServiceCollection)
Adds strongly typed default options for CLI execution.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.extensions.servicecollectionextensions.addtranslatorservices(microsoft.extensions.dependencyinjection.iservicecollection)"></a>
## Method: AddTranslatorServices(IServiceCollection)
Adds all service dependencies used by command handlers and application workflows.


---

<a id="ConsoleModelDrivenAiTraslatorconfig"></a>
# ConsoleModelDrivenAiTraslatorConfig

Root configuration for custom translation behaviors.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.ConsoleModelDrivenAiTraslatorconfig.translationcontext"></a>
## Property: TranslationContext
Optional translation context/instructions appended to AI prompts.


---

<a id="tokendefinition"></a>
# TokenDefinition

Represents a cached OAuth token for a Dataverse connection.


---

<a id="dataverseconnection"></a>
# DataverseConnection

Represents a Dataverse connection configuration. Authentication is performed via OAuth with MFA support.


---

<a id="fieldtranslationconfig"></a>
# FieldTranslationConfig

Configures constant/template translations for Dataverse field display names.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.fieldtranslationconfig.logicalnametranslations"></a>
## Property: LogicalNameTranslations
Per-target-language map: logical field name -> translated display name. Supports optional '*' key as template (may use {logicalName}).


---

<a id="translatorclioptions"></a>
# TranslatorCliOptions

Provides configurable defaults for CLI execution and infrastructure services.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.translatorclioptions.azureopenaitimeoutminutes"></a>
## Property: AzureOpenAiTimeoutMinutes
Gets or sets the timeout, in minutes, for Azure OpenAI requests.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.translatorclioptions.connectionsrelativepath"></a>
## Property: ConnectionsRelativePath
Gets or sets the relative path under AppData used for connection persistence.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.translatorclioptions.connectionsrootdirectory"></a>
## Property: ConnectionsRootDirectory
Gets or sets an optional explicit absolute path for connection persistence.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.translatorclioptions.defaultConsoleModelDrivenAiTraslatorconfigresourcesuffix"></a>
## Property: DefaultConsoleModelDrivenAiTraslatorConfigResourceSuffix
Gets or sets the embedded resource suffix used to locate the default translator config JSON.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.translatorclioptions.defaultimportbatchsize"></a>
## Property: DefaultImportBatchSize
Gets or sets the default number of records imported per Dataverse batch.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.translatorclioptions.defaulttranslationbatchsize"></a>
## Property: DefaultTranslationBatchSize
Gets or sets the default number of rows translated per Azure OpenAI request.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.translatorclioptions.githubcopilotmodel"></a>
## Property: GitHubCopilotModel
Gets or sets the default model used for GitHub Copilot translation sessions.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.translatorclioptions.prompttemplateresourcesuffix"></a>
## Property: PromptTemplateResourceSuffix
Gets or sets the embedded resource suffix used to locate the prompt template.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.translatorclioptions.windowslcidresourcesuffix"></a>
## Property: WindowsLcidResourceSuffix
Gets or sets the embedded resource suffix used to locate the Windows LCID catalog JSON.


---

<a id="viewtranslationconfig"></a>
# ViewTranslationConfig

Configures constant and modular translations for Dataverse views.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.viewtranslationconfig.entitygenderbylanguage"></a>
## Property: EntityGenderByLanguage
Optional per-language entity gender map (logical name -> masculine/feminine/neutral).

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.viewtranslationconfig.exacttranslations"></a>
## Property: ExactTranslations
Optional exact per-language source-to-target constants (highest priority).

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.viewtranslationconfig.sourcepatterns"></a>
## Property: SourcePatterns
Source-language patterns used to infer a rule key from the original text.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.viewtranslationconfig.templates"></a>
## Property: Templates
Per-target-language rule templates.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.models.viewtranslationconfig.viewtyperulemap"></a>
## Property: ViewTypeRuleMap
Maps exported view type text to a logical rule key (for example: "Lookup view" -> "lookup").


---

<a id="viewtranslationtemplateconfig"></a>
# ViewTranslationTemplateConfig

Defines a translation template with optional gender variants.


---

<a id="apikeyprotectorservice"></a>
# ApiKeyProtectorService

Provides simple AES-based encryption for storing API keys locally.


---

<a id="azureopenaitranslationservice"></a>
# AzureOpenAiTranslationService

Default Azure OpenAI client used for translation requests.


---

<a id="dataverseconnectionstoreservice"></a>
# DataverseConnectionStoreService

Service for persisting Dataverse connection configurations. No encryption needed - OAuth tokens are cached separately.


---

<a id="githubcopilottranslationservice"></a>
# GitHubCopilotTranslationService

Translation client backed by the GitHub Copilot SDK.


---

<a id="githubdeviceflowservice"></a>
# GitHubDeviceFlowService

Implements the GitHub OAuth Device Authorization Grant (RFC 8628) to obtain a user access token for the GitHub Copilot bundled CLI connection.


---

<a id="iaiservice"></a>
# IAiService

Sends generic prompts to the configured AI provider and returns text responses.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.services.iaiservice.translateasync(string,augustocrv.tools.consolemodeldrivenaitraslator.models.aiconnection,system.threading.cancellationtoken)"></a>
## Method: TranslateAsync(string, AiConnection, CancellationToken)
Sends a translation prompt using the specified AI connection.


---

<a id="iapikeyprotectorservice"></a>
# IApiKeyProtectorService

Encrypts and decrypts persisted API keys.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.services.iapikeyprotectorservice.decrypt(string)"></a>
## Method: Decrypt(string)
Decrypts cipher text.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.services.iapikeyprotectorservice.encrypt(string)"></a>
## Method: Encrypt(string)
Encrypts plain text.


---

<a id="iexportoriginalexecutor"></a>
# IExportOriginalExecutor

Exports only the original workbook from Dataverse metadata.


---

<a id="igenerateexecutor"></a>
# IGenerateExecutor

Defines orchestration for export and translation generation.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.services.igenerateexecutor.executeasync(string,string,string,string,string,string,string,string,bool,bool,system.threading.cancellationtoken)"></a>
## Method: ExecuteAsync(string, string, string, string, string, string, string, string, bool, bool, CancellationToken)
Executes generation and optional translation for a Dataverse solution.


---

<a id="igithubdeviceflowservice"></a>
# IGitHubDeviceFlowService

Executes the GitHub OAuth Device Flow to obtain a user access token.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.services.igithubdeviceflowservice.authenticateasync(system.threading.cancellationtoken)"></a>
## Method: AuthenticateAsync(CancellationToken)
Runs the full device authorization flow: requests device/user codes, prompts the user, and polls for the access token.


---

<a id="ilastgeneratedpathcache"></a>
# ILastGeneratedPathCache

Persists and retrieves the path of the last generated translated workbook so that the push command can propose it as a default.


---

<a id="itokencacheservice"></a>
# ITokenCacheService

Service for managing OAuth token cache for Dataverse connections.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.services.infrastructure.itokencacheservice.cleartokenasync(string,system.threading.cancellationtoken)"></a>
## Method: ClearTokenAsync(string, CancellationToken)
Removes a token from the cache.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.services.infrastructure.itokencacheservice.savetokenasync(string,system.uri,string,system.threading.cancellationtoken)"></a>
## Method: SaveTokenAsync(string, Uri, string, CancellationToken)
Saves an access token to the cache.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.services.infrastructure.itokencacheservice.trygettokenasync(string,system.threading.cancellationtoken)"></a>
## Method: TryGetTokenAsync(string, CancellationToken)
Attempts to retrieve a cached token for the specified connection.


---

<a id="tokencacheservice"></a>
# TokenCacheService

Implementation of token cache service that persists tokens to disk.


---

<a id="iprompttemplateservice"></a>
# IPromptTemplateService

Provides prompt templates used for AI translation requests.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.services.iprompttemplateservice.gettemplate"></a>
## Method: GetTemplate
Gets the embedded translation prompt template.


---

<a id="ipushexecutor"></a>
# IPushExecutor

Defines orchestration for importing translated content into Dataverse.

<a id="augustocrv.tools.consolemodeldrivenaitraslator.services.ipushexecutor.executeasync(string,string,string,system.nullable[int],bool,system.threading.cancellationtoken)"></a>
## Method: ExecuteAsync(string, string, string, Nullable<int>, bool, CancellationToken)
Executes the translation import operation from a workbook file.

