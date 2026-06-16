
namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Extensions;

/// <summary>Class description.</summary>

public static class TranslationTableExtensions
{
    /// <summary>
    /// Builds a dictionary mapping column header text to its 1-based column index.
    /// Returns an empty dictionary when the table has no dimension.
    /// </summary>
    public static Dictionary<string, int> GetHeaderDictionary(this TranslationTable sheet)
    {
        if (sheet.Dimension is null)
        {
            return new Dictionary<string, int>();
        }

        var header = sheet.Cells[1, 1, 1, sheet.Dimension.End.Column];
        var columnIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= header.Columns; col++)
        {
            string headerText = sheet.Cells[1, col].Text.Trim();
            if (!string.IsNullOrWhiteSpace(headerText))
            {
                columnIndexes[headerText] = col;
            }
        }

        return columnIndexes;
    }
}

