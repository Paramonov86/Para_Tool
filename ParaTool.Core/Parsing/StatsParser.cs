namespace ParaTool.Core.Parsing;

public static class StatsParser
{
    public static List<StatsEntry> Parse(string text)
    {
        var entries = new List<StatsEntry>();
        string? currentName = null;
        string? currentType = null;
        string? currentUsing = null;
        Dictionary<string, string>? currentData = null;

        foreach (var rawLine in text.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.IsEmpty) continue;

            if (line.StartsWith("new entry "))
            {
                // Flush previous entry
                if (currentName != null && currentType != null)
                {
                    entries.Add(new StatsEntry
                    {
                        Name = currentName,
                        Type = currentType,
                        Using = currentUsing,
                        Data = currentData ?? new(StringComparer.OrdinalIgnoreCase)
                    });
                }

                currentName = ExtractQuotedValue(line);
                currentType = null;
                currentUsing = null;
                currentData = new(StringComparer.OrdinalIgnoreCase);
            }
            else if (line.StartsWith("type "))
            {
                currentType = ExtractQuotedValue(line);
            }
            else if (line.StartsWith("using "))
            {
                currentUsing = ExtractQuotedValue(line);
            }
            else if (line.StartsWith("data "))
            {
                var (key, value) = ExtractDataPair(line);
                if (key != null && currentData != null)
                {
                    currentData[key] = value ?? "";
                }
            }
        }

        // Flush last entry
        if (currentName != null && currentType != null)
        {
            entries.Add(new StatsEntry
            {
                Name = currentName,
                Type = currentType,
                Using = currentUsing,
                Data = currentData ?? new(StringComparer.OrdinalIgnoreCase)
            });
        }

        return entries;
    }

    public static List<StatsEntry> Parse(ReadOnlySpan<byte> utf8Bytes)
    {
        return Parse(System.Text.Encoding.UTF8.GetString(utf8Bytes));
    }

    private static string ExtractQuotedValue(ReadOnlySpan<char> line)
    {
        int first = line.IndexOf('"');
        if (first < 0) return "";
        var rest = line[(first + 1)..];
        int second = rest.IndexOf('"');
        if (second < 0) return rest.ToString();
        return rest[..second].ToString();
    }

    private static (string? key, string? value) ExtractDataPair(ReadOnlySpan<char> line)
    {
        // data "KEY" "VALUE"
        int first = line.IndexOf('"');
        if (first < 0) return (null, null);
        var afterFirst = line[(first + 1)..];
        int endKey = afterFirst.IndexOf('"');
        if (endKey < 0) return (null, null);
        var key = afterFirst[..endKey].ToString();

        var afterKey = afterFirst[(endKey + 1)..];
        int startVal = afterKey.IndexOf('"');
        if (startVal < 0) return (key, "");
        var afterValStart = afterKey[(startVal + 1)..];
        int endVal = afterValStart.IndexOf('"');
        if (endVal < 0) return (key, afterValStart.ToString());
        var value = afterValStart[..endVal].ToString();

        return (key, value);
    }
}
