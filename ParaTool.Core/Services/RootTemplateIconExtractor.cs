using ParaTool.Core.LSLib;

namespace ParaTool.Core.Services;

/// <summary>
/// Extracts Icon FixedString from RootTemplate LSF files using full LSF parsing.
/// This is more reliable than binary pattern matching for Icon field extraction.
/// </summary>
public static class RootTemplateIconExtractor
{
    /// <summary>
    /// Parse an LSF file and extract MapKey UUID → Icon name mappings.
    /// </summary>
    public static Dictionary<string, string> ExtractFromLsf(byte[] data)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var ms = new MemoryStream(data);
            var reader = new LSFReader(ms);
            var resource = reader.Read();

            foreach (var region in resource.Regions.Values)
            {
                ExtractFromNode(region, result);
            }
        }
        catch
        {
            // LSF parsing failed — fall back to empty
        }

        return result;
    }

    /// <summary>
    /// Reverse lookup: find RootTemplates that reference given StatIds via their Stats attribute.
    /// Returns StatId → (UUID, DisplayNameHandle, DescriptionHandle).
    /// Used when stats entries don't have RootTemplate UUID but templates reference the stats.
    /// </summary>
    public static Dictionary<string, (string uuid, string? nameHandle, string? descHandle, string? icon)>
        ExtractByStats(byte[] data, IReadOnlySet<string> statIds)
    {
        var result = new Dictionary<string, (string uuid, string? nameHandle, string? descHandle, string? icon)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var ms = new MemoryStream(data);
            var reader = new LSFReader(ms);
            var resource = reader.Read();
            foreach (var region in resource.Regions.Values)
                ExtractByStatsFromNode(region, statIds, result);
        }
        catch { }
        return result;
    }

    private static void ExtractByStatsFromNode(Node node, IReadOnlySet<string> statIds,
        Dictionary<string, (string uuid, string? nameHandle, string? descHandle, string? icon)> result)
    {
        string? mapKey = null, stats = null, nameHandle = null, descHandle = null, icon = null;

        foreach (var attr in node.Attributes)
        {
            var key = attr.Key;
            if (key.Equals("MapKey", StringComparison.OrdinalIgnoreCase))
                mapKey = attr.Value.Value?.ToString();
            else if (key.Equals("Stats", StringComparison.OrdinalIgnoreCase))
                stats = attr.Value.Value?.ToString();
            else if (key.Equals("Icon", StringComparison.OrdinalIgnoreCase))
                icon = attr.Value.Value?.ToString();
            else if (key.Equals("DisplayName", StringComparison.OrdinalIgnoreCase))
            {
                if (attr.Value.Value is TranslatedString ts)
                    nameHandle = ts.Handle;
                else
                    nameHandle = attr.Value.Value?.ToString();
            }
            else if (key.Equals("Description", StringComparison.OrdinalIgnoreCase))
            {
                if (attr.Value.Value is TranslatedString ts)
                    descHandle = ts.Handle;
                else
                    descHandle = attr.Value.Value?.ToString();
            }
        }

        if (mapKey != null && stats != null && statIds.Contains(stats))
            result.TryAdd(stats, (mapKey, nameHandle, descHandle, icon));

        foreach (var childList in node.Children)
            foreach (var child in childList.Value)
                ExtractByStatsFromNode(child, statIds, result);
    }

    private static void ExtractFromNode(Node node, Dictionary<string, string> result)
    {
        string? mapKey = null;
        string? icon = null;

        foreach (var attr in node.Attributes)
        {
            if (attr.Key.Equals("MapKey", StringComparison.OrdinalIgnoreCase))
                mapKey = attr.Value.Value?.ToString();
            else if (attr.Key.Equals("Icon", StringComparison.OrdinalIgnoreCase))
                icon = attr.Value.Value?.ToString();
        }

        if (mapKey != null && icon != null && !string.IsNullOrEmpty(icon))
            result.TryAdd(mapKey, icon);

        foreach (var childList in node.Children)
        {
            foreach (var child in childList.Value)
                ExtractFromNode(child, result);
        }
    }

    /// <summary>
    /// Full template metadata: MapKey UUID → (nameHandle, descHandle, icon, stats, parent).
    /// LSF-structure-aware — each attribute belongs to the node it's defined in, so
    /// merged files containing many templates don't mix up handles across templates.
    /// </summary>
    public static Dictionary<string, (string? nameHandle, string? descHandle, string? icon, string? stats, string? parent)>
        ExtractFullMetadata(byte[] data)
    {
        var result = new Dictionary<string, (string? nameHandle, string? descHandle, string? icon, string? stats, string? parent)>(
            StringComparer.OrdinalIgnoreCase);
        try
        {
            using var ms = new MemoryStream(data);
            var reader = new LSFReader(ms);
            var resource = reader.Read();
            foreach (var region in resource.Regions.Values)
                ExtractFullMetadataFromNode(region, result);
        }
        catch { }
        return result;
    }

    private static void ExtractFullMetadataFromNode(Node node,
        Dictionary<string, (string? nameHandle, string? descHandle, string? icon, string? stats, string? parent)> result)
    {
        string? mapKey = null, nameHandle = null, descHandle = null, icon = null, stats = null, parent = null;

        foreach (var attr in node.Attributes)
        {
            var key = attr.Key;
            if (key.Equals("MapKey", StringComparison.OrdinalIgnoreCase))
                mapKey = attr.Value.Value?.ToString();
            else if (key.Equals("Icon", StringComparison.OrdinalIgnoreCase))
                icon = attr.Value.Value?.ToString();
            else if (key.Equals("Stats", StringComparison.OrdinalIgnoreCase))
                stats = attr.Value.Value?.ToString();
            else if (key.Equals("ParentTemplateId", StringComparison.OrdinalIgnoreCase))
                parent = attr.Value.Value?.ToString();
            else if (key.Equals("DisplayName", StringComparison.OrdinalIgnoreCase))
            {
                if (attr.Value.Value is TranslatedString ts) nameHandle = ts.Handle;
                else nameHandle = attr.Value.Value?.ToString();
            }
            else if (key.Equals("Description", StringComparison.OrdinalIgnoreCase))
            {
                if (attr.Value.Value is TranslatedString ts) descHandle = ts.Handle;
                else descHandle = attr.Value.Value?.ToString();
            }
        }

        if (mapKey != null)
            result.TryAdd(mapKey, (nameHandle, descHandle, icon, stats, parent));

        foreach (var childList in node.Children)
            foreach (var child in childList.Value)
                ExtractFullMetadataFromNode(child, result);
    }
}
