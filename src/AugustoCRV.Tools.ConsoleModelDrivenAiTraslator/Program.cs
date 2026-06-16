global using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Extensions;
global using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Cli;
global using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models;
global using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation;
global using TranslationTable = AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation.TranslationTable;
global using TranslationRange = AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation.TranslationRange;
global using TranslationStyle = AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation.TranslationStyle;
global using TranslationFillStyle = AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation.TranslationFillStyle;
global using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;
global using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Dataverse;
global using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Infrastructure;
global using Microsoft.Crm.Sdk.Messages;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.PowerPlatform.Dataverse.Client;
global using Microsoft.Xrm.Sdk;
global using Microsoft.Xrm.Sdk.Messages;
global using Microsoft.Xrm.Sdk.Metadata;
global using Microsoft.Xrm.Sdk.Metadata.Query;
global using Microsoft.Xrm.Sdk.Query;
global using Spectre.Console;
global using Spectre.Console.Cli;
global using System.ComponentModel;
global using System.Globalization;
global using System.Net.Http.Headers;
global using System.Security.Cryptography;
global using System.Text;
global using System.Text.Json;
global using System.Xml;

Console.OutputEncoding = Encoding.UTF8;

var services = new ServiceCollection();
services.AddLogging();
services.AddTranslatorOptions();
services.AddTranslatorServices();

var serviceProvider = services.BuildServiceProvider();
var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);
app.Configure(config => config.ConfigureTranslatorCli());

if (args.Length == 0)
{
    var dataverseClientFactory = serviceProvider.GetRequiredService<IDataverseClientFactory>();
    var lastGeneratedPathCache = serviceProvider.GetRequiredService<ILastGeneratedPathCache>();
    var interactiveRunner = new InteractiveRunner(dataverseClientFactory, lastGeneratedPathCache);
    return await interactiveRunner.RunAsync(app);
}

return await app.RunAsync(args);

