using nietras.SeparatedValues;

namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services.Csv;

internal sealed class SepTranslationWorkbookStorage : ITranslationWorkbookStorage
{
    private static readonly string[] HeaderColumns =
    [
        "Dataset",
        "RecordKey",
        "RowNumber",
        "EntityLogicalName",
        "FieldLogicalName",
        "SourceLcid",
        "TargetLcid",
        "SourceText",
        "TargetText",
        "ObjectId",
        "ObjectPath",
        "Checksum",
        "MetadataJson"
    ];

    /// <summary>Loads a TranslationWorkbookData from a single workbook file.</summary>
    public TranslationWorkbookData Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (File.Exists(path))
        {
            return LoadFromSingleCsvFile(path);
        }

        // Backward-compatible read for old folder-based exports.
        if (Directory.Exists(path))
        {
            return LoadFromLegacyFolder(path);
        }

        return new TranslationWorkbookData();
    }

    /// <summary>Saves a TranslationWorkbookData as a single file at the given path.</summary>
    public void Save(TranslationWorkbookData data, string path)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var outputDirectory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var records = data.Datasets
            .SelectMany(static kvp => kvp.Value.Select(record => string.IsNullOrWhiteSpace(record.Dataset)
                ? record with { Dataset = kvp.Key }
                : record))
            .OrderBy(static record => record.Dataset, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static record => record.RowNumber)
            .ToList();

        using var writer = Sep.New(',').Writer(o => o with
        {
            WriteHeader = true,
            Escape = true
        }).ToFile(path);

        foreach (var record in records)
        {
            using var sepRow = writer.NewRow();
            sepRow[HeaderColumns[0]].Set(record.Dataset);
            sepRow[HeaderColumns[1]].Set(record.RecordKey);
            sepRow[HeaderColumns[2]].Set(record.RowNumber.ToString(CultureInfo.InvariantCulture));
            sepRow[HeaderColumns[3]].Set(record.EntityLogicalName);
            sepRow[HeaderColumns[4]].Set(record.FieldLogicalName);
            sepRow[HeaderColumns[5]].Set(record.SourceLcid);
            sepRow[HeaderColumns[6]].Set(record.TargetLcid);
            sepRow[HeaderColumns[7]].Set(record.SourceText);
            sepRow[HeaderColumns[8]].Set(record.TargetText);
            sepRow[HeaderColumns[9]].Set(record.ObjectId);
            sepRow[HeaderColumns[10]].Set(record.ObjectPath);
            sepRow[HeaderColumns[11]].Set(record.Checksum);
            sepRow[HeaderColumns[12]].Set(record.MetadataJson);
        }
    }

    private static TranslationWorkbookData LoadFromSingleCsvFile(string path)
    {
        var records = LoadRecordsFromCsv(path);
        if (records.Count == 0)
        {
            return new TranslationWorkbookData();
        }

        var sourceLcid = records
            .Select(static r => r.SourceLcid)
            .FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v));

        var targetLcid = records
            .Select(static r => r.TargetLcid)
            .FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v));

        var datasets = records
            .GroupBy(static r => r.Dataset, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<TranslationRecord>)group.ToList(),
                StringComparer.OrdinalIgnoreCase);

        return new TranslationWorkbookData
        {
            SourceLcid = sourceLcid,
            TargetLcid = targetLcid,
            Datasets = datasets
        };
    }

    private static TranslationWorkbookData LoadFromLegacyFolder(string folder)
    {
        string? sourceLcid = null;
        string? targetLcid = null;
        var datasets = new Dictionary<string, IReadOnlyList<TranslationRecord>>(StringComparer.OrdinalIgnoreCase);

        foreach (var csvPath in Directory.EnumerateFiles(folder, "*.csv")
                     .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(csvPath);
            var records = LoadRecordsFromCsv(csvPath);
            if (records.Count == 0)
            {
                continue;
            }

            sourceLcid ??= records.Select(static r => r.SourceLcid)
                .FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v));
            targetLcid ??= records.Select(static r => r.TargetLcid)
                .FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v));
            datasets[name] = records;
        }

        return new TranslationWorkbookData
        {
            SourceLcid = sourceLcid,
            TargetLcid = targetLcid,
            Datasets = datasets
        };
    }

    private static List<TranslationRecord> LoadRecordsFromCsv(string csvPath)
    {
        var records = new List<TranslationRecord>();

        using var reader = Sep.Reader(o => o with
        {
            HasHeader = true,
            Unescape = true
        }).FromFile(csvPath);

        var headers = reader.Header.ColNames;

        foreach (var row in reader)
        {
            var record = new TranslationRecord
            {
                Dataset = GetColumn(row, headers, "Dataset"),
                RecordKey = GetColumn(row, headers, "RecordKey"),
                RowNumber = int.TryParse(GetColumn(row, headers, "RowNumber"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var rn) ? rn : 0,
                EntityLogicalName = GetColumn(row, headers, "EntityLogicalName"),
                ObjectId = GetColumn(row, headers, "ObjectId"),
                ObjectPath = GetColumn(row, headers, "ObjectPath"),
                FieldLogicalName = GetColumn(row, headers, "FieldLogicalName"),
                SourceLcid = GetColumn(row, headers, "SourceLcid"),
                TargetLcid = GetColumn(row, headers, "TargetLcid"),
                SourceText = GetColumn(row, headers, "SourceText"),
                TargetText = GetColumn(row, headers, "TargetText"),
                Checksum = GetColumn(row, headers, "Checksum"),
                MetadataJson = GetColumn(row, headers, "MetadataJson")
            };
            records.Add(record);
        }

        return records;
    }

    private static string GetColumn(SepReader.Row row, IReadOnlyList<string> headers, string name)
    {
        return headers.Contains(name) ? row[name].ToString() : string.Empty;
    }
}
