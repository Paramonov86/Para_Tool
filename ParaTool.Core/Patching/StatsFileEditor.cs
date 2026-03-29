namespace ParaTool.Core.Patching;

/// <summary>
/// Modifies data fields within existing stat entries in-place (inside their source files).
/// Also appends new skeleton entries for items not found in any file.
/// </summary>
public static class StatsFileEditor
{
    /// <summary>
    /// Modifies data fields for entries in the given stat file text.
    /// Returns the modified text and the set of entry names that were found and modified.
    /// </summary>
    public static (string text, HashSet<string> modifiedEntries) ModifyEntries(
        string text,
        IReadOnlyDictionary<string, Dictionary<string, string>> modifications)
    {
        if (modifications.Count == 0)
            return (text, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var lines = text.Split('\n').ToList();
        var modified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Find entry boundaries: (name, startLine, endLine)
        var entryRanges = new List<(string name, int start, int end)>();
        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith("new entry \"")) continue;

            var name = ExtractQuoted(trimmed);
            if (string.IsNullOrEmpty(name)) continue;

            if (entryRanges.Count > 0)
            {
                var last = entryRanges[^1];
                entryRanges[^1] = (last.name, last.start, i);
            }
            entryRanges.Add((name, i, lines.Count));
        }

        // Process entries in REVERSE order to preserve line indices after insertions
        for (int e = entryRanges.Count - 1; e >= 0; e--)
        {
            var (name, start, end) = entryRanges[e];
            if (!modifications.TryGetValue(name, out var fields)) continue;

            var remaining = new Dictionary<string, string>(fields, StringComparer.OrdinalIgnoreCase);

            // Replace existing data lines
            for (int i = start + 1; i < end; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith("data \"")) continue;

                var key = ExtractDataKey(trimmed);
                if (key != null && remaining.TryGetValue(key, out var newValue))
                {
                    lines[i] = $"data \"{key}\" \"{newValue}\"";
                    remaining.Remove(key);
                }
            }

            // Add fields that weren't found — insert after the last non-empty line in the entry
            if (remaining.Count > 0)
            {
                int insertAt = start + 1;
                for (int i = end - 1; i > start; i--)
                {
                    if (!string.IsNullOrWhiteSpace(lines[i]))
                    {
                        insertAt = i + 1;
                        break;
                    }
                }

                var newLines = remaining.Select(kvp => $"data \"{kvp.Key}\" \"{kvp.Value}\"").ToList();
                lines.InsertRange(insertAt, newLines);
            }

            modified.Add(name);
        }

        return (string.Join('\n', lines), modified);
    }

    /// <summary>
    /// Appends skeleton override entries at the end of the stat file text.
    /// Used for mod items that don't have existing definitions in AMP files.
    /// </summary>
    public static string AppendSkeletonEntries(string text, string entries)
    {
        if (string.IsNullOrWhiteSpace(entries)) return text;

        // Ensure there's a blank line before appended content
        if (!text.EndsWith("\n\n") && !text.EndsWith("\r\n\r\n"))
        {
            if (!text.EndsWith("\n") && !text.EndsWith("\r\n"))
                text += "\n";
            text += "\n";
        }

        text += entries;
        return text;
    }

    /// <summary>
    /// Remove all stat entries whose names match the given set.
    /// Each entry block starts with 'new entry "Name"' and ends at the next 'new entry' or EOF.
    /// </summary>
    public static string RemoveEntries(string text, HashSet<string> names)
    {
        if (names.Count == 0) return text;

        var lines = text.Split('\n');
        var result = new System.Text.StringBuilder();
        bool skipping = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("new entry ", StringComparison.OrdinalIgnoreCase))
            {
                // Extract entry name
                var q1 = trimmed.IndexOf('"');
                var q2 = q1 >= 0 ? trimmed.IndexOf('"', q1 + 1) : -1;
                if (q1 >= 0 && q2 > q1)
                {
                    var entryName = trimmed[(q1 + 1)..q2];
                    skipping = names.Contains(entryName);
                }
                else
                {
                    skipping = false;
                }
            }

            if (!skipping)
                result.AppendLine(line.TrimEnd('\r'));
        }

        return result.ToString();
    }

    private static string ExtractQuoted(string line)
    {
        int first = line.IndexOf('"');
        if (first < 0) return "";
        int second = line.IndexOf('"', first + 1);
        return second < 0 ? "" : line[(first + 1)..second];
    }

    private static string? ExtractDataKey(string line)
    {
        // data "KEY" "VALUE"
        int first = line.IndexOf('"');
        if (first < 0) return null;
        int second = line.IndexOf('"', first + 1);
        return second < 0 ? null : line[(first + 1)..second];
    }
}
