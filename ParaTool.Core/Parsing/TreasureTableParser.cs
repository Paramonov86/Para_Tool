namespace ParaTool.Core.Parsing;

public sealed class TreasureTableDocument
{
    public List<TreasureTable> Tables { get; } = new();
    public string OriginalText { get; init; } = "";
}

public sealed class TreasureTable
{
    public required string Name { get; init; }
    public List<TreasureSubtable> Subtables { get; } = new();
}

public sealed class TreasureSubtable
{
    public required string Spec { get; init; } // e.g. "-1" or "1,1"
    public List<string> Items { get; } = new(); // raw "object category" lines
}

public static class TreasureTableParser
{
    public static TreasureTableDocument Parse(string text)
    {
        var doc = new TreasureTableDocument { OriginalText = text };
        TreasureTable? currentTable = null;
        TreasureSubtable? currentSubtable = null;

        foreach (var rawLine in text.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.IsEmpty) continue;

            if (line.StartsWith("new treasuretable "))
            {
                currentTable = new TreasureTable { Name = ExtractQuoted(line) };
                doc.Tables.Add(currentTable);
                currentSubtable = null;
            }
            else if (line.StartsWith("new subtable "))
            {
                if (currentTable == null) continue;
                currentSubtable = new TreasureSubtable { Spec = ExtractQuoted(line) };
                currentTable.Subtables.Add(currentSubtable);
            }
            else if (line.StartsWith("object category "))
            {
                currentSubtable?.Items.Add(line.ToString());
            }
        }

        return doc;
    }

    private static string ExtractQuoted(ReadOnlySpan<char> line)
    {
        int first = line.IndexOf('"');
        if (first < 0) return "";
        var rest = line[(first + 1)..];
        int second = rest.IndexOf('"');
        return second < 0 ? rest.ToString() : rest[..second].ToString();
    }
}
