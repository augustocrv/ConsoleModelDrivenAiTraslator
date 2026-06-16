using AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.TranslationPipeline;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.TranslationPipeline;

internal interface ITranslationService
{
    Task<TranslationServiceResult> ExecuteAsync(TranslationServiceContext context, CancellationToken cancellationToken);
}
