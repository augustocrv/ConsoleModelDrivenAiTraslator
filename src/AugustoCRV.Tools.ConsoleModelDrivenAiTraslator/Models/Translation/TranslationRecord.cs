namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation;

internal sealed record TranslationRecord
{
    public required string Dataset { get; init; }

    public required string RecordKey { get; init; }

    public required int RowNumber { get; init; }

    public string EntityLogicalName { get; init; } = string.Empty;

    public string ObjectId { get; init; } = string.Empty;

    public string ObjectPath { get; init; } = string.Empty;

    public string FieldLogicalName { get; init; } = string.Empty;

    public required string SourceLcid { get; init; }

    public required string TargetLcid { get; init; }

    public required string SourceText { get; init; }

    public string TargetText { get; init; } = string.Empty;

    public string Checksum { get; init; } = string.Empty;

    public string MetadataJson { get; init; } = "{}";
}
