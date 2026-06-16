namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Models.Translation;

/// <summary>Class description.</summary>

public sealed class TranslationTable
{
    private readonly Dictionary<(int Row, int Column), TranslationCell> cells = new();
    private readonly Dictionary<int, TranslationColumn> columns = new();

    public TranslationTable(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
        Cells = new TranslationCells(this);
    }

    public string Name { get; }

    public TranslationCells Cells { get; }

    public TranslationDimension? Dimension => MaxRow <= 0 || MaxColumn <= 0 ? null : new TranslationDimension(MaxRow, MaxColumn);

    internal int MaxRow { get; private set; }

    internal int MaxColumn { get; private set; }

    public TranslationColumn Column(int index)
    {
        if (!columns.TryGetValue(index, out var column))
        {
            column = new TranslationColumn();
            columns[index] = column;
        }

        return column;
    }

    public void InsertColumn(int fromColumn, int columnsToInsert)
    {
        if (fromColumn <= 0 || columnsToInsert <= 0 || cells.Count == 0)
        {
            return;
        }

        var shifted = new Dictionary<(int Row, int Column), TranslationCell>();
        foreach (var entry in cells)
        {
            var row = entry.Key.Row;
            var col = entry.Key.Column >= fromColumn
                ? entry.Key.Column + columnsToInsert
                : entry.Key.Column;
            shifted[(row, col)] = entry.Value;
        }

        cells.Clear();
        foreach (var entry in shifted)
        {
            cells[entry.Key] = entry.Value;
        }

        MaxColumn += columnsToInsert;
    }

    public TranslationCell GetCell(int row, int column)
    {
        return GetOrCreateCell(row, column);
    }

    internal TranslationRange GetRange(int fromRow, int fromColumn, int toRow, int toColumn)
    {
        return new TranslationRange(this, fromRow, fromColumn, toRow, toColumn);
    }

    internal TranslationCell GetOrCreateCell(int row, int column)
    {
        if (row <= 0 || column <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(row), "Rows and columns are 1-based and must be greater than zero.");
        }

        if (!cells.TryGetValue((row, column), out var cell))
        {
            cell = new TranslationCell();
            cells[(row, column)] = cell;
        }

        if (row > MaxRow)
        {
            MaxRow = row;
        }

        if (column > MaxColumn)
        {
            MaxColumn = column;
        }

        return cell;
    }
}

/// <summary>Class description.</summary>

public sealed class TranslationCells
{
    private readonly TranslationTable table;
    private readonly TranslationStyle style = new();

    internal TranslationCells(TranslationTable table)
    {
        this.table = table;
    }

    public TranslationStyle Style => style;

    public TranslationRange this[int row, int column] => table.GetRange(row, column, row, column);

    public TranslationRange this[int fromRow, int fromColumn, int toRow, int toColumn] => table.GetRange(fromRow, fromColumn, toRow, toColumn);
}

/// <summary>Class description.</summary>

public sealed class TranslationRange
{
    private readonly TranslationTable table;
    private readonly int fromRow;
    private readonly int fromColumn;
    private readonly int toColumn;

    internal TranslationRange(TranslationTable table, int fromRow, int fromColumn, int toRow, int toColumn)
    {
        this.table = table;
        this.fromRow = fromRow;
        this.fromColumn = fromColumn;
        this.toColumn = toColumn;

        _ = toRow;
    }

    public int Columns => toColumn - fromColumn + 1;

    public object? Value
    {
        get => table.GetOrCreateCell(fromRow, fromColumn).Value;
        set => table.GetOrCreateCell(fromRow, fromColumn).Value = value;
    }

    public string Text => Value?.ToString() ?? string.Empty;

    public TranslationStyle Style => table.GetOrCreateCell(fromRow, fromColumn).Style;
}

/// <summary>Class description.</summary>

public sealed class TranslationCell
{
    public object? Value { get; set; }

    public string Text => Value?.ToString() ?? string.Empty;

    public TranslationStyle Style { get; } = new();
}

/// <summary>Class description.</summary>

public sealed class TranslationColumn
{
    public bool Hidden { get; set; }
}

/// <summary>Class description.</summary>

public sealed class TranslationDimension
{
    public TranslationDimension(int rows, int columns)
    {
        Rows = rows;
        Columns = columns;
        End = new TranslationAddress(rows, columns);
    }

    public int Rows { get; }

    public int Columns { get; }

    public TranslationAddress End { get; }
}

/// <summary>Class description.</summary>

public sealed class TranslationAddress
{
    public TranslationAddress(int row, int column)
    {
        Row = row;
        Column = column;
    }

    public int Row { get; }

    public int Column { get; }
}

/// <summary>Enum description.</summary>

public enum TranslationFillStyle
{
    None = 0,
    Solid = 1
}

/// <summary>Class description.</summary>

public sealed class TranslationStyle
{
    public TranslationFill Fill { get; } = new();

    public TranslationFont Font { get; } = new();
}

/// <summary>Class description.</summary>

public sealed class TranslationFill
{
    public TranslationFillStyle PatternType { get; set; }

    public TranslationColor BackgroundColor { get; } = new();
}

/// <summary>Class description.</summary>

public sealed class TranslationColor
{
    public System.Drawing.Color Color { get; private set; }

    public void SetColor(System.Drawing.Color color)
    {
        Color = color;
    }
}

/// <summary>Class description.</summary>

public sealed class TranslationFont
{
    public string Name { get; set; } = string.Empty;

    public float Size { get; set; }

    public bool Bold { get; set; }
}

