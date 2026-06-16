namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Csv;

internal interface ITranslationWorkbookStorage
{
    TranslationWorkbookData Load(string path);

    void Save(TranslationWorkbookData data, string path);
}
